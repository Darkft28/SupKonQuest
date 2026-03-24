using Godot;

public partial class Ship
{
	private void ProcessAttackingState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(ShipState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
		float rangeWithMargin = _stats.Range + 20f;

		if (distanceToTarget > rangeWithMargin)
		{
			ChangeState(ShipState.MovingToTarget);
			return;
		}

		_attackTimer += (float)delta;

		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			AttackTarget(_currentTarget);
		}
	}

	private void AttackTarget(Ship target)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return;
		if (target.GetCurrentHealth() <= 0) return;

		SpawnProjectile(target);
	}

	private void SpawnProjectile(Ship target)
	{
		var projectile = new ShipProjectile();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.Initialize(GlobalPosition, target, _stats.Attack, TeamId, 300f);
	}

	public void TakeDamage(float damage)
	{
		TakeDamageFrom(damage, 0);
	}

	public void TakeDamageFrom(float damage, int attackerTeamId)
	{
		float totalDefense = _stats.Defense;
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
		if (_loadedUnits.Count > 0)
			_loadedUnits.Clear();

		if (IsLocalAuthority && !string.IsNullOrEmpty(NetworkId))
		{
			NetworkSync.Instance?.SendEntityDied(NetworkId);
		}

		QueueFree();
	}

	private void OnBodyEnteredDetectionZone(Node2D body)
	{
		// Transport ne combat pas
		if (ShipType == "Transport") return;

		if (body is Ship otherShip)
		{
			if (otherShip.GetTeamId() != TeamId && otherShip.GetCurrentHealth() > 0)
			{
				if (_currentTarget == null && _currentState == ShipState.Idle)
				{
					SetNewTarget(otherShip);
				}
			}
		}
	}

	private void SetNewTarget(Ship target)
	{
		_currentTarget = target;
		float distanceToTarget = GlobalPosition.DistanceTo(target.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(ShipState.Attacking);
		}
		else
		{
			ChangeState(ShipState.MovingToTarget);
		}
	}

	private bool IsTargetValid()
	{
		if (_currentTarget == null) return false;
		if (!IsInstanceValid(_currentTarget)) return false;
		if (!_currentTarget.IsInsideTree()) return false;
		if (_currentTarget.GetCurrentHealth() <= 0) return false;
		return true;
	}

	private Ship FindEnemyShipInRange()
	{
		var allShips = GetTree().GetNodesInGroup("ships");

		Ship closestEnemy = null;
		float closestDistance = float.MaxValue;

		foreach (var node in allShips)
		{
			if (node is Ship otherShip)
			{
				if (otherShip.GetTeamId() == TeamId) continue;
				if (otherShip.GetCurrentHealth() <= 0) continue;

				float distance = GlobalPosition.DistanceTo(otherShip.GlobalPosition);
				if (distance <= DetectionRange && distance < closestDistance)
				{
					closestEnemy = otherShip;
					closestDistance = distance;
				}
			}
		}

		return closestEnemy;
	}
}
