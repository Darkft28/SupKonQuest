using Godot;

public partial class Unit
{
	private void ProcessIdleState(double delta)
	{
		// Healer : chercher des allies blesses, jamais d'ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_healTarget = woundedAlly;
				_healTimer = 0f;
				GD.Print($"[HEAL] Healer T{TeamId} commence a soigner {woundedAlly.GetUnitType()} T{woundedAlly.GetTeamId()} ({woundedAlly.GetCurrentHealth():F0}/{woundedAlly.GetMaxHealth():F0} HP)");
				ChangeState(UnitState.Healing);
			}
			else if (_savedTargetPosition.HasValue)
			{
				GD.Print($"[MOVE] {UnitType} T{TeamId} reprend sa route");
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
			}
			return;
		}

		// Si on avait un camp cible encore valide, y retourner directement (vérif cheap)
		if (_campTarget != null && IsInstanceValid(_campTarget) && _campTarget.IsInsideTree()
			&& _campTarget.GetTeamId() != TeamId)
		{
			ChangeState(UnitState.AttackingCamp);
			return;
		}

		// Recherches ennemis/camps : throttlées pour éviter O(n²) chaque frame
		_aiSearchTimer += (float)delta;
		if (_aiSearchTimer < EnemySearchInterval)
		{
			// Reprendre la route sans attendre si plus de cible
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
				GD.Print($"[MOVE] {UnitType} T{TeamId} reprend sa route apres combat");
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
		// Vérifier si la cible est encore valide
		if (!IsTargetValid())
		{
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		// Si à portée d'attaque, passer en mode attaque
		if (distanceToTarget <= _stats.Range)
		{
			GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) passe en mode ATTACKING (distance={distanceToTarget:F1} <= range={_stats.Range})");
			ChangeState(UnitState.Attacking);
			return;
		}

		// Se déplacer vers la cible en contournant les obstacles
		MoveWithNav(_currentTarget.GlobalPosition);

		// Détection de blocage
		ProcessStuckDetection();
	}

	private void ProcessMovingToPointState(double delta)
	{
		// Déplacement vers un point ordonné par le joueur
		if (!_targetPosition.HasValue)
		{
			ChangeState(UnitState.Idle);
			return;
		}

		// Healer en deplacement : chercher des allies blesses, pas des ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_savedTargetPosition = _targetPosition;
				_healTarget = woundedAlly;
				_healTimer = 0f;
				GD.Print($"[HEAL] Healer T{TeamId} s'arrete pour soigner {woundedAlly.GetUnitType()} T{woundedAlly.GetTeamId()} ({woundedAlly.GetCurrentHealth():F0}/{woundedAlly.GetMaxHealth():F0} HP)");
				ChangeState(UnitState.Healing);
				return;
			}
		}
		else
		{
			// Unites de combat : chercher des ennemis throttlé (évite O(n²))
			_aiSearchTimer += (float)delta;
			if (_aiSearchTimer >= EnemySearchInterval)
			{
				_aiSearchTimer = 0f;

				Unit enemy = FindEnemyInDetectionRange();
				if (enemy != null)
				{
					GD.Print($"[ENGAGE] {UnitType} T{TeamId} detecte {enemy.GetUnitType()} T{enemy.GetTeamId()} en route, combat!");
					_savedTargetPosition = _targetPosition;
					_currentTarget = enemy;
					float distanceToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
					ChangeState(distanceToEnemy <= _stats.Range ? UnitState.Attacking : UnitState.MovingToTarget);
					return;
				}

				CampSimple camp = FindAttackableCampInRange();
				if (camp != null)
				{
					GD.Print($"[ENGAGE] {UnitType} T{TeamId} detecte Camp #{camp.GetCampId()} sans defenseurs, attaque!");
					_savedTargetPosition = _targetPosition;
					_campTarget = camp;
					ChangeState(UnitState.AttackingCamp);
					return;
				}
			}
		}

		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		// Arrivé à destination
		if (distance < ArrivalDistance)
		{
			_targetPosition = null;
			_savedTargetPosition = null;
			ChangeState(UnitState.Idle);
			return;
		}

		// Se déplacer vers la destination en contournant les obstacles
		MoveWithNav(_targetPosition.Value);

		// Détection de blocage
		ProcessStuckDetection();
	}

	private void ProcessStuckDetection()
	{
		// Attendre le délai initial avant de vérifier le blocage
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		// Détection de blocage - seuil dynamique basé sur la vitesse
		float speedMult = GameManager.Instance?.GetSpeedMultiplier(TeamId) ?? 1f;
		// Plancher de 0.5px pour éviter les faux positifs sur les unités lentes (ex: Tank Speed=50 → ~0.83px/frame)
		float expectedMovement = Mathf.Max((_stats.Speed * speedMult) / 60f * 0.1f, 0.5f);
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloqué - vérifier si on a une cible proche pour attaquer
				if (_currentTarget != null && IsTargetValid())
				{
					float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
					// Si bloqué mais proche de la cible (collision physique), passer en Attacking
					// On utilise une marge de 120 pixels (collision ~80 + marge)
					if (distanceToTarget <= 120f)
					{
						GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) bloque pres de la cible, passage en ATTACKING");
						ChangeState(UnitState.Attacking);
						_stuckFrames = 0;
						return;
					}
				}

				// Sinon, on arrête et on passe en Idle
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
		_savedTargetPosition = null; // Nouvel ordre annule la destination sauvegardee
		_campTarget = null;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;

		// Passer en mode déplacement vers un point (ordre du joueur)
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
			GD.Print($"[MOVE] {UnitType} T{TeamId} reprend sa route apres attaque de camp");
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

	// Déplacement avec pathfinding (contourne les obstacles).
	// Le chemin n'est recalculé que si la cible a bougé de plus de NavUpdateDistance
	// ou si un nouvel ordre vient d'être donné (_navTargetDirty).
	private void MoveWithNav(Vector2 targetPos)
	{
		if (_navAgent == null || !_navAgent.IsInsideTree())
		{
			// Avant que l'agent soit prêt : mouvement direct (1 seul frame au démarrage)
			Vector2 dir = (targetPos - GlobalPosition).Normalized();
			Velocity = dir * _stats.Speed;
			MoveAndSlide();
			return;
		}

		// Throttle : ne pas recalculer le chemin si la cible n'a pas bougé
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

		Vector2 nextPos = _navAgent.GetNextPathPosition();
		Vector2 direction = (nextPos - GlobalPosition).Normalized();
		float speedMult = GameManager.Instance?.GetSpeedMultiplier(TeamId) ?? 1f;
		Velocity = direction * _stats.Speed * speedMult;
		MoveAndSlide();
	}
}
