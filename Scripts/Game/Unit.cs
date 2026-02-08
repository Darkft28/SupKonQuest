using Godot;
using System;

public partial class Unit : CharacterBody2D
{
	// États de l'unité
	private enum UnitState
	{
		Idle,           // En attente
		MovingToTarget, // Se déplace vers une cible ennemie
		Attacking,      // Attaque une cible à portée
		MovingToPoint   // Se déplace vers un point (ordre du joueur)
	}
	
	[Export] public string UnitType = "Infantry";
	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCampUnit = false;
	[Export] public float DetectionRange = 400f; // Portée de détection des ennemis

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
	
	// Système de combat avec machine à états
	private UnitState _currentState = UnitState.Idle;
	private float _attackTimer = 0f;
	private const float AttackInterval = 1f; // attaque toutes les secondes
	private Unit _currentTarget = null;
	
	// Zone de détection
	private Area2D _detectionZone = null;
	
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
		
		// Créer la zone de détection pour le combat
		CreateDetectionZone();

		// Ajouter au groupe pour faciliter la recherche
		AddToGroup("units");
		AddToGroup($"team_{TeamId}");
		
		// État initial
		_currentState = UnitState.Idle;
	}
	
	private void CreateDetectionZone()
	{
		_detectionZone = new Area2D();
		_detectionZone.Name = "DetectionZone";
		
		// Créer la forme de collision circulaire
		var collisionShape = new CollisionShape2D();
		var circleShape = new CircleShape2D();
		circleShape.Radius = DetectionRange;
		collisionShape.Shape = circleShape;
		
		_detectionZone.AddChild(collisionShape);
		AddChild(_detectionZone);
		
		// Connecter les signaux pour détecter les entrées/sorties
		_detectionZone.BodyEntered += OnBodyEnteredDetectionZone;
		_detectionZone.BodyExited += OnBodyExitedDetectionZone;
	}
	
	private void OnBodyEnteredDetectionZone(Node2D body)
	{
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
	
	private void ChangeState(UnitState newState)
	{
		if (_currentState == newState)
			return;
		
		_currentState = newState;
		
		// Actions à l'entrée dans un nouvel état
		switch (newState)
		{
			case UnitState.Idle:
				_currentTarget = null;
				Velocity = Vector2.Zero;
				break;
				
			case UnitState.MovingToTarget:
				// On va se déplacer vers la cible dans _PhysicsProcess
				break;
				
			case UnitState.Attacking:
				Velocity = Vector2.Zero;
				_attackTimer = 0f; // Reset pour attaquer immédiatement
				break;
				
			case UnitState.MovingToPoint:
				_currentTarget = null; // On annule la cible de combat
				break;
		}
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
		// Machine à états principale
		switch (_currentState)
		{
			case UnitState.Idle:
				ProcessIdleState(delta);
				break;
				
			case UnitState.MovingToTarget:
				ProcessMovingToTargetState(delta);
				break;
				
			case UnitState.Attacking:
				ProcessAttackingState(delta);
				break;
				
			case UnitState.MovingToPoint:
				ProcessMovingToPointState(delta);
				break;
		}
	}
	
	private void ProcessIdleState(double delta)
	{
		// En Idle, chercher un ennemi dans la zone de détection
		if (_currentTarget == null)
		{
			Unit enemy = FindEnemyInDetectionRange();
			if (enemy != null)
			{
				SetNewTarget(enemy);
			}
		}
	}
	
	private void ProcessMovingToTargetState(double delta)
	{
		// Vérifier si la cible est encore valide
		if (!IsTargetValid())
		{
			ChangeState(UnitState.Idle);
			return;
		}
		
		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
		
		// DEBUG: Afficher la distance et la portée
		// GD.Print($"[DEBUG] {UnitType} (Team {TeamId}) -> MovingToTarget: distance={distanceToTarget:F1}, range={_stats.Range}");
		
		// Si à portée d'attaque, passer en mode attaque
		if (distanceToTarget <= _stats.Range)
		{
			GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) passe en mode ATTACKING (distance={distanceToTarget:F1} <= range={_stats.Range})");
			ChangeState(UnitState.Attacking);
			return;
		}
		
		// Sinon, se déplacer vers la cible
		Vector2 direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
		Velocity = direction * _stats.Speed;
		MoveAndSlide();
		
		// Détection de blocage
		ProcessStuckDetection();
	}
	
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
	
	private void ProcessMovingToPointState(double delta)
	{
		// Déplacement vers un point ordonné par le joueur
		if (!_targetPosition.HasValue)
		{
			ChangeState(UnitState.Idle);
			return;
		}
		
		Vector2 direction = (_targetPosition.Value - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		// Arrivé à destination
		if (distance < ArrivalDistance)
		{
			_targetPosition = null;
			ChangeState(UnitState.Idle);
			return;
		}

		Velocity = direction * _stats.Speed;
		MoveAndSlide();

		// Détection de blocage
		ProcessStuckDetection();
		
		// Pendant le déplacement, chercher des ennemis à proximité (optionnel: attaque en mouvement)
		// Pour l'instant, on reste concentré sur la destination
	}
	
	private void ProcessStuckDetection()
	{
		// Attendre le délai initial avant de vérifier le blocage
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		// Détection de blocage - seuil dynamique basé sur la vitesse
		float expectedMovement = _stats.Speed / 60f;
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement * 0.1f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloqué - vérifier si on a une cible proche pour attaquer
				if (_currentTarget != null && IsTargetValid())
				{
					float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
					// Si bloqué mais proche de la cible (collision physique), passer en Attacking
					// On utilise une marge de 120 pixels (collision ~80 + marge)
					if (distanceToTarget <= 120f)
					{
						GD.Print($"[COMBAT] {UnitType} (Team {TeamId}) bloque pres de la cible, passage en ATTACKING");
						ChangeState(UnitState.Attacking);
						_stuckFrames = 0;
						return;
					}
				}
				
				// Sinon, on arrête et on passe en Idle
				_targetPosition = null;
				ChangeState(UnitState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
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
		
		// Passer en mode déplacement vers un point (ordre du joueur)
		ChangeState(UnitState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		Velocity = Vector2.Zero;
		ChangeState(UnitState.Idle);
	}
}
