using Godot;
using System.Collections.Generic;

public partial class CampSimple
{
	private static readonly PackedScene PortScene = GD.Load<PackedScene>("res://Scenes/Port.tscn");

	private Sprite2D CreatePortVisual()
	{
		var port = PortScene?.Instantiate<Sprite2D>() ?? new Sprite2D();
		if (port.Texture == null)
			port.Texture = GD.Load<Texture2D>("res://Assets/Objects/Port.png");
		return port;
	}

	public void SetTileMapSol(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
	}

	private void ProcessShipProductionQueue(double delta)
	{
		if (!HasPort) return;
		// Relay mode: only the camp owner advances the queue (spawn is relayed via SpawnShip opcode).
		if (!IsLocallyOwned()) return;

		if (_currentShipProduction == null && _shipProductionQueue.Count > 0)
		{
			_currentShipProduction = _shipProductionQueue.Dequeue();
			_shipProductionTimer = ShipStats.GetStats(_currentShipProduction).ProductionTime;
		}

		if (_currentShipProduction != null)
		{
			_shipProductionTimer -= (float)delta;

			if (_shipProductionTimer <= 0)
			{
				SpawnShip(_currentShipProduction);
				_currentShipProduction = null;
			}
		}
	}

	public bool BuyShip(string shipType)
	{
		if (!HasPort) return false;
		if (ShipStats.IsFleetAtCapacity(TeamId, GetTree())) return false;

		var stats = ShipStats.GetStats(shipType);
		int price = stats.Price;
		int currentGold = GetGold();

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize)
			return false;

