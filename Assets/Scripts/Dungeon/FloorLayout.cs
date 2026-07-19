using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates a dungeon layout using a graph-based, queue-driven BFS algorithm — mirroring
/// the commonly documented approach used in The Binding of Isaac.
///
/// Algorithm overview:
///   1. Start at grid origin (0,0) and enqueue it as the root room.
///   2. Dequeue the oldest cell, try each of its 4 cardinal directions in random order.
///   3. Each attempt has a configurable skip chance (keeps the layout sparse instead of a solid blob).
///   4. Reject placement if: neighbor already occupied, or adding it would create a loop
///      (more than one existing neighbor adjacent to the new cell would close a cycle).
///   5. Repeat until target room count is reached or the queue exhausts with no valid placements.
///   6. After layout completes, isolate dead ends and assign special room types:
///      • Boss = leaf room (exactly one neighbor) farthest from Start (with minimum distance floor).
///      • Treasure/Item = extra leaf room(s) attached after the main path grows, never adjacent to other specials.
///   7. Doors are NEVER hand-placed. After all rooms are placed, ComputeDoors() iterates every cell
///      and checks its 4 grid neighbors in the occupancy set. If a neighbor exists at (cell + direction_offset),
///      that door opens automatically. This means adjacent rooms always have matching doors:
///        - Room A at (-1,2) with East door has a neighbor at (0,2) which gets a West door.
///        - Doors are a derived property of grid adjacency, never assigned independently.
///   8. Because there is no bounded grid — rooms expand unboundedly in all four cardinal directions
///      (positive AND negative x/y) from the origin. This matches Isaac's layout where corridors fan out
///      naturally without an artificial boundary.
/// </summary>
public static class FloorLayout
{
    // -- Room count configuration --
    private const int MinRooms = 8;
    private const int MaxRooms = 15;

    // -- Special room placement --
    private const int TreasureRoomCount = 1;
    private const int MinBossDistance = 3;      // Boss must be at least this many rooms (hops) from Start

    // -- Growth parameters --
    private const float SkipChance = 0.5f;       // chance to skip an otherwise-valid placement for map sparseness
    private const int MaxGenerationAttempts = 50; // retries before falling back to minimal dungeon

    public struct DungeonResult
    {
        public List<Room> Rooms;
        public Vector2Int StartPosition;
        public Vector2Int BossPosition;
        public int Seed;
    }

    private class CellNode
    {
        public Vector2Int Cell;
        public RoomType Type = RoomType.Normal;
        public DoorDirection Doors;
        public int DistanceFromStart;
    }

    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    /// <summary>
    /// Generate a dungeon layout using the Binding of Isaac's room-graph algorithm.
    /// </summary>
    /// <param name="rng">A seeded System.Random for deterministic results.</param>
    /// <returns>A DungeonResult containing placed rooms, their types, and metadata.</returns>
    public static DungeonResult Generate(System.Random rng)
    {
        // Retry on failure — each retry gets a fresh seed state so we can get different layouts.
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var result = TryGenerate(rng);
            if (result.Rooms != null && result.Rooms.Count > 0)
                return result;
        }

