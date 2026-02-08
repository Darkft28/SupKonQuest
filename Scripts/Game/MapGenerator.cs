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

	private PackedScene _campScene;
	private PackedScene _campUpScene;

	private const int MapWidth = 256;
	private const int MapHeight = 256;
	private const int HalfWidth = MapWidth / 2;
	private const int HalfHeight = MapHeight / 2;
	private const int TileSize = 128;

	// IDs des tuiles
	private const int IdEau = 6;
	private const int IdSable = 1;
	private const int IdHerbe = 0;
	private const int IdForet = 3;
	private const int IdRoche = 5;
	private const int IdNeige = 4;

	private const int IdObjetArbre = 100;
	private const int IdObjetMontagne = 101;
	private const int IdObjetCamp = 102;
	private const int IdObjetCampUp = 103;

	// Distance min entre camps (en pixels)
	private const float MinCampDistance = 2500f;

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
			SetupNoise();
			GenererMap();
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

		_tileMapSol.Clear();
		_tileMapObjets.Clear();

		if (_unitsContainer != null)
		{
			foreach (Node child in _unitsContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		int campCount = 0;
		var campPositions = new List<Vector2>();

		for (int x = -HalfWidth; x < HalfWidth; x++)
		{
			for (int y = -HalfHeight; y < HalfHeight; y++)
			{
				float altitude = _noiseElevation.GetNoise2D(x, y);
				float densiteArbre = _noiseForet.GetNoise2D(x, y);

				int solId = -1;
				int objetId = -1;
				bool spawnCamp = false;
				bool spawnCampUp = false;

				// Biome selon l'altitude
				if (altitude < -0.2f)
				{
					solId = IdEau;
				}
				else if (altitude < -0.15f)
				{
					solId = IdSable;
				}
				else if (altitude < 0.4f)
				{
					if (densiteArbre > 0.2f)
					{
						solId = IdForet;
						if (densiteArbre > 0.3f)
						{
							if (_seededRandom.NextDouble() < 0.25)
							{
								objetId = IdObjetArbre;
							}
						}
					}
					else
					{
						solId = IdHerbe;
						if (_seededRandom.NextDouble() < 0.001)
						{
							Vector2 candidatePos = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
							if (IsFarEnoughFromCamps(candidatePos, campPositions))
							{
								spawnCamp = true;
								if (_seededRandom.NextDouble() < 0.2)
								{
									spawnCampUp = true;
									objetId = IdObjetCampUp;
								}
								else
								{
									objetId = IdObjetCamp;
								}
							}
							else
							{
								// garder le Random synchronisé
								_seededRandom.NextDouble();
							}
						}
					}
				}
				else if (altitude < 0.55f)
				{
					solId = IdRoche;
					if (altitude < 0.66f)
					{
						if (_seededRandom.NextDouble() < 0.25)
						{
							objetId = IdObjetMontagne;
						}
					}

				}
				else
				{
					solId = IdNeige;
				}

				Vector2I coords = new Vector2I(x, y);

				if (solId != -1)
				{
					_tileMapSol.SetCell(coords, solId, new Vector2I(0, 0));
				}

				if (objetId != -1)
				{
					_tileMapObjets.SetCell(coords, objetId, new Vector2I(0, 0));
				}

				if ((spawnCamp || spawnCampUp) && !Engine.IsEditorHint() && _unitsContainer != null)
				{
					PackedScene campSceneToUse = spawnCampUp ? _campUpScene : _campScene;
					if (campSceneToUse != null)
					{
						var camp = campSceneToUse.Instantiate<Node2D>();
						Vector2 worldPos = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
						camp.GlobalPosition = worldPos;
						camp.Name = $"Camp_{campCount++}";

						if (camp is CampSimple campSimple)
						{
							campSimple.IsNeutralCamp = true;
							campSimple.TeamId = campCount;
						}

						_unitsContainer.AddChild(camp);
						campPositions.Add(worldPos);
					}
				}
			}
		}

		GD.Print($"{campCount} camps générés");
	}

	private bool IsFarEnoughFromCamps(Vector2 position, List<Vector2> existingCamps)
	{
		foreach (Vector2 campPos in existingCamps)
		{
			if (position.DistanceTo(campPos) < MinCampDistance)
				return false;
		}
		return true;
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

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			_territoryManager?.QueueFree();
			_territoryManager = null;
			SetupNoise();
			GenererMap();
			CallDeferred(nameof(InitTerritory));
		}
	}
}
