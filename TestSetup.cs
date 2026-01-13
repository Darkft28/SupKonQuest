using Godot;
using System;

public partial class TestSetup : Node
{
	// --- RÉFÉRENCES ---
	private TileMapLayer _tileMap;
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

	// Outils de génération
	private FastNoiseLite _noiseElevation = new FastNoiseLite();
	private FastNoiseLite _noiseForet = new FastNoiseLite();

	public override void _Ready()
	{
		// Récupération des noeuds enfants (Attention aux noms exacts dans la scène !)
		_tileMap = GetNode<TileMapLayer>("Sol");
		_camera = GetNode<Camera2D>("Camera2D");

		// Config de la caméra
		if (_camera != null)
		{
			_camera.Zoom = new Vector2(0.5f, 0.5f);
			_camera.Position = new Vector2(MapWidth * 8, MapHeight * 8);
		}

		SetupNoise();
		GenererMap();
		GD.Print("Map C# générée ! Appuyez sur ESPACE pour régénérer.");
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

		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				float altitude = _noiseElevation.GetNoise2D(x, y);
				float densiteArbre = _noiseForet.GetNoise2D(x, y);

				int sourceIdChoisi = -1;

				// Logique de biome
				if (altitude < -0.2f)
				{
					sourceIdChoisi = IdEau;
				}
				else if (altitude < -0.15f)
				{
					sourceIdChoisi = IdSable;
				}
				else if (altitude < 0.4f)
				{
					// Herbe ou Forêt ?
					if (densiteArbre > 0.2f)
						sourceIdChoisi = IdForet;
					else
						sourceIdChoisi = IdHerbe;
				}
				else if (altitude < 0.6f)
				{
					sourceIdChoisi = IdRoche;
				}
				else
				{
					sourceIdChoisi = IdNeige;
				}

				// Placement de la tuile
				// Note: Vector2I est nécessaire pour les coordonnées de grille en C# Godot 4
				_tileMap.SetCell(new Vector2I(x, y), sourceIdChoisi, new Vector2I(0, 0));
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
			if (_camera == null) return;

			if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
			{
				_camera.Zoom += new Vector2(0.1f, 0.1f);
			}
			else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
			{
				if (_camera.Zoom.X > 0.1f)
					_camera.Zoom -= new Vector2(0.1f, 0.1f);
			}
		}
	}
}
