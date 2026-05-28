using Godot;
using System.Collections.Generic;

public partial class UltimateVfxManager : Node2D
{
	[Export] public float HealRadius = 300f;
	[Export] public float SupportRadius = 320f;
	[Export] public float EffectDurationSeconds = 0.7f;
	[Export] public Color HealColor = new(0.25f, 0.95f, 0.55f, 0.85f);
	[Export] public Color SupportColor = new(0.35f, 0.75f, 1.0f, 0.85f);
	[Export] public float RingThickness = 8f;

	private readonly List<UltimateEffectData> _activeEffects = new();

	public override void _Ready()
	{
		ZIndex = 200;
		TopLevel = true;
		Unit.UltimateCast += OnUnitUltimateCast;
	}

	public override void _ExitTree()
	{
		Unit.UltimateCast -= OnUnitUltimateCast;
	}

	public override void _Process(double delta)
	{
		if (_activeEffects.Count == 0)
			return;

		float dt = (float)delta;
		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			UltimateEffectData effect = _activeEffects[i];
			effect.Age += dt;
			if (effect.Age >= effect.Duration)
				_activeEffects.RemoveAt(i);
			else
				_activeEffects[i] = effect;
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (UltimateEffectData effect in _activeEffects)
		{
			float t = Mathf.Clamp(effect.Age / effect.Duration, 0f, 1f);
			float alpha = Mathf.Lerp(effect.BaseColor.A, 0f, t);
			float currentRadius = Mathf.Lerp(effect.Radius * 0.3f, effect.Radius, t);
			Color color = new(effect.BaseColor.R, effect.BaseColor.G, effect.BaseColor.B, alpha);
			DrawArc(effect.WorldPosition, currentRadius, 0f, Mathf.Tau, 64, color, RingThickness);
			DrawCircle(effect.WorldPosition, currentRadius * 0.15f, new Color(color.R, color.G, color.B, alpha * 0.35f));
		}
	}

	private void OnUnitUltimateCast(Unit caster, string abilityId, Vector2 targetPosition)
	{
		if (string.IsNullOrWhiteSpace(abilityId))
			return;

		if (abilityId == "heal_ultimate")
		{
			_activeEffects.Add(new UltimateEffectData(targetPosition, HealRadius, HealColor, EffectDurationSeconds));
		}
		else if (abilityId == "support_ultimate")
		{
			_activeEffects.Add(new UltimateEffectData(targetPosition, SupportRadius, SupportColor, EffectDurationSeconds));
		}
	}

	private struct UltimateEffectData
	{
		public Vector2 WorldPosition;
		public float Radius;
		public Color BaseColor;
		public float Duration;
		public float Age;

		public UltimateEffectData(Vector2 worldPosition, float radius, Color baseColor, float duration)
		{
			WorldPosition = worldPosition;
			Radius = radius;
			BaseColor = baseColor;
			Duration = duration;
			Age = 0f;
		}
	}

}
