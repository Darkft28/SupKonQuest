using Godot;

public partial class CampSimple
{
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

	private void CleanDeadUnits()
	{
		_spawnedUnits.RemoveAll(unit => unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0);
	}

	public bool AreAllUnitsDefeated()
	{
		CleanDeadUnits();
		return _spawnedUnits.Count == 0;
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
}