        // Exhaustive failure — fall back to a minimal valid dungeon.
        Debug.LogWarning("[FloorLayout] All generation attempts failed — using minimal fallback.");
        return BuildMinimal();
    }

    /// <summary>
    /// Attempt one full generation cycle. Retries up to MaxGenerationAttempts times on failure
    /// (empty layout or connectivity validation failure), each retry using a fresh random seed state.
    /// </summary>
    private static DungeonResult TryGenerate(System.Random rng)
    {
        // Use a dictionary for room metadata (type, doors, distance) and ContainsKey() as the occupancy check.
        // There is no bounded grid — rooms expand unboundedly in all four cardinal directions from the origin.
        var cells = new Dictionary<Vector2Int, CellNode>();

        // Start at true (0,0) — the root of the room graph. All subsequent rooms grow outward into
        // both positive and negative axes from this point, matching Isaac's unbounded fan-out pattern.
        Vector2Int startCell = Vector2Int.zero;
        var startNode = new CellNode { Cell = startCell, Type = RoomType.Start, DistanceFromStart = 0 };
        cells[startCell] = startNode;

        int targetRooms = rng.Next(MinRooms, MaxRooms + 1);

        GrowMainPath(cells, startNode, targetRooms, rng);
        if (cells.Count < 4) return default; // too small — retry with a fresh roll

        if (!PlaceBoss(cells)) return default; // no leaf far enough from Start — retry

        PlaceTreasureRooms(cells, rng);
        ComputeDoors(cells);
        ClassifyRemainingTypes(cells, rng);

        List<Room> rooms = BuildRooms(cells, rng);
        if (rooms.Count == 0) return default;

        var result = new DungeonResult
        {
            Rooms = rooms,
            StartPosition = FindRoomPosition(rooms, RoomType.Start),
            BossPosition = FindRoomPosition(rooms, RoomType.Boss),
            Seed = rng.Next(),
        };

        if (!RoomConnector.ValidateConnectivity(result.Rooms, out _))
            return default;

        return result;
    }

    // -- Step 1: BFS queue growth with random direction order + skip chance --
    /// <summary>
    /// Grows the main dungeon path from Start using BFS. Each dequeued cell tries all 4 cardinal
    /// directions in random order. A new room is placed if:
    ///   • The grid cell is not already occupied (no duplicates).
    ///   • A random skip check passes (SkipChance = 0.5 keeps the layout sparse, not a filled blob).
    ///   • Adding this room would NOT create a loop — it may touch at most 1 existing neighbor.
    ///     Touching ≥2 neighbors would close a cycle in the graph, which Isaac's layouts avoid.
    /// There is NO hard grid boundary. Rooms expand infinitely in all four cardinal directions
    /// from (0,0). The generation stops when either the target room count is reached or the queue
    /// drains with no valid placements remaining (at which point a retry generates a fresh layout).
    /// </summary>
    private static void GrowMainPath(Dictionary<Vector2Int, CellNode> cells, CellNode startNode, int targetRooms, System.Random rng)
    {
        var queue = new Queue<CellNode>();
        queue.Enqueue(startNode);

        while (queue.Count > 0 && cells.Count < targetRooms)
        {
            CellNode current = queue.Dequeue();
            var directions = ShuffledDirections(rng);

            foreach (var dir in directions)
            {
                if (cells.Count >= targetRooms) break;

                Vector2Int newCell = current.Cell + UnitOffset(dir);

                // No InBounds check — rooms expand unboundedly from (0,0) into negative and positive axes.
                if (cells.ContainsKey(newCell)) continue;                           // grid cell already occupied by another room
                if (rng.NextDouble() < SkipChance) continue;                   // deliberate sparseness
                if (CountOccupiedNeighbors(newCell, cells) > 1) continue;      // would create a loop — reject

                var newNode = new CellNode { Cell = newCell, DistanceFromStart = current.DistanceFromStart + 1 };
                cells[newCell] = newNode;
                queue.Enqueue(newNode);
            }
        }
    }

    /// <summary>
    /// Dead-end isolation: find every leaf node in the room graph (degree-1 nodes).
    /// The room farthest from Start among all leaves is assigned as Boss. If no leaf
    /// meets the minimum distance requirement, generation retries (returns false).
    /// </summary>
    private static bool PlaceBoss(Dictionary<Vector2Int, CellNode> cells)
    {
        CellNode bossNode = null;
        int bestDist = -1;

        foreach (var kv in cells)
        {
            if (kv.Value.Type == RoomType.Start) continue;
            // A leaf node has exactly one neighbor in the graph — it's a dead end.
            if (CountOccupiedNeighbors(kv.Key, cells) != 1) continue;

            if (kv.Value.DistanceFromStart > bestDist)
            {
                bestDist = kv.Value.DistanceFromStart;
                bossNode = kv.Value;
            }
        }

        if (bossNode == null || bestDist < MinBossDistance) return false;

        bossNode.Type = RoomType.Boss;
        return true;
    }

    /// <summary>
    /// Treasure placement: attempt to attach extra leaf rooms (dead ends) after the main path
    /// completes. Isaac never places special rooms adjacent to each other — they must be separated
    /// by normal corridor space so that their importance is visually communicated.
    /// </summary>
    private static void PlaceTreasureRooms(Dictionary<Vector2Int, CellNode> cells, System.Random rng)
    {
        int placed = 0;
        int attempts = 0;

        while (placed < TreasureRoomCount && attempts < 200)
        {
            attempts++;

            var candidates = cells.Values.Where(n => n.Type != RoomType.Boss).ToList();
            if (candidates.Count == 0) break;

            CellNode fromNode = candidates[rng.Next(candidates.Count)];
            DoorDirection dir = ShuffledDirections(rng)[0];
            Vector2Int newCell = fromNode.Cell + UnitOffset(dir);

            if (cells.ContainsKey(newCell)) continue;              // grid cell already occupied
            if (CountOccupiedNeighbors(newCell, cells) > 1) continue;  // would close a loop — reject
            if (IsAdjacentToSpecialRoom(newCell, cells)) continue;  // Isaac never places specials next to each other

            var treasureNode = new CellNode
            {
                Cell = newCell,
                Type = RoomType.Treasure,
                DistanceFromStart = fromNode.DistanceFromStart + 1
            };
            cells[newCell] = treasureNode;
            placed++;
        }
    }

    private static bool IsAdjacentToSpecialRoom(Vector2Int cell, Dictionary<Vector2Int, CellNode> cells)
    {
        foreach (var dir in AllDirections)
        {
            if (cells.TryGetValue(cell + UnitOffset(dir), out var neighbor) &&
                (neighbor.Type == RoomType.Boss || neighbor.Type == RoomType.Treasure))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Door derivation: post-process the room graph to compute each room's doors based on
    /// its actual grid neighbors. This means doors are a DERIVED property of layout — never
    /// independently assigned. Adjacent rooms always have matching opposite doors:
    ///   Room A at (x-1, y) with East door connects to Room B at (x, y) with West door.
    /// </summary>
    private static void ComputeDoors(Dictionary<Vector2Int, CellNode> cells)
    {
        foreach (var kv in cells)
        {
            DoorDirection doors = DoorDirection.None;
            foreach (var dir in AllDirections)
                if (cells.ContainsKey(kv.Key + UnitOffset(dir)))
                    doors |= dir;

            kv.Value.Doors = doors;
        }
    }

    /// <summary>
    /// Classify remaining unassigned rooms: leaves (1 door) become DeadEnd, straight-through
    /// pairs (2 opposite doors) may become Corridor or Normal, everything else is Normal.
    /// </summary>
    private static void ClassifyRemainingTypes(Dictionary<Vector2Int, CellNode> cells, System.Random rng)
    {
        foreach (var kv in cells)
        {
            var node = kv.Value;
            if (node.Type == RoomType.Start || node.Type == RoomType.Boss || node.Type == RoomType.Treasure)
                continue;

            int doorCount = CountBits((int)node.Doors);

            if (doorCount == 1)
                node.Type = RoomType.DeadEnd;
            else if (doorCount == 2 && IsOppositePair(node.Doors))
                node.Type = (rng.NextDouble() < 0.4) ? RoomType.Corridor : RoomType.Normal;
            else
                node.Type = RoomType.Normal;
        }
    }

    // -- Step 6: convert graph cells into placed Rooms, picking a template per (type, doors) --
    private static List<Room> BuildRooms(Dictionary<Vector2Int, CellNode> cells, System.Random rng)
    {
        var rooms = new List<Room>();

        foreach (var kv in cells)
        {
            var node = kv.Value;
            RoomTemplate template = RoomPool.GetTemplate(node.Type, node.Doors, rng);
            if (template == null)
            {
                Debug.LogWarning($"[FloorLayout] No template for {node.Type} with doors {node.Doors} — skipping room.");
                continue;
            }

            Vector2Int tileOrigin = new Vector2Int(
                kv.Key.x * RoomTemplate.RoomTileSize.x,
                kv.Key.y * RoomTemplate.RoomTileSize.y
            );

            // Pass door combo into the constructor so it's preserved even if cells are replaced.
            Room room = new Room(node.Type, tileOrigin, template.Width, template.Height, node.Doors);
            room.Cells = BuildAbsoluteCells(template.Cells, tileOrigin);
            rooms.Add(room);
        }

        return rooms;
    }

    private static RoomCell[] BuildAbsoluteCells((Vector2Int pos, CellState state)[] localCells, Vector2Int origin)
    {
        var result = new RoomCell[localCells.Length];
        for (int i = 0; i < localCells.Length; i++)
            result[i] = new RoomCell(origin.x + localCells[i].pos.x, origin.y + localCells[i].pos.y, localCells[i].state);
        return result;
    }

    // -- helpers --

    private static List<DoorDirection> ShuffledDirections(System.Random rng)
    {
        var dirs = new List<DoorDirection>(AllDirections);
        for (int i = dirs.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }
        return dirs;
    }

    private static Vector2Int UnitOffset(DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, 1),
        DoorDirection.South => new Vector2Int(0, -1),
        DoorDirection.East  => new Vector2Int(1, 0),
        DoorDirection.West  => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    private static int CountOccupiedNeighbors(Vector2Int cell, Dictionary<Vector2Int, CellNode> cells)
    {
        int count = 0;
        foreach (var dir in AllDirections)
            if (cells.ContainsKey(cell + UnitOffset(dir)))
                count++;
        return count;
    }

    private static int CountBits(int mask)
    {
        int count = 0;
        while (mask != 0) { count += mask & 1; mask >>= 1; }
        return count;
    }

    private static bool IsOppositePair(DoorDirection doors) =>
        doors == (DoorDirection.North | DoorDirection.South) ||
        doors == (DoorDirection.East | DoorDirection.West);

    private static Vector2Int FindRoomPosition(IReadOnlyList<Room> rooms, RoomType type)
    {
        foreach (var room in rooms)
            if (room.Type == type) return room.GridPos;
        return Vector2Int.zero;
    }

    private static DungeonResult BuildMinimal()
    {
        Room start = new Room(RoomType.Start, Vector2Int.zero, RoomTemplate.RoomTileSize.x, RoomTemplate.RoomTileSize.y)
        { Doors = DoorDirection.East };

        Vector2Int bossOrigin = new Vector2Int(RoomTemplate.RoomTileSize.x, 0);
        Room boss = new Room(RoomType.Boss, bossOrigin, RoomTemplate.RoomTileSize.x, RoomTemplate.RoomTileSize.y)
        { Doors = DoorDirection.West };

        return new DungeonResult
        {
            Rooms = new List<Room> { start, boss },
            StartPosition = Vector2Int.zero,
            BossPosition = bossOrigin,
            Seed = 0,
        };
    }
}