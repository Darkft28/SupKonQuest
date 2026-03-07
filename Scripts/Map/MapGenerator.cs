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
