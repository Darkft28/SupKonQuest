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
	private float _minimapZoom;
	private Vector2 _mapPixelSize;

	public override void _Ready()
	{
		_viewport = GetNode<SubViewport>("SubViewport");
		_minimapCamera = _viewport.GetNode<Camera2D>("MinimapCamera");

		// On dit au Viewport d'utiliser le même monde 2D que la fenêtre principale
		_viewport.World2D = GetTree().Root.GetViewport().World2D;

		// Calculer la taille totale de la map en pixels
		_mapPixelSize = new Vector2(MapWidth * TileSize, MapHeight * TileSize);

		// Taille du viewport de la minimap
		Vector2 viewportSize = _viewport.Size;

		// Calculer le zoom pour que toute la map rentre dans le viewport
		float zoomX = viewportSize.X / _mapPixelSize.X;
		float zoomY = viewportSize.Y / _mapPixelSize.Y;
		_minimapZoom = Mathf.Min(zoomX, zoomY);

		// Appliquer le zoom et centrer la caméra
		_minimapCamera.Zoom = new Vector2(_minimapZoom, _minimapZoom);
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

		// Facteur de conversion : pixels monde -> pixels minimap
		float scale = _minimapZoom;

		// Position du rectangle dans la minimap
		Vector2 rectCenterInMinimap = minimapCenter + cameraPos * scale;
		Vector2 rectSizeInMinimap = visibleWorldSize * scale;

		// Calculer le rectangle
		Vector2 rectTopLeft = rectCenterInMinimap - rectSizeInMinimap / 2;
		Rect2 viewRect = new Rect2(rectTopLeft, rectSizeInMinimap);

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

		// Convertir en coordonnées monde
		Vector2 worldPos = relativePos / _minimapZoom;

		// Déplacer la caméra principale
		_mainCamera.Position = worldPos;

		GD.Print($"Minimap click: {clickPos} -> World: {worldPos}");
	}
}
