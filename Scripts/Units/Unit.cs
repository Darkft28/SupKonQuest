using Godot;

public partial class Unit : CharacterBody2D
{
	private enum UnitState
	{
		Idle,
		MovingToTarget,
		Attacking,
		MovingToPoint,
		Healing,
		MovingToTransport,
		AttackingCamp
	}

	[Export] public string UnitType = "Infantry";
	[Export] public int TeamId = 1;
	[Export] public bool IsNeutralCampUnit = false;
	[Export] public float DetectionRange = 400f; // recalculé dans _Ready

	// Réseau
	public string NetworkId = "";
	public bool IsLocalAuthority = true;
	private Vector2? _networkTargetPosition = null;

	private float _currentHealth;
	private float _maxHealth;
	private UnitStatsData _stats;
	private Sprite2D _sprite;

	private Vector2? _targetPosition = null;
	private const float ArrivalDistance = 80f;
	private Vector2 _lastPosition;
	private int _stuckFrames = 0;
	private int _moveStartDelay = 0;
	private const int MaxStuckFrames = 120; // ~2 sec à 60fps
	private const int MoveStartDelayFrames = 60; // ~1 sec avant de vérifier le blocage

	private UnitState _currentState = UnitState.Idle;
	private float _attackTimer = 0f;
	private const float AttackInterval = 1f;
	private Unit _currentTarget = null;

	private Area2D _detectionZone = null;

	// Destination sauvegardée pour reprendre la route après un combat en passant
	private Vector2? _savedTargetPosition = null;

	private Unit _healTarget = null;
	private float _healTimer = 0f;
	private const float HealInterval = 1f;
	private const float HealAmount = 12f;

	private Ship _targetTransport = null;
	private const float BoardingDistance = 250f;

	private CampSimple _campTarget = null;
	private const float CampAttackDetectionRange = 900f;

	private NavigationAgent2D _navAgent = null;
	private Vector2 _lastNavTargetPos = Vector2.Zero;
	private bool _navTargetDirty = true;
	private int _navPathCooldown = 0; // frames avant de relire le chemin nav (calcul asynchrone)
	private const float NavUpdateDistance = 64f; // recalcule le chemin si la cible bouge > 64px
	private Vector2 _intendedDirection = Vector2.Zero; // direction voulue avant MoveAndSlide

	// Throttle recherche ennemis/camps - évite O(n²) chaque frame
	private float _aiSearchTimer = 0f;
	private const float EnemySearchInterval = 0.5f;

	// Throttle vérification défenseurs camp - évite LINQ chaque frame
	private float _campDefeatCheckTimer = 0f;
	private bool _campDefeatCached = false;
	private const float CampDefeatCheckInterval = 0.3f;


	// Ennemi croisé en chemin vers un camp (combat opportuniste)
	private Unit _opportunisticTarget = null;

	private int _lastAttackerTeamId = 0;

	// Camp propriétaire (pour notifier à la mort)
	public CampSimple OwnerCamp = null;

	// Région économique de cette unité (héritée du camp qui l'a produite)
	public int RegionId { get; set; } = 0;

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
		if (IsInGroup($"team_{TeamId}"))
			RemoveFromGroup($"team_{TeamId}");
		TeamId = newTeamId;
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
		_stats = UnitStats.GetStats(UnitType);
		_maxHealth = _stats.MaxHealth;

		// Les unités de camps neutres ont 1.5x HP
		if (IsNeutralCampUnit)
			_maxHealth *= 1.5f;

		_currentHealth = _maxHealth;
		_lastPosition = GlobalPosition;

		// Detection range = portée de l'arme + 400px de buffer
		DetectionRange = _stats.Range + 400f;

		CreateCollision();
		CreateSprite();
		CreateDetectionZone();

		AddToGroup("units");
		AddToGroup($"team_{TeamId}");

		if (!string.IsNullOrEmpty(NetworkId))
			NetworkEntityRegistry.Register(NetworkId, this);

		// Stagger la recherche ennemis : offset aléatoire pour éviter les pics CPU
		_aiSearchTimer = GD.Randf() * EnemySearchInterval;

		// NavigationAgent2D pré-instancié dans la scène (fallback runtime si manquant)
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		if (_navAgent == null)
		{
			_navAgent = new NavigationAgent2D();
			_navAgent.Name = "NavigationAgent2D";
			AddChild(_navAgent);
		}

		_navAgent.PathDesiredDistance = 10f;
		_navAgent.TargetDesiredDistance = ArrivalDistance;
		_navAgent.PathMaxDistance = 512f;
		_navAgent.AvoidanceEnabled = true;
		_navAgent.NavigationLayers = 1u;
		_navAgent.MaxSpeed = _stats.Speed;
		_navAgent.VelocityComputed += OnNavVelocityComputed;

		_currentState = UnitState.Idle;
	}

	public override void _ExitTree()
	{
		if (!string.IsNullOrEmpty(NetworkId))
		{
			NetworkEntityRegistry.Unregister(NetworkId);
		}
	}

	public void ApplyNetworkState(Vector2 pos, float health, int state)
	{
		_networkTargetPosition = pos;
		_currentHealth = health;
		QueueRedraw();
	}

	public int GetStateInt()
	{
		return (int)_currentState;
	}

	private void ChangeState(UnitState newState)
	{
		if (_currentState == newState)
			return;

		_currentState = newState;

		switch (newState)
		{
			case UnitState.Idle:
				_currentTarget = null;
				_healTarget = null;
				Velocity = Vector2.Zero;
				_intendedDirection = Vector2.Zero;
				QueueRedraw();
				break;

			case UnitState.MovingToTarget:
				_navTargetDirty = true;
				break;

			case UnitState.Attacking:
				Velocity = Vector2.Zero;
				_attackTimer = 0f;
				break;

			case UnitState.MovingToPoint:
				_currentTarget = null;
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

		ProcessSupportBattleHorn(delta);
		TickUltimateState(delta);

		if (_intendedDirection != Vector2.Zero)
			UpdateSpriteDirection(_intendedDirection);
	}
}
