using Godot;
using System;

[Tool]
public partial class TestSetup : Node
{
	// --- RÉFÉRENCES ---
	private TileMapLayer _tileMapSol;  
	private TileMapLayer _tileMapObjets;
	private Camera2D _camera;

	// --- CONFIGURATION ---
	// Taille de la map (256x256 tuiles)
	private const int MapWidth = 256;
	private const int MapHeight = 256;

	// --- IDs DES TUILES (A vérifier dans ton TileSet !) ---
	// Assure-toi que ces IDs correspondent à la liste "Source ID" dans ton TileSet
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
	

	// Outils de génération
	private FastNoiseLite _noiseElevation = new FastNoiseLite();
	private FastNoiseLite _noiseForet = new FastNoiseLite();

	public override void _Ready()
	{
		// Récupération des noeuds enfants (Attention aux noms exacts dans la scène !)
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		// Config de la caméra
		if (_camera != null)
		{
			_camera.Zoom = new Vector2(0.25f, 0.25f);
			_camera.Position = new Vector2(MapWidth * 64, MapHeight * 64);
		}

		
		if (_tileMapSol.GetUsedCells().Count == 0)
		{
			SetupNoise();
			GenererMap();
			GD.Print("Map C# générée ! Appuyez sur ESPACE pour régénérer.");
		}
		
	}
	
	[Export]
	public bool GenererMapMaintenant
	{
		get => false;
		set
		{
			if (value)
			{
				// Appelle ta fonction de génération ici
				// Attention : Il faut s'assurer que _tileMap est bien assigné avant !
				InitialiserEtGenerer(); 
			}
		}
	}

	// Crée une fonction intermédiaire pour s'assurer que tout est prêt
	private void InitialiserEtGenerer()
	{
		// En mode Tool, _Ready n'est pas toujours appelé comme on pense,
		// donc on force la récupération du noeud si nécessaire.
		if (_tileMapSol == null) _tileMapSol = GetNode<TileMapLayer>("Sol");
		if (_tileMapObjets == null) _tileMapObjets = GetNode<TileMapLayer>("Objets");
		
		SetupNoise(); // Tes configs de bruit
		GenererMap(); // Ta boucle de génération
		
		
		
		GD.Print("Map générée dans l'éditeur ! N'oublie pas de sauvegarder (Ctrl+S).");
	}

	private void SetupNoise()
	{
		// Bruit pour l'altitude
		_noiseElevation.Seed = (int)GD.Randi();
		_noiseElevation.Frequency = 0.008f;
		_noiseElevation.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseElevation.FractalOctaves = 5;

		// Bruit pour la forêt
		_noiseForet.Seed = (int)GD.Randi();
		_noiseForet.Frequency = 0.05f;
	}

	private void GenererMap()
	{
		GD.Print("Génération C# en cours...");
		
		_tileMapSol.Clear();
		_tileMapObjets.Clear();

		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				float altitude = _noiseElevation.GetNoise2D(x, y);
				float densiteArbre = _noiseForet.GetNoise2D(x, y);

				int solId = -1;
				int objetId = -1;

				// Logique de biome
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
					// Herbe ou Forêt ?
					if (densiteArbre > 0.2f)
					{
						solId = IdForet;
						if (densiteArbre > 0.3f)
						{
							if (GD.Randf() < 0.25f) 
							{
								objetId = IdObjetArbre;
							}
						}
					}
					else
					{
						solId = IdHerbe;
						if (GD.Randf() < 0.001f) 
						{
							objetId = IdObjetCamp;
							if (GD.Randf() < 0.2f) 
							{
								objetId = IdObjetCampUp;
							}
						}
					}
				}
				else if (altitude < 0.55f)
				{
					solId = IdRoche;
					if (altitude < 0.66f)
					{
						if (GD.Randf() < 0.25f) 
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

				// 1. On pose le sol
				if (solId != -1)
				{
					_tileMapSol.SetCell(coords, solId, new Vector2I(0, 0));
				}

				// 2. On pose l'objet (s'il y en a un)
				if (objetId != -1)
				{
					_tileMapObjets.SetCell(coords, objetId, new Vector2I(0, 0));
				}
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Touche ESPACE pour régénérer
		if (@event.IsActionPressed("ui_accept"))
		{
			SetupNoise();
			GenererMap();
		}

		// Zoom Molette
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (_camera == null)
			{
				return;
			}

			if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
			{
				_camera.Zoom += new Vector2(0.1f, 0.1f);
			}
			else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
			{
				if (_camera.Zoom.X > 0.1f)
				{
					_camera.Zoom -= new Vector2(0.1f, 0.1f);
				}
			}
		}
	}
}
