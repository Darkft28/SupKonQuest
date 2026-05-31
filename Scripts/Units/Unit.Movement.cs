using Godot;

public partial class Unit
{
	private void ProcessIdleState(double delta)
	{
		// Healer : chercher des alliés blessés, jamais d'ennemis
		if (UnitType == "Heal")
		{
			_healSearchTimer += (float)delta;
			if (_healSearchTimer >= HealSearchInterval)
			{
				_healSearchTimer = 0f;
				Unit woundedAlly = FindWoundedAllyInRange();
				if (woundedAlly != null)
				{
					_healTarget = woundedAlly;
					_healTimer = 0f;
					ChangeState(UnitState.Healing);
					return;
				}
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
			return;
		}

		// Camp cible encore valide -> reprendre l'attaque directement (vérif cheap)
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
			_healSearchTimer += (float)delta;
			if (_healSearchTimer >= HealSearchInterval)
			{
				_healSearchTimer = 0f;
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

		// Vérifie la progression vers la cible (pas juste le mouvement total)
		// Une unité poussée par d'autres bouge mais ne progresse pas -> doit quand même s'arrêter
		Vector2 goal = _currentTarget?.GlobalPosition
			?? _campTarget?.GlobalPosition
			?? _targetPosition
			?? GlobalPosition;

		float distNow = GlobalPosition.DistanceTo(goal);
		float distPrev = _lastPosition.DistanceTo(goal);

		// Si on ne se rapproche pas du goal depuis 2 secondes -> abandon
		if (distNow >= distPrev - 0.5f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloqué près de la cible -> tenter d'attaquer si applicable
				if (_currentTarget != null && IsTargetValid() && distNow <= _stats.Range + 80f)
				{
					ChangeState(UnitState.Attacking);
					_stuckFrames = 0;
					return;
				}

				if (_currentState == UnitState.AttackingCamp)
				{
					_campTarget = null;
					ChangeState(UnitState.Idle);
					_stuckFrames = 0;
					return;
				}

				_targetPosition = null;
				ChangeState(UnitState.Idle);
				_stuckFrames = 0;
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
		if (MapGenerator.LandNavigationMap.IsValid)
			target = MapGenerator.SnapToLandNavNear(GlobalPosition, target);

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

	// Déplacement avec pathfinding + RVO (velocity_computed).
	// Le chemin n'est recalculé que si la cible a bougé de plus de NavUpdateDistance
	// ou si un nouvel ordre vient d'être donné (_navTargetDirty).
	private void MoveWithNav(Vector2 targetPos)
	{
		float speedMult = GameManager.Instance?.GetSpeedMultiplier(TeamId) ?? 1f;
		float moveSpeed = _stats.Speed * speedMult;

		if (_navAgent == null || !_navAgent.IsInsideTree())
		{
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			if (IsAiControlledUnit())
			{
				if (dir.LengthSquared() < 0.01f
					|| IsWaterTileAt(GlobalPosition + dir * 128f)
					|| !MapGenerator.HasClearLandLine(GlobalPosition, targetPos))
				{
					_targetPosition = null;
					_campTarget = null;
					_currentTarget = null;
					ChangeState(UnitState.Idle);
					return;
				}
			}

			_intendedDirection = dir;
			ApplyMovementVelocity(dir * moveSpeed);
			return;
		}

		if (NavigationServer2D.MapGetIterationId(_navAgent.GetNavigationMap()) == 0)
			return;

		if (_navTargetDirty || targetPos.DistanceTo(_lastNavTargetPos) > NavUpdateDistance)
		{
			_navAgent.TargetPosition = targetPos;
			_lastNavTargetPos = targetPos;
			_navTargetDirty = false;
			_navPathCooldown = 3;
		}

		if (_navPathCooldown > 0)
		{
			_navPathCooldown--;
			return;
		}

		if (_navAgent.IsNavigationFinished())
		{
			if (GlobalPosition.DistanceTo(targetPos) > ArrivalDistance)
			{
				_targetPosition = null;
				_campTarget = null;
				_currentTarget = null;
				ChangeState(UnitState.Idle);
				return;
			}

			_navPathCooldown = 60;
			Velocity = Vector2.Zero;
			return;
		}

		Vector2 nextPos = _navAgent.GetNextPathPosition();
		if (nextPos.DistanceTo(GlobalPosition) > 1800f)
		{
			_targetPosition = null;
			_campTarget = null;
			_currentTarget = null;
			ChangeState(UnitState.Idle);
			return;
		}

		Vector2 direction = (nextPos - GlobalPosition).Normalized();
		_intendedDirection = direction;

		if (IsAiControlledUnit())
		{
			if (TryAbortAiWaterMovement(direction, 128f))
				return;
		}

		ApplyMovementVelocity(direction * moveSpeed);
	}

	private bool TryAbortAiWaterMovement(Vector2 direction, float probeDistance)
	{
		if (!IsAiControlledUnit())
			return false;

		if (TryRecoverAiFromWater())
			return true;

		if (direction.LengthSquared() < 0.01f)
			return false;

		Vector2 probe = GlobalPosition + direction.Normalized() * probeDistance;
		if (!IsWaterTileAt(probe))
			return false;

		_targetPosition = null;
		_campTarget = null;
		_currentTarget = null;
		ChangeState(UnitState.Idle);
		return true;
	}

	private bool TryRecoverAiFromWater()
	{
		if (!IsAiControlledUnit() || !IsWaterTileAt(GlobalPosition))
			return false;

		GlobalPosition = MapGenerator.SnapToLandNavNear(GlobalPosition, GlobalPosition);
		Velocity = Vector2.Zero;
		_targetPosition = null;
		_campTarget = null;
		_currentTarget = null;
		ChangeState(UnitState.Idle);
		return true;
	}

	private bool IsAiControlledUnit()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState != null && gameState.IsAIMode && TeamId != gameState.LocalTeamId;
	}

	private void OnNavVelocityComputed(Vector2 safeVelocity)
	{
		if (IsAiControlledUnit())
		{
			if (TryRecoverAiFromWater())
				return;

			if (safeVelocity.LengthSquared() > 0.01f)
			{
				Vector2 dir = safeVelocity.Normalized();
				if (IsWaterTileAt(GlobalPosition + dir * 96f))
				{
					Velocity = Vector2.Zero;
					MoveAndSlide();
					return;
				}
			}
		}

		Velocity = safeVelocity;
		MoveAndSlide();
		TryRecoverAiFromWater();
	}

	private void UpdateSelectiveRvo(float delta)
	{
		// RVO sélectif : joueur local uniquement — l'IA garde l'évitement activé (évite dérives vers l'eau).
		if (IsAiControlledUnit())
			return;

		if (_navAgent == null || !_navAgent.IsInsideTree())
			return;

		_rvoCheckTimer += delta;
		if (_rvoCheckTimer < RvoCheckInterval)
			return;

		_rvoCheckTimer = 0f;

		bool nearAlly = false;
		const float neighborRadiusSq = 120f * 120f;
		foreach (var node in GetTree().GetNodesInGroup("units"))
		{
			if (node is not Unit ally || ally == this || ally.GetTeamId() != TeamId || ally.GetCurrentHealth() <= 0)
				continue;

			if (GlobalPosition.DistanceSquaredTo(ally.GlobalPosition) <= neighborRadiusSq)
			{
				nearAlly = true;
				break;
			}
		}

		_navAgent.AvoidanceEnabled = nearAlly;
	}

	private void ApplyMovementVelocity(Vector2 desiredVelocity)
	{
		UpdateSelectiveRvo((float)GetPhysicsProcessDeltaTime());

		if (_navAgent != null && _navAgent.IsInsideTree() && _navAgent.AvoidanceEnabled)
			_navAgent.Velocity = desiredVelocity;
		else
			OnNavVelocityComputed(desiredVelocity);
	}
}
