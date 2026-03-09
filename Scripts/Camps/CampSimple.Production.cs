using Godot;

public partial class CampSimple
{
	private void ProcessProductionQueue(double delta)
	{
		if (!IsLocallyOwned()) return;

		if (_currentProduction == null && _productionQueue.Count > 0)
		{
			_currentProduction = _productionQueue.Dequeue();
			_productionTimer = UnitStats.GetStats(_currentProduction).ProductionTime;
		}

		if (_currentProduction != null)
		{
			_productionTimer -= (float)delta;

			if (_productionTimer <= 0)
			{
				SpawnPurchasedUnit(_currentProduction);
				_currentProduction = null;
			}
		}
	}

	public bool BuyUnit(string unitType)
	{
		var stats = UnitStats.GetStats(unitType);
		int price = stats.Price;
		int currentGold = GetGold();

		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
			return false;

		if (currentGold < price)
			return false;

		if (IsNeutralCamp)
		{
			_localGold -= price;
		}
		else
		{
			if (GameManager.Instance == null)
				return false;
			if (!GameManager.Instance.SpendGold(TeamId, price))
				return false;
		}

		_productionQueue.Enqueue(unitType);
		return true;
	}

	private void SpawnPurchasedUnit(string unitType)
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		var unit = unitScene.Instantiate<Unit>();

		float angle = (float)GD.RandRange(0, Mathf.Tau);
		Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

		unit.GlobalPosition = GlobalPosition + offset;
		unit.UnitType = unitType;
		unit.TeamId = TeamId;
		unit.IsNeutralCampUnit = false;
		unit.OwnerCamp = this;

		// Reseau : assigner un NetworkId et broadcaster le spawn
		string networkId = NetworkEntityRegistry.GenerateId();
		unit.NetworkId = networkId;
		unit.IsLocalAuthority = true;

		GetParent().AddChild(unit);
		_spawnedUnits.Add(unit);

		NetworkSync.Instance?.SendSpawnUnit(networkId, unitType, TeamId,
			unit.GlobalPosition.X, unit.GlobalPosition.Y, unit.GetCurrentHealth(), false);
	}

	private void SpawnUnits()
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
		{
			GD.PrintErr("Impossible de charger Unit.tscn");
			return;
		}

		Vector2 campPos = GlobalPosition;

		for (int i = 0; i < UnitTypes.Length; i++)
		{
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / UnitTypes.Length;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = UnitTypes[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = IsNeutralCamp;
			unit.OwnerCamp = this;

			// ID deterministe pour les defenseurs initiaux (sync reseau)
			unit.NetworkId = $"camp_{CampId}_unit_{i}";

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
			_defenders.Add(unit);
		}
	}

	public int GetLiveUnitCount()
	{
		CleanDeadUnits();
		return _spawnedUnits.Count;
	}

	public bool CanBuyUnit(string unitType)
	{
		if (GameManager.Instance == null)
			return false;

		if (GetLiveUnitCount() >= MaxLiveUnitsPerCamp)
			return false;

		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
			return false;

		var stats = UnitStats.GetStats(unitType);
		return GameManager.Instance.CanAfford(TeamId, stats.Price);
	}

	private void CleanDeadUnits()
	{
		_spawnedUnits.RemoveAll(unit => unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0);
		_defenders.RemoveAll(unit => unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0);
	}

	public System.Collections.Generic.List<Unit> GetLiveDefenders()
	{
		CleanDeadUnits();
		return new System.Collections.Generic.List<Unit>(_defenders);
	}

	public bool AreAllUnitsDefeated()
	{
		CleanDeadUnits();
		return _defenders.Count == 0;
	}

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

	public void RefundProductionQueue(int refundTeamId)
	{
		// Pas de remboursement pour les camps neutres (or local, pas de GameManager)
		if (refundTeamId <= 0) return;
		if (GameManager.Instance == null) return;

		int totalRefund = 0;

		// Rembourser l'unité en cours de production
		if (_currentProduction != null)
		{
			int price = UnitStats.GetStats(_currentProduction).Price;
			totalRefund += price;
			_currentProduction = null;
			_productionTimer = 0f;
		}

		// Rembourser toutes les unités dans la file
		while (_productionQueue.Count > 0)
		{
			string unitType = _productionQueue.Dequeue();
			int price = UnitStats.GetStats(unitType).Price;
			totalRefund += price;
		}

		if (totalRefund > 0)
			GameManager.Instance.AddGold(refundTeamId, totalRefund);
	}
}
