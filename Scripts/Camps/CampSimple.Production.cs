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

	// IDs des biomes/objets à éviter au spawn (forêt, neige, eau, arbres, montagnes)
	private const int IdSolForet = 3;
	private const int IdSolNeige = 4;
	private const int IdSolEau = 6;
	private const int IdObjetArbreSpawn = 100;
	private const int IdObjetMontagneSpawn = 101;

	private bool IsSpawnBlocked(Vector2 worldPos)
	{
		if (_tileMapSol == null) return false;
		Vector2I tc = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(worldPos));
		int solId = _tileMapSol.GetCellSourceId(tc);
		if (solId == IdSolForet || solId == IdSolNeige || solId == IdSolEau) return true;
		if (_tileMapObjets != null)
		{
			int objId = _tileMapObjets.GetCellSourceId(tc);
			if (objId == IdObjetArbreSpawn || objId == IdObjetMontagneSpawn) return true;
		}
		return false;
	}

	private Vector2 FindClearSpawnPosition(float angle, float dist)
	{
		Vector2 pos = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
		if (!IsSpawnBlocked(pos)) return pos;

		for (int i = 1; i < 16; i++)
		{
			float a = angle + i * (Mathf.Tau / 16f);
			pos = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
			if (!IsSpawnBlocked(pos)) return pos;
		}

		for (float d = dist + 128f; d <= dist + 512f; d += 128f)
		{
			for (int i = 0; i < 8; i++)
			{
				float a = i * (Mathf.Tau / 8f);
				pos = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
				if (!IsSpawnBlocked(pos)) return pos;
			}
		}

		return GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private void SpawnPurchasedUnit(string unitType)
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		var unit = unitScene.Instantiate<Unit>();

		float spawnAngle = (float)GD.RandRange(0, Mathf.Tau);
		unit.GlobalPosition = FindClearSpawnPosition(spawnAngle, 525f);
		unit.UnitType = unitType;
		unit.TeamId = TeamId;
		unit.IsNeutralCampUnit = false;
		unit.OwnerCamp = this;
		unit.RegionId = RegionId;

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
			unit.GlobalPosition = FindClearSpawnPosition(angle, 525f);
			unit.UnitType = UnitTypes[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = IsNeutralCamp;
			unit.OwnerCamp = this;
			unit.RegionId = RegionId;

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
