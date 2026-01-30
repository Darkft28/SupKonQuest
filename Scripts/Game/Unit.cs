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
	private Sprite2D _sprite;

	private Vector2? _targetPosition = null;
	private const float ArrivalDistance = 20f;
	private Vector2 _lastPosition;
	private int _stuckFrames = 0;
	private int _moveStartDelay = 0;
	private const int MaxStuckFrames = 120; // ~2 sec à 60fps
	private const int MoveStartDelayFrames = 60; // ~1 sec avant de vérifier le blocage

	// Propriétés
	public float GetCurrentHealth => _currentHealth;
	public float MaxHealth => _maxHealth;
	public bool IsMoving => _targetPosition.HasValue;

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
		_lastPosition = GlobalPosition;

		// Créer la collision
		CreateCollision();

		// Créer et configurer le sprite
		CreateSprite();

		// Ajouter au groupe pour faciliter la recherche
		AddToGroup("units");
		AddToGroup($"team_{TeamId}");
	}

	private void CreateSprite()
	{
		_sprite = new Sprite2D();

		// Mapper le type d'unité au chemin de texture (gestion des cas particuliers)
		string texturePath = UnitType switch
		{
			"Heal" => "res://Assets/Units/Characters/Healer/healer_Front.png",
			_ => $"res://Assets/Units/Characters/{UnitType}/{UnitType}_Front.png"
		};

		var texture = GD.Load<Texture2D>(texturePath);

		if (texture != null)
		{
			_sprite.Texture = texture;
			_sprite.Scale = new Vector2(0.255f, 0.255f);
			AddChild(_sprite);
		}
		else
		{
			GD.PrintErr($"Impossible de charger la texture: {texturePath}");
		}
	}

	private void CreateCollision()
	{
		var collision = new CollisionShape2D();
		var shape = new CircleShape2D();
		shape.Radius = 40f;
		collision.Shape = shape;
		AddChild(collision);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_targetPosition.HasValue)
			return;

		Vector2 direction = (_targetPosition.Value - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		// Arrivé à destination
		if (distance < ArrivalDistance)
		{
			Stop();
			return;
		}

		Velocity = direction * _stats.Speed;
		MoveAndSlide();

		// Attendre le délai initial avant de vérifier le blocage
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		// Détection de blocage
		if (GlobalPosition.DistanceTo(_lastPosition) < 1f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				Stop(); // Bloqué, on arrête
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
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
		_targetPosition = target;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
	}

	public void Stop()
	{
		_targetPosition = null;
		Velocity = Vector2.Zero;
	}
}
