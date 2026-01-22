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

		public override void _Ready()
		{
			var tileMap = GetParent().GetNodeOrNull<TileMapLayer>("Sol");

			if (tileMap != null)
			{
				Rect2I usedRect = tileMap.GetUsedRect();
				int tileSize = tileMap.TileSet.TileSize.X;

				float centerX = (usedRect.Position.X + usedRect.Size.X / 2.0f) * tileSize;
				float centerY = (usedRect.Position.Y + usedRect.Size.Y / 2.0f) * tileSize;

				Position = new Vector2(centerX, centerY);
				GD.Print($"Map bounds: {usedRect}, TileSize: {tileSize}");
				GD.Print($"Caméra centrée sur: {Position}");
			}
			else
			{
				Position = Vector2.Zero;
				GD.Print("TileMapLayer 'Sol' non trouvé, position par défaut");
			}

			Zoom = new Vector2(InitialZoom, InitialZoom);
			_targetZoom = Zoom;
			SetupCameraLimits();
		}

		private void SetupCameraLimits()
		{
			LimitLeft = 0;
			LimitTop = 0;
			LimitRight = MapWidth * TileSize;
			LimitBottom = MapHeight * TileSize;
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
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Right))
			{
				Position -= mm.Relative / Zoom;
			}

			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.WheelUp)
					AdjustZoom(1.0f + ZoomSensitivity);
				else if (mb.ButtonIndex == MouseButton.WheelDown)
					AdjustZoom(1.0f - ZoomSensitivity);
			}
		}

		private void AdjustZoom(float factor)
		{
			_targetZoom = (Zoom * factor).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
		}
	}
}
