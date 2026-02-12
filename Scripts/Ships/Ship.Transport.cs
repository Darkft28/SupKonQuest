using Godot;

public partial class Ship
{
	// Distance max de debarquement depuis la position actuelle du transport
	private const float MaxUnloadDistance = 2000f;
	// Nombre de tuiles max entre le point de debarquement et l'eau
	private const int MaxCoastTileDistance = 3;

	// Transport : embarquer une unite
	public bool BoardUnit(Unit unit)
	{
		if (ShipType != "Transport") return false;
		if (_loadedUnits.Count >= _stats.Capacity) return false;
		if (unit == null || !IsInstanceValid(unit)) return false;

		string unitNetId = unit.NetworkId;
		_loadedUnits.Add((unit.GetUnitType(), unit.GetTeamId(), unit.GetCurrentHealth()));
		GD.Print($"[TRANSPORT] {unit.GetUnitType()} T{unit.GetTeamId()} embarque ({_loadedUnits.Count}/{_stats.Capacity}) HP:{unit.GetCurrentHealth():F0}");

		// Reseau : notifier l'autre peer que l'unite a embarque
		if (!string.IsNullOrEmpty(unitNetId) && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkSync.Instance?.SendUnitBoarded(unitNetId, NetworkId);
		}

		unit.QueueFree();
		QueueRedraw();
		return true;
	}

	// Verifie si la position de debarquement est valide (cote + distance)
	public bool IsValidUnloadPosition(Vector2 landPosition)
	{
		// Verifier la distance max depuis le transport
		float distance = GlobalPosition.DistanceTo(landPosition);
		if (distance > MaxUnloadDistance)
			return false;

		// Verifier que la position est pres de la cote (eau a moins de N tuiles)
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

	// Transport : naviguer vers la cote puis debarquer
	public void MoveToUnload(Vector2 landPosition)
	{
		if (ShipType != "Transport" || _loadedUnits.Count == 0) return;

		// Trouver la tuile d'eau la plus proche de la destination terrestre
		Vector2 waterPos = FindNearestWaterTile(landPosition);

		_pendingUnloadPosition = landPosition;
		_targetPosition = waterPos;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
		GD.Print($"[TRANSPORT] Se deplace vers la cote pour debarquer");
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

	// Transport : debarquer toutes les unites
	public void UnloadUnits(Vector2 landPosition)
	{
		if (ShipType != "Transport" || _loadedUnits.Count == 0) return;

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		// Preparer les donnees pour le RPC batch
		var netIds = new System.Collections.Generic.List<string>();
		var types = new System.Collections.Generic.List<string>();
		var posXs = new System.Collections.Generic.List<float>();
		var posYs = new System.Collections.Generic.List<float>();
		var hps = new System.Collections.Generic.List<float>();
		int unloadTeamId = 0;

		for (int i = 0; i < _loadedUnits.Count; i++)
		{
			var (type, teamId, health) = _loadedUnits[i];
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / _loadedUnits.Count;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f;

			unit.GlobalPosition = landPosition + offset;
			unit.UnitType = type;
			unit.TeamId = teamId;
			unit.IsNeutralCampUnit = false;

			// Reseau : assigner un NetworkId
			string networkId = NetworkEntityRegistry.GenerateId();
			unit.NetworkId = networkId;
			unit.IsLocalAuthority = true;

			GetTree().CurrentScene.AddChild(unit);
			unit.SetCurrentHealth(health);

			unloadTeamId = teamId;
			netIds.Add(networkId);
			types.Add(type);
			posXs.Add(unit.GlobalPosition.X);
			posYs.Add(unit.GlobalPosition.Y);
			hps.Add(health);
		}

		GD.Print($"[TRANSPORT] {_loadedUnits.Count} unites debarquees a {landPosition}");

		// Reseau : broadcaster le debarquement
		if (netIds.Count > 0 && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkSync.Instance?.SendTransportUnloaded(NetworkId,
				netIds.ToArray(), types.ToArray(), unloadTeamId,
				posXs.ToArray(), posYs.ToArray(), hps.ToArray());
		}

		_loadedUnits.Clear();
		QueueRedraw();
	}
}
