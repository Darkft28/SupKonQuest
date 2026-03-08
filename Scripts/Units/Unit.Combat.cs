using Godot;

public partial class Unit
{
	private void ProcessAttackingState(double delta)
	{
		// Vérifier si la cible est encore valide
		if (!IsTargetValid())
		{
			GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) -> cible invalide, retour Idle");
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		// Si la cible s'éloigne trop, la poursuivre
		// On ajoute une marge de 20 pixels pour éviter les oscillations
		float rangeWithMargin = _stats.Range + 20f;
		if (distanceToTarget > rangeWithMargin)
		{
			GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) -> cible hors portee (distance={distanceToTarget:F1} > range+marge={rangeWithMargin}), poursuite");
			ChangeState(UnitState.MovingToTarget);
			return;
		}

		// Attaquer à intervalles réguliers
		_attackTimer += (float)delta;

		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			AttackTarget(_currentTarget);
		}
	}

	private void ProcessAttackingCampState(double delta)
	{
		// Validation du camp cible
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

		// Phase 1 : des défenseurs sont encore en vie → les combattre sans quitter l'état
		if (!_campTarget.AreAllUnitsDefeated())
		{
			Unit defender = FindNearestDefenderOfCamp(_campTarget);
			if (defender != null)
			{
				float distToDefender = GlobalPosition.DistanceTo(defender.GlobalPosition);
				if (distToDefender <= _stats.Range)
				{
					Velocity = Vector2.Zero;
					_attackTimer += (float)delta;
					if (_attackTimer >= AttackInterval)
					{
						_attackTimer = 0f;
						AttackTarget(defender);
					}
				}
				else
				{
					MoveWithNav(defender.GlobalPosition);
				}
			}
			else
			{
				// Pas de défenseur visible : avancer vers le camp
				if (distanceToCamp > _stats.Range)
					MoveWithNav(_campTarget.GlobalPosition);
				else
					Velocity = Vector2.Zero;
			}
			return;
		}

		// Phase 2 : plus de défenseurs → attaquer le bâtiment du camp
		if (distanceToCamp > _stats.Range)
		{
			MoveWithNav(_campTarget.GlobalPosition);
			return;
		}

		Velocity = Vector2.Zero;
		_attackTimer += (float)delta;
		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			_campTarget.TakeDamage(_stats.Attack, TeamId);
			GD.Print($"[ATK CAMP] {UnitType} T{TeamId} -> Camp #{_campTarget.GetCampId()} | HP {_campTarget.GetCurrentHealth():F0}/{_campTarget.MaxHealth}");
		}
	}

	private void AttackTarget(Unit target)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsInsideTree())
			return;

		if (target.GetCurrentHealth() <= 0)
			return;

		// Range et Mortar : lancer un projectile au lieu d'appliquer les degats directement
		if (UnitType == "Range" || UnitType == "Mortar")
		{
			SpawnProjectile(target);
			LogAttack(target);
			return;
		}

		// AntiArmor : degats x2 contre les unites Heavy
		float attackDamage = _stats.Attack;
		if (UnitType == "AntiArmor" && target.GetUnitType() == "Heavy")
			attackDamage *= 2f;

		float hpBefore = target.GetCurrentHealth();

		// Reseau : si la cible est un puppet (remote), envoyer via RPC (multi seulement)
		bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
		if (isMulti && !target.IsLocalAuthority && !string.IsNullOrEmpty(target.NetworkId))
		{
			NetworkSync.Instance?.SendUnitDamage(target.NetworkId, attackDamage, TeamId);
			return;
		}

		// Degats directs (solo ou cible locale)
		target.TakeDamageFrom(attackDamage, TeamId);

		float hpAfter = target.GetCurrentHealth();
		float auraBonus = target.GetSupportDefenseBonus();
		float totalDef = target._stats.Defense + auraBonus;
		float actualDamage = attackDamage * 100f / (100f + totalDef);
		string auraStr = auraBonus > 0 ? $" +{auraBonus:F0} aura" : "";
		string bonusStr = attackDamage > _stats.Attack ? " [BONUS AntiArmor x2]" : "";
		GD.Print($"[ATK] {UnitType} T{TeamId} -> {target.GetUnitType()} T{target.GetTeamId()} | {attackDamage} brut -> {actualDamage:F1} reel (def {target._stats.Defense}{auraStr}){bonusStr} | HP {hpBefore:F0} -> {hpAfter:F0}/{target.GetMaxHealth():F0}");
	}

	private void SpawnProjectile(Unit target)
	{
		var projectile = new Projectile();
		GetTree().CurrentScene.AddChild(projectile);

		var type = UnitType == "Mortar" ? Projectile.ProjectileType.Cannonball : Projectile.ProjectileType.Arrow;
		float speed = UnitType == "Mortar" ? 300f : 500f;

		projectile.Initialize(GlobalPosition, target, _stats.Attack, TeamId, type, speed);
	}

	private void LogAttack(Unit target)
	{
		float auraBonus = target.GetSupportDefenseBonus();
		float totalDef = target._stats.Defense + auraBonus;
		float actualDamage = _stats.Attack * 100f / (100f + totalDef);
		string auraStr = auraBonus > 0 ? $" +{auraBonus:F0} aura" : "";
		GD.Print($"[ATK] {UnitType} T{TeamId} -> {target.GetUnitType()} T{target.GetTeamId()} | {_stats.Attack} brut -> {actualDamage:F1} reel (def {target._stats.Defense}{auraStr}) | HP {target.GetCurrentHealth():F0}/{target.GetMaxHealth():F0} (projectile)");
	}

	public void TakeDamage(float damage)
	{
		TakeDamageFrom(damage, 0);
	}

	public void TakeDamageFrom(float damage, int attackerTeamId)
	{
		// Defense = base + bonus aura Support
		float totalDefense = _stats.Defense + GetSupportDefenseBonus();
		float actualDamage = damage * 100f / (100f + totalDefense);
		_currentHealth -= actualDamage;
		_lastAttackerTeamId = attackerTeamId;
		QueueRedraw();

		if (_currentHealth <= 0)
		{
			Die();
		}
	}

	private void Die()
	{
		GD.Print($"[MORT] {UnitType} T{TeamId} elimine (tue par T{_lastAttackerTeamId})");

		// Notifier le camp propriétaire (mort mutuelle ou mort normale)
		if (OwnerCamp != null && IsInstanceValid(OwnerCamp))
		{
			bool mutualKill = _currentTarget != null
				&& IsInstanceValid(_currentTarget)
				&& _currentTarget.GetCurrentHealth() <= 0;
			OwnerCamp.OnDefenderDied(_lastAttackerTeamId, mutualKill);
		}

		// Reseau : notifier l'autre peer de la mort
		if (IsLocalAuthority && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkSync.Instance?.SendEntityDied(NetworkId);
		}

		QueueFree();
	}

	private void OnBodyEnteredDetectionZone(Node2D body)
	{
		// Les healers ne combattent pas
		if (UnitType == "Heal")
			return;

		// Si on est en Idle et qu'un ennemi entre dans la zone, on le cible
		if (body is Unit otherUnit)
		{
			if (otherUnit.GetTeamId() != TeamId && otherUnit.GetCurrentHealth() > 0)
			{
				// Si on n'a pas de cible, on en prend une
				if (_currentTarget == null && _currentState == UnitState.Idle)
				{
					SetNewTarget(otherUnit);
				}
			}
		}
	}

	private void OnBodyExitedDetectionZone(Node2D body)
	{
		// Si notre cible sort de la zone de détection, on continue de la poursuivre
		// (la logique de poursuite gère déjà ce cas)
	}

	private void SetNewTarget(Unit target)
	{
		_currentTarget = target;

		float distanceToTarget = GlobalPosition.DistanceTo(target.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			// À portée d'attaque
			ChangeState(UnitState.Attacking);
		}
		else
		{
			// Hors portée, on se déplace vers la cible
			ChangeState(UnitState.MovingToTarget);
		}
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
		var defenders = camp.GetLiveDefenders();
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
				// Ignorer les alliés et soi-même
				if (otherUnit.GetTeamId() == TeamId)
					continue;

				// Ignorer les unités mortes
				if (otherUnit.GetCurrentHealth() <= 0)
					continue;

				// Vérifier la distance de détection
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
		CampSimple closestCamp = null;
		float closestDistance = float.MaxValue;

		foreach (var node in allCamps)
		{
			if (node is CampSimple camp)
			{
				// Ignorer les camps allies
				if (camp.GetTeamId() == TeamId)
					continue;

				// Le camp doit avoir ses defenseurs morts
				if (!camp.AreAllUnitsDefeated())
					continue;

				float distance = GlobalPosition.DistanceTo(camp.GlobalPosition);
				if (distance <= CampAttackDetectionRange && distance < closestDistance)
				{
					closestCamp = camp;
					closestDistance = distance;
				}
			}
		}

		return closestCamp;
	}
}