		if (currentGold < price)
			return false;

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
		return true;
	}

	public bool CanBuyShip(string shipType)
	{
		if (!HasPort) return false;
		if (GameManager.Instance == null) return false;
		if (ShipStats.IsFleetAtCapacity(TeamId, GetTree())) return false;

		int totalInQueue = _shipProductionQueue.Count + (_currentShipProduction != null ? 1 : 0);
		if (totalInQueue >= MaxShipQueueSize) return false;

		if (GameManager.Instance.GetUnlockedTier(TeamId) < GameManager.GetShipTier(shipType)) return false;

		var stats = ShipStats.GetStats(shipType);
		return GameManager.Instance.CanAfford(TeamId, stats.Price);
	}

	public bool ApplyRelayBuyShip(string shipType)
	{
		// Remote peers do not simulate the queue: the owner sends SpawnShip when production ends.
		return HasPort;
	}

	private void SpawnShip(string shipType)
	{
		var shipScene = GD.Load<PackedScene>("res://Scenes/Ship.tscn");
		if (shipScene == null)
		{
			GD.PrintErr("Failed to load Ship.tscn");
			return;
		}

		var ship = shipScene.Instantiate<Ship>();
		ship.ShipType = shipType;
		ship.TeamId = TeamId;

		// Place the ship on water near the port with an offset
		Vector2 portGlobalPos = GetPortGlobalPosition();
		Vector2 spawnPos = FindWaterSpawnPosition(portGlobalPos);

		// Offset ships to avoid stacking
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

		int spawnSequence = ++_dynamicShipSpawnSequence;
		string networkId = BuildDynamicShipNetworkId(spawnSequence);
		ship.NetworkId = networkId;
		ship.IsLocalAuthority = true;

		GetParent().AddChild(ship);
		_spawnedShips.Add(ship);

		if (IsOnlineMultiplayer())
		{
			NetworkCommandRouter.SendSpawnShip(networkId, shipType, TeamId,
				ship.GlobalPosition.X, ship.GlobalPosition.Y, ship.GetCurrentHealth());
		}
	}

	public void ApplyRemotePortPlacement(float posX, float posY, float rotation, bool flipH)
	{
		if (HasPort) return;

		const float PortScale = 0.07f;
		HasPort = true;
		_portSprite = CreatePortVisual();
		_portSprite.Scale = new Vector2(PortScale, PortScale);
		_portSprite.Rotation = rotation;
		_portSprite.FlipH = flipH;
		AddChild(_portSprite);
		_portSprite.GlobalPosition = new Vector2(posX, posY);
	}

	private string BuildDynamicShipNetworkId(int spawnSequence)
	{
		return $"camp_{CampId}_ship_{spawnSequence}";
	}

	/// <summary>Navigable water point near a world position (naval offense AI usage).</summary>
	public Vector2 FindWaterApproachNear(Vector2 nearWorldPos)
	{
		return FindWaterSpawnPosition(nearWorldPos);
	}

	public bool IsWaterAtWorldPos(Vector2 worldPos)
	{
		if (_tileMapSol == null) return false;
		Vector2I tileCoords = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(worldPos));
		return _tileMapSol.GetCellSourceId(tileCoords) == 6;
	}

	private Vector2 FindWaterSpawnPosition(Vector2 portPos)
	{
		if (_tileMapSol == null) return portPos;

		Vector2I portTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(portPos));

		// Pass 1: search for deep water (surrounded by water) from radius 2
		// to avoid spawning at the coastline edge
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

		// Pass 2: fallback to any water tile (radius 2+)
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
		// Ensure the tile and all 8 neighbors are water
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

	public const int PortCost = 500;

	public bool CanBuyPort()
	{
		if (HasPort) return false;
		if (IsNeutralCamp) return false;
		if (GameManager.Instance == null) return false;
		return GameManager.Instance.CanAfford(TeamId, PortCost);
	}

	public bool BuyPort()
	{
		if (!CanBuyPort()) return false;
		if (!GameManager.Instance.SpendGold(TeamId, PortCost)) return false;
		return true;
	}

	/// <summary>
	/// Returns the number of reachable water tiles in the best direction (AI camp ranking usage).
	/// </summary>
	public int GetNearbyWaterCount()
	{
		if (_tileMapSol == null) return 0;
		Vector2I campTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(GlobalPosition));
		Vector2I[] directions = { new Vector2I(0,-1), new Vector2I(0,1), new Vector2I(1,0), new Vector2I(-1,0) };
		int best = 0;
		foreach (var dir in directions)
		{
			int waterCount = 0;
			for (int dist = 1; dist <= 8; dist++)
				for (int offset = -2; offset <= 2; offset++)
				{
					Vector2I tilePos = dir.X == 0
						? campTile + new Vector2I(offset, dir.Y * dist)
						: campTile + new Vector2I(dir.X * dist, offset);
					if (_tileMapSol.GetCellSourceId(tilePos) == 6)
						waterCount++;
				}
			if (waterCount > best) best = waterCount;
		}
		return best;
	}

	/// <summary>
	/// AI port placement: same validation as the player (PlacePortAt + coastal territory across map).
	/// </summary>
	public bool TryAIPlacePort()
	{
		if (!CanBuyPort()) return false;
		if (_tileMapSol == null) return false;
		if (!BuyPort()) return false;

		var shoreline = TerritoryManager.Instance?.EnumerateShorelinePositionsForTeam(TeamId);
		if (shoreline != null)
		{
			foreach (Vector2 worldPos in shoreline)
			{
				if (PlacePortAt(worldPos))
					return true;
			}
		}

		GameManager.Instance?.AddGold(TeamId, PortCost);
		return false;
	}

	public bool PlacePortAt(Vector2 worldPos)
	{
		if (_tileMapSol == null) return false;

		Vector2I clickedTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(worldPos));
		if (_tileMapSol.GetCellSourceId(clickedTile) == 6)
			return false;

		// Count water tiles in each cardinal direction
		Vector2I[] directions = { new Vector2I(0, -1), new Vector2I(0, 1), new Vector2I(1, 0), new Vector2I(-1, 0) };
		float[]  rotations  = { -Mathf.Pi / 2f, Mathf.Pi / 2f, 0f, 0f };
		bool[]   flips      = { false, false, false, true };

		int bestWaterCount = 0;
		int bestDir = -1;
		bool hasAdjacentWater = false;

		for (int d = 0; d < directions.Length; d++)
		{
			Vector2I dir = directions[d];

			// Manual port placement requires a coastal land tile with immediate adjacent water.
			Vector2I immediateWaterTile = clickedTile + dir;
			if (_tileMapSol.GetCellSourceId(immediateWaterTile) != 6)
				continue;

			hasAdjacentWater = true;

			int waterCount = 0;

			for (int dist = 1; dist <= 8; dist++)
				for (int offset = -2; offset <= 2; offset++)
				{
					Vector2I tilePos = dir.X == 0
						? clickedTile + new Vector2I(offset, dir.Y * dist)
						: clickedTile + new Vector2I(dir.X * dist, offset);

					if (_tileMapSol.GetCellSourceId(tilePos) == 6)
						waterCount++;
				}

			if (waterCount > bestWaterCount)
			{
				bestWaterCount = waterCount;
				bestDir = d;
			}
		}

		if (!hasAdjacentWater || bestWaterCount < 1 || bestDir < 0)
			return false;

		// Interpret the click as the coastal land tile and anchor from tile center
		// to avoid erratic placements when clicking inside a camp.
		Vector2 shorelineTileCenter = _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(clickedTile));

		// Shift sprite center toward water.
		// halfLen in world space = texture_length * sprite_scale * camp_scale / 2
		const float PortLongAxis = 1256f;
		const float PortScale    = 0.07f;
		float halfLen = PortLongAxis * PortScale * Scale.X / 2f;
		Vector2 waterDir2D = new Vector2(directions[bestDir].X, directions[bestDir].Y);
		Vector2 spriteCenter = shorelineTileCenter + waterDir2D * halfLen;

		HasPort = true;
		_portSprite = CreatePortVisual();
		_portSprite.Scale = new Vector2(PortScale, PortScale);
		_portSprite.Rotation = rotations[bestDir];
		_portSprite.FlipH    = flips[bestDir];
		AddChild(_portSprite);
		_portSprite.GlobalPosition = spriteCenter;

		if (IsOnlineMultiplayer())
		{
			NetworkCommandRouter.SendBuildPort(this, spriteCenter.X, spriteCenter.Y, rotations[bestDir], flips[bestDir]);
		}

		return true;
	}

	public void RefundShipProductionQueue(int refundTeamId)
	{
		if (!HasPort) return;
		if (refundTeamId <= 0) return;
		if (GameManager.Instance == null) return;

		int totalRefund = 0;

		// Refund currently produced ship
		if (_currentShipProduction != null)
		{
			int price = ShipStats.GetStats(_currentShipProduction).Price;
			totalRefund += price;
			_currentShipProduction = null;
			_shipProductionTimer = 0f;
		}

		// Refund all queued ships
		while (_shipProductionQueue.Count > 0)
		{
			string shipType = _shipProductionQueue.Dequeue();
			int price = ShipStats.GetStats(shipType).Price;
			totalRefund += price;
		}

		if (totalRefund > 0)
			GameManager.Instance.AddGold(refundTeamId, totalRefund);
	}

}
