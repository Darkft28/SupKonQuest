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
		MovingToPoint,  // Se déplace vers un point (ordre du joueur)
		Healing         // Soigne un allie (Healer uniquement)
	}
	
	[Export] public string UnitType = "Infantry";
	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCampUnit = false;
	[Export] public float DetectionRange = 400f; // Sera recalcule dans _Ready

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

	// Destination sauvegardee pour reprendre la route apres un combat en passant
	private Vector2? _savedTargetPosition = null;

	// Systeme de soin (Healer)
	private Unit _healTarget = null;
	private float _healTimer = 0f;
	private const float HealInterval = 1f;
	private const float HealAmount = 12f;
	
	// Tracking pour la mort mutuelle
	private int _lastAttackerTeamId = 0;

	// Aura de defense (Support)
	private const float SupportAuraRadius = 200f;
	private const float SupportDefenseBonus = 10f;
	private static readonly Color AuraColor = new Color(0.3f, 0.5f, 1f, 0.12f);
	private static readonly Color AuraBorderColor = new Color(0.3f, 0.5f, 1f, 0.35f);

	// Barre de vie
	private const float HealthBarWidth = 80f;
	private const float HealthBarHeight = 10f;
	private const float HealthBarOffsetY = -75f; // Au-dessus du sprite
	private static readonly Color HealthBarBackground = new Color(0.15f, 0.15f, 0.15f, 0.8f);
	private static readonly Color HealthBarBorder = new Color(0f, 0f, 0f, 0.9f);
	private static readonly Color HealthColorFull = new Color(0.2f, 0.85f, 0.2f, 1f);    // Vert
	private static readonly Color HealthColorMid = new Color(1f, 0.8f, 0f, 1f);           // Jaune
	private static readonly Color HealthColorLow = new Color(0.9f, 0.15f, 0.15f, 1f);     // Rouge

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

		// Detection range = portee de l'arme + 150px de buffer
		DetectionRange = _stats.Range + 150f;

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
				_healTarget = null;
				Velocity = Vector2.Zero;
				QueueRedraw(); // Effacer le rayon de soin
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

	public override void _Draw()
	{
		// Cercle d'aura du Support
		if (UnitType == "Support")
		{
			DrawCircle(Vector2.Zero, SupportAuraRadius, AuraColor);
			DrawArc(Vector2.Zero, SupportAuraRadius, 0, Mathf.Tau, 64, AuraBorderColor, 2f);
		}

		// Rayon de soin vert du Healer
		if (UnitType == "Heal" && _currentState == UnitState.Healing
			&& _healTarget != null && IsInstanceValid(_healTarget) && _healTarget.IsInsideTree())
		{
			Vector2 targetLocal = _healTarget.GlobalPosition - GlobalPosition;
			Color healRayColor = new Color(0.2f, 0.9f, 0.3f, 0.6f);
			Color healRayGlow = new Color(0.2f, 0.9f, 0.3f, 0.15f);
			// Glow large
			DrawLine(Vector2.Zero, targetLocal, healRayGlow, 8f);
			// Rayon principal
			DrawLine(Vector2.Zero, targetLocal, healRayColor, 3f);
			// Petit cercle au point d'impact
			DrawCircle(targetLocal, 6f, healRayColor);
		}

		// Barre de vie (seulement si blesse)
		float healthPercent = _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
		if (healthPercent >= 1f)
			return;

		float barX = -HealthBarWidth / 2f;
		float barY = HealthBarOffsetY;

		DrawRect(new Rect2(barX - 1, barY - 1, HealthBarWidth + 2, HealthBarHeight + 2), HealthBarBorder);
		DrawRect(new Rect2(barX, barY, HealthBarWidth, HealthBarHeight), HealthBarBackground);

		Color healthColor;
		if (healthPercent > 0.6f)
			healthColor = HealthColorFull;
		else if (healthPercent > 0.3f)
			healthColor = HealthColorMid;
		else
			healthColor = HealthColorLow;

		float fillWidth = HealthBarWidth * healthPercent;
		DrawRect(new Rect2(barX, barY, fillWidth, HealthBarHeight), healthColor);
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

			case UnitState.Healing:
				ProcessHealingState(delta);
				break;
		}
	}
	
	private void ProcessIdleState(double delta)
	{
		// Healer : chercher des allies blesses, jamais d'ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_healTarget = woundedAlly;
				_healTimer = 0f;
				GD.Print($"[HEAL] Healer T{TeamId} commence a soigner {woundedAlly.GetUnitType()} T{woundedAlly.GetTeamId()} ({woundedAlly.GetCurrentHealth():F0}/{woundedAlly.GetMaxHealth():F0} HP)");
				ChangeState(UnitState.Healing);
			}
			else if (_savedTargetPosition.HasValue)
			{
				GD.Print($"[MOVE] {UnitType} T{TeamId} reprend sa route");
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
			}
			return;
		}

		// Unites de combat : chercher un ennemi dans la zone de detection
		if (_currentTarget == null)
		{
			Unit enemy = FindEnemyInDetectionRange();
			if (enemy != null)
			{
				SetNewTarget(enemy);
				return;
			}

			// Plus d'ennemis : reprendre la route sauvegardee si elle existe
			if (_savedTargetPosition.HasValue)
			{
				GD.Print($"[MOVE] {UnitType} T{TeamId} reprend sa route apres combat");
				_targetPosition = _savedTargetPosition;
				_savedTargetPosition = null;
				_stuckFrames = 0;
				_moveStartDelay = MoveStartDelayFrames;
				_lastPosition = GlobalPosition;
				_currentState = UnitState.MovingToPoint;
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

		// Healer en deplacement : chercher des allies blesses, pas des ennemis
		if (UnitType == "Heal")
		{
			Unit woundedAlly = FindWoundedAllyInRange();
			if (woundedAlly != null)
			{
				_savedTargetPosition = _targetPosition;
				_healTarget = woundedAlly;
				_healTimer = 0f;
				GD.Print($"[HEAL] Healer T{TeamId} s'arrete pour soigner {woundedAlly.GetUnitType()} T{woundedAlly.GetTeamId()} ({woundedAlly.GetCurrentHealth():F0}/{woundedAlly.GetMaxHealth():F0} HP)");
				ChangeState(UnitState.Healing);
				return;
			}
		}
		else
		{
			// Unites de combat : chercher des ennemis a proximite pendant le deplacement
			Unit enemy = FindEnemyInDetectionRange();
			if (enemy != null)
			{
				GD.Print($"[ENGAGE] {UnitType} T{TeamId} detecte {enemy.GetUnitType()} T{enemy.GetTeamId()} en route, combat!");
				_savedTargetPosition = _targetPosition;
				_currentTarget = enemy;

				float distanceToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distanceToEnemy <= _stats.Range)
				{
					ChangeState(UnitState.Attacking);
				}
				else
				{
					ChangeState(UnitState.MovingToTarget);
				}
				return;
			}
		}

		Vector2 direction = (_targetPosition.Value - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		// Arrivé à destination
		if (distance < ArrivalDistance)
		{
			_targetPosition = null;
			_savedTargetPosition = null;
			ChangeState(UnitState.Idle);
			return;
		}

		Velocity = direction * _stats.Speed;
		MoveAndSlide();

		// Détection de blocage
		ProcessStuckDetection();
	}
	
	private void ProcessHealingState(double delta)
	{
		// Verifier si la cible de soin est encore valide et blessee
		if (_healTarget == null || !IsInstanceValid(_healTarget) || !_healTarget.IsInsideTree()
			|| _healTarget.GetCurrentHealth() <= 0 || _healTarget.GetCurrentHealth() >= _healTarget.GetMaxHealth())
		{
			_healTarget = null;
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToAlly = GlobalPosition.DistanceTo(_healTarget.GlobalPosition);

		// Si l'allie est trop loin, se rapprocher
		if (distanceToAlly > _stats.Range)
		{
			Vector2 direction = (_healTarget.GlobalPosition - GlobalPosition).Normalized();
			Velocity = direction * _stats.Speed;
			MoveAndSlide();
			return;
		}

		// A portee : soigner
		Velocity = Vector2.Zero;
		_healTimer += (float)delta;
		QueueRedraw(); // Mettre a jour le rayon vert

		if (_healTimer >= HealInterval)
		{
			_healTimer = 0f;
			_healTarget.Heal(HealAmount);
		}
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

		return bonus;
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

	public void Heal(float amount)
	{
		float hpBefore = _currentHealth;
		SetCurrentHealth(_currentHealth + amount);
		QueueRedraw();
		GD.Print($"[HEAL] {UnitType} T{TeamId} +{amount} HP | {hpBefore:F0} -> {_currentHealth:F0}/{_maxHealth:F0}");
	}

	private void Die()
	{
		GD.Print($"[MORT] {UnitType} T{TeamId} elimine (tue par T{_lastAttackerTeamId})");
		QueueFree();
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

		float hpBefore = target.GetCurrentHealth();

		// Autres unites : degats directs
		target.TakeDamageFrom(_stats.Attack, TeamId);

		float hpAfter = target.GetCurrentHealth();
		float auraBonus = target.GetSupportDefenseBonus();
		float totalDef = target._stats.Defense + auraBonus;
		float actualDamage = _stats.Attack * 100f / (100f + totalDef);
		string auraStr = auraBonus > 0 ? $" +{auraBonus:F0} aura" : "";
		GD.Print($"[ATK] {UnitType} T{TeamId} -> {target.GetUnitType()} T{target.GetTeamId()} | {_stats.Attack} brut -> {actualDamage:F1} reel (def {target._stats.Defense}{auraStr}) | HP {hpBefore:F0} -> {hpAfter:F0}/{target.GetMaxHealth():F0}");
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

	public void MoveTo(Vector2 target)
	{
		_targetPosition = target;
		_savedTargetPosition = null; // Nouvel ordre annule la destination sauvegardee
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;

		// Passer en mode déplacement vers un point (ordre du joueur)
		ChangeState(UnitState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		_savedTargetPosition = null;
		Velocity = Vector2.Zero;
		ChangeState(UnitState.Idle);
	}
}
