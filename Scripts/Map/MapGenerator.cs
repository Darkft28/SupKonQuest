using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
	// Territory grid (256x256) decoded from preset map RLE
	private int[,] _territoryGrid;
	private string[] _territoireNoms;

	// Territory connectivity graph (exposed for AI and units)
	public static Dictionary<int, HashSet<int>> TerritoryGraph { get; private set; }

	private PackedScene _campScene;

	private int _mapWidth = 256;
	private int _mapHeight = 256;
	private const int TileSize = 128;

	private int? _networkSeed = null;
	private CanvasLayer _loadingOverlay;
	private Label _loadingStatusLabel;

	private Random _seededRandom;

	public override async void _Ready()
	{
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		_campScene = GD.Load<PackedScene>("res://Scenes/camp_simple.tscn");

		_unitsContainer = GetNodeOrNull<Node2D>("Units");
		if (_unitsContainer == null && !Engine.IsEditorHint())
			GD.PrintErr("[MAP] Missing 'Units' node in Game.tscn");

		_selectionManager = GetNodeOrNull<SelectionManager>("SelectionManager");
		if (_selectionManager == null && !Engine.IsEditorHint())
			GD.PrintErr("[MAP] Missing 'SelectionManager' node in Game.tscn");

		if (_camera != null)
		{
			_camera.Zoom = new Vector2(0.25f, 0.25f);
			_camera.Position = Vector2.Zero;
		}

		if (!Engine.IsEditorHint())
		{
			var gameState = GetNodeOrNull<GameState>("/root/GameState");
			if (gameState != null)
				_networkSeed = gameState.GetEffectiveMapSeed();
		}

		if (!Engine.IsEditorHint())
		{
			if (GetNodeOrNull<NetworkSync>("NetworkSync") == null)
				GD.PrintErr("[MAP] Missing 'NetworkSync' node in Game.tscn");

			await LancerAvecChargement();
		}
	}

	// Starts map generation and waits for nav sync before starting AI
	private async System.Threading.Tasks.Task LancerAvecChargement()
	{
		ShowLoadingScreen("Generating map...");

		// Allow one frame so the overlay is visible before heavy work
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GenererMap();

		// NavigationServer2D processes nav regions asynchronously.
		// Wait until navmesh sync is done before units compute paths,
		// otherwise two AIs between identical points can produce different
		// paths depending on which one computes first.
		SetLoadingStatus("Precomputing paths...");
		for (int i = 0; i < 5; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		HideLoadingScreen();

		InitTerritory();
		InitAIController();
	}

	private void ShowLoadingScreen(string status)
	{
		_loadingOverlay = new CanvasLayer();
		_loadingOverlay.Layer = 128; // above everything
		AddChild(_loadingOverlay);

		// Opaque background
		var bg = new ColorRect();
		bg.Color = new Color(0.06f, 0.07f, 0.1f, 1f);
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop; // blocks all player clicks
		_loadingOverlay.AddChild(bg);

		// Centered container
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 16);
		_loadingOverlay.AddChild(vbox);

		var title = new Label();
		title.Text = "SupKonQuest";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 36);
		title.Modulate = new Color(1f, 0.85f, 0.4f);
		vbox.AddChild(title);

		_loadingStatusLabel = new Label();
		_loadingStatusLabel.Text = status;
		_loadingStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_loadingStatusLabel.AddThemeFontSizeOverride("font_size", 18);
		_loadingStatusLabel.Modulate = new Color(0.75f, 0.85f, 1f);
		vbox.AddChild(_loadingStatusLabel);
	}

	private void SetLoadingStatus(string status)
	{
		if (_loadingStatusLabel != null && IsInstanceValid(_loadingStatusLabel))
			_loadingStatusLabel.Text = status;
	}

	private void HideLoadingScreen()
	{
		if (_loadingOverlay != null && IsInstanceValid(_loadingOverlay))
		{
			_loadingOverlay.QueueFree();
			_loadingOverlay = null;
			_loadingStatusLabel = null;
		}
	}

	public void SetSeed(int seed)
	{
		_networkSeed = seed;
	}

	private void GenererMap()
	{
		// Reset deterministic IDs for multiplayer
		CampSimple.ResetCampIdCounter();
		NetworkEntityRegistry.Clear();

		TerrainGenerator.InitTileVariants(_tileMapSol);

		_tileMapSol.Clear();
		_tileMapObjets.Clear();
		_tileMapObjets.Visible = true;

		// Full content reset without recreating pre-instanced scene nodes.
		if (_unitsContainer == null)
		{
			GD.PrintErr("[MAP] Missing 'Units' node, generation cancelled.");
			return;
		}
		ClearContainerChildren(_unitsContainer);

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

		// Generate terrain from selected preset map
		var gsMap = GetNodeOrNull<GameState>("/root/GameState");
		int baseSeed = _networkSeed ?? (int)GD.Randi();
		_seededRandom = new Random(baseSeed + 2000);
		float[] armAngles;
		var presetCampPositions = new System.Collections.Generic.List<Vector2I>();
		armAngles = ApplyPresetMap(gsMap?.SelectedMapType ?? GameState.MapType.Irridium, halfWidth, halfHeight, out presetCampPositions);
		SpawnPresetObjectSprites(halfWidth, halfHeight);

		// Build navigation meshes (land for units, water for ships)
		BuildNavigationMesh();
		BuildWaterNavigationMesh();

		// Trees and mountains are rendered via Sprite2D in _objectsContainer (larger visuals).
		// The Objects layer stays active for navigation checks (GetCellSourceId) but is hidden.
		_tileMapObjets.Visible = false;

		// Place camps from preset map positions
		CampPlacer.PlacePresetCamps(presetCampPositions, _tileMapSol, _unitsContainer, _campScene,
			_seededRandom, TileSize, armAngles, _territoryGrid, halfWidth, halfHeight);

		// Build territory connectivity graph
		TerritoryGraph = TerritoryConnectivity.Build(_territoryGrid, _tileMapSol, halfWidth, halfHeight);

		if (GameManager.Instance != null)
		{
			GameManager.Instance.OnMapGenerationComplete();
		}

		// Update camera bounds using actual map size
		if (_camera is SupKonQuest.CameraController cam)
			cam.SetupForMap(_mapWidth, _mapHeight);

		// Intro zoom to local player's base
		TriggerIntroZoom();
	}

	private static void ClearContainerChildren(Node container)
	{
		foreach (Node child in container.GetChildren())
		{
			// Use Free() to remove nodes immediately from the scene tree and groups,
			// ensuring they are not visible to subsequent logic in the same frame.
			child.Free();
		}
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
		int[] solRle = mapType switch
		{
			GameState.MapType.Alabasta => AlabastaMap.SolRle,
			GameState.MapType.Torskey => TorskeyMap.SolRle,
			_                          => IrridiumMap.SolRle,
		};
		int[] objetsRle = mapType switch
		{
			GameState.MapType.Alabasta => AlabastaMap.ObjetsRle,
			GameState.MapType.Torskey => TorskeyMap.ObjetsRle,
			_                          => IrridiumMap.ObjetsRle,
		};

		int width  = halfWidth  * 2;
		int height = halfHeight * 2;

		ApplyPresetLayer(_tileMapSol, solRle, width, height, -halfWidth, -halfHeight, skipId: -1, addVariants: true, skipCamps: true);
		// skipCamps=true : les camps (ID 102) ne sont pas placés sur le tilemap pour ne pas bloquer le nav mesh
		ApplyPresetLayer(_tileMapObjets, objetsRle, width, height, -halfWidth, -halfHeight, skipId: -1, addVariants: false, skipCamps: true);

		// Collect camp positions from RLE for later randomized assignment
		campPositions = CollectPresetCampPositions(objetsRle, width, height, -halfWidth, -halfHeight);

		// Load territory grid from preset map RLE
		int[] territoiresRle = mapType switch
		{
			GameState.MapType.Alabasta => AlabastaMap.TerritoiresRle,
			GameState.MapType.Torskey => TorskeyMap.TerritoiresRle,
			_                          => IrridiumMap.TerritoiresRle,
		};
		_territoireNoms = mapType switch
		{
			GameState.MapType.Alabasta => AlabastaMap.TerritoireNoms,
			GameState.MapType.Torskey => TorskeyMap.TerritoireNoms,
			_                          => IrridiumMap.TerritoireNoms,
		};
		LoadTerritoryMap(territoiresRle, width, height, halfWidth, halfHeight);

		// Default region angles for presets (3 sectors at 120 deg)
		return new float[] { 0f, 2.094f, 4.189f }; // 0°, 120°, 240°
	}

	// Decode territory RLE (count/id pairs) into _territoryGrid[256,256].
	// id 0 = unassigned territory.
	private void LoadTerritoryMap(int[] rleData, int width, int height, int halfWidth, int halfHeight)
	{
		_territoryGrid = new int[width, height];

		int x = 0, y = 0;
		for (int i = 0; i < rleData.Length - 1; i += 2)
		{
			int count = rleData[i];
			int id    = rleData[i + 1];
			for (int j = 0; j < count; j++)
			{
				if (x >= width) { x = 0; y++; }
				if (y >= height) return;

				// Store in grid coordinates [0, width[ x [0, height[
				_territoryGrid[x, y] = id;
				x++;
			}
		}
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
					int alt = addVariants ? TerrainGenerator.PickAlt(originX + x, originY + y, tileId) : 0;
					layer.SetCell(new Vector2I(originX + x, originY + y), tileId, Vector2I.Zero, alt);
				}
				x++;
			}
		}
	}

	private bool IsSand(int tx, int ty)
	{
		return _tileMapSol.GetCellSourceId(new Vector2I(tx, ty)) == 1;
	}

	private void TriggerIntroZoom()
	{
		var camera = _camera as SupKonQuest.CameraController;
		if (camera == null)
		{
			GD.PrintErr("[INTRO] Camera not found or not a CameraController");
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
			GD.PrintErr($"[INTRO] No camp found for team {localTeamId}");
			return;
		}

		camera.StartIntroZoom(playerCamp.GlobalPosition, _mapWidth);
	}

	private void InitTerritory()
	{
		_territoryManager = new TerritoryManager();
		_territoryManager.Name = "TerritoryManager";
		AddChild(_territoryManager);
		MoveChild(_territoryManager, 1); // after Sol for z-order
		_territoryManager.SetSolLayer(_tileMapSol);
		_territoryManager.SetTerritoryGrid(_territoryGrid);
		_territoryManager.Initialize();
	}

	// Shared helper: builds a NavigationPolygon from a walkability predicate
	private NavigationPolygon BuildNavPolygon(
		int groupSize, int halfWidth, int halfHeight,
		System.Func<int, int, int, int, int, bool> cellPredicate,
		out int polyCount)
	{
		int cellCols = _mapWidth  / groupSize;
		int cellRows = _mapHeight / groupSize;

		// 1) Collect vertices and polygons
		var verticesList = new System.Collections.Generic.List<Vector2>();
		var vertexMap    = new System.Collections.Generic.Dictionary<long, int>();
		var polygonList  = new System.Collections.Generic.List<int[]>();

		int GetOrAddVertex(int vx, int vy)
		{
			long key = ((long)vx << 32) | (uint)vy;
			if (vertexMap.TryGetValue(key, out int idx))
				return idx;
			// World coordinates aligned exactly on tile edges
			float wx = (vx * groupSize - halfWidth)  * TileSize;
			float wy = (vy * groupSize - halfHeight) * TileSize;
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

		// 2) Build NavigationPolygon: vertices first, polygons after
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
		navRegion.NavigationLayers = 1u; // Layer 1: land (units)
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
		navRegion.NavigationLayers = 2u; // Layer 2: sea (ships)
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
					return false; // one non-water tile -> invalid cell
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
				if (solId == 6)
					return false; // water in cell -> non-walkable
				if (solId == -1)
					continue;     // empty (map edge) -> ignore

				// Forest = obstacle (even without an object tile)
				if (solId == 3 || solId == 5 || solId == 4)
					return false;

				// Object tile present (tree=100 or mountain=101) -> physical obstacle
				int objetId = _tileMapObjets.GetCellSourceId(tileCoord);
				if (objetId != -1)
					return false; // at least one blocking tile in the cell

				hasLand = true;
			}
		}
		return hasLand;
	}

	private void InitAIController()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState == null || !gameState.IsAIMode || gameState.IsOnline) return;

		// Remove previous AIControllers
		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			if (GetChild(i) is AIController old)
				old.QueueFree();
		}

		var botTeams = GameManager.Instance?.GetBotTeamIds() ?? new System.Collections.Generic.List<int>();
		int playerRegion = GameManager.Instance?.GetHomeRegion(1) ?? -1;

		AIController.BossTeamIds.Clear();

		// Boss difficulty = one step above player selection
		AIController.Difficulty bossLevel = gameState.AILevel switch
		{
			AIController.Difficulty.Easy   => AIController.Difficulty.Medium,
			AIController.Difficulty.Medium => AIController.Difficulty.Hard,
			_                              => AIController.Difficulty.Hard
		};

		// One boss per non-player region: farthest bot from player in each region
		var playerCamp = GameManager.Instance?.GetAllCamps()?.Find(c => c.GetTeamId() == 1);
		var allCamps   = GameManager.Instance?.GetAllCamps();

		// Group bots by region
		var botsByRegion = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
		foreach (int teamId in botTeams)
		{
			int region = GameManager.Instance?.GetHomeRegion(teamId) ?? -1;
			if (!botsByRegion.ContainsKey(region))
				botsByRegion[region] = new System.Collections.Generic.List<int>();
			botsByRegion[region].Add(teamId);
		}

		// For each non-player region: boss = farthest bot from player
		foreach (var (region, teams) in botsByRegion)
		{
			if (region == playerRegion) continue;

			int bossInRegion = -1;
			float maxDist = float.MinValue;
			foreach (int teamId in teams)
			{
				var botCamp = allCamps?.Find(c => c.GetTeamId() == teamId);
				float dist = playerCamp != null && botCamp != null
					? playerCamp.GlobalPosition.DistanceTo(botCamp.GlobalPosition)
					: 0f;
				if (dist > maxDist) { maxDist = dist; bossInRegion = teamId; }
			}
			if (bossInRegion != -1)
				AIController.BossTeamIds.Add(bossInRegion);
		}

		foreach (int teamId in botTeams)
		{
			bool isBoss = AIController.BossTeamIds.Contains(teamId);
			var ai = new AIController();
			ai.Name = $"AIController_team{teamId}";
			AddChild(ai);
			ai.Initialize(isBoss ? bossLevel : AIController.Difficulty.Easy, teamId);
		}

		GD.Print($"[MAP] {botTeams.Count} AIController(s) - {AIController.BossTeamIds.Count} boss ({bossLevel}), others Easy");
		GD.Print($"[IA DEBUG] Player region (team 1): {playerRegion}");
		foreach (int teamId in botTeams)
		{
			int region = GameManager.Instance?.GetHomeRegion(teamId) ?? -1;
			bool isBoss = AIController.BossTeamIds.Contains(teamId);
			GD.Print($"[IA DEBUG]   Team {teamId} -> region {region} -> {(isBoss ? $"BOSS ({bossLevel})" : "Easy")}");
		}

		// Refresh camp labels now that BossTeamIds is populated
		foreach (var node in GetTree().GetNodesInGroup("camps"))
		{
			if (node is CampSimple camp)
				camp.RefreshCampLabel();
		}
	}

	public override async void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			if (_territoryManager != null)
			{
				RemoveChild(_territoryManager);
				_territoryManager.QueueFree();
				_territoryManager = null;
			}

			await LancerAvecChargement();
		}
	}
}
