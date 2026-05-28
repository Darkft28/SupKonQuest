using Godot;

public partial class Unit
{
	private static readonly PackedScene ProjectileScene = GD.Load<PackedScene>("res://Scenes/Projectile.tscn");

	private void ProcessAttackingState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		// Marge de 20px pour éviter les oscillations
		float rangeWithMargin = _stats.Range + 20f;
		if (distanceToTarget > rangeWithMargin)
		{
			ChangeState(UnitState.MovingToTarget);
			return;
		}

		_attackTimer += (float)delta;

		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			AttackTarget(_currentTarget);
		}
	}

	private void ProcessAttackingCampState(double delta)
	{
		if (_campTarget == null || !IsInstanceValid(_campTarget) || !_campTarget.IsInsideTree())
		{
			_campTarget = null;
			ReturnToSavedPositionOrIdle();
			return;
		}

		// Camp capturé par notre équipe → succès
		if (_campTarget.GetTeamId() == TeamId)
		{
			_campTarget = null;
			ReturnToSavedPositionOrIdle();
			return;
		}

		float distanceToCamp = GlobalPosition.DistanceTo(_campTarget.GlobalPosition);

		// Cache AreAllUnitsDefeated : évite le LINQ RemoveAll chaque frame
		_campDefeatCheckTimer += (float)delta;
		if (_campDefeatCheckTimer >= CampDefeatCheckInterval)
		{
			_campDefeatCheckTimer = 0f;
			_campDefeatCached = _campTarget.AreAllUnitsDefeated();
		}

		// Recherche throttlée d'ennemis croisés en chemin (combat opportuniste)
		_aiSearchTimer += (float)delta;
		if (_aiSearchTimer >= EnemySearchInterval)
		{
			_aiSearchTimer = 0f;
			_opportunisticTarget = FindEnemyInDetectionRange();
		}
		// Invalider si mort ou sorti de portée
		if (_opportunisticTarget != null &&
			(!IsInstanceValid(_opportunisticTarget)
			|| _opportunisticTarget.GetCurrentHealth() <= 0
			|| GlobalPosition.DistanceTo(_opportunisticTarget.GlobalPosition) > DetectionRange))
		{
			_opportunisticTarget = null;
		}

		float effectiveRange = _stats.Range + 80f;

		// Phase 1 : des défenseurs sont encore en vie → les combattre en priorité
		if (!_campDefeatCached)
		{
			Unit defender = FindNearestDefenderOfCamp(_campTarget);
			Unit combatTarget = defender ?? _opportunisticTarget;

			if (combatTarget != null)
			{
				float distToTarget = GlobalPosition.DistanceTo(combatTarget.GlobalPosition);
				if (distToTarget <= effectiveRange)
				{
					Velocity = Vector2.Zero;
					_attackTimer += (float)delta;
					if (_attackTimer >= AttackInterval)
					{
						_attackTimer = 0f;
						AttackTarget(combatTarget);
					}
				}
				else
				{
					MoveWithNav(combatTarget.GlobalPosition);
					ProcessStuckDetection();
				}
			}
			else
			{
				// Aucun ennemi visible : avancer vers le camp
				if (distanceToCamp > _stats.Range)
				{
					MoveWithNav(_campTarget.GlobalPosition);
					ProcessStuckDetection();
				}
				else
					Velocity = Vector2.Zero;
			}
			return;
		}

		// Phase 2 : plus de défenseurs → attaquer le bâtiment, mais engager les ennemis de passage
		if (_opportunisticTarget != null)
		{
			float distToOpp = GlobalPosition.DistanceTo(_opportunisticTarget.GlobalPosition);
			if (distToOpp <= effectiveRange)
			{
				Velocity = Vector2.Zero;
				_attackTimer += (float)delta;
				if (_attackTimer >= AttackInterval)
				{
					_attackTimer = 0f;
					AttackTarget(_opportunisticTarget);
				}
				return;
			}
		}

		// Marge de 80px pour les unités bloquées par la collision du bâtiment ou des alliés
		float effectiveCampRange = _stats.Range + 80f;
		if (distanceToCamp > effectiveCampRange)
		{
			MoveWithNav(_campTarget.GlobalPosition);
			ProcessStuckDetection();
			return;
		}

		Velocity = Vector2.Zero;
		_attackTimer += (float)delta;
		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			_campTarget.TakeDamage(_stats.Attack, TeamId);
		}
	}

	private void AttackTarget(Unit target)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsInsideTree())
			return;

		if (target.GetCurrentHealth() <= 0)
			return;

		PlayAttackSfx();

		// Range et Mortar : projectile au lieu de dégâts directs
		if (UnitType == "Range" || UnitType == "Mortar")
		{
			SpawnProjectile(target);
			return;
		}

		// AntiArmor : dégâts x2 contre les unités Heavy
		float attackDamage = _stats.Attack;
		if (UnitType == "AntiArmor" && target.GetUnitType() == "Heavy")
			attackDamage *= 2f;

		bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
		if (isMulti && !string.IsNullOrEmpty(target.NetworkId))
		{
			NetworkSync.Instance?.SendUnitDamage(target.NetworkId, attackDamage, TeamId);
			return;
		}

		target.TakeDamageFrom(attackDamage, TeamId);
	}

	private void SpawnProjectile(Unit target)
	{
		var projectile = ProjectileScene?.Instantiate<Projectile>() ?? new Projectile();
		GetTree().CurrentScene.AddChild(projectile);

		var type = UnitType == "Mortar" ? Projectile.ProjectileType.Cannonball : Projectile.ProjectileType.Arrow;
		float speed = UnitType == "Mortar" ? 300f : 500f;

		projectile.Initialize(GlobalPosition, target, _stats.Attack, TeamId, type, speed);
	}

	public void TakeDamage(float damage)
	{
		TakeDamageFrom(damage, 0);
	}

	public void TakeDamageFrom(float damage, int attackerTeamId)
	{
		// Formule de réduction : damage * 100 / (100 + defense)
		float totalDefense = _stats.Defense + GetSupportDefenseBonus();
		float actualDamage = damage * 100f / (100f + totalDefense);
		_currentHealth -= actualDamage;
		_lastAttackerTeamId = attackerTeamId;
		QueueRedraw();

		if (_currentHealth <= 0)
			Die();
	}

	private void Die()
	{
		if (OwnerCamp != null && IsInstanceValid(OwnerCamp))
		{
			bool mutualKill = _currentTarget != null
				&& IsInstanceValid(_currentTarget)
				&& _currentTarget.GetCurrentHealth() <= 0;
			OwnerCamp.OnDefenderDied(_lastAttackerTeamId, mutualKill);
		}

		if (NetworkSync.Instance?.IsMultiplayer() == true && !string.IsNullOrEmpty(NetworkId))
			NetworkSync.Instance?.SendEntityDied(NetworkId);

		QueueFree();
	}

	private void OnBodyEnteredDetectionZone(Node2D body)
	{
		if (UnitType == "Heal") return;
		if (body is not Unit otherUnit) return;
		if (otherUnit.GetTeamId() == TeamId || otherUnit.GetCurrentHealth() <= 0) return;
		if (_currentTarget != null) return;

		if (_currentState == UnitState.Idle)
		{
			SetNewTarget(otherUnit);
		}
		else if (_currentState == UnitState.MovingToPoint)
		{
			// Sauvegarder la destination et engager l'ennemi croisé
			_savedTargetPosition ??= _targetPosition;
			SetNewTarget(otherUnit);
		}
	}

	private void SetNewTarget(Unit target)
	{
		_currentTarget = target;
		float distanceToTarget = GlobalPosition.DistanceTo(target.GlobalPosition);
		ChangeState(distanceToTarget <= _stats.Range ? UnitState.Attacking : UnitState.MovingToTarget);
	}

	private bool IsTargetValid()
	{
		if (_currentTarget == null)
			return false;

		if (!IsInstanceValid(_currentTarget))
			return false;

		if (!_currentTarget.IsInsideTree())
			return false;

		if (_currentTarget.GetCurrentHealth() <= 0)
			return false;

		return true;
	}

	private Unit FindNearestDefenderOfCamp(CampSimple camp)
	{
		var defenders = camp.GetRelevantDefenders();
		Unit nearest = null;
		float nearestDist = float.MaxValue;

		foreach (var unit in defenders)
		{
			if (unit == null || !IsInstanceValid(unit) || unit.GetCurrentHealth() <= 0)
				continue;
			if (unit.GetTeamId() == TeamId)
				continue;

			float dist = GlobalPosition.DistanceTo(unit.GlobalPosition);
			if (dist < nearestDist)
			{
				nearestDist = dist;
				nearest = unit;
			}
		}
		return nearest;
	}

	private Unit FindEnemyInDetectionRange()
	{
		var allUnits = GetTree().GetNodesInGroup("units");
		Unit closestEnemy = null;
		float closestDistance = float.MaxValue;

		foreach (var node in allUnits)
		{
			if (node is Unit otherUnit)
			{
				if (otherUnit.GetTeamId() == TeamId) continue;
				if (otherUnit.GetCurrentHealth() <= 0) continue;

				float distance = GlobalPosition.DistanceTo(otherUnit.GlobalPosition);
				if (distance <= DetectionRange && distance < closestDistance)
				{
					closestEnemy = otherUnit;
					closestDistance = distance;
				}
			}
		}

		return closestEnemy;
	}

	private CampSimple FindAttackableCampInRange()
	{
		var allCamps = GetTree().GetNodesInGroup("camps");
		CampSimple bestCamp = null;
		float bestScore = float.MaxValue;

		// Région de cette unité (héritée du camp propriétaire ou assignée directement)
		int myRegion = RegionId > 0 ? RegionId : (OwnerCamp?.RegionId ?? 0);
		var graph = MapGenerator.TerritoryGraph;

		foreach (var node in allCamps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() == TeamId) continue;

			float distance = GlobalPosition.DistanceTo(camp.GlobalPosition);
			if (distance > CampAttackDetectionRange) continue;

			// Score de base : distance euclidienne
			float score = distance;

			// Bonus territoire : réduire le score selon la proximité de région
			if (myRegion > 0 && graph != null)
			{
				if (camp.RegionId == myRegion)
					score -= 3000f; // Même région = priorité maximale
				else if (camp.RegionId > 0 && TerritoryConnectivity.IsReachable(graph, new[] { myRegion }, camp.RegionId))
					score -= 1500f; // Territoire terrestre accessible = priorité secondaire
			}
			else if (myRegion > 0 && camp.RegionId == myRegion)
			{
				// Fallback sans graphe : même région quand même prioritaire
				score -= 2000f;
			}

			if (score < bestScore)
			{
				bestScore = score;
				bestCamp = camp;
			}
		}

		return bestCamp;
	}
}
