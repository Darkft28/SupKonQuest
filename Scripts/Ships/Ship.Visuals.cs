using Godot;

public partial class Ship
{
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

	public override void _Draw()
	{
		if (ShipType == "Transport" && _loadedUnits.Count > 0)
		{
			var font = ThemeDB.FallbackFont;
			DrawString(font, new Vector2(-10, -215), $"{_loadedUnits.Count}/{_stats.Capacity}",
				HorizontalAlignment.Center, -1, 16, Colors.White);
		}

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
}
