using Godot;

public partial class Ship
{
	private static readonly PackedScene ShipProjectileScene = GD.Load<PackedScene>("res://Scenes/ShipProjectile.tscn");

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

	private bool IsEnemyShipTarget(Ship ship) =>
		ship != null && ship.GetTeamId() != TeamId && ship.GetCurrentHealth() > 0;

	private void AttackTarget(Ship target)
	{
		if (!IsEnemyShipTarget(target)) return;
		if (!IsInstanceValid(target) || !target.IsInsideTree()) return;

		SpawnProjectile(target);
	}

	private void SpawnProjectile(Ship target)
	{
		var projectile = ShipProjectileScene?.Instantiate<ShipProjectile>() ?? new ShipProjectile();
		GetTree().CurrentScene.AddChild(projectile);

		bool isDestroyer = ShipType == "Destroyer";
		var type = isDestroyer ? ShipProjectile.ShipProjectileType.Cannonball : ShipProjectile.ShipProjectileType.Arrow;
		float speed = isDestroyer ? 300f : 500f;

		projectile.Initialize(GlobalPosition, target, _stats.Attack, TeamId, type, speed, isDestroyer);
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

		if (NetworkSync.Instance?.IsMultiplayer() == true && !string.IsNullOrEmpty(NetworkId))
			NetworkSync.Instance?.SendEntityDied(NetworkId);

		QueueFree();
	}

	private void OnBodyEnteredDetectionZone(Node2D body)
	{
		if (ShipType == "Transport") return;
		if (body is Unit) return;

		if (body is Ship otherShip)
		{
			if (!IsEnemyShipTarget(otherShip)) return;

			if (_currentTarget == null && _currentState == ShipState.Idle)
				SetNewTarget(otherShip);
		}
	}

	private void SetNewTarget(Ship target)
	{
		if (!IsEnemyShipTarget(target)) return;

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
		return IsEnemyShipTarget(_currentTarget);
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
				if (!IsEnemyShipTarget(otherShip)) continue;

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
