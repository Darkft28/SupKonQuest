using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class MapGenerator : Node
{
	private TileMapLayer _tileMapSol;
	private TileMapLayer _tileMapObjets;
	private Camera2D _camera;
	private Node2D _unitsContainer;
	private SelectionManager _selectionManager;
	private TerritoryManager _territoryManager;
	private System.Collections.Generic.List<AIController> _aiControllers = new System.Collections.Generic.List<AIController>();

	private PackedScene _campScene;
	private PackedScene _campUpScene;

	private int _mapWidth = 256;
	private int _mapHeight = 256;
	private const int TileSize = 128;

	// Mode test : spawn seulement 2 camps proches pour tester la victoire
	private const bool TestMode = false;

	private int? _networkSeed = null;

	private FastNoiseLite _noiseElevation = new FastNoiseLite();
	private FastNoiseLite _noiseForet = new FastNoiseLite();
	private Random _seededRandom;

	public override void _Ready()
	{
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		_campScene = GD.Load<PackedScene>("res://Scenes/camp_simple.tscn");
		_campUpScene = GD.Load<PackedScene>("res://Scenes/camp_avancé.tscn");

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
				GD.Print($"Seed réseau détectée: {_networkSeed}");
			}
		}

		if (!Engine.IsEditorHint())
		{
			// Ajouter NetworkSync si pas deja present
			if (GetNodeOrNull<NetworkSync>("NetworkSync") == null)
			{
				var networkSync = new NetworkSync();
				networkSync.Name = "NetworkSync";
				AddChild(networkSync);
			}

			ReadMapSettings();
			SetupNoise();
			GenererMap();
			InitAIControllers();

			CallDeferred(nameof(InitTerritory));
			GD.Print("Map générée. Appuyez sur ESPACE pour régénérer.");
		}
		else if (_tileMapSol.GetUsedCells().Count == 0)
		{
			SetupNoise();
			GenererMap();
		}

	}

	public void SetSeed(int seed)
	{
		_networkSeed = seed;
		GD.Print($"Seed définie: {seed}");
	}

	[Export]
	public bool GenererMapMaintenant
	{
		get => false;
		set
		{
			if (value)
			{
				InitialiserEtGenerer();
			}
		}
	}

	private void InitialiserEtGenerer()
	{
		if (_tileMapSol == null) _tileMapSol = GetNode<TileMapLayer>("Sol");
		if (_tileMapObjets == null) _tileMapObjets = GetNode<TileMapLayer>("Objets");

		SetupNoise();
		GenererMap();

		GD.Print("Map générée dans l'éditeur.");
	}

	private void SetupNoise()
	{
		int baseSeed = _networkSeed ?? (int)GD.Randi();

		_noiseElevation.Seed = baseSeed;
		_noiseElevation.Frequency = 0.008f;
		_noiseElevation.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseElevation.FractalOctaves = 5;

		_noiseForet.Seed = baseSeed + 1000;
		_noiseForet.Frequency = 0.05f;

		_seededRandom = new Random(baseSeed + 2000);

		GD.Print($"Noise initialisé avec seed: {baseSeed}");
	}

	private void GenererMap()
	{
		GD.Print("Génération en cours...");

		// Reset les IDs deterministes pour le multi
		CampSimple.ResetCampIdCounter();
		NetworkEntityRegistry.Clear();

		_tileMapSol.Clear();
		_tileMapObjets.Clear();

		// Detruire et recreer le conteneur d'unites pour un reset complet
		if (_unitsContainer != null)
		{
			RemoveChild(_unitsContainer);
			_unitsContainer.QueueFree();
		}
		_unitsContainer = new Node2D();
		_unitsContainer.Name = "Units";
		AddChild(_unitsContainer);

		int halfWidth = _mapWidth / 2;
		int halfHeight = _mapHeight / 2;

		// Générer le terrain
		TerrainGenerator.Generate(_tileMapSol, _tileMapObjets, _noiseElevation, _noiseForet, _seededRandom,
			halfWidth, halfHeight, TileSize, TestMode);

		// Construire les meshes de navigation (terrestre pour unités, maritime pour bateaux)
		BuildNavigationMesh();
		BuildWaterNavigationMesh();

		// Placer les camps
		var gsMap = GetNodeOrNull<GameState>("/root/GameState");
		int maxCamps = (gsMap?.IsFreeForAll == true) ? gsMap.MaxCamps : 0;
		int campCount = CampPlacer.PlaceCamps(_tileMapSol, _tileMapObjets, _noiseElevation, _noiseForet, _seededRandom,
			_unitsContainer, _campScene, _campUpScene, halfWidth, halfHeight, TileSize, TestMode, maxCamps);

		GD.Print($"{campCount} camps générés");

		// Notifier le GameManager que les camps sont prêts
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

		GD.Print($"[INTRO] Zoom vers camp team {localTeamId} à {playerCamp.GlobalPosition}");
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
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		switch (gameState?.MapSize)
		{
			case GameState.MapSizePreset.Small:  _mapWidth = _mapHeight = 128; break;
			case GameState.MapSizePreset.Large:  _mapWidth = _mapHeight = 384; break;
			default:                             _mapWidth = _mapHeight = 256; break;
		}
		GD.Print($"[MAP] Taille: {_mapWidth}x{_mapHeight} tuiles");
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

		GD.Print($"[NAV] Mesh terrestre : {navPoly.Vertices.Length} sommets, {polyCount} polygones (groupSize={groupSize})");
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

		GD.Print($"[NAV] Mesh maritime : {navPoly.Vertices.Length} sommets, {polyCount} polygones (groupSize={groupSize})");
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
			// Un AIController par team bot
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
			GD.Print($"[IA] {_aiControllers.Count} AIControllers créés (FFA - niveau {gameState.AILevel})");
		}
		else
		{
			var ai = new AIController();
			ai.Name = "AIController";
			ai.AITeamId = 2;
			ai.Level = gameState.AILevel;
			AddChild(ai);
			_aiControllers.Add(ai);
			GD.Print($"[IA] AIController initialisé - niveau {gameState.AILevel}");
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			// Supprimer le territoire et l'IA existants
			if (_territoryManager != null)
			{
				RemoveChild(_territoryManager);
				_territoryManager.QueueFree();
				_territoryManager = null;
			}

			ReadMapSettings();
			SetupNoise();
			GenererMap();
			InitAIControllers();
			InitTerritory();
		}
	}
}
