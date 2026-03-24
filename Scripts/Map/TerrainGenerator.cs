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

	// Crée les alternatives de flip (1=FlipH, 2=FlipV, 3=FlipH+V) pour chaque source terrain.
	// Idempotent : vérifie avant de créer. À appeler avant toute génération.
	public static void InitTileVariants(TileMapLayer sol)
	{
		var tileSet = sol.TileSet;
		if (tileSet == null) return;

		int[] terrainSources = { IdHerbe, IdSable, IdForet, IdRoche, IdNeige, IdEau };
		var atlasCoords = new Vector2I(0, 0);

		foreach (int sourceId in terrainSources)
		{
			if (tileSet.GetSource(sourceId) is not TileSetAtlasSource src) continue;

			if (!src.HasAlternativeTile(atlasCoords, 1))
			{
				src.CreateAlternativeTile(atlasCoords, 1);
				src.GetTileData(atlasCoords, 1).FlipH = true;
			}
			if (!src.HasAlternativeTile(atlasCoords, 2))
			{
				src.CreateAlternativeTile(atlasCoords, 2);
				src.GetTileData(atlasCoords, 2).FlipV = true;
			}
			if (!src.HasAlternativeTile(atlasCoords, 3))
			{
				src.CreateAlternativeTile(atlasCoords, 3);
				var td = src.GetTileData(atlasCoords, 3);
				td.FlipH = true;
				td.FlipV = true;
			}
		}
	}

	// Hash déterministe par position → variante 0-3 (FlipH/FlipV combinés).
	// N'utilise pas le Random → déterminisme multijoueur garanti.
	public static int PickAlt(int x, int y)
	{
		unchecked
		{
			uint h = (uint)(x * 1664525 + y * 22695477 + 1013904223);
			h ^= h >> 14;
			return (int)(h & 3);
		}
	}

	public static void SpawnObjectSprite(Node2D container, int objetId, int tx, int ty, int tileSize)
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
		sprite.Position = new Vector2(tx * tileSize + tileSize / 2f, ty * tileSize + tileSize / 2f);
		// Arbre x5, Montagne x10 (relatif à la tuile de 128px)
		float scale = objetId == IdObjetArbre ? 2.5f : 10f;
		sprite.Scale = new Vector2(scale, scale);
		sprite.ZIndex = 5;
		sprite.ZAsRelative = false;
		sprite.YSortEnabled = false;
		container.AddChild(sprite);
	}
}
