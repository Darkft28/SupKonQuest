using Godot;

public partial class Minimap : SubViewportContainer
{
	[Export] public int MapWidth = 256;
	[Export] public int MapHeight = 256;
	[Export] public int TileSize = 128;
	[Export] public Color ViewRectColor = new Color(1, 0, 0, 1);
	[Export] public float ViewRectBorderWidth = 2f;

	private SubViewport _viewport;
	private Camera2D _minimapCamera;
	private Camera2D _mainCamera;
	private Vector2 _minimapZoom;
	private Vector2 _mapPixelSize;

	public override void _Ready()
	{
		_viewport = GetNode<SubViewport>("SubViewport");
		_minimapCamera = _viewport.GetNode<Camera2D>("MinimapCamera");

		_viewport.World2D = GetTree().Root.GetViewport().World2D;

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

		_mapPixelSize = new Vector2(MapWidth * TileSize, MapHeight * TileSize);

		Vector2 viewportSize = _viewport.Size;

		_minimapZoom = new Vector2(
			viewportSize.X / _mapPixelSize.X,
			viewportSize.Y / _mapPixelSize.Y
		);

		_minimapCamera.Zoom = _minimapZoom;
		_minimapCamera.Position = Vector2.Zero;

		CallDeferred(nameof(FindMainCamera));
	}

	private void FindMainCamera()
	{
		var currentScene = GetTree().CurrentScene;
		_mainCamera = currentScene?.GetNodeOrNull<Camera2D>("Camera2D");
		if (_mainCamera == null)
			GD.PrintErr("Minimap: Camera2D non trouvée!");
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_mainCamera == null)
			return;

		Vector2 cameraPos = _mainCamera.Position;
		Vector2 cameraZoom = _mainCamera.Zoom;

		Vector2 mainViewportSize = _mainCamera.GetViewportRect().Size;
		Vector2 visibleWorldSize = mainViewportSize / cameraZoom;

		Vector2 minimapSize = _viewport.Size;

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

	private bool _isDragging = false;

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			_isDragging = mb.Pressed;
			if (mb.Pressed)
			{
				HandleMinimapClick(mb.Position);
			}
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

		Vector2 minimapSize = _viewport.Size;
		Vector2 minimapCenter = minimapSize / 2;

		Vector2 relativePos = clickPos - minimapCenter;

		Vector2 worldPos = new Vector2(
			relativePos.X / _minimapZoom.X,
			relativePos.Y / _minimapZoom.Y
		);

		_mainCamera.Position = worldPos;
	}
}
