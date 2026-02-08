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
	
	// Système de combat
	private float _attackTimer = 0f;
	private const float AttackInterval = 1f; // attaque toutes les secondes
	private Unit _currentTarget = null;
	
	// Tracking pour la mort mutuelle
	private int _lastAttackerTeamId = 0;

	// Méthodes Getter et Setter explicites
	public float GetCurrentHealth()
	{
		return _currentHealth;
	}
	
	public void SetCurrentHealth(float value)
	{
		_currentHealth = value;
		if (_currentHealth < 0)
		{
			_currentHealth = 0;
		}
		if (_currentHealth > _maxHealth)
		{
			_currentHealth = _maxHealth;
		}
	}
	
	public float GetMaxHealth()
	{
		return _maxHealth;
	}
	
	public int GetTeamId()
	{
		return TeamId;
	}
	
	public void SetTeamId(int newTeamId)
	{
		// Retirer l'ancien groupe d'équipe
		if (IsInGroup($"team_{TeamId}"))
		{
			RemoveFromGroup($"team_{TeamId}");
		}
		
		// Mettre à jour le TeamId
		TeamId = newTeamId;
		
		// Ajouter au nouveau groupe d'équipe
		AddToGroup($"team_{TeamId}");
	}
	
	public string GetUnitType()
	{
		return UnitType;
	}
	
	public float GetRange()
	{
		return _stats.Range;
	}
	
	public float GetAttack()
	{
		return _stats.Attack;
	}
	
	public bool GetIsMoving()
	{
		return _targetPosition.HasValue;
	}

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
			"AntiArmor" => "res://Assets/Units/Characters/Anti-armor/Anti-armor_front.png",
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
		// Système de combat
		ProcessCombat(delta);
		
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

		// Détection de blocage - seuil dynamique basé sur la vitesse
		// Une unité est bloquée si elle bouge à moins de 10% de sa vitesse normale
		float expectedMovement = _stats.Speed / 60f; // Distance attendue par frame à 60fps
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement * 0.1f)
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
		TakeDamageFrom(damage, 0);
	}
	
	public void TakeDamageFrom(float damage, int attackerTeamId)
	{
		float actualDamage = Mathf.Max(0, damage - _stats.Defense);
		_currentHealth -= actualDamage;
		_lastAttackerTeamId = attackerTeamId;

		if (_currentHealth <= 0)
		{
			Die();
		}
	}

	public void Heal(float amount)
	{
		SetCurrentHealth(_currentHealth + amount);
	}

	private void Die()
	{
		GD.Print($"Unite {UnitType} (Team {TeamId}) eliminee");
		QueueFree();
	}
	
	private void ProcessCombat(double delta)
	{
		_attackTimer += (float)delta;
		
		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			
			// Chercher une cible ennemie à portée
			Unit target = FindEnemyInRange();
			
			if (target != null)
			{
				AttackTarget(target);
			}
		}
	}
	
	private Unit FindEnemyInRange()
	{
		// Récupérer toutes les unités
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
				
				// Vérifier la distance
				float distance = GlobalPosition.DistanceTo(otherUnit.GlobalPosition);
				
				if (distance <= _stats.Range && distance < closestDistance)
				{
					closestEnemy = otherUnit;
					closestDistance = distance;
				}
			}
		}
		
		return closestEnemy;
	}
	
	private void AttackTarget(Unit target)
	{
		if (target == null || !IsInstanceValid(target))
			return;
		
		// Infliger des dégâts à la cible
		target.TakeDamageFrom(_stats.Attack, TeamId);
		
		GD.Print($"{UnitType} (Team {TeamId}) attaque {target.GetUnitType()} (Team {target.GetTeamId()}) - Degats: {_stats.Attack}");
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
