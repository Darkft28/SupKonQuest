using Godot;
using System;
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

	// Transport : debarquement en attente (le bateau navigue d'abord, puis debarque)
	private Vector2? _pendingUnloadPosition = null;

	// Transport : unites embarquees (type, equipe, sante)
	private List<(string type, int teamId, float health)> _loadedUnits = new List<(string, int, float)>();

	// Direction du sprite
	private enum SpriteDirection { Front, Back, Left, Right }
	private SpriteDirection _currentDirection = SpriteDirection.Front;

	// Barre de vie
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

		_currentState = ShipState.Idle;

		// Chercher le TileMapSol dans la scene si pas deja set
		if (_tileMapSol == null)
		{
			FindTileMapSol();
		}
	}

	private void FindTileMapSol()
	{
		var currentScene = GetTree().CurrentScene;
		if (currentScene == null) return;

		var mapGenerator = currentScene.FindChild("MapGenerator", true, false);
		if (mapGenerator != null)
		{
			_tileMapSol = mapGenerator.GetNodeOrNull<TileMapLayer>("Sol");
		}
	}

	private void CreateCollision()
	{
		var collision = new CollisionShape2D();
		var shape = new CircleShape2D();
		shape.Radius = 60f;
		collision.Shape = shape;
		AddChild(collision);

		// Bateaux sur leur propre layer pour eviter collisions avec unites terrestres
		SetCollisionLayerValue(1, false);
		SetCollisionLayerValue(2, true);
		SetCollisionMaskValue(1, false);
		SetCollisionMaskValue(2, true);
	}

	private void CreateSprite()
	{
		_sprite = new Sprite2D();

		string texturePath = GetTexturePath(SpriteDirection.Front);
		var texture = GD.Load<Texture2D>(texturePath);

		if (texture != null)
		{
			_sprite.Texture = texture;
			_sprite.Scale = new Vector2(0.525f, 0.525f);
			AddChild(_sprite);
		}
		else
		{
			GD.PrintErr($"Impossible de charger la texture bateau: {texturePath}");
		}
	}

	private string GetTexturePath(SpriteDirection direction)
	{
		string dirSuffix = direction switch
		{
			SpriteDirection.Front => "Front",
			SpriteDirection.Back => "Back",
			SpriteDirection.Left => "Left",
			SpriteDirection.Right => "Right",
			_ => "Front"
		};

		return ShipType switch
		{
			"Destroyer" => $"res://Assets/Units/Ships/Destroyer/Destroyers_{dirSuffix}.png",
			"Fregate" => $"res://Assets/Units/Ships/Frégate/frégate_{dirSuffix}.png",
			"Transport" => $"res://Assets/Units/Ships/Transport/Transport_{dirSuffix}.png",
			_ => $"res://Assets/Units/Ships/Transport/Transport_{dirSuffix}.png"
		};
	}

	private void UpdateSpriteDirection(Vector2 velocity)
	{
		if (velocity.LengthSquared() < 1f) return;

		SpriteDirection newDir;
		if (Mathf.Abs(velocity.X) > Mathf.Abs(velocity.Y))
		{
			newDir = velocity.X > 0 ? SpriteDirection.Right : SpriteDirection.Left;
		}
		else
		{
			newDir = velocity.Y > 0 ? SpriteDirection.Front : SpriteDirection.Back;
		}

		if (newDir != _currentDirection)
		{
			_currentDirection = newDir;
			string texturePath = GetTexturePath(newDir);
			var texture = GD.Load<Texture2D>(texturePath);
			if (texture != null && _sprite != null)
			{
				_sprite.Texture = texture;
			}
		}
	}

	private void CreateDetectionZone()
	{
		_detectionZone = new Area2D();
		_detectionZone.Name = "DetectionZone";

		var collisionShape = new CollisionShape2D();
		var circleShape = new CircleShape2D();
		circleShape.Radius = DetectionRange;
		collisionShape.Shape = circleShape;

		_detectionZone.AddChild(collisionShape);
		AddChild(_detectionZone);

		_detectionZone.BodyEntered += OnBodyEnteredDetectionZone;
	}

	private void OnBodyEnteredDetectionZone(Node2D body)
	{
		// Transport ne combat pas
		if (ShipType == "Transport") return;

		if (body is Ship otherShip)
		{
			if (otherShip.GetTeamId() != TeamId && otherShip.GetCurrentHealth() > 0)
			{
				if (_currentTarget == null && _currentState == ShipState.Idle)
				{
					SetNewTarget(otherShip);
				}
			}
		}
	}

	private void SetNewTarget(Ship target)
	{
		_currentTarget = target;
		float distanceToTarget = GlobalPosition.DistanceTo(target.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(ShipState.Attacking);
		}
		else
		{
			ChangeState(ShipState.MovingToTarget);
		}
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
				break;
			case ShipState.Attacking:
				Velocity = Vector2.Zero;
				_attackTimer = 0f;
				break;
			case ShipState.MovingToPoint:
				_currentTarget = null;
				break;
		}
	}

	public override void _Draw()
	{
		// Indicateur du nombre d'unites embarquees pour Transport
		if (ShipType == "Transport" && _loadedUnits.Count > 0)
		{
			var font = ThemeDB.FallbackFont;
			DrawString(font, new Vector2(-10, -215), $"{_loadedUnits.Count}/{_stats.Capacity}",
				HorizontalAlignment.Center, -1, 16, Colors.White);
		}

		// Barre de vie
		float healthPercent = _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
		if (healthPercent >= 1f) return;

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

	private void ProcessIdleState(double delta)
	{
		if (ShipType == "Transport") return;

		if (_currentTarget == null)
		{
			Ship enemy = FindEnemyShipInRange();
			if (enemy != null)
			{
				SetNewTarget(enemy);
			}
		}
	}

	private void ProcessMovingToTargetState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(ShipState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(ShipState.Attacking);
			return;
		}

		Vector2 direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
		Vector2 desiredVelocity = direction * _stats.Speed;

		// Verifier que la destination est sur l'eau
		Vector2 nextPos = GlobalPosition + desiredVelocity * (float)delta;
		if (IsWaterTile(nextPos))
		{
			Velocity = desiredVelocity;
			UpdateSpriteDirection(desiredVelocity);
			MoveAndSlide();
		}
		else
		{
			// Essayer de contourner l'obstacle
			Vector2 slideVelocity = FindAlternativeWaterDirection(direction, (float)delta);
			if (slideVelocity != Vector2.Zero)
			{
				Velocity = slideVelocity;
				UpdateSpriteDirection(slideVelocity);
				MoveAndSlide();
			}
			else
			{
				Velocity = Vector2.Zero;
			}
		}

		ProcessStuckDetection();
	}

	private void ProcessAttackingState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(ShipState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);
		float rangeWithMargin = _stats.Range + 20f;

		if (distanceToTarget > rangeWithMargin)
		{
			ChangeState(ShipState.MovingToTarget);
			return;
		}

		_attackTimer += (float)delta;

		if (_attackTimer >= AttackInterval)
		{
			_attackTimer = 0f;
			AttackTarget(_currentTarget);
		}
	}

	private void ProcessMovingToPointState(double delta)
	{
		if (!_targetPosition.HasValue)
		{
			ChangeState(ShipState.Idle);
			return;
		}

		// Chercher des ennemis en route (sauf Transport)
		if (ShipType != "Transport")
		{
			Ship enemy = FindEnemyShipInRange();
			if (enemy != null)
			{
				_currentTarget = enemy;
				float distToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distToEnemy <= _stats.Range)
					ChangeState(ShipState.Attacking);
				else
					ChangeState(ShipState.MovingToTarget);
				return;
			}
		}

		Vector2 direction = (_targetPosition.Value - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		if (distance < ArrivalDistance)
		{
			_targetPosition = null;

			// Debarquement en attente : decharger les troupes a l'arrivee
			if (_pendingUnloadPosition.HasValue)
			{
				UnloadUnits(_pendingUnloadPosition.Value);
				_pendingUnloadPosition = null;
			}

			ChangeState(ShipState.Idle);
			return;
		}

		Vector2 desiredVelocity = direction * _stats.Speed;

		// Verifier que la destination est sur l'eau
		Vector2 nextPos = GlobalPosition + desiredVelocity * (float)delta;
		if (IsWaterTile(nextPos))
		{
			Velocity = desiredVelocity;
			UpdateSpriteDirection(desiredVelocity);
			MoveAndSlide();
		}
		else
		{
			// Essayer de contourner l'obstacle au lieu de s'arreter
			Vector2 slideVelocity = FindAlternativeWaterDirection(direction, (float)delta);
			if (slideVelocity != Vector2.Zero)
			{
				Velocity = slideVelocity;
				UpdateSpriteDirection(slideVelocity);
				MoveAndSlide();
			}
			else
			{
				// Bloque par la terre - si debarquement en attente, decharger ici
				if (_pendingUnloadPosition.HasValue)
				{
					UnloadUnits(_pendingUnloadPosition.Value);
					_pendingUnloadPosition = null;
				}

				_targetPosition = null;
				ChangeState(ShipState.Idle);
				GD.Print($"[SHIP] {ShipType} T{TeamId} bloque par la terre, arret");
				return;
			}
		}

		ProcessStuckDetection();
	}

	private void ProcessStuckDetection()
	{
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		float expectedMovement = _stats.Speed / 60f;
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement * 0.1f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloque - si debarquement en attente, decharger quand meme
				if (_pendingUnloadPosition.HasValue)
				{
					UnloadUnits(_pendingUnloadPosition.Value);
					_pendingUnloadPosition = null;
				}

				_targetPosition = null;
				ChangeState(ShipState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
	}

	private Vector2 FindAlternativeWaterDirection(Vector2 desiredDirection, float delta)
	{
		// Essayer des angles alternatifs pour longer la cote
		float[] angles = { 30f, -30f, 60f, -60f, 90f, -90f };

		foreach (float angleDeg in angles)
		{
			float angleRad = Mathf.DegToRad(angleDeg);
			Vector2 rotated = desiredDirection.Rotated(angleRad);
			Vector2 altVelocity = rotated * _stats.Speed;
			Vector2 altNextPos = GlobalPosition + altVelocity * delta;

			if (IsWaterTile(altNextPos))
			{
				return altVelocity;
			}
		}

		return Vector2.Zero;
	}

	public bool IsWaterTile(Vector2 globalPos)
	{
		if (_tileMapSol == null) return false;

		Vector2I tileCoords = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(globalPos));
		int tileId = _tileMapSol.GetCellSourceId(tileCoords);
		return tileId == 6; // IdEau
	}

	private bool IsTargetValid()
	{
		if (_currentTarget == null) return false;
		if (!IsInstanceValid(_currentTarget)) return false;
		if (!_currentTarget.IsInsideTree()) return false;
		if (_currentTarget.GetCurrentHealth() <= 0) return false;
		return true;
	}

	private Ship FindEnemyShipInRange()
	{
		var allShips = GetTree().GetNodesInGroup("ships");

		Ship closestEnemy = null;
		float closestDistance = float.MaxValue;

		foreach (var node in allShips)
		{
			if (node is Ship otherShip)
			{
				if (otherShip.GetTeamId() == TeamId) continue;
				if (otherShip.GetCurrentHealth() <= 0) continue;

				float distance = GlobalPosition.DistanceTo(otherShip.GlobalPosition);
				if (distance <= DetectionRange && distance < closestDistance)
				{
					closestEnemy = otherShip;
					closestDistance = distance;
				}
			}
		}

		return closestEnemy;
	}

	private void AttackTarget(Ship target)
	{
		if (target == null || !IsInstanceValid(target) || !target.IsInsideTree()) return;
		if (target.GetCurrentHealth() <= 0) return;

		// Lancer un projectile (cannonball)
		SpawnProjectile(target);

		float totalDef = target._stats.Defense;
		float actualDamage = _stats.Attack * 100f / (100f + totalDef);
		GD.Print($"[SHIP ATK] {ShipType} T{TeamId} -> {target.GetShipType()} T{target.GetTeamId()} | {_stats.Attack} brut -> {actualDamage:F1} reel (def {totalDef}) | HP {target.GetCurrentHealth():F0}/{target.GetMaxHealth():F0} (projectile)");
	}

	private void SpawnProjectile(Ship target)
	{
		var projectile = new ShipProjectile();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.Initialize(GlobalPosition, target, _stats.Attack, TeamId, 300f);
	}

	public void TakeDamage(float damage)
	{
		TakeDamageFrom(damage, 0);
	}

	public void TakeDamageFrom(float damage, int attackerTeamId)
	{
		float totalDefense = _stats.Defense;
		float actualDamage = damage * 100f / (100f + totalDefense);
		_currentHealth -= actualDamage;
		_lastAttackerTeamId = attackerTeamId;
		QueueRedraw();

		if (_currentHealth <= 0)
		{
			Die();
		}
	}

	private void Die()
	{
		GD.Print($"[SHIP MORT] {ShipType} T{TeamId} coule (tue par T{_lastAttackerTeamId})");

		// Si c'est un Transport avec des unites, elles coulent aussi
		if (_loadedUnits.Count > 0)
		{
			GD.Print($"[SHIP] {_loadedUnits.Count} unites perdues avec le Transport!");
			_loadedUnits.Clear();
		}

		QueueFree();
	}

	public void MoveTo(Vector2 target)
	{
		// Verifier que la destination est sur l'eau
		if (!IsWaterTile(target))
		{
			GD.Print($"[SHIP] Destination refusee: pas sur l'eau");
			return;
		}

		_pendingUnloadPosition = null; // Nouvel ordre annule le debarquement
		_targetPosition = target;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		_pendingUnloadPosition = null;
		Velocity = Vector2.Zero;
		ChangeState(ShipState.Idle);
	}

	// Transport : embarquer une unite
	public bool BoardUnit(Unit unit)
	{
		if (ShipType != "Transport") return false;
		if (_loadedUnits.Count >= _stats.Capacity) return false;
		if (unit == null || !IsInstanceValid(unit)) return false;

		_loadedUnits.Add((unit.GetUnitType(), unit.GetTeamId(), unit.GetCurrentHealth()));
		GD.Print($"[TRANSPORT] {unit.GetUnitType()} T{unit.GetTeamId()} embarque ({_loadedUnits.Count}/{_stats.Capacity}) HP:{unit.GetCurrentHealth():F0}");
		unit.QueueFree();
		QueueRedraw();
		return true;
	}

	// Distance max de debarquement depuis la position actuelle du transport
	private const float MaxUnloadDistance = 2000f;
	// Nombre de tuiles max entre le point de debarquement et l'eau
	private const int MaxCoastTileDistance = 3;

	// Verifie si la position de debarquement est valide (cote + distance)
	public bool IsValidUnloadPosition(Vector2 landPosition)
	{
		// Verifier la distance max depuis le transport
		float distance = GlobalPosition.DistanceTo(landPosition);
		if (distance > MaxUnloadDistance)
			return false;

		// Verifier que la position est pres de la cote (eau a moins de N tuiles)
		if (_tileMapSol == null)
			return false;

		Vector2I landTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(landPosition));

		for (int dx = -MaxCoastTileDistance; dx <= MaxCoastTileDistance; dx++)
		{
			for (int dy = -MaxCoastTileDistance; dy <= MaxCoastTileDistance; dy++)
			{
				Vector2I checkTile = landTile + new Vector2I(dx, dy);
				if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					return true;
			}
		}

		return false;
	}

	// Transport : naviguer vers la cote puis debarquer
	public void MoveToUnload(Vector2 landPosition)
	{
		if (ShipType != "Transport" || _loadedUnits.Count == 0) return;

		// Trouver la tuile d'eau la plus proche de la destination terrestre
		Vector2 waterPos = FindNearestWaterTile(landPosition);

		_pendingUnloadPosition = landPosition;
		_targetPosition = waterPos;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
		GD.Print($"[TRANSPORT] Se deplace vers la cote pour debarquer");
	}

	private Vector2 FindNearestWaterTile(Vector2 landPos)
	{
		if (_tileMapSol == null) return GlobalPosition;

		Vector2I landTile = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(landPos));

		for (int radius = 1; radius <= 10; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

					Vector2I checkTile = landTile + new Vector2I(dx, dy);
					if (_tileMapSol.GetCellSourceId(checkTile) == 6)
					{
						return _tileMapSol.ToGlobal(_tileMapSol.MapToLocal(checkTile));
					}
				}
			}
		}

		return GlobalPosition;
	}

	// Transport : debarquer toutes les unites
	public void UnloadUnits(Vector2 landPosition)
	{
		if (ShipType != "Transport" || _loadedUnits.Count == 0) return;

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		for (int i = 0; i < _loadedUnits.Count; i++)
		{
			var (type, teamId, health) = _loadedUnits[i];
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / _loadedUnits.Count;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 100f;

			unit.GlobalPosition = landPosition + offset;
			unit.UnitType = type;
			unit.TeamId = teamId;
			unit.IsNeutralCampUnit = false;

			GetTree().CurrentScene.AddChild(unit);

			// Restaurer la sante d'embarquement
			unit.SetCurrentHealth(health);
		}

		GD.Print($"[TRANSPORT] {_loadedUnits.Count} unites debarquees a {landPosition}");
		_loadedUnits.Clear();
		QueueRedraw();
	}
}

