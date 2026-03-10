using Godot;
using System;

public static class TerrainGenerator
{
	// IDs des tuiles
	private const int IdEau = 6;
	private const int IdSable = 1;
	private const int IdHerbe = 0;
	private const int IdForet = 3;
	private const int IdRoche = 5;
	private const int IdNeige = 4;

	private const int IdObjetArbre = 100;
	private const int IdObjetMontagne = 101;

	private static Texture2D _texTree;
	private static Texture2D _texMontagne;

	public static void Generate(TileMapLayer sol, TileMapLayer objets, Node2D objectsContainer,
		FastNoiseLite noiseElevation, FastNoiseLite noiseForet, Random seededRandom,
		int halfWidth, int halfHeight, int tileSize, bool testMode,
		out float[] armAngles, out float armHalfWidth, out float centralRadius)
	{
		// Calculer les bras pizza EN PREMIER pour préserver le déterminisme du RNG
		ComputePizzaArms(seededRandom, out armAngles, out armHalfWidth, out centralRadius,
			out float[][] armStraits);

		float[] _armAngles = armAngles;
		float _armHW = armHalfWidth;
		float _centralRadius = centralRadius;
		float[][] _armStraits = armStraits;

		for (int x = -halfWidth; x < halfWidth; x++)
		{
			for (int y = -halfHeight; y < halfHeight; y++)
			{
				float altitude = noiseElevation.GetNoise2D(x, y);
				float densiteArbre = noiseForet.GetNoise2D(x, y);

				int solId = -1;
				int objetId = -1;

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
							if (seededRandom.NextDouble() < 0.25)
							{
								objetId = IdObjetArbre;
							}
						}
					}
					else
					{
						solId = IdHerbe;
						// Consommer le random pour garder le RNG synchronise
						// (le placement de camps est gere par CampPlacer)
						if (!testMode && seededRandom.NextDouble() < 0.001)
						{
							// Consommer le 2eme random (etait pour camp up check)
							seededRandom.NextDouble();
						}
					}
				}
				else if (altitude < 0.55f)
				{
					solId = IdRoche;
					if (altitude < 0.66f)
					{
						if (seededRandom.NextDouble() < 0.25)
						{
							objetId = IdObjetMontagne;
						}
					}

				}
				else
				{
					solId = IdNeige;
				}

				// Masque eau forcé pour les bras pizza (appliqué après les tirages normaux)
				if (IsInPizzaWater(x, y, _armAngles, _armHW, _centralRadius, _armStraits, noiseElevation))
				{
					solId = IdEau;
					objetId = -1;
				}

				Vector2I coords = new Vector2I(x, y);

				if (solId != -1)
				{
					sol.SetCell(coords, solId, new Vector2I(0, 0));
				}

				if (objetId != -1)
				{
					objets.SetCell(coords, objetId, new Vector2I(0, 0));
					SpawnObjectSprite(objectsContainer, objetId, x, y, tileSize);
				}
			}
		}
	}

	// Calcule les 3 bras radiaux (parts de pizza) qui séparent la map en 3 régions
	private static void ComputePizzaArms(Random seededRandom,
		out float[] armAngles, out float armHalfWidth, out float centralRadius,
		out float[][] armStraits)
	{
		// Rotation aléatoire de base (0 à 120°) pour varier la map à chaque partie
		double baseAngle = seededRandom.NextDouble() * Math.PI * 2.0 / 3.0;
		armAngles = new float[3];
		armAngles[0] = (float)baseAngle;
		armAngles[1] = (float)(baseAngle + Math.PI * 2.0 / 3.0);
		armAngles[2] = (float)(baseAngle + Math.PI * 4.0 / 3.0);

		armHalfWidth = 8 + seededRandom.Next(0, 5);    // 8-12 tuiles de demi-largeur
		centralRadius = 12 + seededRandom.Next(0, 8);  // 12-19 tuiles pour le hub central

		// 1 détroit (passage) par bras — 6 tirages au total
		armStraits = new float[3][];
		for (int i = 0; i < 3; i++)
		{
			float t  = (float)(centralRadius + 20 + seededRandom.Next(0, 30)); // position le long du bras
			float hw = 3 + seededRandom.Next(0, 4);                            // demi-largeur du passage (3-6)
			armStraits[i] = new float[] { t, hw };
		}
	}

	private static void SpawnObjectSprite(Node2D container, int objetId, int tx, int ty, int tileSize)
	{
		if (container == null) return;

		string texPath = objetId == IdObjetArbre
			? "res://Assets/Objects/Tree.png"
			: "res://Assets/Objects/montagne.png";

		if (objetId == IdObjetArbre)
			_texTree ??= GD.Load<Texture2D>(texPath);
		else
			_texMontagne ??= GD.Load<Texture2D>(texPath);

		var tex = objetId == IdObjetArbre ? _texTree : _texMontagne;
		if (tex == null) return;

		var sprite = new Sprite2D();
		sprite.Texture = tex;
		// Centre sur la tuile
		sprite.Position = new Vector2(tx * tileSize + tileSize / 2f, ty * tileSize + tileSize / 2f);
		// Arbre x5, Montagne x10
		float scale = objetId == IdObjetArbre ? 5f : 10f;
		sprite.Scale = new Vector2(scale, scale);
		sprite.ZIndex = 1;
		container.AddChild(sprite);
	}

	// Vérifie si une tuile (x,y) est dans l'eau pizza (hub central ou bras radial)
	public static bool IsInPizzaWater(int x, int y,
		float[] armAngles, float armHalfWidth, float centralRadius,
		float[][] armStraits, FastNoiseLite noiseElevation)
	{
		float dist = (float)Math.Sqrt(x * x + y * y);

		// Hub central : petit disque d'eau là où les 3 bras se rejoignent
		if (dist < centralRadius) return true;

		for (int i = 0; i < 3; i++)
		{
			float cos = (float)Math.Cos(armAngles[i]);
			float sin = (float)Math.Sin(armAngles[i]);

			// Domain warp : déformer les coordonnées avec du bruit pour des bords organiques
			float wx = noiseElevation.GetNoise2D(x * 2f + 400f + i * 150f, y * 2f) * 12f;
			float wy = noiseElevation.GetNoise2D(x * 2f, y * 2f + 400f + i * 150f) * 12f;
			float warpedX = x + wx;
			float warpedY = y + wy;

			// Projection le long du bras
			float t = warpedX * cos + warpedY * sin;
			if (t < centralRadius) continue; // seulement au-delà du hub

			// Distance perpendiculaire au bras
			float perp = Math.Abs(warpedY * cos - warpedX * sin);
			if (perp > armHalfWidth) continue;

			// Détroit : passage où l'eau est interrompue
			float straitT  = armStraits[i][0];
			float straitHW = armStraits[i][1];
			if (Math.Abs(t - straitT) < straitHW) return false;

			return true;
		}

		return false;
	}
}
