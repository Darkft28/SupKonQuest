using Godot;

public partial class Unit
{
	private void ProcessHealingState(double delta)
	{
		if (_healTarget == null || !IsInstanceValid(_healTarget) || !_healTarget.IsInsideTree()
			|| _healTarget.GetCurrentHealth() <= 0 || _healTarget.GetCurrentHealth() >= _healTarget.GetMaxHealth())
		{
			_healTarget = null;
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToAlly = GlobalPosition.DistanceTo(_healTarget.GlobalPosition);

		if (distanceToAlly > _stats.Range)
		{
			Vector2 direction = (_healTarget.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * _stats.Speed;
			MoveAndSlide();
			return;
		}

		Velocity = Vector2.Zero;
		_healTimer += (float)delta;
		QueueRedraw();

		if (_healTimer >= HealInterval)
		{
			_healTimer = 0f;
			PlayHealerSfx();
			_healTarget.Heal(HealAmount);
		}
	}

	public void Heal(float amount)
	{
		SetCurrentHealth(_currentHealth + amount);
		QueueRedraw();
	}

	public float GetSupportDefenseBonus()
	{
		// Un Support ne se buff pas lui-meme
		if (UnitType == "Support")
			return 0f;

		var allUnits = GetTree().GetNodesInGroup("units");
		float bonus = 0f;

		foreach (var node in allUnits)
		{
			if (node is Unit ally && ally.UnitType == "Support" && ally.GetTeamId() == TeamId
				&& ally.GetCurrentHealth() > 0)
			{
				float distance = GlobalPosition.DistanceTo(ally.GlobalPosition);
				if (distance <= SupportAuraRadius)
				{
					bonus += SupportDefenseBonus;
				}
			}
		}

		return Mathf.Min(bonus, 40f) + GetTemporaryDefenseBonus(); // cap aura + buff ultimate
	}

	private Unit FindWoundedAllyInRange()
	{
		var allUnits = GetTree().GetNodesInGroup("units");

		Unit mostWounded = null;
		float lowestHealthPercent = 1f;

		foreach (var node in allUnits)
		{
			if (node is Unit ally && ally != this && ally.GetTeamId() == TeamId
				&& ally.GetCurrentHealth() > 0 && ally.GetCurrentHealth() < ally.GetMaxHealth())
			{
				float distance = GlobalPosition.DistanceTo(ally.GlobalPosition);
				if (distance <= DetectionRange)
				{
					float healthPercent = ally.GetCurrentHealth() / ally.GetMaxHealth();
					if (healthPercent < lowestHealthPercent)
					{
						lowestHealthPercent = healthPercent;
						mostWounded = ally;
					}
				}
			}
		}

		return mostWounded;
	}
}
