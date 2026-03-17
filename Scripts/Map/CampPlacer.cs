using Godot;
using System;
using System.Collections.Generic;

public static class CampPlacer
{
	// Spawn les camps depuis une liste de positions pré-définies (maps presets)
	// Les positions sont mélangées aléatoirement pour créer des parties différentes
	public static int PlacePresetCamps(
		System.Collections.Generic.List<Vector2I> campCells,
		TileMapLayer sol, Node2D unitsContainer, PackedScene campScene,
		Random seededRandom, int tileSize, float[] armAngles = null)
	{
		if (Engine.IsEditorHint() || unitsContainer == null || campScene == null)
			return 0;

		// Mélange Fisher-Yates déterministe (même seed = même résultat pour tous les joueurs)
		for (int i = campCells.Count - 1; i > 0; i--)
		{
			int j = seededRandom.Next(i + 1);
			(campCells[i], campCells[j]) = (campCells[j], campCells[i]);
		}

		int campCount = 0;
		foreach (var cell in campCells)
		{
			var camp = campScene.Instantiate<Node2D>();
			Vector2 worldPos = new Vector2(cell.X * tileSize + tileSize / 2f, cell.Y * tileSize + tileSize / 2f);
			camp.GlobalPosition = worldPos;
			camp.Name = $"Camp_{campCount++}";

			if (camp is CampSimple campSimple)
			{
				campSimple.IsNeutralCamp = true;
				campSimple.TeamId = campCount;
				campSimple.RegionId = GetRegionId(worldPos, armAngles, tileSize);
				campSimple.SetTileMapSol(sol);
			}

			unitsContainer.AddChild(camp);
		}

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
