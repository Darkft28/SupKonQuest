using Godot;

namespace SupKonQuest
{
	public partial class CameraController : Camera2D
	{
		[Export] public float ZoomSensitivity = 0.15f;
		[Export] public float MinZoom = 0.05f;
		[Export] public float MaxZoom = 2.0f;
		[Export] public float ZoomSpeed = 20.0f;
		[Export] public float PanSpeed = 600.0f;
		[Export] public float InitialZoom = 0.5f;

		private Vector2 _targetZoom;

		public override void _Ready()
		{
			// Récupérer le TileMapLayer pour calculer le centre réel de la map
			var tileMap = GetParent().GetNodeOrNull<TileMapLayer>("Sol");

			if (tileMap != null)
			{
				// Obtenir les bounds réels de la map générée
				Rect2I usedRect = tileMap.GetUsedRect();
				int tileSize = tileMap.TileSet.TileSize.X;

				// Calculer le centre en coordonnées monde
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
		}

		public override void _Process(double delta)
		{
			// Déplacement clavier
			Vector2 dir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
			Position += dir * (PanSpeed / Zoom.X) * (float)delta;

			// Interpolation du zoom uniquement si l'écart est significatif
			if (Zoom.DistanceTo(_targetZoom) > 0.001f)
			{
				Vector2 mousePosBefore = GetGlobalMousePosition();
				Zoom = Zoom.Lerp(_targetZoom, (float)delta * ZoomSpeed);
				Vector2 mousePosAfter = GetGlobalMousePosition();
				
				// On compense le mouvement pendant l'interpolation
				Position += mousePosBefore - mousePosAfter;
			}
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			// Drag avec clic droit
			if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Right))
			{
				Position -= mm.Relative / Zoom;
			}

			// Molette souris
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
