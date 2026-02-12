using Godot;
using System.Collections.Generic;
public partial class CampSimple : Area2D
{
	[Signal] public delegate void CampCapturedEventHandler(int newTeamId);

	[Export] public int TeamId = 1; // id équipe
	[Export] public bool IsNeutralCamp = false; // camp neutre
	[Export] public float MaxHealth = 500f; // pv max du camp
	[Export] public int GoldPerSecond = 50; // or généré par seconde (50 pour tests)

	// Paramètres de la tourelle
	[Export] public float TurretDamage = 10f; // dégâts par seconde
	[Export] public float TurretRange = 600f; // portée de la tourelle
	private float _turretTimer = 0f;
	private const float TurretAttackInterval = 1f; // attaque toutes les secondes

	private float _currentHealth;
	private float _goldTimer = 0f;
	private int _localGold = 0; // Or local pour les camps neutres

	// Tracking du dernier attaquant pour la mécanique de capture
	private int _lastAttackerTeamId = 0;

	// id unique camp
	public int CampId;
	private static int _nextCampId = 1;

	private Label _campIdLabel;

	// liste des unites spawned par ce camp (pour verifier si elles sont mortes)
	private List<Unit> _spawnedUnits = new List<Unit>();

	// File d'attente de production
	private Queue<string> _productionQueue = new Queue<string>();
	private string _currentProduction = null;
	private float _productionTimer = 0f;
	private const int MaxQueueSize = 7;

	// Port
	public bool HasPort { get; private set; }
	private Sprite2D _portSprite;

	// File d'attente de production navale
	private Queue<string> _shipProductionQueue = new Queue<string>();
	private string _currentShipProduction = null;
	private float _shipProductionTimer = 0f;
	private const int MaxShipQueueSize = 5;
	private List<Ship> _spawnedShips = new List<Ship>();
	private TileMapLayer _tileMapSol;

	//barre de vie
	private ColorRect _healthBarBackground;
	private ColorRect _healthBarForeground;
	private const float HealthBarWidth = 100f;
	private const float HealthBarHeight = 10f;

	//liste des unités
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

	public void SetTeam(int newTeamId, bool isNeutral)
	{
		int oldTeamId = TeamId;
		TeamId = newTeamId;
		IsNeutralCamp = isNeutral;

		GD.Print($"[DEBUG] Camp #{CampId} change d'equipe: {oldTeamId} -> {newTeamId} (Neutre: {isNeutral})");

		// Mettre à jour la couleur de la barre de vie
		if (_healthBarForeground != null)
		{
			_healthBarForeground.Color = GetTeamColor();
		}

		// Mettre à jour la couleur du label
		if (_campIdLabel != null)
		{
			_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
		}

		// Initialiser l'équipe dans le GameManager
		if (!isNeutral && GameManager.Instance != null)
		{
			GameManager.Instance.InitializeTeam(TeamId);
		}

		// IMPORTANT: Mettre à jour le TeamId de toutes les unités déjà spawned
		UpdateSpawnedUnitsTeam();
	}

	private void UpdateSpawnedUnitsTeam()
	{
		// Nettoyer les unités mortes d'abord
		CleanDeadUnits();

		foreach (var unit in _spawnedUnits)
		{
			if (unit != null && IsInstanceValid(unit))
			{
				int oldUnitTeam = unit.GetTeamId();
				unit.SetTeamId(TeamId);
				unit.IsNeutralCampUnit = IsNeutralCamp;
				GD.Print($"[DEBUG] Unite {unit.GetUnitType()} mise a jour: Team {oldUnitTeam} -> {TeamId}");
			}
		}
	}

	public override void _Ready()
	{
		//assignations de base
		CampId = _nextCampId++;
		_currentHealth = MaxHealth;

		// Ajouter le camp au groupe "camps" pour la recherche optimisee
		AddToGroup("camps");

		//initialisation équipe
		if (GameManager.Instance != null)
		{
			GameManager.Instance.InitializeTeam(TeamId);
		}

		//création d'ui
		CreateHealthBar();
		CreateCampIdLabel();

		//spawn des unitées
		SpawnUnits();

		GD.Print($"Camp #{CampId} cree - Team {TeamId}");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		//update des infos
		UpdateHealthBar();
		CleanDeadUnits();
		GeneratePassiveGold(delta);
		ProcessProductionQueue(delta);
		ProcessTurret(delta);
		ProcessShipProductionQueue(delta);
	}

	private void GeneratePassiveGold(double delta)
	{
		_goldTimer += (float)delta;
		if (_goldTimer >= 1.0f)
		{
			_goldTimer = 0f;
			if (IsNeutralCamp)
			{
				// Les camps neutres stockent leur or localement
				_localGold += GoldPerSecond;
			}
			else if (GameManager.Instance != null && TeamId > 0)
			{
				// Les camps d'équipe utilisent le GameManager
				GameManager.Instance.AddGold(TeamId, GoldPerSecond);
			}
		}
	}
}
