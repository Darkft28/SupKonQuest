using Godot;
using System;

public partial class Unit
{
	private const int WaterTileSourceId = 6;
	private const float BoardingApproachNavDistance = 120f;
	private const float BoardingCoastArrivalDistance = 100f;
	// Ship center is on water; coast tile can be several hundred px away
	private const float BoardingShipMaxDistance = 600f;
	private const int BoardingSearchTileRadius = 12;

	private TileMapLayer _tileMapSol;
	private Vector2? _boardingPoint;

	private void EnsureTileMapSol()
	{
		if (_tileMapSol != null && IsInstanceValid(_tileMapSol))
			return;

		var currentScene = GetTree()?.CurrentScene;
		if (currentScene == null) return;

		_tileMapSol = currentScene.GetNodeOrNull<TileMapLayer>("Sol");
		if (_tileMapSol != null) return;

		var mapRoot = currentScene.FindChild("MapGenerator", true, false) ?? currentScene;
		_tileMapSol = mapRoot.GetNodeOrNull<TileMapLayer>("Sol");
	}

	public bool IsWaterTileAt(Vector2 globalPos)
	{
		EnsureTileMapSol();
		if (_tileMapSol == null) return false;

		Vector2I tileCoords = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(globalPos));
		return _tileMapSol.GetCellSourceId(tileCoords) == WaterTileSourceId;
	}

	private bool IsCoastTile(Vector2I tileCoords)
	{
		if (_tileMapSol.GetCellSourceId(tileCoords) == WaterTileSourceId) return false;

		ReadOnlySpan<Vector2I> neighbors = stackalloc Vector2I[]
		{
			new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
		};

		foreach (var offset in neighbors)
		{
			Vector2I neighbor = tileCoords + offset;
			if (_tileMapSol.GetCellSourceId(neighbor) == WaterTileSourceId)
				return true;
		}

		return false;
	}

	private bool TryGetBoardingPoint(Ship transport, out Vector2 boardingPoint)
	{
		boardingPoint = transport.GlobalPosition;
		EnsureTileMapSol();
		if (_tileMapSol == null) return false;

		Vector2I transportTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(transport.GlobalPosition));
		Vector2 bestPoint = Vector2.Zero;
		float bestDist = float.MaxValue;

		for (int ring = 1; ring <= BoardingSearchTileRadius; ring++)
		{
			for (int dx = -ring; dx <= ring; dx++)
			{
				for (int dy = -ring; dy <= ring; dy++)
				{
					if (Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring)
						continue;

					Vector2I tile = transportTile + new Vector2I(dx, dy);
					if (_tileMapSol.GetCellSourceId(tile) == WaterTileSourceId)
						continue;
					if (!IsCoastTile(tile))
						continue;

					Vector2 world = _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(tile));
					if (world.DistanceTo(transport.GlobalPosition) > BoardingShipMaxDistance)
						continue;

					float distToUnit = GlobalPosition.DistanceTo(world);
					if (distToUnit < bestDist)
					{
						bestDist = distToUnit;
						bestPoint = world;
					}
				}
			}
		}

		if (bestDist >= float.MaxValue)
			return false;

		boardingPoint = bestPoint;
		return true;
	}

	public bool CanBoardTransport() => UnitType != "Mortar";

	public void MoveToTransport(Ship transport)
	{
		if (!CanBoardTransport())
			return;
		if (transport == null || !IsInstanceValid(transport))
			return;
		if (transport.GetLoadedUnitCount() >= transport.GetCapacity())
			return;

		_targetTransport = transport;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;

		if (TryGetBoardingPoint(transport, out Vector2 point))
		{
			_boardingPoint = point;
			_targetPosition = point;
		}
		else
		{
			_boardingPoint = null;
			_targetPosition = transport.GlobalPosition;
		}

		_savedTargetPosition = null;
		_navTargetDirty = true;
		ChangeState(UnitState.MovingToTransport);
	}

	private void ProcessMovingToTransportState(double delta)
	{
		if (_targetTransport == null || !IsInstanceValid(_targetTransport) || !_targetTransport.IsInsideTree())
		{
			_targetTransport = null;
			_boardingPoint = null;
			ChangeState(UnitState.Idle);
			return;
		}

		if (_targetTransport.GetLoadedUnitCount() >= _targetTransport.GetCapacity())
		{
			_targetTransport = null;
			_boardingPoint = null;
			ChangeState(UnitState.Idle);
			return;
		}

		Vector2 approachTarget = _boardingPoint ?? _targetTransport.GlobalPosition;
		float distanceToTransport = GlobalPosition.DistanceTo(_targetTransport.GlobalPosition);
		float distanceToApproach = GlobalPosition.DistanceTo(approachTarget);

		if (!IsWaterTileAt(GlobalPosition) && TryCompleteBoarding(distanceToTransport, distanceToApproach))
			return;

		if (distanceToApproach > BoardingApproachNavDistance)
		{
			MoveWithNav(approachTarget);
		}
		else
		{
			Vector2 direction = (approachTarget - GlobalPosition).Normalized();
			if (direction.LengthSquared() < 0.01f)
				direction = (_targetTransport.GlobalPosition - GlobalPosition).Normalized();

			Vector2 nextPos = GlobalPosition + direction * (_stats.Speed / 60f);
			if (!IsWaterTileAt(nextPos))
			{
				_intendedDirection = direction;
				ApplyMovementVelocity(direction * _stats.Speed);
			}

			if (!IsWaterTileAt(GlobalPosition) && TryCompleteBoarding(distanceToTransport, distanceToApproach))
				return;
		}

		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		Vector2 goal = _targetTransport.GlobalPosition;
		float distNow = GlobalPosition.DistanceTo(goal);
		float distPrev = _lastPosition.DistanceTo(goal);

		if (distNow >= distPrev - 0.4f)
		{
			_stuckFrames++;
			if (_stuckFrames > 60 && !IsWaterTileAt(GlobalPosition)
				&& TryCompleteBoarding(distanceToTransport, distanceToApproach))
				return;

			if (_stuckFrames > MaxStuckFrames)
			{
				_targetTransport = null;
				_boardingPoint = null;
				ChangeState(UnitState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}

		_lastPosition = GlobalPosition;
	}

	private bool TryCompleteBoarding(float distanceToTransport, float distanceToApproach)
	{
		if (IsWaterTileAt(GlobalPosition)) return false;

		bool inRange;
		if (_boardingPoint.HasValue)
		{
			inRange = distanceToApproach <= BoardingCoastArrivalDistance
				&& distanceToTransport <= BoardingShipMaxDistance;
		}
		else
		{
			inRange = distanceToTransport <= BoardingDistance;
		}

		if (!inRange) return false;

		return _targetTransport.BoardUnit(this);
	}
}
