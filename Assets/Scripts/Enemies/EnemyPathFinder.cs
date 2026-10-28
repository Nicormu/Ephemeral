using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared 8-directional A* grid pathfinder for grounded enemies (see EnemyChaseMovement's
/// _avoidsObstacles toggle). Orthogonal steps cost 1 and diagonal steps cost sqrt(2), so the
/// returned path is the shortest in real distance, not just in number of steps. The heuristic
/// is octile distance, which is exact for this cost model on an open grid, so A* stays
/// optimal and explores very few cells.
///
/// A diagonal step is only allowed if BOTH orthogonal cells it "cuts across" are walkable too —
/// otherwise the enemy's collider would clip the corner of a rock, or slip between a rock and a
/// Void/hazard cell diagonally.
///
/// Purely static/stateless, same spirit as DungeonGeometry/RoomConnector.
/// </summary>
public static class EnemyPathfinder
{
    private const float OrthogonalCost = 1f;
    private const float DiagonalCost = 1.41421356f; // sqrt(2)

    private static readonly Vector2Int[] Neighbors8 =
    {
        // Orthogonal
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
        // Diagonal
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    /// <summary>
    /// Finds the shortest 8-directional path from start to goal, searching only cells inside
    /// [boundsMin, boundsMax) — boundsMin inclusive, boundsMax exclusive, matching
    /// Room.GridPos/Width/Height (i.e. pass boundsMax = GridPos + (Width, Height)).
    ///
    /// isWalkable is queried for every cell EXCEPT start and goal — start because the caller is
    /// already standing there (a knockback or hazard tick could otherwise leave it stuck with no
    /// valid path out), goal because the player's exact cell shouldn't block the enemy from
    /// reaching them. The same exemption applies when checking the corner cells of a diagonal step.
    ///
    /// Returns the path as a list of grid cells EXCLUDING start, INCLUDING goal, in travel order.
    /// Returns null if start == goal, goal is outside bounds, or no path exists.
    /// </summary>
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, Vector2Int boundsMin, Vector2Int boundsMax, System.Func<Vector2Int, bool> isWalkable)
    {
        if (start == goal) return null;
        if (!InBounds(goal, boundsMin, boundsMax)) return null;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        var fScore = new Dictionary<Vector2Int, float> { [start] = Heuristic(start, goal) };

        // A plain list is enough: rooms are 13x7 (91 cells), so scanning it for the lowest
        // fScore is trivially cheap. A priority queue would only add complexity here.
        var open = new List<Vector2Int> { start };
        var closed = new HashSet<Vector2Int>();

        // Cell is usable if it's in bounds AND (start, goal, or walkable).
        bool CanEnter(Vector2Int cell) =>
            InBounds(cell, boundsMin, boundsMax) && (cell == start || cell == goal || isWalkable(cell));

        while (open.Count > 0)
        {
            Vector2Int current = PopLowestF(open, fScore);

            if (current == goal)
                return ReconstructPath(cameFrom, start, goal);

            closed.Add(current);

            foreach (var offset in Neighbors8)
            {
                Vector2Int next = current + offset;
                if (closed.Contains(next)) continue;
                if (!CanEnter(next)) continue;

                bool isDiagonal = offset.x != 0 && offset.y != 0;

                if (isDiagonal)
                {
                    // No corner cutting: both orthogonal cells beside this diagonal step must be open.
                    Vector2Int sideA = new Vector2Int(current.x + offset.x, current.y);
                    Vector2Int sideB = new Vector2Int(current.x, current.y + offset.y);
                    if (!CanEnter(sideA) || !CanEnter(sideB)) continue;
                }

                float tentativeG = gScore[current] + (isDiagonal ? DiagonalCost : OrthogonalCost);

                if (gScore.TryGetValue(next, out float existingG) && tentativeG >= existingG)
                    continue; // already reached this cell by an equal or cheaper route

                cameFrom[next] = current;
                gScore[next] = tentativeG;
                fScore[next] = tentativeG + Heuristic(next, goal);

                if (!open.Contains(next))
                    open.Add(next);
            }
        }

        return null; // no path exists with the current obstacle layout
    }

    /// <summary>Octile distance: the exact shortest cost between two cells on an open 8-way grid.</summary>
    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int diagonalSteps = Mathf.Min(dx, dy);
        int straightSteps = Mathf.Max(dx, dy) - diagonalSteps;
        return diagonalSteps * DiagonalCost + straightSteps * OrthogonalCost;
    }

    /// <summary>Removes and returns the open cell with the lowest fScore.</summary>
    private static Vector2Int PopLowestF(List<Vector2Int> open, Dictionary<Vector2Int, float> fScore)
    {
        int bestIndex = 0;
        float bestF = fScore[open[0]];

        for (int i = 1; i < open.Count; i++)
        {
            float f = fScore[open[i]];
            if (f < bestF)
            {
                bestF = f;
                bestIndex = i;
            }
        }

        Vector2Int best = open[bestIndex];
        open.RemoveAt(bestIndex);
        return best;
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