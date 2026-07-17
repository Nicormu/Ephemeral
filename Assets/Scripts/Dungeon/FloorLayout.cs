using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FloorLayout
{
    public const int RoomGridSize = 9;       // 9x9 room-grid (each cell = one room slot, Isaac-style)
    private const int MinRooms = 8;
    private const int MaxRooms = 15;
    private const int TreasureRoomCount = 1;
    private const int MaxGenerationAttempts = 30;
    private const int MaxGrowthSteps = 2000; // safety cap against infinite loops

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

    public static DungeonResult Generate(System.Random rng)
    {
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var result = TryGenerate(rng);
            if (result.Rooms != null && result.Rooms.Count > 0)
                return result;
        }

        Debug.LogWarning("[FloorLayout] All generation attempts failed — using minimal fallback.");
        return BuildMinimal();
    }

    private static DungeonResult TryGenerate(System.Random rng)
    {
        var cells = new Dictionary<Vector2Int, CellNode>();
        Vector2Int startCell = new Vector2Int(RoomGridSize / 2, RoomGridSize / 2);
        cells[startCell] = new CellNode { Cell = startCell, Type = RoomType.Start, DistanceFromStart = 0 };

        int targetRooms = rng.Next(MinRooms, MaxRooms + 1);

        GrowMainPath(cells, startCell, targetRooms, rng);
        if (cells.Count < 3) return default; // too small, retry

        if (!PlaceBoss(cells)) return default; // no valid leaf found, retry

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

    // -- Step 1: grow the main tree via random walk, rejecting placements that would create a loop --
    private static void GrowMainPath(Dictionary<Vector2Int, CellNode> cells, Vector2Int startCell, int targetRooms, System.Random rng)
    {
        int safety = 0;
        while (cells.Count < targetRooms && safety < MaxGrowthSteps)
        {
            safety++;

            var placed = cells.Keys.ToList();
            Vector2Int fromCell = placed[rng.Next(placed.Count)];
            DoorDirection dir = RandomDirection(rng);
            Vector2Int newCell = fromCell + UnitOffset(dir);

            if (!InBounds(newCell)) continue;
            if (cells.ContainsKey(newCell)) continue;
            if (CountOccupiedNeighbors(newCell, cells) > 1) continue; // would create a loop — reject

            cells[newCell] = new CellNode { Cell = newCell, DistanceFromStart = cells[fromCell].DistanceFromStart + 1 };
        }
    }

    // -- Step 2: Boss = leaf room (1 neighbor) farthest from Start --
    private static bool PlaceBoss(Dictionary<Vector2Int, CellNode> cells)
    {
        CellNode bossNode = null;
        int bestDist = -1;

        foreach (var kv in cells)
        {
            if (kv.Value.Type == RoomType.Start) continue;
            if (CountOccupiedNeighbors(kv.Key, cells) != 1) continue; // must be a leaf

            if (kv.Value.DistanceFromStart > bestDist)
            {
                bestDist = kv.Value.DistanceFromStart;
                bossNode = kv.Value;
            }
        }

        if (bossNode == null) return false;
        bossNode.Type = RoomType.Boss;
        return true;
    }

    // -- Step 3: Treasure rooms are NEW leaf cells attached off the existing tree — never required to reach Boss --
    private static void PlaceTreasureRooms(Dictionary<Vector2Int, CellNode> cells, System.Random rng)
    {
        int placed = 0;
        int attempts = 0;

        while (placed < TreasureRoomCount && attempts < 100)
        {
            attempts++;

            var candidates = cells.Keys.ToList();
            Vector2Int fromCell = candidates[rng.Next(candidates.Count)];
            if (cells[fromCell].Type == RoomType.Boss) continue; // keep Boss single-door

            DoorDirection dir = RandomDirection(rng);
            Vector2Int newCell = fromCell + UnitOffset(dir);

            if (!InBounds(newCell) || cells.ContainsKey(newCell)) continue;
            if (CountOccupiedNeighbors(newCell, cells) > 1) continue;

            cells[newCell] = new CellNode
            {
                Cell = newCell,
                Type = RoomType.Treasure,
                DistanceFromStart = cells[fromCell].DistanceFromStart + 1
            };
            placed++;
        }
    }

    // -- Step 4: derive each cell's real doors from its actual occupied neighbors --
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

    // -- Step 5: leaves become DeadEnd, non-leaves become Normal (or Corridor if straight-through) --
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

            Room room = new Room(node.Type, tileOrigin, template.Width, template.Height)
            {
                Doors = node.Doors,
                Cells = BuildAbsoluteCells(template.FloorCells, tileOrigin)
            };
            rooms.Add(room);
        }

        return rooms;
    }

    private static RoomCell[] BuildAbsoluteCells(Vector2Int[] localCells, Vector2Int origin)
    {
        var result = new RoomCell[localCells.Length];
        for (int i = 0; i < localCells.Length; i++)
            result[i] = new RoomCell(origin.x + localCells[i].x, origin.y + localCells[i].y);
        return result;
    }

    // -- helpers --

    private static DoorDirection RandomDirection(System.Random rng) => (rng.Next(4)) switch
    {
        0 => DoorDirection.North,
        1 => DoorDirection.South,
        2 => DoorDirection.East,
        _ => DoorDirection.West,
    };

    private static Vector2Int UnitOffset(DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, 1),
        DoorDirection.South => new Vector2Int(0, -1),
        DoorDirection.East  => new Vector2Int(1, 0),
        DoorDirection.West  => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    private static bool InBounds(Vector2Int cell) =>
        cell.x >= 0 && cell.x < RoomGridSize && cell.y >= 0 && cell.y < RoomGridSize;

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