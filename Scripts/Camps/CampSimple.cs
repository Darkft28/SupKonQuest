using Godot;
using System.Collections.Generic;

public partial class CampSimple : Area2D
{
	[Signal] public delegate void CampCapturedEventHandler(int newTeamId);

	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCamp = false;
	[Export] public float MaxHealth = 600f;
	[Export] public int GoldPerSecond = 50;

	[Export] public float TurretDamage = 5f;
	[Export] public float TurretRange = 600f;

	private float _currentHealth;
	private float _goldTimer = 0f;
	private int _localGold = 0;

	// Tracking du dernier attaquant pour la mécanique de capture
	private int _lastAttackerTeamId = 0;

	public int CampId;
	private static int _nextCampId = 1;

	private Label _campIdLabel;

	private List<Unit> _spawnedUnits = new List<Unit>();

	// Defenseurs du camp (initiaux + bonus capture) — distinct des unites produites
	private List<Unit> _defenders = new List<Unit>();

	private Queue<string> _productionQueue = new Queue<string>();
	private string _currentProduction = null;
	private float _productionTimer = 0f;
	private int _dynamicUnitSpawnSequence = 0;
	private int _dynamicShipSpawnSequence = 0;
	private const int MaxQueueSize = 7;
	// Plafond global d'unités géré par GameManager.GetMaxUnitsForTeam() (10 par camp contrôlé)

	// Region economique (1, 2 ou 3) — secteur angulaire par rapport au centre
	public int RegionId { get; set; } = 0;

	public bool HasPort { get; private set; }
	private Sprite2D _portSprite;

	private Queue<string> _shipProductionQueue = new Queue<string>();
	private string _currentShipProduction = null;
	private float _shipProductionTimer = 0f;
	private const int MaxShipQueueSize = 5;
	private List<Ship> _spawnedShips = new List<Ship>();
	private TileMapLayer _tileMapSol;
	private TileMapLayer _tileMapObjets;

	private ProgressBar _healthBar;
	private StyleBoxFlat _healthBarBackgroundStyle;
	private StyleBoxFlat _healthBarFillStyle;
	private const float HealthBarWidth = 100f;
	private const float HealthBarHeight = 10f;
	private bool _campTimersSetup = false;

	private Timer _turretTimerNode;
	private Timer _territoryAlertTimerNode;
	private Timer _territoryAlertCooldownTimerNode;

	private static readonly string[] UnitTypes = new[]
	{
		"Infantry",
		"Support",
		"Heal",
		"Range",
	};

	public float GetCurrentHealth()
	{
		return _currentHealth;
	}
	private void SetCurrentHealth(float value)
	{
		_currentHealth = value;
	}

	public int GetCampId()
	{
		return CampId;
	}

	private void SetCampId(int value)
	{
		CampId = value;
	}

	public int GetGold()
	{
		if (IsNeutralCamp)
		{
			return _localGold;
		}
		return GameManager.Instance?.GetGold(TeamId) ?? 0;
	}

	public int GetTeamId()
	{
		return TeamId;
	}

	public bool IsLocallyOwned()
	{
		if (NetworkSync.Instance == null || !NetworkSync.Instance.IsMultiplayer())
			return true;

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int localTeamId = gameState?.LocalTeamId ?? 1;

		// Online: neutral camps are not simulated on every peer (capture via CampCaptured opcode).
		if (IsOnlineMultiplayer() && (IsNeutralCamp || TeamId == 0))
			return false;

		return TeamId == localTeamId;
	}

	private static bool IsOnlineMultiplayer() => GameState.IsOnlineMultiplayer;

	// Reseau : mettre a jour l'autorite des defenseurs apres assignation
	public void UpdateDefendersAuthority()
	{
		bool isLocal = IsLocallyOwned();
		foreach (var unit in _spawnedUnits)
		{
			if (unit != null && IsInstanceValid(unit))
				unit.IsLocalAuthority = isLocal;
		}
	}

