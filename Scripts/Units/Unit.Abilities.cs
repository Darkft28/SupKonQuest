using Godot;
using System.Collections.Generic;

public partial class Unit
{
	private const string HealUltimateId = "heal_ultimate";
	private const string SupportUltimateId = "support_ultimate";
	private const float HealUltimateRadius = 300f;
	private const float HealUltimateAmount = 70f;
	private const float SupportUltimateRadius = 320f;
	private const float SupportUltimateDefenseBonus = 20f;
	private const float SupportUltimateDuration = 8f;
	private const float HealUltimateCooldown = 20f;
	private const float SupportUltimateCooldown = 25f;

	private readonly Dictionary<string, float> _ultimateCooldowns = new();
	private float _temporaryDefenseBonus;
	private float _temporaryDefenseBonusRemaining;

	public bool CanUseUltimate(string abilityId)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
			return false;

		if (abilityId == HealUltimateId && UnitType != "Heal")
			return false;
		if (abilityId == SupportUltimateId && UnitType != "Support")
			return false;

		return GetUltimateCooldownRemaining(abilityId) <= 0f;
	}

	public float GetUltimateCooldownRemaining(string abilityId)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
			return 0f;

		return _ultimateCooldowns.TryGetValue(abilityId, out float remaining) ? remaining : 0f;
	}

	public bool TryCastUltimate(string abilityId, Vector2 targetPosition)
	{
		if (!CanUseUltimate(abilityId))
			return false;

		switch (abilityId)
		{
			case HealUltimateId:
				CastHealUltimate(targetPosition);
				_ultimateCooldowns[abilityId] = HealUltimateCooldown;
				return true;

			case SupportUltimateId:
				CastSupportUltimate(targetPosition);
				_ultimateCooldowns[abilityId] = SupportUltimateCooldown;
				return true;
		}

		return false;
	}

	private void CastHealUltimate(Vector2 targetPosition)
	{
		var allUnits = GetTree().GetNodesInGroup("units");
		foreach (var node in allUnits)
		{
			if (node is not Unit ally || ally.GetTeamId() != TeamId || ally.GetCurrentHealth() <= 0)
				continue;

			if (ally.GlobalPosition.DistanceTo(targetPosition) <= HealUltimateRadius)
				ally.Heal(HealUltimateAmount);
		}
	}

	private void CastSupportUltimate(Vector2 targetPosition)
	{
		var allUnits = GetTree().GetNodesInGroup("units");
		foreach (var node in allUnits)
		{
			if (node is not Unit ally || ally.GetTeamId() != TeamId || ally.GetCurrentHealth() <= 0)
				continue;

			if (ally.GlobalPosition.DistanceTo(targetPosition) <= SupportUltimateRadius)
				ally.ApplyTemporaryDefenseBonus(SupportUltimateDefenseBonus, SupportUltimateDuration);
		}
	}

	private void ApplyTemporaryDefenseBonus(float bonus, float duration)
	{
		_temporaryDefenseBonus = Mathf.Max(_temporaryDefenseBonus, bonus);
		_temporaryDefenseBonusRemaining = Mathf.Max(_temporaryDefenseBonusRemaining, duration);
	}

	private float GetTemporaryDefenseBonus()
	{
		return _temporaryDefenseBonusRemaining > 0f ? _temporaryDefenseBonus : 0f;
	}

	private void TickUltimateState(double delta)
	{
		if (_temporaryDefenseBonusRemaining > 0f)
		{
			_temporaryDefenseBonusRemaining -= (float)delta;
			if (_temporaryDefenseBonusRemaining <= 0f)
			{
				_temporaryDefenseBonusRemaining = 0f;
				_temporaryDefenseBonus = 0f;
			}
		}

		if (_ultimateCooldowns.Count == 0)
			return;

		var keys = new List<string>(_ultimateCooldowns.Keys);
		foreach (string key in keys)
		{
			float updated = _ultimateCooldowns[key] - (float)delta;
			_ultimateCooldowns[key] = Mathf.Max(0f, updated);
		}
	}
}
