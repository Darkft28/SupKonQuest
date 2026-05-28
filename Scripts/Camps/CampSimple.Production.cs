using Godot;

public partial class CampSimple
{
	private void ProcessProductionQueue(double delta)
	{
		if (!IsRelayModeActive() && !IsLocallyOwned()) return;

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

	public bool ApplyRelayBuyUnit(string unitType)
	{
		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
			return false;

		_productionQueue.Enqueue(unitType);
		return true;
	}

	// IDs des biomes/objets à éviter au spawn (forêt, neige, eau, arbres, montagnes)
	private const int IdSolForet = 3;
	private const int IdSolNeige = 4;
	private const int IdSolEau = 6;
	private const int IdObjetArbreSpawn = 100;
	private const int IdObjetMontagneSpawn = 101;
	private const float SpawnRadius = 525f;
	public const float DefenderRelevanceRadius = 1200f;

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

		int spawnSequence = ++_dynamicUnitSpawnSequence;
		float spawnAngle = GetDeterministicSpawnAngle(spawnSequence);
		unit.GlobalPosition = FindClearSpawnPosition(spawnAngle, SpawnRadius);
		unit.UnitType = unitType;
		unit.TeamId = TeamId;
		unit.IsNeutralCampUnit = false;
		unit.OwnerCamp = this;
		unit.RegionId = RegionId;

		// Reseau : assigner un NetworkId et broadcaster le spawn
		string networkId = BuildDynamicUnitNetworkId(spawnSequence);
		unit.NetworkId = networkId;
		unit.IsLocalAuthority = true;

		GetParent().AddChild(unit);
		_spawnedUnits.Add(unit);

		if (!IsRelayModeActive())
		{
			NetworkSync.Instance?.SendSpawnUnit(networkId, unitType, TeamId,
				unit.GlobalPosition.X, unit.GlobalPosition.Y, unit.GetCurrentHealth(), false);
		}
	}

	private string BuildDynamicUnitNetworkId(int spawnSequence)
	{
		return $"camp_{CampId}_dyn_{spawnSequence}";
	}

	private float GetDeterministicSpawnAngle(int spawnSequence)
	{
		int hash = (CampId * 73856093) ^ (spawnSequence * 19349663);
		hash &= int.MaxValue;
		return (hash / (float)int.MaxValue) * Mathf.Tau;
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
			unit.GlobalPosition = FindClearSpawnPosition(angle, SpawnRadius);
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

	public void RespawnNeutralDefenders()
	{
		ClearCampUnits();
		SpawnUnits();
		UpdateDefendersAuthority();
	}

	private void ClearCampUnits()
	{
		foreach (var unit in _spawnedUnits)
		{
			if (unit != null && IsInstanceValid(unit))
				unit.QueueFree();
		}

		_spawnedUnits.Clear();
		_defenders.Clear();
	}

	public int GetLiveUnitCount()
	{
		CleanDeadUnits();
		return _spawnedUnits.Count;
	}

	public bool CanBuyUnit(string unitType)
	{
		return GetBuyUnitDenyReason(unitType) == null;
	}

	/// <summary>Raison d'achat refusé, ou null si l'achat est possible.</summary>
	public string GetBuyUnitDenyReason(string unitType)
	{
		if (GameManager.Instance == null)
			return "Jeu non initialisé";

		if (IsNeutralCamp)
			return "Camp neutre";

		int unitCount = GameManager.Instance.GetTeamUnitCount(TeamId);
		int maxUnits = GameManager.Instance.GetMaxUnitsForTeam(TeamId);
		if (unitCount >= maxUnits)
			return $"Limite d'unités ({unitCount}/{maxUnits})";

		int totalInQueue = _productionQueue.Count + (_currentProduction != null ? 1 : 0);
		if (totalInQueue >= MaxQueueSize)
			return "File de production pleine";

		if (GameManager.Instance.GetUnlockedTier(TeamId) < GameManager.GetUnitTier(unitType))
			return $"Palier {GameManager.GetUnitTier(unitType)} requis";

		var stats = UnitStats.GetStats(unitType);
		int gold = GameManager.Instance.GetGold(TeamId);
		if (gold < stats.Price)
			return $"Or insuffisant ({gold}g / {stats.Price}g)";

		return null;
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

	public bool IsRelevantDefender(Unit unit)
	{
		if (unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0)
			return false;

		return GlobalPosition.DistanceTo(unit.GlobalPosition) <= DefenderRelevanceRadius;
	}

	public System.Collections.Generic.List<Unit> GetRelevantDefenders()
	{
		var relevant = new System.Collections.Generic.List<Unit>();
		foreach (var unit in GetLiveDefenders())
		{
			if (IsRelevantDefender(unit))
				relevant.Add(unit);
		}
		return relevant;
	}

	public bool AreAllUnitsDefeated()
	{
		return GetRelevantDefenders().Count == 0;
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