	// Reseau : appliquer une capture recue du peer distant
	public void ApplyRemoteCapture(int newTeamId)
	{
		if (!IsNeutralCamp && TeamId == newTeamId)
			return;

		int oldTeamId = TeamId;
		TeamId = newTeamId;
		IsNeutralCamp = false;

		SetCurrentHealth(MaxHealth);
		UpdateCampVisualTheme();
		UpdateCampTimers();
		RefreshCampLabel(); // label boss/normal selon la nouvelle équipe

		if (GameManager.Instance != null)
		{
			GameManager.Instance.GiveCaptureBonus(newTeamId);
			if (_localGold > 0)
			{
				GameManager.Instance.AddGold(newTeamId, _localGold);
				_localGold = 0;
			}
		}

		UpdateSpawnedUnitsTeam();
		UpdateDefendersAuthority();

		// En mode relay, une capture peut n'être confirmée que par message distant.
		// On ajoute donc les bonus units ici pour converger avec le peer qui a capturé localement.
		if (IsOnlineMultiplayer())
			SpawnBonusUnits();

		GD.Print($"[NET] Camp #{CampId} capture a distance: Team {oldTeamId} -> {newTeamId}");
		EmitSignal(SignalName.CampCaptured, newTeamId);

		// Appel direct garanti — ne dépend pas de la connexion signal
		TerritoryManager.Instance?.RefreshTerritory(newTeamId);
	}

	// Reseau : reset l'ID counter pour les IDs deterministes
	public static void ResetCampIdCounter()
	{
		_nextCampId = 1;
	}

	public void SetTeam(int newTeamId, bool isNeutral)
	{
		int oldTeamId = TeamId;

		if (oldTeamId != newTeamId)
		{
			RefundProductionQueue(oldTeamId);
			RefundShipProductionQueue(oldTeamId);
		}

		TeamId = newTeamId;
		IsNeutralCamp = isNeutral;

		UpdateCampVisualTheme();
		UpdateCampTimers();
		RefreshCampLabel();

		if (!isNeutral && GameManager.Instance != null)
		{
			GameManager.Instance.InitializeTeam(TeamId);
		}

		UpdateSpawnedUnitsTeam();
	}

	public void NeutralizeCampAfterPlayerLeave()
	{
		SetTeam(0, true);
		SetCurrentHealth(MaxHealth);
		RefreshCampLabel();
		UpdateCampTimers();
	}

	private void UpdateSpawnedUnitsTeam()
	{
		CleanDeadUnits();

		foreach (var unit in _spawnedUnits)
		{
			if (unit != null && IsInstanceValid(unit))
			{
				int oldUnitTeam = unit.GetTeamId();
				bool wasNeutral = unit.IsNeutralCampUnit;
				unit.SetTeamId(TeamId);
				unit.IsNeutralCampUnit = IsNeutralCamp;
				// Si l'unité perd le statut neutre, recalculer ses HP (retire le x1.5)
				if (wasNeutral && !IsNeutralCamp)
					unit.RecalculateMaxHealth();
				// Si le camp est capturé, désassocier les unités de l'ancien propriétaire
				// pour que l'IA les traite comme roamingUnits et non comme défenseurs de camp ennemi
				if (oldUnitTeam != TeamId && unit.OwnerCamp == this)
					unit.OwnerCamp = null;
			}
		}
	}

	public override void _Ready()
	{
		CampId = _nextCampId++;
		_currentHealth = MaxHealth;

		AddToGroup("camps");

		if (GameManager.Instance != null)
		{
			GameManager.Instance.InitializeTeam(TeamId);
		}

		CreateHealthBar();
		CreateCampIdLabel();
		SetupCampTimers();
		UpdateCampTimers();
		SpawnUnits();

		GD.Print($"Camp #{CampId} cree - Team {TeamId}");
	}

	public override void _Process(double delta)
	{
		UpdateHealthBar();
		CleanDeadUnits();
		GeneratePassiveGold(delta);
		ProcessProductionQueue(delta);
		ProcessShipProductionQueue(delta);
	}

	private void GeneratePassiveGold(double delta)
	{
		// Reseau : seul le peer qui possede ce camp genere son or
		if (!IsLocallyOwned()) return;

		_goldTimer += (float)delta;
		if (_goldTimer >= 1.0f)
		{
			_goldTimer = 0f;
			if (IsNeutralCamp)
			{
				_localGold += GoldPerSecond;
			}
			else if (GameManager.Instance != null && TeamId > 0)
			{
				GameManager.Instance.AddGold(TeamId, GoldPerSecond);
			}
		}
	}
}
