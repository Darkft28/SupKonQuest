using Godot;
using System.Collections.Generic;
public partial class CampSimple : Area2D
{
	[Export] public int TeamId = 1; // id équipe
	[Export] public bool IsNeutralCamp = false; // camp neutre
	[Export] public float MaxHealth = 500f; // pv max du camp
	[Export] public int GoldPerSecond = 3; // or généré par seconde

	private float _currentHealth;
	private float _goldTimer = 0f;
	private int _localGold = 0; // Or local pour les camps neutres
	
	// id unique camp
	public int CampId;
	private static int _nextCampId = 1;
	
	private Label _campIdLabel;

	// liste des unites spawned par ce camp (pour verifier si elles sont mortes)
	private List<Unit> _spawnedUnits = new List<Unit>();

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
	
	private void CreateCampIdLabel()
	{
		_campIdLabel = new Label();
		_campIdLabel.Text = $"#{CampId}";
		_campIdLabel.Position = new Vector2(-20, -130);
		_campIdLabel.AddThemeFontSizeOverride("font_size", 20);
		_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
		_campIdLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		_campIdLabel.AddThemeConstantOverride("outline_size", 3);
		AddChild(_campIdLabel);
	}
	
	private void CreateHealthBar()
	{
		//fond noir
		_healthBarBackground = new ColorRect();
		_healthBarBackground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarBackground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarBackground.Color = new Color(0, 0, 0, 0.8f);
		AddChild(_healthBarBackground);

		//bare de vie à la couleur de l'équipe
		_healthBarForeground = new ColorRect();
		_healthBarForeground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarForeground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarForeground.Color = GetTeamColor();
		AddChild(_healthBarForeground);
	}
	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		//update des infos
		UpdateHealthBar();
		CleanDeadUnits();
		GeneratePassiveGold(delta);
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
	
	private void UpdateHealthBar()
	{
		if (_healthBarForeground == null)
			return;

		float healthPercent = GetCurrentHealth() / MaxHealth;
		_healthBarForeground.Size = new Vector2(HealthBarWidth * healthPercent, HealthBarHeight);
	}

	private void CleanDeadUnits()
	{
		_spawnedUnits.RemoveAll(unit => unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth <= 0);
	}

	
	public bool AreAllUnitsDefeated()
	{
		CleanDeadUnits();
		return _spawnedUnits.Count == 0;
	}

	
	public bool TakeDamage(float damage, int attackerTeamId)
	{
		//attaquable seulement si les unitées sont mortes
		if (!AreAllUnitsDefeated())
			return false;

		SetCurrentHealth(GetCurrentHealth() - damage);

		if (GetCurrentHealth() <= 0)
		{
			CaptureCamp(attackerTeamId);
		}

		return true;
	}
	
	private void CaptureCamp(int newTeamId)
	{
		int oldTeamId = TeamId;
		TeamId = newTeamId;
		IsNeutralCamp = false; //les camps neutre ne le sont plus apt=res capture

		
		SetCurrentHealth(MaxHealth);

		//changement couleur barre de vie
		if (_healthBarForeground != null)
		{
			_healthBarForeground.Color = GetTeamColor();
		}

		//or gagné pour la capture
		if (GameManager.Instance != null)
		{
			GameManager.Instance.GiveCaptureBonus(newTeamId);
		}

		//spawn quelques troupes après capture
		SpawnBonusUnits();

		GD.Print($"Camp capture! Equipe {oldTeamId} -> Equipe {newTeamId}");
	}
	
	//spawn 3 troupes
	private void SpawnBonusUnits()
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;
		
		//position du camp
		Vector2 campPos = GlobalPosition;

		
		string[] bonusUnits = new[] { "Infantry", "Range", "Infantry" };
		
		//spawn en cercle
		for (int i = 0; i < bonusUnits.Length; i++)
		{
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / bonusUnits.Length;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 700f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = bonusUnits[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = false;

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
		}
	}
	
	private Color GetTeamColor()
	{
		Color[] teamColors = new Color[]
		{
			new Color(1, 0, 0, 1f),      // Rouge (Team 1)
			new Color(0, 0.5f, 1, 1f),   // Bleu (Team 2)
			new Color(0, 1, 0, 1f),      // Vert (Team 3)
			new Color(1, 1, 0, 1f),      // Jaune (Team 4)
			new Color(1, 0, 1, 1f),      // Magenta (Team 5)
			new Color(0, 1, 1, 1f),      // Cyan (Team 6)
			new Color(1, 0.5f, 0, 1f),   // Orange (Team 7)
			new Color(0.5f, 0, 1, 1f),   // Violet (Team 8)
		};

		if (TeamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f); // Gris pour les camps neutres/invalides

		int colorIndex = (TeamId - 1) % teamColors.Length;
		return teamColors[colorIndex];
	}

	private void SpawnUnits()
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
		{
			GD.PrintErr("Impossible de charger Unit.tscn");
			return;
		}

		//position du camp
		Vector2 campPos = GlobalPosition;

		//spawn en cercle
		for (int i = 0; i < UnitTypes.Length; i++)
		{
			var unit = unitScene.Instantiate<Unit>();

			//caclul de la position de spawn
			float angle = (i * Mathf.Tau) / UnitTypes.Length;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 700f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = UnitTypes[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = IsNeutralCamp; //plus fortes

			//ajoute l'unité au camp
			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
		}
	}
	
	public bool BuyUnit(string unitType)
	{
		var stats = UnitStats.GetStats(unitType);
		int price = stats.Price;
		int currentGold = GetGold();

		GD.Print($"[Camp #{CampId}] Tentative achat {unitType} - Or: {currentGold}, Cout: {price}, IsNeutral: {IsNeutralCamp}, TeamId: {TeamId}");

		// Vérifier si on a assez d'or
		if (currentGold < price)
		{
			GD.Print($"Pas assez d'or pour acheter {unitType} (cout: {price}, or: {currentGold})");
			return false;
		}

		// Dépenser l'or selon le type de camp
		if (IsNeutralCamp)
		{
			_localGold -= price;
		}
		else
		{
			if (GameManager.Instance == null)
				return false;
			if (!GameManager.Instance.SpendGold(TeamId, price))
			{
				GD.Print($"GameManager.SpendGold a échoué pour TeamId {TeamId}");
				return false;
			}
		}

		// Spawn l'unité
		SpawnPurchasedUnit(unitType);
		GD.Print($"Unite {unitType} achetee pour {price} or! (reste: {GetGold()})");
		return true;
	}
	
	private void SpawnPurchasedUnit(string unitType)
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		var unit = unitScene.Instantiate<Unit>();

		//random positionement
		float angle = (float)GD.RandRange(0, Mathf.Tau);
		Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 700f;

		unit.GlobalPosition = GlobalPosition + offset;
		unit.UnitType = unitType;
		unit.TeamId = TeamId;
		unit.IsNeutralCampUnit = false;

		GetParent().AddChild(unit);
		_spawnedUnits.Add(unit);
	}
	
	public bool CanBuyUnit(string unitType)
	{
		if (GameManager.Instance == null)
		{
			GD.Print($"Camp #{CampId}: GameManager.Instance est null!");
			return false;
		}

		var stats = UnitStats.GetStats(unitType);
		int gold = GameManager.Instance.GetGold(TeamId);
		bool canAfford = GameManager.Instance.CanAfford(TeamId, stats.Price);
		
		return canAfford;
	}
}
