using Godot;
using System;
using SupKonQuest.Map.Presets;

[Tool]
public partial class MapGenerator : Node
{
	private TileMapLayer _tileMapSol;
	private TileMapLayer _tileMapObjets;
	private Camera2D _camera;
	private Node2D _unitsContainer;
	private Node2D _objectsContainer;
	private SelectionManager _selectionManager;
	private TerritoryManager _territoryManager;
	private System.Collections.Generic.List<AIController> _aiControllers = new System.Collections.Generic.List<AIController>();

	private PackedScene _campScene;

	private int _mapWidth = 256;
	private int _mapHeight = 256;
	private const int TileSize = 128;

	private int? _networkSeed = null;

	private Random _seededRandom;

	public override void _Ready()
	{
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		_campScene = GD.Load<PackedScene>("res://Scenes/camp_simple.tscn");

		_unitsContainer = GetNodeOrNull<Node2D>("Units");
		if (_unitsContainer == null && !Engine.IsEditorHint())
		{
			_unitsContainer = new Node2D();
			_unitsContainer.Name = "Units";
			AddChild(_unitsContainer);
		}

		_selectionManager = GetNodeOrNull<SelectionManager>("SelectionManager");
		if (_selectionManager == null && !Engine.IsEditorHint())
		{
			_selectionManager = new SelectionManager();
			_selectionManager.Name = "SelectionManager";
			AddChild(_selectionManager);
		}

		if (_camera != null)
		{
			_camera.Zoom = new Vector2(0.25f, 0.25f);
			_camera.Position = Vector2.Zero;
		}

		if (!Engine.IsEditorHint())
		{
			var gameState = GetNodeOrNull<GameState>("/root/GameState");
			if (gameState != null && gameState.MapSeed != 0)
			{
				_networkSeed = gameState.MapSeed;
			}
		}

		if (!Engine.IsEditorHint())
		{
			if (GetNodeOrNull<NetworkSync>("NetworkSync") == null)
			{
				var networkSync = new NetworkSync();
				networkSync.Name = "NetworkSync";
				AddChild(networkSync);
			}

			ReadMapSettings();
			GenererMap();
			InitAIControllers();

			CallDeferred(nameof(InitTerritory));
		}
		else if (_tileMapSol.GetUsedCells().Count == 0)
		{
			GenererMap();
		}
	}

	public void SetSeed(int seed)
	{
		_networkSeed = seed;
	}

	private void GenererMap()
	{
		// Reset les IDs déterministes pour le multijoueur
		CampSimple.ResetCampIdCounter();
		NetworkEntityRegistry.Clear();

		TerrainGenerator.InitTileVariants(_tileMapSol);

		_tileMapSol.Clear();
		_tileMapObjets.Clear();
		_tileMapObjets.Visible = true;

		// Detruire et recreer les conteneurs pour un reset complet
		if (_unitsContainer != null)
		{
			RemoveChild(_unitsContainer);
			_unitsContainer.QueueFree();
		}
		_unitsContainer = new Node2D();
		_unitsContainer.Name = "Units";
		AddChild(_unitsContainer);

		if (_objectsContainer != null)
		{
			RemoveChild(_objectsContainer);
			_objectsContainer.QueueFree();
		}
		_objectsContainer = new Node2D();
		_objectsContainer.Name = "Objects";
		AddChild(_objectsContainer);

		int halfWidth = _mapWidth / 2;
		int halfHeight = _mapHeight / 2;

		// Générer le terrain depuis la map preset sélectionnée
		var gsMap = GetNodeOrNull<GameState>("/root/GameState");
		int baseSeed = _networkSeed ?? (int)GD.Randi();
		_seededRandom = new Random(baseSeed + 2000);
		float[] armAngles;
		var presetCampPositions = new System.Collections.Generic.List<Vector2I>();
		armAngles = ApplyPresetMap(gsMap.SelectedMapType, halfWidth, halfHeight, out presetCampPositions);
		SpawnPresetObjectSprites(halfWidth, halfHeight);

		// Construire les meshes de navigation (terrestre pour unités, maritime pour bateaux)
		BuildNavigationMesh();
		BuildWaterNavigationMesh();

		// Les arbres et montagnes sont rendus via Sprite2D dans _objectsContainer (plus grands).
		// La couche Objets reste active pour la navigation (GetCellSourceId) mais n'est pas affichée.
		_tileMapObjets.Visible = false;

		// Placer les camps depuis les positions prédéfinies de la map preset
		CampPlacer.PlacePresetCamps(presetCampPositions, _tileMapSol, _unitsContainer, _campScene,
			_seededRandom, TileSize, armAngles);

		if (GameManager.Instance != null)
		{
			GameManager.Instance.OnMapGenerationComplete();
		}

		// Mettre à jour les limites de la caméra avec la vraie taille de map
		if (_camera is SupKonQuest.CameraController cam)
			cam.SetupForMap(_mapWidth, _mapHeight);

		// Zoom intro vers la base du joueur local
		TriggerIntroZoom();
	}

