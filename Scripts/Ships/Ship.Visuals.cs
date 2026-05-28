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
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision == null)
		{
			collision = new CollisionShape2D();
			collision.Name = "CollisionShape2D";
			AddChild(collision);
		}

		var shape = collision.Shape as CircleShape2D ?? new CircleShape2D();
		shape.Radius = 60f;
		collision.Shape = shape;

		// Bateaux sur leur propre layer pour eviter collisions avec unites terrestres
		SetCollisionLayerValue(1, false);
		SetCollisionLayerValue(2, true);
		SetCollisionMaskValue(1, false);
		SetCollisionMaskValue(2, true);
	}

	private void CreateSprite()
	{
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite == null)
		{
			_sprite = new Sprite2D();
			_sprite.Name = "Sprite2D";
			AddChild(_sprite);
		}

		string texturePath = GetTexturePath(SpriteDirection.Front);
		var texture = LoadShipTexture(texturePath);

		if (texture != null)
		{
			_sprite.Texture = texture;
			_sprite.Scale = new Vector2(0.525f, 0.525f);
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

	private static Texture2D LoadShipTexture(string path)
	{
		var texture = GD.Load<Texture2D>(path);
		if (texture != null)
			return texture;

		// Secours si le chemin accentué échoue (export / FS) — noms ASCII alternatifs.
		if (path.Contains("Frégate"))
		{
			string ascii = path
				.Replace("Frégate", "Fregate")
				.Replace("frégate_", "Fregate_");
			texture = GD.Load<Texture2D>(ascii);
		}

		return texture;
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
			var texture = LoadShipTexture(texturePath);
			if (texture != null && _sprite != null)
			{
				_sprite.Texture = texture;
			}
		}
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
