using Godot;
using System;

public partial class Unit : CharacterBody2D
{
	[Export] public string UnitType = "Infantry";
	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCampUnit = false;

	private float _currentHealth;
	private float _maxHealth;
	private UnitStatsData _stats;

	// Propriétés
	public float GetCurrentHealth => _currentHealth;
	public float MaxHealth => _maxHealth;

	public override void _Ready()
	{
		// Charger les stats en fonction du type
		_stats = UnitStats.GetStats(UnitType);
		_maxHealth = _stats.MaxHealth;

		// Les unités de camps neutres sont plus fortes
		if (IsNeutralCampUnit)
		{
			_maxHealth *= 1.5f;
		}

		_currentHealth = _maxHealth;

		// Ajouter au groupe pour faciliter la recherche
		AddToGroup("units");
		AddToGroup($"team_{TeamId}");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Logique de mouvement à implémenter
	}

	public void TakeDamage(float damage)
	{
		float actualDamage = Mathf.Max(0, damage - _stats.Defense);
		_currentHealth -= actualDamage;

		if (_currentHealth <= 0)
		{
			Die();
		}
	}

	public void Heal(float amount)
	{
		_currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
	}

	private void Die()
	{
		GD.Print($"Unite {UnitType} (Team {TeamId}) eliminee");
		QueueFree();
	}

	public void MoveTo(Vector2 target)
	{
		// À implémenter : déplacement vers la cible
	}
}