	private void SpawnPresetObjectSprites(int halfWidth, int halfHeight)
	{
		foreach (var cell in _tileMapObjets.GetUsedCells())
		{
			int objId = _tileMapObjets.GetCellSourceId(cell);
			if (objId == 100 || objId == 101)
				TerrainGenerator.SpawnObjectSprite(_objectsContainer, objId, cell.X, cell.Y, TileSize);
		}
	}

	private float[] ApplyPresetMap(GameState.MapType mapType, int halfWidth, int halfHeight,
		out System.Collections.Generic.List<Vector2I> campPositions)
	{
		int[] solRle = mapType == GameState.MapType.Irridium
			? IrridiumMap.SolRle : AlabastaMap.SolRle;
		int[] objetsRle = mapType == GameState.MapType.Irridium
			? IrridiumMap.ObjetsRle : AlabastaMap.ObjetsRle;

		int width  = halfWidth  * 2;
		int height = halfHeight * 2;

		ApplyPresetLayer(_tileMapSol, solRle, width, height, -halfWidth, -halfHeight, skipId: -1, addVariants: true, skipCamps: true);
		// skipCamps=true : les camps (ID 102) ne sont pas placés sur le tilemap pour ne pas bloquer le nav mesh
		ApplyPresetLayer(_tileMapObjets, objetsRle, width, height, -halfWidth, -halfHeight, skipId: -1, addVariants: false, skipCamps: true);

		// Collecter les positions de camps depuis le RLE pour un placement aléatoire ensuite
		campPositions = CollectPresetCampPositions(objetsRle, width, height, -halfWidth, -halfHeight);

		// Angles de régions par défaut pour les presets (3 secteurs à 120°)
		return new float[] { 0f, 2.094f, 4.189f }; // 0°, 120°, 240°
	}

	private System.Collections.Generic.List<Vector2I> CollectPresetCampPositions(
		int[] rleData, int width, int height, int originX, int originY)
	{
		var result = new System.Collections.Generic.List<Vector2I>();
		int x = 0, y = 0;
		for (int i = 0; i < rleData.Length - 1; i += 2)
		{
			int count  = rleData[i];
			int tileId = rleData[i + 1];
			for (int j = 0; j < count; j++)
			{
				if (x >= width) { x = 0; y++; }
				if (y >= height) return result;
				if (tileId == 102)
					result.Add(new Vector2I(originX + x, originY + y));
				x++;
			}
		}
		return result;
	}

	private static void ApplyPresetLayer(TileMapLayer layer, int[] rleData,
		int width, int height, int originX, int originY, int skipId = -1, bool addVariants = false, bool skipCamps = false)
	{
		int x = 0, y = 0;
		for (int i = 0; i < rleData.Length - 1; i += 2)
		{
			int count  = rleData[i];
			int tileId = rleData[i + 1];
			for (int j = 0; j < count; j++)
			{
				if (x >= width) { x = 0; y++; }
				if (y >= height) return;

				if (tileId != skipId && !(skipCamps && tileId == 102))
				{
					int alt = addVariants ? TerrainGenerator.PickAlt(originX + x, originY + y) : 0;
					layer.SetCell(new Vector2I(originX + x, originY + y), tileId, Vector2I.Zero, alt);
				}
				x++;
			}
		}
	}

