using Godot;

public partial class Ship
{
	private void ProcessIdleState(double delta)
	{
		if (ShipType == "Transport") return;

		_shipSearchTimer += (float)delta;
		if (_shipSearchTimer < ShipSearchInterval)
			return;

		_shipSearchTimer = 0f;

		if (_currentTarget == null)
		{
			Ship enemy = FindEnemyShipInRange();
			if (enemy != null)
				SetNewTarget(enemy);
		}
	}

	private void ProcessMovingToTargetState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(ShipState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(ShipState.Attacking);
			return;
		}

		MoveWithNav(_currentTarget.GlobalPosition);
		ProcessStuckDetection();
	}

	private void ProcessMovingToPointState(double delta)
	{
		if (!_targetPosition.HasValue)
		{
			ChangeState(ShipState.Idle);
			return;
		}

		// Chercher des ennemis en route (sauf Transport)
		if (ShipType != "Transport")
		{
			_shipSearchTimer += (float)delta;
			if (_shipSearchTimer >= ShipSearchInterval)
			{
				_shipSearchTimer = 0f;
				Ship enemy = FindEnemyShipInRange();
				if (enemy != null)
				{
					_currentTarget = enemy;
					float distToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
					if (distToEnemy <= _stats.Range)
						ChangeState(ShipState.Attacking);
					else
						ChangeState(ShipState.MovingToTarget);
					return;
				}
			}
		}

		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		if (distance < ArrivalDistance)
		{
			_targetPosition = null;

			// Debarquement en attente : decharger les troupes a l'arrivee
			if (_pendingUnloadPosition.HasValue)
			{
				UnloadUnits(_pendingUnloadPosition.Value);
				_pendingUnloadPosition = null;
			}

			ChangeState(ShipState.Idle);
			return;
		}

		MoveWithNav(_targetPosition.Value);
		ProcessStuckDetection();
	}

	private void ProcessStuckDetection()
	{
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		float expectedMovement = _stats.Speed / 60f;
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement * 0.1f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloque - si debarquement en attente, decharger quand meme
				if (_pendingUnloadPosition.HasValue)
				{
					UnloadUnits(_pendingUnloadPosition.Value);
					_pendingUnloadPosition = null;
				}

				_targetPosition = null;
				ChangeState(ShipState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
	}

	// Déplacement maritime avec pathfinding sur le navmesh eau + RVO (velocity_computed).
	// Aucun fallback vers la terre : le bateau s'arrête si aucun chemin n'est trouvé.
	private void MoveWithNav(Vector2 targetPos)
	{
		if (_navAgent == null || !_navAgent.IsInsideTree())
		{
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			Vector2 desiredVel = dir * _stats.Speed;
			Vector2 nextPos = GlobalPosition + desiredVel / 60f;
			if (IsWaterTile(nextPos))
				ApplyMovementVelocity(desiredVel);
			else
				Velocity = Vector2.Zero;
			return;
		}

		if (NavigationServer2D.MapGetIterationId(_navAgent.GetNavigationMap()) == 0)
			return;

		if (_navTargetDirty || targetPos.DistanceTo(_lastNavTargetPos) > NavUpdateDistance)
		{
			_navAgent.TargetPosition = targetPos;
			_lastNavTargetPos = targetPos;
			_navTargetDirty = false;
		}

		if (_navAgent.IsNavigationFinished())
		{
			Velocity = Vector2.Zero;
			return;
		}

		Vector2 nextNavPos = _navAgent.GetNextPathPosition();
		Vector2 direction = (nextNavPos - GlobalPosition).Normalized();
		ApplyMovementVelocity(direction * _stats.Speed);
	}

	private void OnNavVelocityComputed(Vector2 safeVelocity)
	{
		Velocity = safeVelocity;
		if (safeVelocity.LengthSquared() > 0.01f)
			UpdateSpriteDirection(safeVelocity);
		MoveAndSlide();
	}

	private void ApplyMovementVelocity(Vector2 desiredVelocity)
	{
		if (_navAgent != null && _navAgent.IsInsideTree() && _navAgent.AvoidanceEnabled)
			_navAgent.Velocity = desiredVelocity;
		else
			OnNavVelocityComputed(desiredVelocity);
	}

	public bool IsWaterTile(Vector2 globalPos)
	{
		if (_tileMapSol == null) return false;

		Vector2I tileCoords = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(globalPos));
		int tileId = _tileMapSol.GetCellSourceId(tileCoords);
		return tileId == 6; // IdEau
	}

	public void MoveTo(Vector2 target, bool trustRelayTarget = false)
	{
		if (!trustRelayTarget && !IsWaterTile(target))
			return;

		_pendingUnloadPosition = null; // Nouvel ordre annule le debarquement
		_targetPosition = target;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		_pendingUnloadPosition = null;
		Velocity = Vector2.Zero;
		ChangeState(ShipState.Idle);
	}
}
