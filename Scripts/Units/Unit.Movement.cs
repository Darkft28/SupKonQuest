using Godot;

public partial class Unit
{
	private void ProcessIdleState(double delta)
	{
		// Healer : chercher des alliés blessés, jamais d'ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_healTarget = woundedAlly;
				_healTimer = 0f;
				ChangeState(UnitState.Healing);
			}
			else if (_savedTargetPosition.HasValue)
			{
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
			}
			return;
		}

		// Camp cible encore valide → reprendre l'attaque directement (vérif cheap)
		if (_campTarget != null && IsInstanceValid(_campTarget) && _campTarget.IsInsideTree()
			&& _campTarget.GetTeamId() != TeamId)
		{
			ChangeState(UnitState.AttackingCamp);
			return;
		}

		// Recherches throttlées pour éviter O(n²) chaque frame
		_aiSearchTimer += (float)delta;
		if (_aiSearchTimer < EnemySearchInterval)
		{
			// Reprendre la route immédiatement si plus de cible
			if (_currentTarget == null && _savedTargetPosition.HasValue)
			{
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
			}
			return;
		}
		_aiSearchTimer = 0f;

		if (_currentTarget == null)
		{
			Unit enemy = FindEnemyInDetectionRange();
			if (enemy != null)
			{
				SetNewTarget(enemy);
				return;
			}

			CampSimple camp = FindAttackableCampInRange();
			if (camp != null)
			{
				_campTarget = camp;
				ChangeState(UnitState.AttackingCamp);
				return;
			}

			if (_savedTargetPosition.HasValue)
			{
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
			}
		}
	}

	private void ProcessMovingToTargetState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(UnitState.Attacking);
			return;
		}

		MoveWithNav(_currentTarget.GlobalPosition);
		ProcessStuckDetection();
	}

	private void ProcessMovingToPointState(double delta)
	{
		if (!_targetPosition.HasValue)
		{
			ChangeState(UnitState.Idle);
			return;
		}

		// Healer en déplacement : chercher des alliés blessés, jamais d'ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_savedTargetPosition = _targetPosition;
				_healTarget = woundedAlly;
				_healTimer = 0f;
				ChangeState(UnitState.Healing);
				return;
			}
		}
		else
		{
			// Unités de combat : recherche throttlée (évite O(n²))
			_aiSearchTimer += (float)delta;
			if (_aiSearchTimer >= EnemySearchInterval)
			{
				_aiSearchTimer = 0f;

				Unit enemy = FindEnemyInDetectionRange();
				if (enemy != null)
				{
					_savedTargetPosition = _targetPosition;
					_currentTarget = enemy;
					float distanceToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
					ChangeState(distanceToEnemy <= _stats.Range ? UnitState.Attacking : UnitState.MovingToTarget);
					return;
				}

				CampSimple camp = FindAttackableCampInRange();
				if (camp != null)
				{
					_savedTargetPosition = _targetPosition;
					_campTarget = camp;
					ChangeState(UnitState.AttackingCamp);
					return;
				}
			}
		}

		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		if (distance < ArrivalDistance)
		{
			_targetPosition = null;
			_savedTargetPosition = null;
			ChangeState(UnitState.Idle);
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

		float speedMult = GameManager.Instance?.GetSpeedMultiplier(TeamId) ?? 1f;
		// Plancher 0.5px pour éviter les faux positifs sur les unités lentes (ex: Tank Speed=50 → ~0.83px/frame)
		float expectedMovement = Mathf.Max((_stats.Speed * speedMult) / 60f * 0.1f, 0.5f);
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloqué près de la cible (collision physique) → passer en Attacking (marge 120px)
				if (_currentTarget != null && IsTargetValid())
				{
					float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
					if (distanceToTarget <= 120f)
					{
						ChangeState(UnitState.Attacking);
						_stuckFrames = 0;
						return;
					}
				}

				_targetPosition = null;
				ChangeState(UnitState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
	}

	public void MoveTo(Vector2 target)
	{
		_targetPosition = target;
		_savedTargetPosition = null;
		_campTarget = null;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(UnitState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		_savedTargetPosition = null;
		_targetTransport = null;
		_campTarget = null;
		Velocity = Vector2.Zero;
		ChangeState(UnitState.Idle);
	}

	public void AttackCamp(CampSimple camp)
	{
		if (UnitType == "Heal") return;
		_campTarget = camp;
		_savedTargetPosition = null;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		_attackTimer = 0f;
		ChangeState(UnitState.AttackingCamp);
	}

	private void ReturnToSavedPositionOrIdle()
	{
		if (_savedTargetPosition.HasValue)
		{
			_targetPosition = _savedTargetPosition;
			_savedTargetPosition = null;
			_stuckFrames = 0;
			_moveStartDelay = MoveStartDelayFrames;
			_lastPosition = GlobalPosition;
			_currentState = UnitState.MovingToPoint;
		}
		else
		{
			ChangeState(UnitState.Idle);
		}
	}

	// Déplacement avec pathfinding.
	// Le chemin n'est recalculé que si la cible a bougé de plus de NavUpdateDistance
	// ou si un nouvel ordre vient d'être donné (_navTargetDirty).
	private void MoveWithNav(Vector2 targetPos)
	{
		if (_navAgent == null || !_navAgent.IsInsideTree())
		{
			// Agent pas encore prêt : mouvement direct pour ce frame
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			Velocity = dir * _stats.Speed;
			MoveAndSlide();
			return;
		}

		if (_navTargetDirty || targetPos.DistanceTo(_lastNavTargetPos) > NavUpdateDistance)
		{
			_navAgent.TargetPosition = targetPos;
			_lastNavTargetPos = targetPos;
			_navTargetDirty = false;
		}

		if (!_navAgent.IsTargetReachable() || _navAgent.IsNavigationFinished())
		{
			Velocity = Vector2.Zero;
			return;
		}

		Vector2 nextPos = _navAgent.GetNextPathPosition();
		Vector2 direction = (nextPos - GlobalPosition).Normalized();
		float speedMult = GameManager.Instance?.GetSpeedMultiplier(TeamId) ?? 1f;
		Velocity = direction * _stats.Speed * speedMult;
		MoveAndSlide();
	}
}
