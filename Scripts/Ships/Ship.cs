using Godot;
using System.Collections.Generic;

public partial class Ship : CharacterBody2D
{
	private enum ShipState
	{
		Idle,
		MovingToPoint,
		MovingToTarget,
		Attacking
	}

	[Export] public string ShipType = "Fregate";
	[Export] public int TeamId = 1;
	[Export] public float DetectionRange = 500f;

	public string NetworkId = "";
	public bool IsLocalAuthority = true;
	private Vector2? _networkTargetPosition = null;

	private float _currentHealth;
	private float _maxHealth;
	private ShipStatsData _stats;
	private Sprite2D _sprite;
	private TileMapLayer _tileMapSol;

	private Vector2? _targetPosition = null;
	private const float ArrivalDistance = 30f;
	private Vector2 _lastPosition;
	private int _stuckFrames = 0;
	private int _moveStartDelay = 0;
	private const int MaxStuckFrames = 120;
	private const int MoveStartDelayFrames = 60;

	private ShipState _currentState = ShipState.Idle;
	private float _attackTimer = 0f;
	private const float AttackInterval = 1.5f;
	private Ship _currentTarget = null;

	private Area2D _detectionZone = null;

	private int _lastAttackerTeamId = 0;

	// Navigation maritime
	private NavigationAgent2D _navAgent = null;
	private Vector2 _lastNavTargetPos = Vector2.Zero;
	private bool _navTargetDirty = true;
	private const float NavUpdateDistance = 64f;

	// debarquement en attente (le bateau navigue d'abord, puis debarque)
	private Vector2? _pendingUnloadPosition = null;
	private List<(string type, int teamId, float health)> _loadedUnits = new List<(string, int, float)>();

	private enum SpriteDirection { Front, Back, Left, Right }
	private SpriteDirection _currentDirection = SpriteDirection.Front;

	private const float HealthBarWidth = 100f;
	private const float HealthBarHeight = 12f;
	private const float HealthBarOffsetY = -200f;
	private static readonly Color HealthBarBackground = new Color(0.15f, 0.15f, 0.15f, 0.8f);
	private static readonly Color HealthBarBorder = new Color(0f, 0f, 0f, 0.9f);
	private static readonly Color HealthColorFull = new Color(0.2f, 0.85f, 0.2f, 1f);
	private static readonly Color HealthColorMid = new Color(1f, 0.8f, 0f, 1f);
	private static readonly Color HealthColorLow = new Color(0.9f, 0.15f, 0.15f, 1f);

	public float GetCurrentHealth() => _currentHealth;
	public float GetMaxHealth() => _maxHealth;
	public int GetTeamId() => TeamId;
	public string GetShipType() => ShipType;
	public float GetRange() => _stats.Range;
	public float GetAttack() => _stats.Attack;
	public bool GetIsMoving() => _targetPosition.HasValue;
	public int GetLoadedUnitCount() => _loadedUnits.Count;
	public int GetCapacity() => _stats.Capacity;

	public void SetTileMapSol(TileMapLayer tileMapSol)
	{
		_tileMapSol = tileMapSol;
	}

	public override void _Ready()
	{
		_stats = ShipStats.GetStats(ShipType);
		_maxHealth = _stats.MaxHealth;
		_currentHealth = _maxHealth;
		_lastPosition = GlobalPosition;

		DetectionRange = _stats.Range + 150f;
		if (ShipType == "Transport")
			DetectionRange = 200f;

		CreateCollision();
		CreateSprite();
		CreateDetectionZone();

		AddToGroup("ships");
		AddToGroup($"team_{TeamId}");

		if (!string.IsNullOrEmpty(NetworkId))
		{
			NetworkEntityRegistry.Register(NetworkId, this);
		}

		// NavigationAgent2D pré-instancié dans la scène (fallback runtime si manquant)
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		if (_navAgent == null)
		{
			_navAgent = new NavigationAgent2D();
			_navAgent.Name = "NavigationAgent2D";
			AddChild(_navAgent);
		}

		_navAgent.PathDesiredDistance = 15f;
		_navAgent.TargetDesiredDistance = ArrivalDistance;
		_navAgent.AvoidanceEnabled = false;
		_navAgent.NavigationLayers = 2u;
		_navAgent.Radius = 60f; // marge autour des côtes

		_currentState = ShipState.Idle;

		if (_tileMapSol == null)
		{
			FindTileMapSol();
		}
	}

	public override void _ExitTree()
	{
		if (!string.IsNullOrEmpty(NetworkId))
		{
			NetworkEntityRegistry.Unregister(NetworkId);
		}
	}

	public void ApplyNetworkState(Vector2 pos, float health)
	{
		_networkTargetPosition = pos;
		_currentHealth = health;
		QueueRedraw();
	}

	private void ChangeState(ShipState newState)
	{
		if (_currentState == newState) return;

		_currentState = newState;

		switch (newState)
		{
			case ShipState.Idle:
				_currentTarget = null;
				Velocity = Vector2.Zero;
				break;
			case ShipState.MovingToTarget:
				_navTargetDirty = true;
				break;
			case ShipState.Attacking:
				Velocity = Vector2.Zero;
				_attackTimer = 0f;
				break;
			case ShipState.MovingToPoint:
				_currentTarget = null;
				_navTargetDirty = true;
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Puppet : interpoler vers la position reseau, pas d'IA (multi seulement)
		bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
		bool isRelay = NetworkSync.Instance?.IsRelayMode() == true;
		if (isMulti && !isRelay && !IsLocalAuthority)
		{
			if (_networkTargetPosition.HasValue)
			{
				GlobalPosition = GlobalPosition.Lerp(_networkTargetPosition.Value, 10f * (float)delta);
			}
			return;
		}

		switch (_currentState)
		{
			case ShipState.Idle:
				ProcessIdleState(delta);
				break;
			case ShipState.MovingToTarget:
				ProcessMovingToTargetState(delta);
				break;
			case ShipState.Attacking:
				ProcessAttackingState(delta);
				break;
			case ShipState.MovingToPoint:
				ProcessMovingToPointState(delta);
				break;
		}
	}
}
