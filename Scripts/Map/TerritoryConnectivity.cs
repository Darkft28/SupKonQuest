using Godot;
using System.Collections.Generic;

/// <summary>
/// Construit et interroge le graphe de connectivité des territoires.
/// Deux territoires sont "voisins" si des tuiles terrestres adjacentes appartiennent à des zones différentes.
/// </summary>
public static class TerritoryConnectivity
{
    /// <summary>
    /// Construit le graphe de voisinage des territoires.
    /// Pour chaque paire de tuiles adjacentes (4-voisinage) ayant des IDs territoire différents (>0)
    /// et toutes deux terrestres (sol != eau, sol != vide), on enregistre la connexion dans les deux sens.
    /// </summary>
    public static Dictionary<int, HashSet<int>> Build(
        int[,] territoryGrid,
        TileMapLayer solLayer,
        int halfWidth,
        int halfHeight)
    {
        var graph = new Dictionary<int, HashSet<int>>();

        if (territoryGrid == null) return graph;

        int gridW = territoryGrid.GetLength(0);
        int gridH = territoryGrid.GetLength(1);

        // 4-voisinage : droite et bas seulement (évite de traiter chaque paire deux fois)
        int[] dx = { 1, 0 };
        int[] dy = { 0, 1 };

        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                int idA = territoryGrid[x, y];
                if (idA <= 0) continue;

                // Vérifier que la tuile A est terrestre
                if (!IsTerrestrialTile(solLayer, x - halfWidth, y - halfHeight))
                    continue;

                for (int dir = 0; dir < 2; dir++)
                {
                    int nx = x + dx[dir];
                    int ny = y + dy[dir];

                    if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH) continue;

                    int idB = territoryGrid[nx, ny];
                    if (idB <= 0 || idB == idA) continue;

                    // Vérifier que la tuile B est terrestre
                    if (!IsTerrestrialTile(solLayer, nx - halfWidth, ny - halfHeight))
                        continue;

                    // Enregistrer la connexion dans les deux sens
                    if (!graph.TryGetValue(idA, out var neighborsA))
                        graph[idA] = neighborsA = new HashSet<int>();
                    neighborsA.Add(idB);

                    if (!graph.TryGetValue(idB, out var neighborsB))
                        graph[idB] = neighborsB = new HashSet<int>();
                    neighborsB.Add(idA);
                }
            }
        }

        GD.Print($"[TERRITOIRE] Graphe de connectivité : {graph.Count} territoire(s) avec voisins.");
        return graph;
    }

    /// <summary>
    /// Vérifie si toRegion est atteignable depuis fromRegion par le graphe (BFS transitif).
    /// </summary>
    public static bool AreConnected(Dictionary<int, HashSet<int>> graph, int fromRegion, int toRegion)
    {
        if (graph == null) return false;
        if (fromRegion == toRegion) return true;
        if (!graph.ContainsKey(fromRegion)) return false;

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(fromRegion);
        visited.Add(fromRegion);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!graph.TryGetValue(current, out var neighbors)) continue;

            foreach (int neighbor in neighbors)
            {
                if (neighbor == toRegion) return true;
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    // Retourne true si la tuile (en coordonnées monde-tilemap, pas grille) est terrestre
    // (non eau, non vide)
    private static bool IsTerrestrialTile(TileMapLayer solLayer, int tileX, int tileY)
    {
        if (solLayer == null) return true; // fallback : on suppose terrestre si pas de couche
        int solId = solLayer.GetCellSourceId(new Vector2I(tileX, tileY));
        // 6 = eau, -1 = vide
        return solId != 6 && solId != -1;
    }
}
