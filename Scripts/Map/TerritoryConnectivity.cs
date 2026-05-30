using Godot;
using System.Collections.Generic;

/// <summary>
/// Builds and queries the territory connectivity graph.
/// Two territories are "neighbors"if adjacent land tiles belong to different zones.
/// </summary>
public static class TerritoryConnectivity
{
    /// <summary>
    /// Builds the territory adjacency graph.
    /// For each adjacent tile pair (4-neighborhood) with different territory IDs (>0)
    /// and both on land (ground != water, ground != empty), record a bidirectional edge.
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

        // 4-neighborhood: right and down only (avoids processing each pair twice)
        int[] dx = { 1, 0 };
        int[] dy = { 0, 1 };

        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                int idA = territoryGrid[x, y];
                if (idA <= 0) continue;

                // Check that tile A is land
                if (!IsTerrestrialTile(solLayer, x - halfWidth, y - halfHeight))
                    continue;

                for (int dir = 0; dir < 2; dir++)
                {
                    int nx = x + dx[dir];
                    int ny = y + dy[dir];

                    if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH) continue;

                    int idB = territoryGrid[nx, ny];
                    if (idB <= 0 || idB == idA) continue;

                    // Check that tile B is land
                    if (!IsTerrestrialTile(solLayer, nx - halfWidth, ny - halfHeight))
                        continue;

                    // Record the connection in both directions
                    if (!graph.TryGetValue(idA, out var neighborsA))
                        graph[idA] = neighborsA = new HashSet<int>();
                    neighborsA.Add(idB);

                    if (!graph.TryGetValue(idB, out var neighborsB))
                        graph[idB] = neighborsB = new HashSet<int>();
                    neighborsB.Add(idA);
                }
            }
        }

        GD.Print($"[TERRITOIRE] Connectivity graph: {graph.Count} territory node(s) with neighbors.");
        return graph;
    }

    /// <summary>
    /// Checks whether toRegion is reachable from fromRegion in the graph (transitive BFS).
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

    /// <summary>
    /// Returns all land regions reachable from owned regions (transitive BFS).
    /// </summary>
    public static HashSet<int> GetReachableRegions(Dictionary<int, HashSet<int>> graph, IEnumerable<int> ownedRegions)
    {
        var reachable = new HashSet<int>();
        if (graph == null || ownedRegions == null)
            return reachable;

        var queue = new Queue<int>();
        foreach (int region in ownedRegions)
        {
            if (region <= 0)
                continue;
            if (reachable.Add(region))
                queue.Enqueue(region);
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!graph.TryGetValue(current, out var neighbors))
                continue;

            foreach (int neighbor in neighbors)
            {
                if (reachable.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return reachable;
    }

    /// <summary>
    /// Checks whether targetRegion is in the connected component of owned regions.
    /// </summary>
    public static bool IsReachable(Dictionary<int, HashSet<int>> graph, IEnumerable<int> ownedRegions, int targetRegion)
    {
        if (targetRegion <= 0)
            return true;

        return GetReachableRegions(graph, ownedRegions).Contains(targetRegion);
    }

    // Returns true if tile (world-tilemap coordinates, not grid) is land
    // (non-water, non-empty)
    private static bool IsTerrestrialTile(TileMapLayer solLayer, int tileX, int tileY)
    {
        if (solLayer == null) return true; // fallback: assume land when no layer is available
        int solId = solLayer.GetCellSourceId(new Vector2I(tileX, tileY));
        // 6 = water, -1 = empty
        return solId != 6 && solId != -1;
    }
}
