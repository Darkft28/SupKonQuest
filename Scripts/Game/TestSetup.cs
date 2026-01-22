using Godot;
using System;

/// <summary>
/// Génération procédurale de la map avec biomes et objets.
/// </summary>
[Tool]
public partial class TestSetup : Node
{
	private TileMapLayer _tileMapSol;
	private TileMapLayer _tileMapObjets;
	private Camera2D _camera;

	private const int MapWidth = 256;
	private const int MapHeight = 256;
	private const int HalfWidth = MapWidth / 2;
	private const int HalfHeight = MapHeight / 2;

	// IDs des tuiles (correspondant au TileSet)
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

	private FastNoiseLite _noiseElevation = new FastNoiseLite();
	private FastNoiseLite _noiseForet = new FastNoiseLite();

	public override void _Ready()
	{
		_tileMapSol = GetNode<TileMapLayer>("Sol");
		_tileMapObjets = GetNode<TileMapLayer>("Objets");
		_camera = GetNode<Camera2D>("Camera2D");

		if (_camera != null)
		{
			_camera.Zoom = new Vector2(0.25f, 0.25f);
			_camera.Position = Vector2.Zero;
		}

		if (_tileMapSol.GetUsedCells().Count == 0)
		{
			SetupNoise();
			GenererMap();
			GD.Print("Map générée. Appuyez sur ESPACE pour régénérer.");
		}
	}

	/// <summary>
	/// Propriété exportée pour générer la map depuis l'éditeur.
	/// </summary>
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

	/// <summary>
	/// Initialise les références et génère la map (utilisé en mode Tool).
	/// </summary>
	private void InitialiserEtGenerer()
	{
		if (_tileMapSol == null) _tileMapSol = GetNode<TileMapLayer>("Sol");
		if (_tileMapObjets == null) _tileMapObjets = GetNode<TileMapLayer>("Objets");

		SetupNoise();
		GenererMap();

		GD.Print("Map générée dans l'éditeur.");
	}

	/// <summary>
	/// Configure les paramètres de bruit pour la génération.
	/// </summary>
	private void SetupNoise()
	{
		_noiseElevation.Seed = (int)GD.Randi();
		_noiseElevation.Frequency = 0.008f;
		_noiseElevation.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseElevation.FractalOctaves = 5;

		_noiseForet.Seed = (int)GD.Randi();
		_noiseForet.Frequency = 0.05f;
	}

	/// <summary>
	/// Génère la map en parcourant toutes les tuiles.
	/// </summary>
	private void GenererMap()
	{
		GD.Print("Génération en cours...");

		_tileMapSol.Clear();
		_tileMapObjets.Clear();

		for (int x = -HalfWidth; x < HalfWidth; x++)
		{
			for (int y = -HalfHeight; y < HalfHeight; y++)
			{
				float altitude = _noiseElevation.GetNoise2D(x, y);
				float densiteArbre = _noiseForet.GetNoise2D(x, y);

				int solId = -1;
				int objetId = -1;

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
							objetId = IdObjetArbre;
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
						objetId = IdObjetMontagne;
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
			}
		}
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
