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

		[ExportGroup("Map Limits")]
		[Export] public int MapWidth = 256;
		[Export] public int MapHeight = 256;
		[Export] public int TileSize = 128;

		private Vector2 _targetZoom;
		private Vector2 _mapCenter;
		private Vector2 _minBounds;
		private Vector2 _maxBounds;
		private bool _introPlaying = false;

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
			}
			else
			{
				_mapCenter = Vector2.Zero;
				Position = Vector2.Zero;
			}

			Zoom = new Vector2(InitialZoom, InitialZoom);
			_targetZoom = Zoom;
			SetupCameraLimits();
		}

		private void SetupCameraLimits()
		{
			// La map est centrée sur (0,0), de -MapWidth/2 à +MapWidth/2 en tiles
			int halfWidth = MapWidth / 2;
			int halfHeight = MapHeight / 2;

			_minBounds = new Vector2(-halfWidth * TileSize, -halfHeight * TileSize);
			_maxBounds = new Vector2(halfWidth * TileSize, halfHeight * TileSize);

			// Désactiver les limites built-in pour gérer le clamping manuellement
			LimitLeft = -10000000;
			LimitTop = -10000000;
			LimitRight = 10000000;
			LimitBottom = 10000000;
		}

		public override void _Process(double delta)
		{
			if (_introPlaying) return;

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

		public void SetupForMap(int mapTilesWidth, int mapTilesHeight)
		{
			MapWidth  = mapTilesWidth;
			MapHeight = mapTilesHeight;
			SetupCameraLimits();

			// Zoom minimum dynamique : aucune dimension ne doit dépasser la map
			// Max (et non Min) : on prend le facteur le plus grand pour que
			// la map remplisse toujours entièrement l'écran, même en 16:9
			Vector2 viewportSize = GetViewportRect().Size;
			float mapPixelsW = MapWidth  * TileSize;
			float mapPixelsH = MapHeight * TileSize;
			float zoomToFit  = Mathf.Max(viewportSize.X / mapPixelsW, viewportSize.Y / mapPixelsH);
			MinZoom = Mathf.Max(0.02f, zoomToFit);

			// Recaler le zoom actuel si nécessaire
			if (_targetZoom.X < MinZoom)
				_targetZoom = new Vector2(MinZoom, MinZoom);
		}

		public void StartIntroZoom(Vector2 basePos, int mapTilesWidth)
		{
			// Zoom de départ : adapter à la taille de la map pour voir toute la map
			Vector2 viewportSize = GetViewportRect().Size;
			float mapPixels = mapTilesWidth * TileSize;
			float startZoom = Mathf.Max(MinZoom, Mathf.Min(viewportSize.X / mapPixels, viewportSize.Y / mapPixels));

			float endZoom  = mapTilesWidth <= 128 ? 0.5f  : mapTilesWidth <= 256 ? 0.35f : 0.22f;
			float duration = mapTilesWidth <= 128 ? 3.0f  : mapTilesWidth <= 256 ? 4.0f  : 5.0f;

			// Départ : centre de la map, dézoomé au max
			Position = _mapCenter;
			Zoom = new Vector2(startZoom, startZoom);
			_targetZoom = new Vector2(endZoom, endZoom);
			_introPlaying = true;

			// Animer position ET zoom en parallèle vers la base
			var tween = CreateTween().SetParallel(true);
			tween.TweenProperty(this, "position", basePos, duration)
				.SetEase(Tween.EaseType.InOut)
				.SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(this, "zoom", new Vector2(endZoom, endZoom), duration)
				.SetEase(Tween.EaseType.InOut)
				.SetTrans(Tween.TransitionType.Cubic);
			tween.Chain().TweenCallback(Callable.From(() =>
			{
				_targetZoom = Zoom;
				_introPlaying = false;
			}));
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			if (_introPlaying) return;

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

			// Touche C ou Home pour recentrer
			if (@event is InputEventKey key && key.Pressed && !key.Echo)
			{
				if (key.Keycode == Key.C || key.Keycode == Key.Home)
					Position = _mapCenter;
			}
		}

		private void ClampPosition()
		{
			Vector2 viewportSize = GetViewportRect().Size / Zoom;
			Vector2 halfViewport = viewportSize / 2;

			// Si viewport > map sur un axe : centrer, sinon clamper normalement
			float clampedX = (_maxBounds.X - _minBounds.X >= viewportSize.X)
				? Mathf.Clamp(Position.X, _minBounds.X + halfViewport.X, _maxBounds.X - halfViewport.X)
				: _mapCenter.X;
			float clampedY = (_maxBounds.Y - _minBounds.Y >= viewportSize.Y)
				? Mathf.Clamp(Position.Y, _minBounds.Y + halfViewport.Y, _maxBounds.Y - halfViewport.Y)
				: _mapCenter.Y;

			Position = new Vector2(clampedX, clampedY);
		}

		private void AdjustZoom(float factor)
		{
			_targetZoom = (Zoom * factor).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
		}
	}
}