	private void TriggerIntroZoom()
	{
		var camera = _camera as SupKonQuest.CameraController;
		if (camera == null)
		{
			GD.PrintErr("[INTRO] Camera introuvable ou n'est pas un CameraController");
			return;
		}

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int localTeamId = gameState?.LocalTeamId ?? 1;

		CampSimple playerCamp = null;
		foreach (var node in GetTree().GetNodesInGroup("camps"))
		{
			if (node is CampSimple camp && camp.TeamId == localTeamId)
			{
				playerCamp = camp;
				break;
			}
		}

		if (playerCamp == null)
		{
			GD.PrintErr($"[INTRO] Aucun camp trouvé pour team {localTeamId}");
			return;
		}

		camera.StartIntroZoom(playerCamp.GlobalPosition, _mapWidth);
	}

	private void InitTerritory()
	{
		_territoryManager = new TerritoryManager();
		_territoryManager.Name = "TerritoryManager";
		AddChild(_territoryManager);
		MoveChild(_territoryManager, 1); // après Sol pour le Z-order
		_territoryManager.SetSolLayer(_tileMapSol);
		_territoryManager.Initialize();
	}

	private void ReadMapSettings()
	{
		_mapWidth = _mapHeight = 256;
	}

	// Helper commun : construit un NavigationPolygon à partir d'un prédicat de marchabilité
	private NavigationPolygon BuildNavPolygon(
		int groupSize, int halfWidth, int halfHeight,
		System.Func<int, int, int, int, int, bool> cellPredicate,
		out int polyCount)
	{
		int cellCols = _mapWidth  / groupSize;
		int cellRows = _mapHeight / groupSize;

		// 1) Collecter vertices ET polygones
		var verticesList = new System.Collections.Generic.List<Vector2>();
		var vertexMap    = new System.Collections.Generic.Dictionary<long, int>();
		var polygonList  = new System.Collections.Generic.List<int[]>();

		int GetOrAddVertex(int vx, int vy)
		{
			long key = ((long)vx << 32) | (uint)vy;
			if (vertexMap.TryGetValue(key, out int idx))
				return idx;
			float wx = (vx * groupSize - halfWidth)  * TileSize - TileSize / 2f;
			float wy = (vy * groupSize - halfHeight) * TileSize - TileSize / 2f;
			int newIdx = verticesList.Count;
			verticesList.Add(new Vector2(wx, wy));
			vertexMap[key] = newIdx;
			return newIdx;
		}

		for (int cy = 0; cy < cellRows; cy++)
		{
			for (int cx = 0; cx < cellCols; cx++)
			{
				if (!cellPredicate(cx, cy, groupSize, halfWidth, halfHeight))
					continue;

				int tl = GetOrAddVertex(cx,     cy);
				int tr = GetOrAddVertex(cx + 1, cy);
				int br = GetOrAddVertex(cx + 1, cy + 1);
				int bl = GetOrAddVertex(cx,     cy + 1);
				polygonList.Add(new int[] { tl, tr, br, bl });
			}
		}

		// 2) Construire le NavigationPolygon : Vertices d'abord, polygones ensuite
		var navPoly = new NavigationPolygon();
		navPoly.Vertices = verticesList.ToArray();
		foreach (var poly in polygonList)
			navPoly.AddPolygon(poly);

		polyCount = polygonList.Count;
		return navPoly;
	}

	private void BuildNavigationMesh()
	{
		var oldNav = GetNodeOrNull<NavigationRegion2D>("NavRegion");
		if (oldNav != null) { RemoveChild(oldNav); oldNav.QueueFree(); }

		int groupSize = System.Math.Max(1, _mapWidth / 128);
		int halfWidth = _mapWidth / 2, halfHeight = _mapHeight / 2;

		var navPoly = BuildNavPolygon(groupSize, halfWidth, halfHeight, IsCellWalkable, out int polyCount);

		var navRegion = new NavigationRegion2D();
		navRegion.Name = "NavRegion";
		navRegion.NavigationLayers = 1u; // Couche 1 : terrestre (unités)
		navRegion.NavigationPolygon = navPoly;
		AddChild(navRegion);

	}

