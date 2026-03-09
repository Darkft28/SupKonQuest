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
		Healing,        // Soigne un allie (Healer uniquement)
		MovingToTransport, // Se deplace vers un Transport pour embarquer
		AttackingCamp     // Attaque un camp ennemi/neutre
	}

	[Export] public string UnitType = "Infantry";
	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCampUnit = false;
	[Export] public float DetectionRange = 400f; // Sera recalcule dans _Ready

	// Reseau
	public string NetworkId = "";
	public bool IsLocalAuthority = true;
	private Vector2? _networkTargetPosition = null;

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

	// Transport : bateau cible pour embarquement
	private Ship _targetTransport = null;
	private const float BoardingDistance = 250f;

	// Attaque de camp
	private CampSimple _campTarget = null;
	private const float CampAttackDetectionRange = 600f;

	// Navigation
	private NavigationAgent2D _navAgent = null;
	private Vector2 _lastNavTargetPos = Vector2.Zero;
	private bool _navTargetDirty = true;
	private const float NavUpdateDistance = 64f; // recalcule le chemin si la cible bouge > 64px

	// Throttle recherche ennemis/camps (évite O(n²) chaque frame)
	private float _aiSearchTimer = 0f;
	private const float EnemySearchInterval = 0.5f;

	// Throttle vérification défenseurs camp (évite LINQ chaque frame)
	private float _campDefeatCheckTimer = 0f;
	private bool _campDefeatCached = false;
	private const float CampDefeatCheckInterval = 0.3f;

	// Ennemi croisé en chemin vers un camp (combat opportuniste)
	private Unit _opportunisticTarget = null;

	// Tracking pour la mort mutuelle
	private int _lastAttackerTeamId = 0;

	// Camp propriétaire (pour notifier à la mort)
	public CampSimple OwnerCamp = null;

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

	// Recalcule _maxHealth selon IsNeutralCampUnit et remet les HP à fond
	// Utilisé quand un camp neutre est assigné à une équipe (FFA)
	public void RecalculateMaxHealth()
	{
		_maxHealth = UnitStats.GetStats(UnitType).MaxHealth;
		if (IsNeutralCampUnit)
			_maxHealth *= 1.5f;
		_currentHealth = _maxHealth;
		QueueRedraw();
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

	public bool IsIdleState()
	{
		return _currentState == UnitState.Idle;
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

		// Reseau : enregistrer dans le registre
		if (!string.IsNullOrEmpty(NetworkId))
		{
			NetworkEntityRegistry.Register(NetworkId, this);
		}

		// Stagger la recherche ennemis : chaque unité a un offset aléatoire
		_aiSearchTimer = GD.Randf() * EnemySearchInterval;

		// Créer le NavigationAgent2D pour le pathfinding (couche 1 = terrestre)
		_navAgent = new NavigationAgent2D();
		_navAgent.PathDesiredDistance = 10f;
		_navAgent.TargetDesiredDistance = ArrivalDistance;
		_navAgent.AvoidanceEnabled = true;
		_navAgent.NavigationLayers = 1u;
		_navAgent.Radius = 40f; // marge autour des obstacles (= rayon collision unité)
		AddChild(_navAgent);

		// État initial
		_currentState = UnitState.Idle;
	}

	public override void _ExitTree()
	{
		if (!string.IsNullOrEmpty(NetworkId))
		{
			NetworkEntityRegistry.Unregister(NetworkId);
		}
	}

	// Reseau : appliquer l'etat recu du peer distant
	public void ApplyNetworkState(Vector2 pos, float health, int state)
	{
		_networkTargetPosition = pos;
		_currentHealth = health;
		QueueRedraw();
	}

	// Reseau : retourner l'etat courant en int
	public int GetStateInt()
	{
		return (int)_currentState;
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
				_navTargetDirty = true; // Nouvelle cible → forcer recalcul chemin
				break;

			case UnitState.Attacking:
				Velocity = Vector2.Zero;
				_attackTimer = 0f; // Reset pour attaquer immédiatement
				break;

			case UnitState.MovingToPoint:
				_currentTarget = null; // On annule la cible de combat
				_targetTransport = null;
				_campTarget = null;
				_navTargetDirty = true;
				break;

			case UnitState.MovingToTransport:
				_currentTarget = null;
				_campTarget = null;
				_navTargetDirty = true;
				break;

			case UnitState.AttackingCamp:
				_currentTarget = null;
				_targetTransport = null;
				_opportunisticTarget = null;
				_attackTimer = 0f;
				_aiSearchTimer = 0f;
				_navTargetDirty = true;
				_campDefeatCached = false;
				_campDefeatCheckTimer = 0f;
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Puppet : interpoler vers la position reseau, pas d'IA (multi seulement)
		bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
		if (isMulti && !IsLocalAuthority)
		{
			if (_networkTargetPosition.HasValue)
			{
				GlobalPosition = GlobalPosition.Lerp(_networkTargetPosition.Value, 10f * (float)delta);
			}
			return;
		}

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

			case UnitState.MovingToTransport:
				ProcessMovingToTransportState(delta);
				break;

			case UnitState.AttackingCamp:
				ProcessAttackingCampState(delta);
				break;
		}
	}
}
