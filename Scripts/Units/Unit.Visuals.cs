using Godot;

public partial class Unit
{
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
}
