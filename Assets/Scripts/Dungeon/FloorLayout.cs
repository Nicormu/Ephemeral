using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generates a dungeon layout using a graph-based, queue-driven BFS algorithm — mirroring
/// the commonly documented approach used in The Binding of Isaac.
/// </summary>
public static class FloorLayout
{
    private const int MinRooms = 8;
    private const int MaxRooms = 15;

    private const int TreasureRoomCount = 1;
    private const int MinBossDistance = 3;

    private const float SkipChance = 0.5f;
    private const int MaxGenerationAttempts = 50;

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

        Vector2Int startCell = Vector2Int.zero;
        var startNode = new CellNode { Cell = startCell, Type = RoomType.Start, DistanceFromStart = 0 };
        cells[startCell] = startNode;

        int targetRooms = rng.Next(MinRooms, MaxRooms + 1);

        GrowMainPath(cells, startNode, targetRooms, rng);
        if (cells.Count < 4) return default;

        if (!PlaceBoss(cells)) return default;

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

                if (cells.ContainsKey(newCell)) continue;
                if (rng.NextDouble() < SkipChance) continue;
                if (CountOccupiedNeighbors(newCell, cells) > 1) continue;

                var newNode = new CellNode { Cell = newCell, DistanceFromStart = current.DistanceFromStart + 1 };
                cells[newCell] = newNode;
                queue.Enqueue(newNode);
            }
        }
    }

    private static bool PlaceBoss(Dictionary<Vector2Int, CellNode> cells)
    {
        CellNode bossNode = null;
        int bestDist = -1;

        foreach (var kv in cells)
        {
            if (kv.Value.Type == RoomType.Start) continue;
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

            if (cells.ContainsKey(newCell)) continue;
            if (CountOccupiedNeighbors(newCell, cells) > 1) continue;
            if (IsAdjacentToSpecialRoom(newCell, cells)) continue;

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

            Room room = new Room(node.Type, tileOrigin, template.Width, template.Height, node.Doors);
            room.Cells = BuildAbsoluteCells(template.Cells, tileOrigin);
            room.EnemySpawns = BuildAbsoluteEnemySpawns(template.EnemySpawns, tileOrigin);
            room.FloorTile = template.FloorTile;
            room.WallFrontTile = template.WallFrontTile;
            room.WallTopTile = template.WallTopTile;
            rooms.Add(room);
        }

        return rooms;
    }

    private static RoomCell[] BuildAbsoluteCells(
        (Vector2Int pos, CellState state, TileBase obstacleTile, bool obstacleBlocksMovement, int obstacleDamage)[] localCells,
        Vector2Int origin)
    {
        var result = new RoomCell[localCells.Length];
        for (int i = 0; i < localCells.Length; i++)
            result[i] = new RoomCell(
                origin.x + localCells[i].pos.x,
                origin.y + localCells[i].pos.y,
                localCells[i].state,
                localCells[i].obstacleTile,
                localCells[i].obstacleBlocksMovement,
                localCells[i].obstacleDamage);
        return result;
    }

    private static Room.EnemySpawn[] BuildAbsoluteEnemySpawns((Vector2Int pos, GameObject prefab)[] localSpawns, Vector2Int origin)
    {
        var result = new Room.EnemySpawn[localSpawns.Length];
        for (int i = 0; i < localSpawns.Length; i++)
        {
            result[i] = new Room.EnemySpawn
            {
                WorldCell = new Vector2Int(origin.x + localSpawns[i].pos.x, origin.y + localSpawns[i].pos.y),
                Prefab = localSpawns[i].prefab
            };
        }
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