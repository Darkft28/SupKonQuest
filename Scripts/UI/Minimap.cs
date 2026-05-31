using Godot;
using System;

public partial class Minimap : Control
{
	[Export] public int MapWidth = 256;
	[Export] public int MapHeight = 256;
	[Export] public int TileSize = 128;
	[Export] public Color ViewRectColor = new Color(1, 0, 0, 1);
	[Export] public float ViewRectBorderWidth = 2f;

	private Camera2D _mainCamera;
	private Vector2 _minimapZoom;
	private Vector2 _mapPixelSize;
	private ImageTexture _baseTexture;
	private float _overlayTimer;
	private const float OverlayInterval = 0.1f;

	private static readonly Color[] TeamColors =
	{
		new Color(0.2f, 0.5f, 1f),
		new Color(1f, 0.3f, 0.3f),
		new Color(0.3f, 1f, 0.4f),
		new Color(1f, 0.85f, 0.2f),
		new Color(0.85f, 0.3f, 1f),
		new Color(1f, 0.5f, 0.2f),
		new Color(0.3f, 0.9f, 0.9f),
		new Color(0.9f, 0.4f, 0.7f),
	};

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState != null)
		{
			MapWidth = MapHeight = gameState.MapSize switch
			{
				GameState.MapSizePreset.Small => 128,
				GameState.MapSizePreset.Large => 384,
				_ => 256
			};
		}

		if (MapGenerator.MinimapBaseTexture != null)
			ApplyBaseTexture(MapGenerator.MinimapBaseTexture);
		else
			MapGenerator.MinimapTextureReady += OnMinimapTextureReady;

		_mapPixelSize = new Vector2(MapWidth * TileSize, MapHeight * TileSize);
		RecalculateZoom();
		CallDeferred(nameof(FindMainCamera));
	}

	public override void _ExitTree()
	{
		MapGenerator.MinimapTextureReady -= OnMinimapTextureReady;
	}

	private void OnMinimapTextureReady()
	{
		if (MapGenerator.MinimapBaseTexture != null)
			ApplyBaseTexture(MapGenerator.MinimapBaseTexture);
	}

	private void ApplyBaseTexture(ImageTexture texture)
	{
		_baseTexture = texture;
		MapWidth = MapGenerator.MinimapMapWidth;
		MapHeight = MapGenerator.MinimapMapHeight;
		TileSize = MapGenerator.MinimapTileSize;
		_mapPixelSize = new Vector2(MapWidth * TileSize, MapHeight * TileSize);
		RecalculateZoom();
		QueueRedraw();
	}

	private void RecalculateZoom()
	{
		Vector2 viewportSize = Size;
		if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
			viewportSize = new Vector2(129, 128);

		_minimapZoom = new Vector2(
			viewportSize.X / _mapPixelSize.X,
			viewportSize.Y / _mapPixelSize.Y
		);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			RecalculateZoom();
	}

	public override void _Process(double delta)
	{
		_overlayTimer += (float)delta;
		if (_overlayTimer >= OverlayInterval)
		{
			_overlayTimer = 0f;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Vector2 minimapSize = Size;
		if (minimapSize.X <= 0f || minimapSize.Y <= 0f)
			return;

		if (_baseTexture != null)
			DrawTextureRect(_baseTexture, new Rect2(Vector2.Zero, minimapSize), false);

		DrawCampOverlay(minimapSize);
		DrawCameraRect(minimapSize);
	}

	private void DrawCampOverlay(Vector2 minimapSize)
	{
		Vector2 minimapCenter = minimapSize / 2;

		foreach (var node in GetTree().GetNodesInGroup("camps"))
		{
			if (node is not CampSimple camp || !IsInstanceValid(camp))
				continue;

			int teamId = camp.GetTeamId();
			if (teamId <= 0)
				continue;

			Vector2 p = WorldToMinimap(camp.GlobalPosition, minimapCenter);
			Color c = GetTeamColor(teamId);
			DrawRect(new Rect2(p - new Vector2(3, 3), new Vector2(6, 6)), c);
		}
	}

	private void DrawCameraRect(Vector2 minimapSize)
	{
		if (_mainCamera == null)
			return;

		Vector2 cameraPos = _mainCamera.Position;
		Vector2 cameraZoom = _mainCamera.Zoom;
		Vector2 mainViewportSize = _mainCamera.GetViewportRect().Size;
		Vector2 visibleWorldSize = mainViewportSize / cameraZoom;
		Vector2 minimapCenter = minimapSize / 2;

		Vector2 rectCenterInMinimap = minimapCenter + new Vector2(
			cameraPos.X * _minimapZoom.X,
			cameraPos.Y * _minimapZoom.Y
		);
		Vector2 rectSizeInMinimap = new Vector2(
			visibleWorldSize.X * _minimapZoom.X,
			visibleWorldSize.Y * _minimapZoom.Y
		);

		Vector2 rectTopLeft = rectCenterInMinimap - rectSizeInMinimap / 2;
		Rect2 viewRect = new Rect2(rectTopLeft, rectSizeInMinimap);
		viewRect = viewRect.Intersection(new Rect2(Vector2.Zero, minimapSize));
		DrawRect(viewRect, ViewRectColor, false, ViewRectBorderWidth);
	}

	private Vector2 WorldToMinimap(Vector2 worldPos, Vector2 minimapCenter)
	{
		return minimapCenter + new Vector2(
			worldPos.X * _minimapZoom.X,
			worldPos.Y * _minimapZoom.Y
		);
	}

	private static Color GetTeamColor(int teamId)
	{
		if (teamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f);
		return TeamColors[(teamId - 1) % TeamColors.Length];
	}

	private void FindMainCamera()
	{
		var currentScene = GetTree().CurrentScene;
		_mainCamera = currentScene?.GetNodeOrNull<Camera2D>("Camera2D");
		if (_mainCamera == null)
			GD.PrintErr("Minimap: Camera2D non trouvée!");
	}

	private bool _isDragging;

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			_isDragging = mb.Pressed;
			if (mb.Pressed)
				HandleMinimapClick(mb.Position);
			GetViewport().SetInputAsHandled();
		}

		if (@event is InputEventMouseMotion mm && _isDragging)
		{
			HandleMinimapClick(mm.Position);
			GetViewport().SetInputAsHandled();
		}
	}

	private void HandleMinimapClick(Vector2 clickPos)
	{
		if (_mainCamera == null)
			return;

		Vector2 minimapSize = Size;
		Vector2 minimapCenter = minimapSize / 2;
		Vector2 relativePos = clickPos - minimapCenter;

		Vector2 worldPos = new Vector2(
			relativePos.X / _minimapZoom.X,
			relativePos.Y / _minimapZoom.Y
		);

		_mainCamera.Position = worldPos;
	}
}
