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

	public static int PlaceCamps(TileMapLayer sol, TileMapLayer objets,
		FastNoiseLite noiseElevation, FastNoiseLite noiseForet, Random seededRandom,
		Node2D unitsContainer, PackedScene campScene, PackedScene campUpScene,
		int halfWidth, int halfHeight, int tileSize, bool testMode)
	{
		int campCount = 0;
		var campPositions = new List<Vector2>();

		for (int x = -halfWidth; x < halfWidth; x++)
		{
			for (int y = -halfHeight; y < halfHeight; y++)
			{
				float altitude = noiseElevation.GetNoise2D(x, y);
				float densiteArbre = noiseForet.GetNoise2D(x, y);

				// Seul le biome herbe (pas foret) peut avoir des camps
				if (altitude >= -0.15f && altitude < 0.4f && densiteArbre <= 0.2f)
				{
					if (!testMode && seededRandom.NextDouble() < 0.001)
					{
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
									}

									unitsContainer.AddChild(camp);
									campPositions.Add(worldPos);

									if (camp is CampSimple campPort)
									{
										campPort.TrySpawnPort(sol);
									}
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

		// Mode test : spawn 2 camps proches au centre de la map
		if (testMode && !Engine.IsEditorHint() && unitsContainer != null && campScene != null)
		{
			campCount = SpawnTestModeCamps(sol, unitsContainer, campScene, campCount);
		}

		return campCount;
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
		}
		unitsContainer.AddChild(camp1);
		if (camp1 is CampSimple csPort1)
		{
			csPort1.TrySpawnPort(sol);
		}

		var camp2 = campScene.Instantiate<Node2D>();
		camp2.GlobalPosition = camp2Pos;
		camp2.Name = $"Camp_{campCount++}";
		if (camp2 is CampSimple cs2)
		{
			cs2.IsNeutralCamp = true;
			cs2.TeamId = campCount;
		}
		unitsContainer.AddChild(camp2);
		if (camp2 is CampSimple csPort2)
		{
			csPort2.TrySpawnPort(sol);
		}

		GD.Print("MODE TEST: 2 camps spawnes au centre de la map");
		return campCount;
	}
}
