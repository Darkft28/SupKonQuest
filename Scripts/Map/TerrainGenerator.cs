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

	public static void Generate(TileMapLayer sol, TileMapLayer objets,
		FastNoiseLite noiseElevation, FastNoiseLite noiseForet, Random seededRandom,
		int halfWidth, int halfHeight, int tileSize, bool testMode)
	{
		for (int x = -halfWidth; x < halfWidth; x++)
		{
			for (int y = -halfHeight; y < halfHeight; y++)
			{
				float altitude = noiseElevation.GetNoise2D(x, y);
				float densiteArbre = noiseForet.GetNoise2D(x, y);

				int solId = -1;
				int objetId = -1;

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

				Vector2I coords = new Vector2I(x, y);

				if (solId != -1)
				{
					sol.SetCell(coords, solId, new Vector2I(0, 0));
				}

				if (objetId != -1)
				{
					objets.SetCell(coords, objetId, new Vector2I(0, 0));
				}
			}
		}
	}
}
