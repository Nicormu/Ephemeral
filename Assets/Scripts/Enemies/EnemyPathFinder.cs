using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared 4-directional BFS grid pathfinder for grounded enemies (see EnemyChaseMovement's
/// _avoidsObstacles toggle). Unweighted — every walkable cell costs the same to enter — which is
/// enough here since rooms are small (13x7) and have no terrain "cost" differences, only
/// walkable/blocked; a full A* would be unnecessary complexity for this grid size.
///
/// Purely static/stateless, same spirit as DungeonGeometry/RoomConnector — one shared
/// implementation instead of every grounded-movement system rolling its own.
/// </summary>
public static class EnemyPathfinder
{
    private static readonly Vector2Int[] Neighbors4 =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

    /// <summary>
    /// Finds the shortest 4-directional path from start to goal, searching only cells inside
    /// [boundsMin, boundsMax) — boundsMin inclusive, boundsMax exclusive, matching
    /// Room.GridPos/Width/Height (i.e. pass boundsMax = GridPos + (Width, Height)).
    ///
    /// isWalkable is queried for every cell EXCEPT start and goal — start because the caller is
    /// already standing there (a knockback or hazard tick could otherwise leave it stuck with no
    /// valid path out), goal because the player's exact cell shouldn't block the enemy from
    /// reaching them (e.g. if a hazard obstacle cell is being treated as blocked for pathing but
    /// the player happens to be standing on one anyway).
    ///
    /// Returns the path as a list of grid cells EXCLUDING start, INCLUDING goal, in travel order.
    /// Returns null if start == goal, goal is outside bounds, or no path exists.
    /// </summary>
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, Vector2Int boundsMin, Vector2Int boundsMax, System.Func<Vector2Int, bool> isWalkable)
    {
        if (start == goal) return null;
        if (!InBounds(goal, boundsMin, boundsMax)) return null;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (var offset in Neighbors4)
            {
                Vector2Int next = current + offset;
                if (visited.Contains(next)) continue;
                if (!InBounds(next, boundsMin, boundsMax)) continue;

                bool isGoal = next == goal;
                if (!isGoal && !isWalkable(next)) continue;

                visited.Add(next);
                cameFrom[next] = current;

                if (isGoal)
                    return ReconstructPath(cameFrom, start, goal);

                queue.Enqueue(next);
            }
        }

        return null; // no path exists with the current obstacle layout
    }

    private static bool InBounds(Vector2Int cell, Vector2Int min, Vector2Int max) =>
        cell.x >= min.x && cell.x < max.x && cell.y >= min.y && cell.y < max.y;

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        Vector2Int cursor = goal;
        while (cursor != start)
        {
            path.Add(cursor);
            cursor = cameFrom[cursor];
        }
        path.Reverse();
        return path;
    }
}