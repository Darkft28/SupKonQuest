using Godot;

public partial class Ship
{
	private const float MaxUnloadDistance = 2000f;
	private const int MaxCoastTileDistance = 3;
	private int _transportUnloadBatch;

	private static bool IsOnlineMultiplayer() => GameState.IsOnlineMultiplayer;

	public bool BoardUnit(Unit unit)
	{
		if (ShipType != "Transport") return false;
		if (_loadedUnits.Count >= _stats.Capacity) return false;
		if (unit == null || !IsInstanceValid(unit)) return false;
		if (!unit.CanBoardTransport()) return false;

		string unitNetId = unit.NetworkId;
		string unitType = unit.GetUnitType();
		int teamId = unit.GetTeamId();
		float health = unit.GetCurrentHealth();

		_loadedUnits.Add((unitType, teamId, health));

		if (IsOnlineMultiplayer() && !string.IsNullOrEmpty(unitNetId) && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkCommandRouter.SendBoardTransport(NetworkId, unitNetId, unitType, teamId, health);
		}

		unit.QueueFree();
		QueueRedraw();
		return true;
	}

	public void ApplyRelayBoardTransport(string unitNetworkId, string unitType, int teamId, float health)
	{
		if (ShipType != "Transport") return;
		if (_loadedUnits.Count >= _stats.Capacity) return;

		_loadedUnits.Add((unitType, teamId, health));

		var unit = NetworkEntityRegistry.Get<Unit>(unitNetworkId);
		if (unit != null && GodotObject.IsInstanceValid(unit))
			unit.QueueFree();

		QueueRedraw();
	}

	public bool IsValidUnloadPosition(Vector2 landPosition)
	{
		float distance = GlobalPosition.DistanceTo(landPosition);
		if (distance > MaxUnloadDistance)
			return false;

		if (_tileMapSol == null)
			return false;

		Vector2I landTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(landPosition));

		for (int dx = -MaxCoastTileDistance; dx <= MaxCoastTileDistance; dx++)
		{
			for (int dy = -MaxCoastTileDistance; dy <= MaxCoastTileDistance; dy++)
			{
				Vector2I checkTile = landTile + new Vector2I(dx, dy);
				if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					return true;
			}
		}

		return false;
	}

	public void MoveToUnload(Vector2 landPosition)
	{
		if (ShipType != "Transport"|| _loadedUnits.Count == 0) return;

		Vector2 waterPos = FindNearestWaterTile(landPosition);

		_pendingUnloadPosition = landPosition;
		_targetPosition = waterPos;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
	}

	private Vector2 FindNearestWaterTile(Vector2 landPos)
	{
		if (_tileMapSol == null) return GlobalPosition;

		Vector2I landTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(landPos));

		for (int radius = 1; radius <= 10; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = landTile + new Vector2I(dx, dy);
					if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		return GlobalPosition;
	}

	public void UnloadUnits(Vector2 landPosition)
	{
		if (ShipType != "Transport"|| _loadedUnits.Count == 0) return;

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		int unloadBatch = ++_transportUnloadBatch;
		int count = _loadedUnits.Count;
		var netIds = new string[count];
		var types = new string[count];
		var posXs = new float[count];
		var posYs = new float[count];
		var hps = new float[count];
		int unloadTeamId = 0;

		for (int i = 0; i < count; i++)
		{
			var (type, teamId, health) = _loadedUnits[i];
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / count;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f;
			Vector2 spawnPos = landPosition + offset;

			unit.GlobalPosition = spawnPos;
			unit.UnitType = type;
			unit.TeamId = teamId;
			unit.IsNeutralCampUnit = false;

			string networkId = $"{NetworkId}_unload_{unloadBatch}_{i}";
			unit.NetworkId = networkId;
			unit.IsLocalAuthority = !IsOnlineMultiplayer();

			GetTree().CurrentScene.AddChild(unit);
			unit.SetCurrentHealth(health);

			unloadTeamId = teamId;
			netIds[i] = networkId;
			types[i] = type;
			posXs[i] = spawnPos.X;
			posYs[i] = spawnPos.Y;
			hps[i] = health;
		}

		if (IsOnlineMultiplayer() && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkCommandRouter.SendTransportUnloaded(NetworkId, netIds, types, unloadTeamId, posXs, posYs, hps);
		}

		_loadedUnits.Clear();
		QueueRedraw();
	}

	public void ApplyRelayTransportUnloaded(string[] unitNetworkIds, string[] unitTypes, int teamId, float[] posXs, float[] posYs, float[] healths)
	{
		if (ShipType != "Transport") return;
		if (unitNetworkIds == null || unitTypes == null || posXs == null || posYs == null || healths == null)
			return;

		int count = unitNetworkIds.Length;
		if (count == 0 || unitTypes.Length != count || posXs.Length != count || posYs.Length != count || healths.Length != count)
			return;

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		for (int i = 0; i < count; i++)
		{
			if (NetworkEntityRegistry.Get(unitNetworkIds[i]) != null)
				continue;

			var unit = unitScene.Instantiate<Unit>();
			unit.UnitType = unitTypes[i];
			unit.TeamId = teamId;
			unit.IsNeutralCampUnit = false;
			unit.GlobalPosition = new Vector2(posXs[i], posYs[i]);
			unit.NetworkId = unitNetworkIds[i];
			unit.IsLocalAuthority = !IsOnlineMultiplayer();

			GetTree().CurrentScene.AddChild(unit);
			unit.SetCurrentHealth(healths[i]);
		}

		_loadedUnits.Clear();
		QueueRedraw();
	}
}
