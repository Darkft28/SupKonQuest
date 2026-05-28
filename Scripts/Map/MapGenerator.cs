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
	// Grille de territoires (256×256) décodée depuis le RLE de la map preset
	private int[,] _territoryGrid;
	private string[] _territoireNoms;

	// Graphe de connectivité des territoires (exposé pour l'IA et les unités)
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
			GD.PrintErr("[MAP] Noeud 'Units' manquant dans Game.tscn");

		_selectionManager = GetNodeOrNull<SelectionManager>("SelectionManager");
		if (_selectionManager == null && !Engine.IsEditorHint())
			GD.PrintErr("[MAP] Noeud 'SelectionManager' manquant dans Game.tscn");

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
				GD.PrintErr("[MAP] Noeud 'NetworkSync' manquant dans Game.tscn");

			await LancerAvecChargement();
		}
	}

	// Lance la génération de map + attend la synchronisation nav avant de démarrer l'IA
	private async System.Threading.Tasks.Task LancerAvecChargement()
	{
		ShowLoadingScreen("Génération de la carte...");

		// Laisser un frame pour que l'overlay s'affiche avant le travail lourd
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GenererMap();

		// Le NavigationServer2D traite les régions de nav de façon asynchrone.
		// Il faut attendre qu'il ait synchronisé le navmesh avant que les unités
		// puissent calculer des chemins — sinon deux IA entre les mêmes points
		// peuvent obtenir des chemins différents selon qui calcule en premier.
		SetLoadingStatus("Pré-calcul des chemins...");
		for (int i = 0; i < 5; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		HideLoadingScreen();

		InitTerritory();
		InitAIController();
	}

	private void ShowLoadingScreen(string status)
	{
		_loadingOverlay = new CanvasLayer();
		_loadingOverlay.Layer = 128; // au-dessus de tout
		AddChild(_loadingOverlay);

		// Fond opaque
		var bg = new ColorRect();
		bg.Color = new Color(0.06f, 0.07f, 0.1f, 1f);
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop; // bloque tous les clics joueur
		_loadingOverlay.AddChild(bg);

		// Conteneur centré
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
		// Reset les IDs déterministes pour le multijoueur
		CampSimple.ResetCampIdCounter();
		NetworkEntityRegistry.Clear();

		TerrainGenerator.InitTileVariants(_tileMapSol);

		_tileMapSol.Clear();
		_tileMapObjets.Clear();
		_tileMapObjets.Visible = true;

		// Reset complet du contenu sans recréer les noeuds pré-instanciés dans la scène.
		if (_unitsContainer == null)
		{
			GD.PrintErr("[MAP] Noeud 'Units' manquant, génération annulée.");
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

		// Générer le terrain depuis la map preset sélectionnée
		var gsMap = GetNodeOrNull<GameState>("/root/GameState");
		int baseSeed = _networkSeed ?? (int)GD.Randi();
		_seededRandom = new Random(baseSeed + 2000);
		float[] armAngles;
		var presetCampPositions = new System.Collections.Generic.List<Vector2I>();
		armAngles = ApplyPresetMap(gsMap?.SelectedMapType ?? GameState.MapType.Irridium, halfWidth, halfHeight, out presetCampPositions);
		SpawnPresetObjectSprites(halfWidth, halfHeight);

		// Construire les meshes de navigation (terrestre pour unités, maritime pour bateaux)
		BuildNavigationMesh();
		BuildWaterNavigationMesh();

		// Les arbres et montagnes sont rendus via Sprite2D dans _objectsContainer (plus grands).
		// La couche Objets reste active pour la navigation (GetCellSourceId) mais n'est pas affichée.
		_tileMapObjets.Visible = false;

		// Placer les camps depuis les positions prédéfinies de la map preset
		CampPlacer.PlacePresetCamps(presetCampPositions, _tileMapSol, _unitsContainer, _campScene,
			_seededRandom, TileSize, armAngles, _territoryGrid, halfWidth, halfHeight);

		// Construire le graphe de connectivité des territoires
		TerritoryGraph = TerritoryConnectivity.Build(_territoryGrid, _tileMapSol, halfWidth, halfHeight);

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

		// Charger la grille de territoires depuis le RLE de la map preset
		int[] territoiresRle = mapType == GameState.MapType.Irridium
			? IrridiumMap.TerritoiresRle : AlabastaMap.TerritoiresRle;
		_territoireNoms = mapType == GameState.MapType.Irridium
			? IrridiumMap.TerritoireNoms : AlabastaMap.TerritoireNoms;
		LoadTerritoryMap(territoiresRle, width, height, halfWidth, halfHeight);

		// Angles de régions par défaut pour les presets (3 secteurs à 120°)
		return new float[] { 0f, 2.094f, 4.189f }; // 0°, 120°, 240°
	}

	// Décode le RLE territoire (paires count/id) et remplit _territoryGrid[256,256].
	// id 0 = pas de territoire assigné.
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

				// Stocker en coordonnées de grille [0, width[ × [0, height[
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
		_territoryManager.SetTerritoryGrid(_territoryGrid);
		_territoryManager.Initialize();
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
			// Coordonnées monde alignées exactement sur les bords de tuiles
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
				if (solId == 6)
					return false; // eau dans la cellule → non-marchable (navmesh ne déborde plus dans l'eau)
				if (solId == -1)
					continue;     // vide (bord de map) → ignorer

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

	private void InitAIController()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState == null || !gameState.IsAIMode || gameState.IsOnline) return;

		// Supprimer les anciens AIControllers
		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			if (GetChild(i) is AIController old)
				old.QueueFree();
		}

		var botTeams = GameManager.Instance?.GetBotTeamIds() ?? new System.Collections.Generic.List<int>();
		int playerRegion = GameManager.Instance?.GetHomeRegion(1) ?? -1;

		AIController.BossTeamIds.Clear();

		// Difficulté boss = un cran au-dessus de la sélection du joueur
		AIController.Difficulty bossLevel = gameState.AILevel switch
		{
			AIController.Difficulty.Easy   => AIController.Difficulty.Medium,
			AIController.Difficulty.Medium => AIController.Difficulty.Hard,
			_                              => AIController.Difficulty.Hard
		};

		// 1 boss par région non-joueur : le bot le plus éloigné du joueur dans chaque région
		var playerCamp = GameManager.Instance?.GetAllCamps()?.Find(c => c.GetTeamId() == 1);
		var allCamps   = GameManager.Instance?.GetAllCamps();

		// Regrouper les bots par région
		var botsByRegion = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
		foreach (int teamId in botTeams)
		{
			int region = GameManager.Instance?.GetHomeRegion(teamId) ?? -1;
			if (!botsByRegion.ContainsKey(region))
				botsByRegion[region] = new System.Collections.Generic.List<int>();
			botsByRegion[region].Add(teamId);
		}

		// Pour chaque région non-joueur : boss = bot le plus éloigné du joueur
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

		GD.Print($"[MAP] {botTeams.Count} AIController(s) — {AIController.BossTeamIds.Count} boss ({bossLevel}), reste Easy");
		GD.Print($"[IA DEBUG] Région joueur (team 1) : {playerRegion}");
		foreach (int teamId in botTeams)
		{
			int region = GameManager.Instance?.GetHomeRegion(teamId) ?? -1;
			bool isBoss = AIController.BossTeamIds.Contains(teamId);
			GD.Print($"[IA DEBUG]   Team {teamId} → région {region} → {(isBoss ? $"BOSS ({bossLevel})" : "Easy")}");
		}

		// Rafraîchir les labels des camps maintenant que BossTeamIds est rempli
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
