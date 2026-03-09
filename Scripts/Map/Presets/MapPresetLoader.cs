using Godot;

namespace SupKonQuest.Map.Presets;

public static class MapPresetLoader
{
    // Décode le RLE et applique les tuiles au TileMapLayer
    // rleData : tableau de paires {count, tileId, count, tileId, ...}
    // skipId  : ID à ignorer (ne pas placer — pour Objets, ignorer -1)
    public static void ApplyToTileMap(TileMapLayer layer, int[] rleData,
        int width, int height, int skipId = -1)
    {
        int x = 0, y = 0;

        for (int i = 0; i < rleData.Length - 1; i += 2)
        {
            int count  = rleData[i];
            int tileId = rleData[i + 1];

            for (int j = 0; j < count; j++)
            {
                if (x >= width) { x = 0; y++; }
                if (y >= height) return;

                if (tileId != skipId)
                    layer.SetCell(new Vector2I(x, y), tileId, Vector2I.Zero);

                x++;
            }
        }
    }
}
