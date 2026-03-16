using Godot;
using System;
using System.Collections.Generic;

public static class CampPlacer
{
	// IDs des tuiles
	private const int IdEau = 6;
	private const int IdHerbe = 0;
	private const int IdObjetCamp = 102;
	private const int IdObjetCampUp = 103;

	// Distance min entre camps (en pixels)
	private const float MinCampDistance = 3500f;

	// Marge en tuiles depuis les bords de la map
	private const int EdgeMargin = 15;

	public static int PlaceCamps(TileMapLayer sol, TileMapLayer objets,
		FastNoiseLite noiseElevation, FastNoiseLite noiseForet, Random seededRandom,
		Node2D unitsContainer, PackedScene campScene, PackedScene campUpScene,
		int halfWidth, int halfHeight, int tileSize, bool testMode, int maxCamps = 0,
		float[] armAngles = null)
	{
		int campCount = 0;
		var campPositions = new List<Vector2>();

		for (int x = -halfWidth + EdgeMargin; x < halfWidth - EdgeMargin; x++)
		{
			if (maxCamps > 0 && campPositions.Count >= maxCamps) break;
			for (int y = -halfHeight + EdgeMargin; y < halfHeight - EdgeMargin; y++)
			{
				if (maxCamps > 0 && campPositions.Count >= maxCamps) break;
				float altitude = noiseElevation.GetNoise2D(x, y);
				float densiteArbre = noiseForet.GetNoise2D(x, y);

				// Seul le biome herbe (pas foret, montagne, neige) peut avoir des camps
				if (altitude >= -0.15f && altitude < 0.4f && densiteArbre <= 0.2f)
				{
					// Vérification supplémentaire : pas d'objet (arbre/montagne) sur cette tuile
					int existingObj = objets.GetCellSourceId(new Vector2I(x, y));
					if (existingObj == 100 || existingObj == 101) continue;

					if (!testMode && seededRandom.NextDouble() < 0.001)
					{
						if (IsNearTilemapWater(x, y, sol)) continue;
						Vector2 candidatePos = new Vector2(x * tileSize + tileSize / 2, y * tileSize + tileSize / 2);
						if (IsFarEnoughFromCamps(candidatePos, campPositions))
						{
							bool spawnCampUp = false;
							int objetId;
							if (seededRandom.NextDouble() < 0.2)
							{
								spawnCampUp = true;
								objetId = IdObjetCampUp;
							}
							else
							{
								objetId = IdObjetCamp;
							}

							objets.SetCell(new Vector2I(x, y), objetId, new Vector2I(0, 0));

							if (!Engine.IsEditorHint() && unitsContainer != null)
							{
								PackedScene campSceneToUse = spawnCampUp ? campUpScene : campScene;
								if (campSceneToUse != null)
								{
									var camp = campSceneToUse.Instantiate<Node2D>();
									Vector2 worldPos = new Vector2(x * tileSize + tileSize / 2, y * tileSize + tileSize / 2);
									camp.GlobalPosition = worldPos;
									camp.Name = $"Camp_{campCount++}";

									if (camp is CampSimple campSimple)
									{
										campSimple.IsNeutralCamp = true;
										campSimple.TeamId = campCount;
										campSimple.RegionId = GetRegionId(worldPos, armAngles, tileSize);
										campSimple.SetTileMapSol(sol);
										campSimple.SetTileMapObjets(objets);
									}

									unitsContainer.AddChild(camp);
									campPositions.Add(worldPos);
								}
							}
						}
						else
						{
							// garder le Random synchronisé
							seededRandom.NextDouble();
						}
					}
				}
			}
		}

		if (testMode && !Engine.IsEditorHint() && unitsContainer != null && campScene != null)
		{
			campCount = SpawnTestModeCamps(sol, unitsContainer, campScene, campCount);
		}

		return campCount;
	}

	// Vérifie si une tuile ou ses voisins proches sont de l'eau (naturelle ou pizza)
	private static bool IsNearTilemapWater(int x, int y, TileMapLayer sol, int radius = 4)
	{
		for (int dx = -radius; dx <= radius; dx++)
			for (int dy = -radius; dy <= radius; dy++)
				if (dx * dx + dy * dy <= radius * radius)
					if (sol.GetCellSourceId(new Vector2I(x + dx, y + dy)) == IdEau)
						return true;
		return false;
	}

	private static bool IsFarEnoughFromCamps(Vector2 position, List<Vector2> existingCamps)
	{
		foreach (Vector2 campPos in existingCamps)
		{
			if (position.DistanceTo(campPos) < MinCampDistance)
				return false;
		}
		return true;
	}

	private static int SpawnTestModeCamps(TileMapLayer sol, Node2D unitsContainer, PackedScene campScene, int campCount)
	{
		Vector2 camp1Pos = new Vector2(0, -800);
		Vector2 camp2Pos = new Vector2(0, 800);

		var camp1 = campScene.Instantiate<Node2D>();
		camp1.GlobalPosition = camp1Pos;
		camp1.Name = $"Camp_{campCount++}";
		if (camp1 is CampSimple cs1)
		{
			cs1.IsNeutralCamp = true;
			cs1.TeamId = campCount;
			cs1.RegionId = 1; // zone Ouest
			cs1.SetTileMapSol(sol);
		}
		unitsContainer.AddChild(camp1);

		var camp2 = campScene.Instantiate<Node2D>();
		camp2.GlobalPosition = camp2Pos;
		camp2.Name = $"Camp_{campCount++}";
		if (camp2 is CampSimple cs2)
		{
			cs2.IsNeutralCamp = true;
			cs2.TeamId = campCount;
			cs2.RegionId = 3; // zone Est
			cs2.SetTileMapSol(sol);
		}
		unitsContainer.AddChild(camp2);

		return campCount;
	}

	// Assigne une région (1, 2 ou 3) selon le secteur angulaire du camp par rapport au centre
	private static int GetRegionId(Vector2 worldPos, float[] armAngles, int tileSize)
	{
		if (armAngles == null) return 1;

		float tx = worldPos.X / tileSize;
		float ty = worldPos.Y / tileSize;

		float angle = (float)Math.Atan2(ty, tx);
		if (angle < 0) angle += (float)(Math.PI * 2.0); // normaliser vers [0, 2π)

		// Trier les angles des bras dans [0, 2π)
		float[] a = new float[3];
		for (int i = 0; i < 3; i++)
		{
			a[i] = armAngles[i] % (float)(Math.PI * 2.0);
			if (a[i] < 0) a[i] += (float)(Math.PI * 2.0);
		}
		System.Array.Sort(a);

		// Secteur 1 : entre a[0] et a[1], secteur 2 : entre a[1] et a[2], secteur 3 : le reste
		if (angle >= a[0] && angle < a[1]) return 1;
		if (angle >= a[1] && angle < a[2]) return 2;
		return 3;
	}
}