// Projectile specifique aux bateaux (cible un Ship au lieu d'un Unit)
public partial class ShipProjectile : Node2D
{
	private Vector2 _startPos;
	private Vector2 _targetPos;
	private float _speed;
	private float _progress = 0f;
	private float _arcHeight;
	private Sprite2D _sprite;

	private Ship _targetShip;
	private float _damage;
	private int _attackerTeamId;

	public void Initialize(Vector2 start, Ship target, float damage, int attackerTeamId, float speed)
	{
		_startPos = start;
		_targetShip = target;
		_targetPos = target.GlobalPosition;
		_damage = damage;
		_attackerTeamId = attackerTeamId;
		_speed = speed;
		GlobalPosition = start;

		float distance = start.DistanceTo(_targetPos);
		_arcHeight = distance * 0.3f;

		CreateSprite();
	}

	private void CreateSprite()
	{
		_sprite = new Sprite2D();
		string texturePath = "res://Assets/Units/Characters/Mortar/Mortar_Ammo.png";
		_sprite.Scale = new Vector2(0.2f, 0.2f);

		var texture = GD.Load<Texture2D>(texturePath);
		if (texture != null)
		{
			_sprite.Texture = texture;
		}
		AddChild(_sprite);
	}

	public override void _Process(double delta)
	{
		if (_progress >= 1f) return;

		if (_targetShip != null && IsInstanceValid(_targetShip) && _targetShip.IsInsideTree())
		{
			_targetPos = _targetShip.GlobalPosition;
		}

		float distance = _startPos.DistanceTo(_targetPos);
		if (distance < 1f)
		{
			ApplyDamageAndDestroy();
			return;
		}

		_progress += (float)delta * _speed / distance;

		if (_progress >= 1f)
		{
			_progress = 1f;
			ApplyDamageAndDestroy();
			return;
		}

		Vector2 linearPos = _startPos.Lerp(_targetPos, _progress);
		float arc = -4f * _arcHeight * _progress * (_progress - 1f);
		linearPos.Y -= arc;

		GlobalPosition = linearPos;
	}

	private void ApplyDamageAndDestroy()
	{
		if (_targetShip != null && IsInstanceValid(_targetShip) && _targetShip.IsInsideTree()
			&& _targetShip.GetCurrentHealth() > 0)
		{
			_targetShip.TakeDamageFrom(_damage, _attackerTeamId);
		}
		QueueFree();
	}
}
