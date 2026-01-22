using Godot;

namespace SupKonQuest
{
	// Contrôleur de caméra avec zoom et déplacement
	public partial class CameraController : Camera2D
	{
		[Export] public float ZoomSensitivity = 0.15f;
		[Export] public float MinZoom = 0.05f;
		[Export] public float MaxZoom = 2.0f;
		[Export] public float ZoomSpeed = 20.0f;
		[Export] public float PanSpeed = 600.0f;
		[Export] public float InitialZoom = 0.5f;

		[ExportGroup("Map Limits")]
		[Export] public int MapWidth = 256;
		[Export] public int MapHeight = 256;
		[Export] public int TileSize = 128;

		private Vector2 _targetZoom;
		private Vector2 _mapCenter;
		private Vector2 _minBounds;
		private Vector2 _maxBounds;

		public override void _Ready()
		{
			var tileMap = GetParent().GetNodeOrNull<TileMapLayer>("Sol");

			if (tileMap != null)
			{
				Rect2I usedRect = tileMap.GetUsedRect();
				int tileSize = tileMap.TileSet.TileSize.X;

				float centerX = (usedRect.Position.X + usedRect.Size.X / 2.0f) * tileSize;
				float centerY = (usedRect.Position.Y + usedRect.Size.Y / 2.0f) * tileSize;

				_mapCenter = new Vector2(centerX, centerY);
				Position = _mapCenter;
				GD.Print($"Map bounds: {usedRect}, TileSize: {tileSize}");
				GD.Print($"Caméra centrée sur: {Position}");
			}
			else
			{
				_mapCenter = Vector2.Zero;
				Position = Vector2.Zero;
				GD.Print("TileMapLayer 'Sol' non trouvé, position par défaut");
			}

			Zoom = new Vector2(InitialZoom, InitialZoom);
			_targetZoom = Zoom;
			SetupCameraLimits();
		}

		private void SetupCameraLimits()
		{
			// La map va de -MapWidth/2 à +MapWidth/2 en tiles (centrée sur 0)
			int halfWidth = MapWidth / 2;
			int halfHeight = MapHeight / 2;

			_minBounds = new Vector2(-halfWidth * TileSize, -halfHeight * TileSize);
			_maxBounds = new Vector2(halfWidth * TileSize, halfHeight * TileSize);

			// Désactiver les limites built-in pour gérer manuellement
			LimitLeft = -10000000;
			LimitTop = -10000000;
			LimitRight = 10000000;
			LimitBottom = 10000000;
		}

		public override void _Process(double delta)
		{
			Vector2 dir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
			Position += dir * (PanSpeed / Zoom.X) * (float)delta;

			if (Zoom.DistanceTo(_targetZoom) > 0.001f)
			{
				Vector2 mousePosBefore = GetGlobalMousePosition();
				Zoom = Zoom.Lerp(_targetZoom, (float)delta * ZoomSpeed);
				Vector2 mousePosAfter = GetGlobalMousePosition();

				Position += mousePosBefore - mousePosAfter;
			}

			ClampPosition();
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Right))
			{
				Position -= mm.Relative / Zoom;
				ClampPosition();
			}

			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.WheelUp)
					AdjustZoom(1.0f + ZoomSensitivity);
				else if (mb.ButtonIndex == MouseButton.WheelDown)
					AdjustZoom(1.0f - ZoomSensitivity);
			}

			// Touche C ou Home pour recentrer la caméra
			if (@event is InputEventKey key && key.Pressed && !key.Echo)
			{
				if (key.Keycode == Key.C || key.Keycode == Key.Home)
				{
					Position = _mapCenter;
				}
			}
		}

		private void ClampPosition()
		{
			// Calculer la taille visible de la caméra
			Vector2 viewportSize = GetViewportRect().Size / Zoom;
			Vector2 halfViewport = viewportSize / 2;

			// Clamper la position pour que la caméra reste dans les limites
			float clampedX = Mathf.Clamp(Position.X, _minBounds.X + halfViewport.X, _maxBounds.X - halfViewport.X);
			float clampedY = Mathf.Clamp(Position.Y, _minBounds.Y + halfViewport.Y, _maxBounds.Y - halfViewport.Y);

			// Si la map est plus petite que le viewport, centrer
			if (_maxBounds.X - _minBounds.X < viewportSize.X)
				clampedX = _mapCenter.X;
			if (_maxBounds.Y - _minBounds.Y < viewportSize.Y)
				clampedY = _mapCenter.Y;

			Position = new Vector2(clampedX, clampedY);
		}

		private void AdjustZoom(float factor)
		{
			_targetZoom = (Zoom * factor).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
		}
	}
}
