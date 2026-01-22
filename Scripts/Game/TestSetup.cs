using Godot;
using System;
using SupKonQuest;

// Génération procédurale de la map avec biomes et objets
[Tool]
public partial class TestSetup : Node
{
	// --- RÉFÉRENCES ---
	private TileMapLayer _tileMapSol;
	private TileMapLayer _tileMapObjets;
	private Camera2D _camera;
	private Node2D _unitsContainer;
	private SelectionManager _selectionManager;

	private const int MapWidth = 256;
	private const int MapHeight = 256;
	private const int HalfWidth = MapWidth / 2;
	private const int HalfHeight = MapHeight / 2;
	private const int TileSize = 128;

	// IDs des tuiles (correspondant au TileSet)
	private const int IdEau = 6;
	private const int IdSable = 1;
	private const int IdHerbe = 0;
	private const int IdForet = 3;
	private const int IdRoche = 5;
	private const int IdNeige = 4;

	private const int IdObjetArbre = 100;
	private const int IdObjetMontagne = 101;

	// Textures pour les camps (chargées dynamiquement)
	private Texture2D _textureCamp;
	private Texture2D _textureCampUp;

	// Seed réseau pour synchronisation multijoueur
	private int? _networkSeed = null;

	private FastNoiseLite _noiseElevation = new FastNoiseLite();
	private FastNoiseLite _noiseForet = new FastNoiseLite();
	private Random _seededRandom;

	public override void _Ready()
	{
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		// Charger les textures des camps
		_textureCamp = GD.Load<Texture2D>("res://Assets/Objects/Camps.png");
		_textureCampUp = GD.Load<Texture2D>("res://Assets/Objects/Camps_UP.png");

		// Créer ou récupérer le conteneur d'unités
		_unitsContainer = GetNodeOrNull<Node2D>("Units");
		if (_unitsContainer == null && !Engine.IsEditorHint())
		{
			_unitsContainer = new Node2D();
			_unitsContainer.Name = "Units";
			AddChild(_unitsContainer);
		}

		// Créer le SelectionManager
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

		// Vérifier si on a une seed réseau depuis GameState
		if (!Engine.IsEditorHint())
		{
			var gameState = GetNodeOrNull<GameState>("/root/GameState");
			if (gameState != null && gameState.MapSeed != 0)
			{
				_networkSeed = gameState.MapSeed;
				GD.Print($"Seed réseau détectée: {_networkSeed}");
			}
		}

		if (_tileMapSol.GetUsedCells().Count == 0)
		{
			SetupNoise();
			GenererMap();
			GD.Print("Map générée. Appuyez sur ESPACE pour régénérer.");
		}
	}

	/// <summary>
	/// Définit la seed pour la génération de map (utilisé pour la synchronisation réseau)
	/// </summary>
	public void SetSeed(int seed)
	{
		_networkSeed = seed;
		GD.Print($"Seed définie: {seed}");
	}

	// Propriété exportée pour générer la map depuis l'éditeur
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

	// Initialise les références et génère la map (utilisé en mode Tool)
	private void InitialiserEtGenerer()
	{
		if (_tileMapSol == null) _tileMapSol = GetNode<TileMapLayer>("Sol");
		if (_tileMapObjets == null) _tileMapObjets = GetNode<TileMapLayer>("Objets");

		SetupNoise();
		GenererMap();

		GD.Print("Map générée dans l'éditeur.");
	}

	// Configure les paramètres de bruit pour la génération
	private void SetupNoise()
	{
		// Utiliser la seed réseau si disponible, sinon en générer une aléatoire
		int baseSeed = _networkSeed ?? (int)GD.Randi();

		_noiseElevation.Seed = baseSeed;
		_noiseElevation.Frequency = 0.008f;
		_noiseElevation.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseElevation.FractalOctaves = 5;

		// Bruit pour la forêt (dérivé de la seed de base pour être déterministe)
		_noiseForet.Seed = baseSeed + 1000;
		_noiseForet.Frequency = 0.05f;

		// Générateur aléatoire seedé pour le placement des objets
		_seededRandom = new Random(baseSeed + 2000);

		GD.Print($"Noise initialisé avec seed: {baseSeed}");
	}

	// Génère la map en parcourant toutes les tuiles
	private void GenererMap()
	{
		GD.Print("Génération en cours...");

		_tileMapSol.Clear();
		_tileMapObjets.Clear();

		// Nettoyer les unités existantes
		if (_unitsContainer != null)
		{
			foreach (Node child in _unitsContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		int campCount = 0;

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

				// Détermination du biome selon l'altitude
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
							spawnCamp = true;
							if (_seededRandom.NextDouble() < 0.2)
							{
								spawnCampUp = true;
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

				// Spawn les camps comme unités (pas en mode éditeur)
				if ((spawnCamp || spawnCampUp) && !Engine.IsEditorHint() && _unitsContainer != null)
				{
					Texture2D texture = spawnCampUp ? _textureCampUp : _textureCamp;
					Vector2 worldPos = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
					var camp = CampUnit.CreateInstance(texture, worldPos);
					camp.Name = $"Camp_{campCount++}";
					_unitsContainer.AddChild(camp);
				}
			}
		}

		GD.Print($"Camps générés: {campCount}");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			SetupNoise();
			GenererMap();
		}
	}
}
