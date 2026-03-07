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
	private Vector2 _minimapZoom; // zoom par axe pour remplir le viewport sans gris
	private Vector2 _mapPixelSize;

	public override void _Ready()
	{
		_viewport = GetNode<SubViewport>("SubViewport");
		_minimapCamera = _viewport.GetNode<Camera2D>("MinimapCamera");

		// On dit au Viewport d'utiliser le même monde 2D que la fenêtre principale
		_viewport.World2D = GetTree().Root.GetViewport().World2D;

		// Lire la taille de la map depuis GameState si disponible
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

		// Calculer la taille totale de la map en pixels
		_mapPixelSize = new Vector2(MapWidth * TileSize, MapHeight * TileSize);

		// Taille du viewport de la minimap
		Vector2 viewportSize = _viewport.Size;

		// Zoom par axe : la map remplit exactement le viewport (pas de gris hors-carte)
		_minimapZoom = new Vector2(
			viewportSize.X / _mapPixelSize.X,
			viewportSize.Y / _mapPixelSize.Y
		);

		// Appliquer le zoom et centrer la caméra
		_minimapCamera.Zoom = _minimapZoom;
		_minimapCamera.Position = Vector2.Zero;

		// Chercher la caméra principale
		CallDeferred(nameof(FindMainCamera));

		GD.Print($"Minimap: zoom={_minimapZoom}, viewport={viewportSize}, mapSize={_mapPixelSize}");
	}

	private void FindMainCamera()
	{
		var currentScene = GetTree().CurrentScene;
		_mainCamera = currentScene?.GetNodeOrNull<Camera2D>("Camera2D");

		if (_mainCamera != null)
		{
			GD.Print($"Minimap: Camera principale trouvée - {_mainCamera.GetPath()}");
		}
		else
		{
			GD.PrintErr("Minimap: Camera2D non trouvée!");
		}
	}

	public override void _Process(double delta)
	{
		// Redessiner pour mettre à jour le rectangle de vue
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_mainCamera == null)
			return;

		// Récupérer la position et le zoom de la caméra principale
		Vector2 cameraPos = _mainCamera.Position;
		Vector2 cameraZoom = _mainCamera.Zoom;

		// Calculer la taille visible par la caméra principale (en pixels monde)
		Vector2 mainViewportSize = _mainCamera.GetViewportRect().Size;
		Vector2 visibleWorldSize = mainViewportSize / cameraZoom;

		// Convertir en coordonnées minimap
		// La minimap montre la map centrée sur (0,0) avec un certain zoom
		Vector2 minimapSize = _viewport.Size;

		// Position du centre de la vue dans la minimap
		// Le centre de la map (0,0) correspond au centre de la minimap
		Vector2 minimapCenter = minimapSize / 2;

		// Conversion monde -> minimap avec zoom par axe
		Vector2 rectCenterInMinimap = minimapCenter + new Vector2(
			cameraPos.X * _minimapZoom.X,
			cameraPos.Y * _minimapZoom.Y
		);
		Vector2 rectSizeInMinimap = new Vector2(
			visibleWorldSize.X * _minimapZoom.X,
			visibleWorldSize.Y * _minimapZoom.Y
		);

		// Calculer le rectangle et le borner aux limites de la minimap
		Vector2 rectTopLeft = rectCenterInMinimap - rectSizeInMinimap / 2;
		Rect2 viewRect = new Rect2(rectTopLeft, rectSizeInMinimap);
		viewRect = viewRect.Intersection(new Rect2(Vector2.Zero, minimapSize));

		// Dessiner le contour du rectangle
		DrawRect(viewRect, ViewRectColor, false, ViewRectBorderWidth);
	}

	private bool _isDragging = false;

	public override void _GuiInput(InputEvent @event)
	{
		// Clic gauche : début du drag
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			_isDragging = mb.Pressed;
			if (mb.Pressed)
			{
				HandleMinimapClick(mb.Position);
			}
			GetViewport().SetInputAsHandled();
		}

		// Mouvement souris pendant le drag
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

		// Convertir la position du clic (coordonnées minimap) en coordonnées monde
		Vector2 minimapSize = _viewport.Size;
		Vector2 minimapCenter = minimapSize / 2;

		// Position relative au centre de la minimap
		Vector2 relativePos = clickPos - minimapCenter;

		// Convertir en coordonnées monde avec zoom par axe
		Vector2 worldPos = new Vector2(
			relativePos.X / _minimapZoom.X,
			relativePos.Y / _minimapZoom.Y
		);

		// Déplacer la caméra principale
		_mainCamera.Position = worldPos;

		GD.Print($"Minimap click: {clickPos} -> World: {worldPos}");
	}
}
