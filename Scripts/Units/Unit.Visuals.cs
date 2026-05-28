using Godot;

public partial class Unit
{
	private Texture2D _texFront;
	private Texture2D _texBack;
	private Texture2D _texLeft;
	private Texture2D _texRight;
	private bool _flipForLeft; // true si pas de texture Left (ex: AntiArmor)

	private void CreateSprite()
	{
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite == null)
		{
			_sprite = new Sprite2D();
			_sprite.Name = "Sprite2D";
			AddChild(_sprite);
		}

		_texFront = LoadUnitTexture("Front");
		_texBack  = LoadUnitTexture("Back");
		_texRight = LoadUnitTexture("Right");
		_texLeft  = LoadUnitTexture("Left");

		// AntiArmor n'a pas de texture Left : utiliser Right retournée
		if (_texLeft == null)
		{
			_texLeft = _texRight;
			_flipForLeft = true;
		}

		if (_texFront != null)
		{
			_sprite.Texture = _texFront;
			_sprite.Scale = new Vector2(0.255f, 0.255f);
		}
		else
		{
			GD.PrintErr($"[Unit] Texture Front introuvable pour: {UnitType}");
		}
	}

	private Texture2D LoadUnitTexture(string direction)
	{
		string path = UnitType switch
		{
			"Heal"      => $"res://Assets/Units/Characters/Healer/healer_{direction}.png",
			"AntiArmor" => direction == "Left"
				? null
				: $"res://Assets/Units/Characters/Anti-armor/Anti-armor_{direction.ToLower()}.png",
			_ => $"res://Assets/Units/Characters/{UnitType}/{UnitType}_{direction}.png"
		};

		if (path == null) return null;
		return GD.Load<Texture2D>(path);
	}

	public void UpdateSpriteDirection(Vector2 velocity)
	{
		if (_sprite == null || velocity == Vector2.Zero) return;

		float ax = Mathf.Abs(velocity.X);
		float ay = Mathf.Abs(velocity.Y);

		Texture2D tex;
		bool flip = false;

		if (ay >= ax)
		{
			// Mouvement vertical dominant
			tex = velocity.Y > 0 ? _texFront : _texBack;
		}
		else
		{
			// Mouvement horizontal dominant
			if (velocity.X > 0)
			{
				tex = _texRight;
			}
			else
			{
				tex = _texLeft;
				flip = _flipForLeft;
			}
		}

		if (tex != null && _sprite.Texture != tex)
			_sprite.Texture = tex;

		_sprite.FlipH = flip;
	}

	private void CreateCollision()
	{
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision == null)
		{
			collision = new CollisionShape2D();
			collision.Name = "CollisionShape2D";
			AddChild(collision);
		}

		var shape = collision.Shape as CapsuleShape2D ?? new CapsuleShape2D();
		shape.Radius = 34f;
		shape.Height = 50f;
		collision.Shape = shape;

		SetCollisionLayerValue(1, true);
		SetCollisionLayerValue(2, false);
		SetCollisionLayerValue(3, false);
		SetCollisionMaskValue(1, true);
		SetCollisionMaskValue(2, false);
		SetCollisionMaskValue(3, true);
	}

	private void CreateDetectionZone()
	{
		_detectionZone = GetNodeOrNull<Area2D>("DetectionZone");
		if (_detectionZone == null)
		{
			_detectionZone = new Area2D();
			_detectionZone.Name = "DetectionZone";
			AddChild(_detectionZone);
		}

		var collisionShape = _detectionZone.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collisionShape == null)
		{
			collisionShape = new CollisionShape2D();
			collisionShape.Name = "CollisionShape2D";
			_detectionZone.AddChild(collisionShape);
		}

		var circleShape = collisionShape.Shape as CircleShape2D ?? new CircleShape2D();
		circleShape.Radius = DetectionRange;
		collisionShape.Shape = circleShape;

		// Layer 0 = invisible ; Mask 1 = détecte les unités terrestres (layer 1)
		_detectionZone.CollisionLayer = 0u;
		_detectionZone.CollisionMask = 1u;

		_detectionZone.BodyEntered += OnBodyEnteredDetectionZone;
	}

	public override void _Draw()
	{
		if (UnitType == "Support")
		{
			DrawCircle(Vector2.Zero, SupportAuraRadius, AuraColor);
			DrawArc(Vector2.Zero, SupportAuraRadius, 0, Mathf.Tau, 64, AuraBorderColor, 2f);
		}

		if (UnitType == "Heal" && _currentState == UnitState.Healing
			&& _healTarget != null && IsInstanceValid(_healTarget) && _healTarget.IsInsideTree())
		{
			Vector2 targetLocal = _healTarget.GlobalPosition - GlobalPosition;
			Color healRayColor = new Color(0.2f, 0.9f, 0.3f, 0.6f);
			Color healRayGlow = new Color(0.2f, 0.9f, 0.3f, 0.15f);
			DrawLine(Vector2.Zero, targetLocal, healRayGlow, 8f);
			DrawLine(Vector2.Zero, targetLocal, healRayColor, 3f);
			DrawCircle(targetLocal, 6f, healRayColor);
		}

		// Barre de vie (seulement si blessé)
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
}