	private void BuildWaterNavigationMesh()
	{
		var oldNav = GetNodeOrNull<NavigationRegion2D>("NavRegionWater");
		if (oldNav != null) { RemoveChild(oldNav); oldNav.QueueFree(); }

		int groupSize = System.Math.Max(1, _mapWidth / 128);
		int halfWidth = _mapWidth / 2, halfHeight = _mapHeight / 2;

		var navPoly = BuildNavPolygon(groupSize, halfWidth, halfHeight, IsCellAllWater, out int polyCount);

		var navRegion = new NavigationRegion2D();
		navRegion.Name = "NavRegionWater";
		navRegion.NavigationLayers = 2u; // Couche 2 : maritime (bateaux)
		navRegion.NavigationPolygon = navPoly;
		AddChild(navRegion);

	}

	private bool IsCellAllWater(int cx, int cy, int groupSize, int halfWidth, int halfHeight)
	{
		for (int dy = 0; dy < groupSize; dy++)
		{
			for (int dx = 0; dx < groupSize; dx++)
			{
				int tx = cx * groupSize - halfWidth  + dx;
				int ty = cy * groupSize - halfHeight + dy;
				if (_tileMapSol.GetCellSourceId(new Vector2I(tx, ty)) != 6)
					return false; // une tuile non-eau → cellule invalide
			}
		}
		return true;
	}

	private bool IsCellWalkable(int cx, int cy, int groupSize, int halfWidth, int halfHeight)
	{
		bool hasLand = false;
		for (int dy = 0; dy < groupSize; dy++)
		{
			for (int dx = 0; dx < groupSize; dx++)
			{
				int tx = cx * groupSize - halfWidth  + dx;
				int ty = cy * groupSize - halfHeight + dy;
				var tileCoord = new Vector2I(tx, ty);

				int solId = _tileMapSol.GetCellSourceId(tileCoord);
				if (solId == 6 || solId == -1)
					continue; // eau ou vide → pas de terrain valide ici

				// Forêt = obstacle (même sans objet arbre placé dessus)
				if (solId == 3 || solId == 5 || solId == 4)
					return false;

				// Si une tuile objet (arbre=100 ou montagne=101) est présente → obstacle physique
				int objetId = _tileMapObjets.GetCellSourceId(tileCoord);
				if (objetId != -1)
					return false; // au moins une tuile bloquante dans la cellule

				hasLand = true;
			}
		}
		return hasLand;
	}

	private void InitAIControllers()
	{
		// Supprimer les anciens contrôleurs
		foreach (var ai in _aiControllers)
		{
			if (ai != null && IsInstanceValid(ai))
			{
				RemoveChild(ai);
				ai.QueueFree();
			}
		}
		_aiControllers.Clear();

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState == null || !gameState.IsAIMode) return;

		if (gameState.IsFreeForAll)
		{
			var botTeams = GameManager.Instance?.GetBotTeamIds()
				?? new System.Collections.Generic.List<int>();
			foreach (int teamId in botTeams)
			{
				var ai = new AIController();
				ai.Name = $"AIController_Team{teamId}";
				ai.AITeamId = teamId;
				ai.Level = gameState.AILevel;
				AddChild(ai);
				_aiControllers.Add(ai);
			}
		}
		else
		{
			var ai = new AIController();
			ai.Name = "AIController";
			ai.AITeamId = 2;
			ai.Level = gameState.AILevel;
			AddChild(ai);
			_aiControllers.Add(ai);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			if (_territoryManager != null)
			{
				RemoveChild(_territoryManager);
				_territoryManager.QueueFree();
				_territoryManager = null;
			}

			ReadMapSettings();
			GenererMap();
			InitAIControllers();
			CallDeferred(nameof(InitTerritory)); // différé comme dans _Ready(), pour que les camps aient leur _Ready()
		}
	}
}
