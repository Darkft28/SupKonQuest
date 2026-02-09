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
		ProcessProductionQueue(delta);
		ProcessTurret(delta);
		ProcessCaptureCheck(delta);
		ProcessShipProductionQueue(delta);
	}
	
	// Timer pour vérifier la capture
	private float _captureCheckTimer = 0f;
	private const float CaptureCheckInterval = 0.5f; // Vérifier toutes les 0.5 secondes
	
	private void ProcessCaptureCheck(double delta)
	{
		_captureCheckTimer += (float)delta;
		
		if (_captureCheckTimer >= CaptureCheckInterval)
		{
			_captureCheckTimer = 0f;
			CheckForCapture();
		}
	}
	
	private void CheckForCapture()
	{
		// Si le camp a encore des défenseurs, pas de capture possible
		if (!AreAllUnitsDefeated())
			return;
		
		// Chercher des unités ennemies dans la zone du camp
		int capturingTeamId = FindCapturingTeam();
		
		if (capturingTeamId > 0 && capturingTeamId != TeamId)
		{
			// Une équipe ennemie est présente sans défenseurs -> capture !
			GD.Print($"[CAPTURE] Camp #{CampId} (Team {TeamId}) capture par Team {capturingTeamId} - Plus de defenseurs!");
			CaptureCamp(capturingTeamId);
		}
	}
	
	private int FindCapturingTeam()
	{
		// Chercher les unités dans la zone de capture (même zone que la tourelle)
		var allUnits = GetTree().GetNodesInGroup("units");
		
		// Compter les unités par équipe dans la zone
		Dictionary<int, int> unitsPerTeam = new Dictionary<int, int>();
		
		foreach (var node in allUnits)
		{
			if (node is Unit unit)
			{
				if (!IsInstanceValid(unit) || !unit.IsInsideTree())
					continue;
				
				if (unit.GetCurrentHealth() <= 0)
					continue;
				
				int unitTeamId = unit.GetTeamId();
				
				// Ignorer les unités de notre équipe
				if (unitTeamId == TeamId)
					continue;
				
				// Vérifier si l'unité est dans la zone de capture
				float distance = GlobalPosition.DistanceTo(unit.GlobalPosition);
				
				if (distance <= TurretRange)
				{
					if (!unitsPerTeam.ContainsKey(unitTeamId))
					{
						unitsPerTeam[unitTeamId] = 0;
					}
					unitsPerTeam[unitTeamId]++;
				}
			}
		}
		
		// Trouver l'équipe avec le plus d'unités dans la zone
		int bestTeam = 0;
		int bestCount = 0;
		
		foreach (var pair in unitsPerTeam)
		{
			if (pair.Value > bestCount)
			{
				bestTeam = pair.Key;
				bestCount = pair.Value;
			}
		}
		
		return bestTeam;
	}
	
	private void ProcessTurret(double delta)
	{
		_turretTimer += (float)delta;
		
		if (_turretTimer >= TurretAttackInterval)
		{
			_turretTimer = 0f;  // Reset à 0, pas 60 !
			AttackEnemiesInRange();
		}
	}
	
	private void AttackEnemiesInRange()
	{
		// Les camps neutres n'attaquent pas
		if (IsNeutralCamp)
			return;
		
		// Récupérer toutes les unités
		var allUnits = GetTree().GetNodesInGroup("units");
		
		foreach (var node in allUnits)
		{
			if (node is Unit unit)
			{
				// Vérifier si l'unité est valide et initialisée
				if (!IsInstanceValid(unit))
					continue;
				
				// Vérifier si l'unité est dans l'arbre de scène (initialisée)
				if (!unit.IsInsideTree())
					continue;
				
				// Récupérer le TeamId de l'unité
				int unitTeamId = unit.GetTeamId();
				
				// DEBUG: Afficher les IDs pour diagnostiquer
				// GD.Print($"[DEBUG TURRET] Camp #{CampId} (Team {TeamId}) voit {unit.GetUnitType()} (Team {unitTeamId})");
				
				// Vérifier si c'est un allié (même TeamId)
				if (unitTeamId == TeamId)
					continue; // Allié, on ignore
				
				// Vérifier si l'unité est vivante
				if (unit.GetCurrentHealth() <= 0)
					continue;
				
				// Vérifier la distance
				float distance = GlobalPosition.DistanceTo(unit.GlobalPosition);
				
				if (distance <= TurretRange)
				{
					// Infliger des dégâts
					unit.TakeDamage(TurretDamage);
					GD.Print($"Camp #{CampId} (Team {TeamId}) attaque {unit.GetUnitType()} (Team {unitTeamId}) - Degats: {TurretDamage}");
				}
			}
		}
	}

	private void ProcessProductionQueue(double delta)
	{
		// Si rien en production, prendre le prochain dans la queue
		if (_currentProduction == null && _productionQueue.Count > 0)
		{
			_currentProduction = _productionQueue.Dequeue();
			_productionTimer = UnitStats.GetStats(_currentProduction).ProductionTime;
			GD.Print($"[Camp #{CampId}] Debut production: {_currentProduction} ({_productionTimer}s)");
		}

		// Si une unité est en production, décrémenter le timer
		if (_currentProduction != null)
		{
			_productionTimer -= (float)delta;

			if (_productionTimer <= 0)
			{
				// Production terminée, spawn l'unité
				SpawnPurchasedUnit(_currentProduction);
				GD.Print($"[Camp #{CampId}] Production terminee: {_currentProduction}");
				_currentProduction = null;
			}
		}
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
		_spawnedUnits.RemoveAll(unit => unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0);
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
		
		// Tracker le dernier attaquant
		_lastAttackerTeamId = attackerTeamId;

		SetCurrentHealth(GetCurrentHealth() - damage);

		if (GetCurrentHealth() <= 0)
		{
			CaptureCamp(attackerTeamId);
		}

		return true;
	}
	
	// Appelée quand la dernière unité défendant le camp meurt
	public void OnDefenderDied(int killerTeamId, bool mutualKill)
	{
		// Si mort mutuelle (attaquant et défenseur meurent en même temps)
		if (mutualKill)
		{
			// Le camp devient neutre
			SetTeam(0, true);
			SetCurrentHealth(MaxHealth);
			GD.Print($"Camp #{CampId} devient neutre suite a une mort mutuelle!");
		}
		else
		{
			// Sinon, le camp peut être capturé par l'équipe du tueur
			_lastAttackerTeamId = killerTeamId;
		}
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
		EmitSignal(SignalName.CampCaptured, newTeamId);
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
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = bonusUnits[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = false;

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
		}
	}
	
	private static readonly Color[] _teamColors = new Color[]
	{
		new Color(1, 0, 0, 1f),        // Rouge
		new Color(0, 0.5f, 1, 1f),     // Bleu
		new Color(0, 0.8f, 0, 1f),     // Vert
		new Color(1, 1, 0, 1f),        // Jaune
		new Color(1, 0, 1, 1f),        // Magenta
		new Color(0, 1, 1, 1f),        // Cyan
		new Color(1, 0.5f, 0, 1f),     // Orange
		new Color(0.5f, 0, 1, 1f),     // Violet
		new Color(0.6f, 0.3f, 0, 1f),  // Marron
		new Color(1, 0.4f, 0.7f, 1f),  // Rose
		new Color(0, 0.5f, 0, 1f),     // Vert foncé
		new Color(0.3f, 0.3f, 1, 1f),  // Bleu moyen
		new Color(1, 0.8f, 0, 1f),     // Or
		new Color(0, 0.8f, 0.6f, 1f),  // Turquoise
		new Color(0.8f, 0, 0.4f, 1f),  // Cramoisi
		new Color(0.5f, 0.8f, 0, 1f),  // Chartreuse
		new Color(1, 0.6f, 0.4f, 1f),  // Saumon
		new Color(0.4f, 0, 0.6f, 1f),  // Indigo
		new Color(0, 0.4f, 0.4f, 1f),  // Sarcelle
		new Color(0.8f, 0.8f, 0, 1f),  // Olive
		new Color(0.9f, 0.2f, 0.5f, 1f), // Framboise
		new Color(0.2f, 0.6f, 1, 1f),  // Azur
		new Color(0.7f, 1, 0.3f, 1f),  // Lime
		new Color(1, 0.3f, 0.3f, 1f),  // Corail
		new Color(0.6f, 0.4f, 1, 1f),  // Lavande
		new Color(0, 1, 0.5f, 1f),     // Menthe
		new Color(1, 0.5f, 0.5f, 1f),  // Pêche
		new Color(0.3f, 0, 0.3f, 1f),  // Prune
		new Color(0.4f, 0.7f, 0.4f, 1f), // Sauge
		new Color(0.9f, 0.6f, 0, 1f),  // Ambre
		new Color(0.5f, 0.5f, 1, 1f),  // Pervenche
		new Color(0.8f, 0.5f, 0.2f, 1f), // Cuivre
		new Color(0, 0.6f, 0.3f, 1f),  // Émeraude
		new Color(0.9f, 0, 0.9f, 1f),  // Fuchsia
		new Color(0.4f, 0.8f, 0.8f, 1f), // Aigue-marine
		new Color(0.7f, 0.2f, 0, 1f),  // Rouille
		new Color(0.5f, 1, 0.5f, 1f),  // Vert pâle
		new Color(0.3f, 0.5f, 0.7f, 1f), // Acier
		new Color(1, 0.9f, 0.4f, 1f),  // Crème
		new Color(0.6f, 0, 0.2f, 1f),  // Bordeaux
		new Color(0.2f, 0.8f, 0.4f, 1f), // Jade
		new Color(0.8f, 0.4f, 0.6f, 1f), // Mauve
	};

	private Color GetTeamColor()
	{
		if (TeamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f);

		int colorIndex = (TeamId - 1) % _teamColors.Length;
		return _teamColors[colorIndex];
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
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

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

		// Vérifier si la queue n'est pas pleine
		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
		{
			GD.Print($"File d'attente pleine ({MaxQueueSize} max)");
			return false;
		}

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

		// Ajouter à la file d'attente
		_productionQueue.Enqueue(unitType);
		GD.Print($"Unite {unitType} ajoutee a la file ({_productionQueue.Count}/{MaxQueueSize}) - Cout: {price}, Reste: {GetGold()}");
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
		Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

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

		// Vérifier si la queue n'est pas pleine
		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
			return false;

		var stats = UnitStats.GetStats(unitType);
		bool canAfford = GameManager.Instance.CanAfford(TeamId, stats.Price);

		return canAfford;
	}

	// Méthodes pour l'UI de la file d'attente
	public int GetQueueCount()
	{
		return _productionQueue.Count + (_currentProduction != null ? 1 : 0);
	}

	public int GetMaxQueueSize()
	{
		return MaxQueueSize;
	}

	public string GetCurrentProduction()
	{
		return _currentProduction;
	}

	public float GetProductionProgress()
	{
		if (_currentProduction == null)
			return 0f;

		float totalTime = UnitStats.GetStats(_currentProduction).ProductionTime;
		return 1f - (_productionTimer / totalTime);
	}

	public string[] GetQueuedUnits()
	{
		return _productionQueue.ToArray();
	}

	// --- Production navale ---

	public void SetTileMapSol(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
	}

	private void ProcessShipProductionQueue(double delta)
	{
		if (!HasPort) return;

		if (_currentShipProduction == null && _shipProductionQueue.Count > 0)
		{
			_currentShipProduction = _shipProductionQueue.Dequeue();
			_shipProductionTimer = ShipStats.GetStats(_currentShipProduction).ProductionTime;
			GD.Print($"[Camp #{CampId}] Debut production navale: {_currentShipProduction} ({_shipProductionTimer}s)");
		}

		if (_currentShipProduction != null)
		{
			_shipProductionTimer -= (float)delta;

			if (_shipProductionTimer <= 0)
			{
				SpawnShip(_currentShipProduction);
				GD.Print($"[Camp #{CampId}] Production navale terminee: {_currentShipProduction}");
				_currentShipProduction = null;
			}
		}
	}

	public bool BuyShip(string shipType)
	{
		if (!HasPort) return false;

		var stats = ShipStats.GetStats(shipType);
		int price = stats.Price;
		int currentGold = GetGold();

		GD.Print($"[Camp #{CampId}] Tentative achat bateau {shipType} - Or: {currentGold}, Cout: {price}");

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize)
		{
			GD.Print($"File navale pleine ({MaxShipQueueSize} max)");
			return false;
		}

		if (currentGold < price)
		{
			GD.Print($"Pas assez d'or pour acheter {shipType} (cout: {price}, or: {currentGold})");
			return false;
		}

		if (IsNeutralCamp)
		{
			_localGold -= price;
		}
		else
		{
			if (GameManager.Instance == null) return false;
			if (!GameManager.Instance.SpendGold(TeamId, price)) return false;
		}

		_shipProductionQueue.Enqueue(shipType);
		GD.Print($"Bateau {shipType} ajoute a la file navale ({_shipProductionQueue.Count}/{MaxShipQueueSize}) - Cout: {price}");
		return true;
	}

	public bool CanBuyShip(string shipType)
	{
		if (!HasPort) return false;
		if (GameManager.Instance == null) return false;

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize) return false;

		var stats = ShipStats.GetStats(shipType);
		return GameManager.Instance.CanAfford(TeamId, stats.Price);
	}

	private void SpawnShip(string shipType)
	{
		var shipScene = GD.Load<PackedScene>("res://Scenes/Ship.tscn");
		if (shipScene == null)
		{
			GD.PrintErr("Impossible de charger Ship.tscn");
			return;
		}

		var ship = shipScene.Instantiate<Ship>();
		ship.ShipType = shipType;
		ship.TeamId = TeamId;

		// Positionner le bateau sur l'eau pres du port avec decalage
		Vector2 portGlobalPos = GetPortGlobalPosition();
		Vector2 spawnPos = FindWaterSpawnPosition(portGlobalPos);

		// Decaler les bateaux pour eviter l'empilement
		_spawnedShips.RemoveAll(s => s == null || !IsInstanceValid(s));
		if (_spawnedShips.Count > 0)
		{
			float angle = _spawnedShips.Count * Mathf.Tau / 6f;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f;
			Vector2 offsetPos = spawnPos + offset;
			Vector2I offsetTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(offsetPos));
			if (_tileMapSol != null && _tileMapSol.GetCellSourceId(offsetTile) == 6)
				spawnPos = offsetPos;
		}

		ship.GlobalPosition = spawnPos;
		if (_tileMapSol != null)
		{
			ship.SetTileMapSol(_tileMapSol);
		}

		GetParent().AddChild(ship);
		_spawnedShips.Add(ship);
		GD.Print($"[Camp #{CampId}] Bateau {shipType} spawne a {spawnPos}");
	}

	private Vector2 FindWaterSpawnPosition(Vector2 portPos)
	{
		if (_tileMapSol == null) return portPos;

		Vector2I portTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(portPos));

		// Passe 1: chercher eau profonde (entouree d'eau) a partir de radius 2
		// pour eviter de spawner au bord de la cote
		for (int radius = 2; radius <= 8; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = portTile + new Vector2I(dx, dy);
					if (IsDeepWaterTile(checkTile))
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		// Passe 2: fallback sur simple tuile d'eau (radius 2+)
		for (int radius = 2; radius <= 8; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = portTile + new Vector2I(dx, dy);
					if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		return portPos;
	}

	private bool IsDeepWaterTile(Vector2I tile)
	{
		// Verifier que la tuile ET ses 8 voisins sont de l'eau
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				if (_tileMapSol.GetCellSourceId(tile + new Vector2I(dx, dy)) != 6)
					return false;
			}
		}
		return true;
	}

	public Vector2 GetPortGlobalPosition()
	{
		if (_portSprite != null)
		{
			return _portSprite.GlobalPosition;
		}
		return GlobalPosition;
	}

	public int GetShipQueueCount()
	{
		return _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
	}

	public int GetMaxShipQueueSize()
	{
		return MaxShipQueueSize;
	}

	public string GetCurrentShipProduction()
	{
		return _currentShipProduction;
	}

	public float GetShipProductionProgress()
	{
		if (_currentShipProduction == null) return 0f;
		float totalTime = ShipStats.GetStats(_currentShipProduction).ProductionTime;
		return 1f - (_shipProductionTimer / totalTime);
	}

	public string[] GetQueuedShips()
	{
		return _shipProductionQueue.ToArray();
	}

	public void TrySpawnPort(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
		if (tileMapSol == null)
			return;

		const int TileSize = 128;
		const float CampScale = 4.5f;
		const float PortScale = 0.15f;
		const float LandOverlap = 0.2f; // 20% du port sur terre, 80% dans l'eau
		const float PortLongAxis = 1256f; // longueur en pixels des deux textures

		Vector2I campTile = tileMapSol.LocalToMap(GlobalPosition);

		// Directions cardinales : N, S, E, W
		Vector2I[] directions = new Vector2I[]
		{
			new Vector2I(0, -1), // Nord
			new Vector2I(0, 1),  // Sud
			new Vector2I(1, 0),  // Est
			new Vector2I(-1, 0), // Ouest
		};

		// 1) Trouver la meilleure direction (plus d'eau)
		int bestWaterCount = 0;
		int bestDirectionIndex = -1;

		for (int d = 0; d < directions.Length; d++)
		{
			int waterCount = 0;
			Vector2I dir = directions[d];

			for (int dist = 1; dist <= 8; dist++)
			{
				for (int offset = -2; offset <= 2; offset++)
				{
					Vector2I tilePos;
					if (dir.X == 0)
						tilePos = campTile + new Vector2I(offset, dir.Y * dist);
					else
						tilePos = campTile + new Vector2I(dir.X * dist, offset);

					if (tileMapSol.GetCellSourceId(tilePos) == 6)
						waterCount++;
				}
			}

			if (waterCount > bestWaterCount)
			{
				bestWaterCount = waterCount;
				bestDirectionIndex = d;
			}
		}

		if (bestWaterCount < 3 || bestDirectionIndex < 0)
			return;

		// 2) Trouver la distance de la première tuile d'eau (ligne centrale, offset=0)
		Vector2I bestDir = directions[bestDirectionIndex];
		int waterDist = -1;
		for (int dist = 1; dist <= 8; dist++)
		{
			Vector2I tilePos;
			if (bestDir.X == 0)
				tilePos = campTile + new Vector2I(0, bestDir.Y * dist);
			else
				tilePos = campTile + new Vector2I(bestDir.X * dist, 0);

			if (tileMapSol.GetCellSourceId(tilePos) == 6)
			{
				waterDist = dist;
				break;
			}
		}

		if (waterDist < 0)
			waterDist = 3;

		HasPort = true;
		_portSprite = new Sprite2D();

		// 3) Calculer la position du port
		// Côte = bord entre dernière tuile terre et première tuile eau
		// En monde : (waterDist - 0.5) * TileSize depuis le centre du camp
		// En local camp (scale 3) : diviser par CampScale
		float coastLocalDist = (waterDist - 0.5f) * TileSize / CampScale;

		// Demi-longueur du port en coordonnées locales du camp
		float halfLen = PortLongAxis * PortScale / 2f;
		// Décalage du centre vers l'eau pour avoir 20% terre / 80% eau
		float shift = (1f - 2f * LandOverlap) * halfLen;

		string texturePath;
		Vector2 portPosition;

		switch (bestDirectionIndex)
		{
			case 0: // Nord - eau vers Y négatif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.Rotation = -Mathf.Pi / 2f;
				portPosition = new Vector2(0, -(coastLocalDist + shift));
				break;
			case 1: // Sud - eau vers Y positif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.Rotation = Mathf.Pi / 2f;
				portPosition = new Vector2(0, coastLocalDist + shift);
				break;
			case 2: // Est - eau vers X positif
				texturePath = "res://Assets/Objects/Port.png";
				portPosition = new Vector2(coastLocalDist + shift, 0);
				break;
			case 3: // Ouest - eau vers X négatif
				texturePath = "res://Assets/Objects/Port.png";
				_portSprite.FlipH = true;
				portPosition = new Vector2(-(coastLocalDist + shift), 0);
				break;
			default:
				return;
		}

		_portSprite.Texture = GD.Load<Texture2D>(texturePath);
		_portSprite.Position = portPosition;
		_portSprite.Scale = new Vector2(PortScale, PortScale);
		AddChild(_portSprite);

		string[] dirNames = { "Nord", "Sud", "Est", "Ouest" };
		GD.Print($"[PORT] Camp #{CampId} - Port direction {dirNames[bestDirectionIndex]} (eau a {waterDist} tuiles, {bestWaterCount} tuiles d'eau)");
	}
}
