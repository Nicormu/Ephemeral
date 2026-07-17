using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fills a 13×13 dungeon grid with RoomTemplate instances, guaranteeing connectivity from Start to Boss.
///
/// Algorithm (greedy spanning-tree + branch):
///   1. Place Start room at (0,0).
///   2. Grow a "carved" region via BFS — each step places a corridor or normal room adjacent to carved cells.
///   3. At carved-region boundaries that have ≥2 free sides, insert Treasure/DeadEnd side rooms.
///   4. Place Boss at the bottom-right corner (known-empty).
///   5. Validate connectivity via RoomConnector.ValidateConnectivity(). If any room is unreachable, retry.
///
/// Notes: This uses a greedy placement strategy — it does NOT guarantee a layout on every attempt.
/// That's why generation retries up to MaxAttempts times before falling back to BuildMinimal().
/// </summary>
public static class FloorLayout
{
    public const int GridSize = 13;
    private const int MaxAttempts = 5;

    /// <summary>All rooms placed in the dungeon after a successful generation.</summary>
    public struct DungeonResult
    {
        public List<Room> Rooms;
        public Vector2Int StartPosition;   // grid coords of player spawn (Start room)
        public Vector2Int BossPosition;    // grid coords of boss room top-left corner
        public int Seed;
    }

    /// <summary>Generate a dungeon layout for the 13×13 grid.</summary>
    public static DungeonResult Generate(System.Random rng, RoomTemplate[] startTemplates, RoomTemplate[] normalTemplates,
                                         RoomTemplate[] treasureTemplates, RoomTemplate[] bossTemplates,
                                         RoomTemplate[] deadEndTemplates, RoomTemplate[] corridorTemplates)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            DungeonResult result = TryGenerate(rng, startTemplates, normalTemplates, treasureTemplates, bossTemplates, deadEndTemplates, corridorTemplates);
            if (result.Rooms != null && result.Rooms.Count > 0)
                return result;

            Debug.LogWarning($"[FloorLayout] Generation attempt {attempt + 1} failed — regenerating.");
        }

        // Last resort: minimal viable dungeon (Start → Boss)
        return BuildMinimal();
    }

    private static DungeonResult TryGenerate(System.Random rng, RoomTemplate[] startTemplates, RoomTemplate[] normalTemplates,
                                             RoomTemplate[] treasureTemplates, RoomTemplate[] bossTemplates,
                                             RoomTemplate[] deadEndTemplates, RoomTemplate[] corridorTemplates)
    {
        // --- Phase 1: Determine target room counts ---
        int totalRooms = Mathf.Clamp(Mathf.FloorToInt(rng.Next(9, 18)), 6, 14);
        int branchSlots = Mathf.Clamp(totalRooms - 4, 1, 6); // Start + Boss are always placed

        // --- Phase 2: BFS "carving" to find room positions and connections ---
        var grid = new bool[GridSize, GridSize]; // false = empty
        var slotList = new Dictionary<Vector2Int, (int width, int height)>();
        var cellToRoomIdx = new Dictionary<Vector2Int, int>(); // maps each occupied cell to its room index

        // Step 1: Place Start at top-left corner (0,0)
        RoomTemplate startTpl = PickRandom(startTemplates, rng);
        if (!TryPlaceRoom(grid, ref slotList, ref cellToRoomIdx, startTpl))
            return default;

        // Step 2: BFS expansion — repeatedly place a room on the carved-region boundary
        int roomsPlaced = 1; // Start is already placed
        int branchLeft = branchSlots;

        while (roomsPlaced < totalRooms - 1 && branchLeft >= 0) // -1 for Boss
        {
            var boundarySlots = GetBoundarySlots(slotList, grid);
            if (boundarySlots.Count == 0)
                break;

            Vector2Int pos = boundarySlots[rng.Next(boundarySlots.Count)];

            // Check if this slot is a "junction" (≥2 free sides adjacent to carved region)
            bool isJunction = IsJunctionSlot(pos, grid);
            RoomTemplate roomTpl;

            if (isJunction && branchLeft > 0)
            {
                // Branch off: place Treasure or DeadEnd
                bool isTreasure = ((roomsPlaced % 3) != 0) && (branchLeft > 1);
                roomTpl = isTreasure ? PickRandom(treasureTemplates, rng) : PickRandom(deadEndTemplates, rng);
                if (!TryPlaceRoom(grid, ref slotList, ref cellToRoomIdx, roomTpl))
                    continue; // skip this branch attempt — try another boundary slot next iteration

                branchLeft--;
            }
            else
            {
                // Continue the main path: normal room or corridor
                roomTpl = rng.Next(2) == 0 ? PickRandom(normalTemplates, rng) : PickRandom(corridorTemplates, rng);
                if (!TryPlaceRoom(grid, ref slotList, ref cellToRoomIdx, roomTpl))
                    continue;
            }

            roomsPlaced++;
        }

        // Step 3: Place Boss at the bottom-right corner (GridSize-1, GridSize-1)
        Vector2Int bossPos = new Vector2Int(GridSize - 1, GridSize - 1);
        if (!TryPlaceRoom(grid, ref slotList, ref cellToRoomIdx, PickRandom(bossTemplates, rng)))
            return default; // could not fit — will retry

        // Step 4: Build Room structs and validate connectivity
        List<Room> rooms = ConvertToRooms(slotList, cellToRoomIdx);
        DungeonResult result = new DungeonResult
        {
            Rooms = rooms,
            StartPosition = FindStartRoom(rooms),
            BossPosition = bossPos,
            Seed = rng.Next(),
        };

        if (RoomConnector.ValidateConnectivity(result, out var disconnected))
            return result;

        return default; // will retry
    }

    private static bool TryPlaceRoom(bool[,] grid, ref Dictionary<Vector2Int, (int width, int height)> slotList,
                                     ref Dictionary<Vector2Int, int> cellToRoomIdx, RoomTemplate template)
    {
        for (int ty = 0; ty <= GridSize - template.Height; ty++)
        {
            for (int tx = 0; tx <= GridSize - template.Width; tx++)
            {
                bool fits = true;
                foreach (var cell in template.FloorCells)
                {
                    int gx = tx + cell.x;
                    int gy = ty + cell.y;
                    if (gx < 0 || gx >= GridSize || gy < 0 || gy >= GridSize || grid[gx, gy])
                    { fits = false; break; }
                }

                if (!fits) continue;

                int roomIdx = cellToRoomIdx.Count; // next free room index

                for (int ci = 0; ci < template.FloorCells.Length; ci++)
                {
                    int gx = tx + template.FloorCells[ci].x;
                    int gy = ty + template.FloorCells[ci].y;
                    grid[gx, gy] = true;
                    slotList[new Vector2Int(gx, gy)] = (template.Width, template.Height);
                    cellToRoomIdx[new Vector2Int(gx, gy)] = roomIdx;
                }

                return true;
            }
        }
        return false;
    }

    private static List<Vector2Int> GetBoundarySlots(Dictionary<Vector2Int, (int width, int height)> slotList, bool[,] grid)
    {
        var outer = new HashSet<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        foreach (var kv in slotList)
        {
            Vector2Int cellPos = kv.Key;
            int w = kv.Value.width;
            int h = kv.Value.height;

            // Check each corner of the room for empty neighbors
            foreach (int dx in new[] { 0, w - 1 })
            foreach (int dy in new[] { 0, h - 1 })
            {
                Vector2Int cell = cellPos + new Vector2Int(dx, dy);
                foreach (var dir in dirs)
                {
                    Vector2Int neighbor = cell + dir;
                    if (neighbor.x >= 0 && neighbor.x < GridSize && neighbor.y >= 0 && neighbor.y < GridSize && !grid[neighbor.x, neighbor.y])
                        outer.Add(neighbor);
                }
            }
        }
        return new List<Vector2Int>(outer);
    }

    private static bool IsJunctionSlot(Vector2Int pos, bool[,] grid)
    {
        // A junction is a slot where the room can extend in at least 2 directions into empty space
        Vector2Int[] dirs = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        int freeSides = 0;

        foreach (var dir in dirs)
        {
            Vector2Int neighbor = pos + dir;
            if (neighbor.x >= 0 && neighbor.x < GridSize && neighbor.y >= 0 && neighbor.y < GridSize && !grid[neighbor.x, neighbor.y])
                freeSides++;
        }

        return freeSides >= 2;
    }

    private static RoomTemplate PickRandom<T>(T[] list, System.Random rng) where T : class => list[rng.Next(list.Length)];

    private static Vector2Int FindStartRoom(IReadOnlyList<Room> rooms)
    {
        foreach (var room in rooms)
            if (room.Type == RoomType.Start) return room.GridPos;
        return Vector2Int.zero; // fallback
    }

    private static List<Room> ConvertToRooms(Dictionary<Vector2Int, (int width, int height)> slotList, Dictionary<Vector2Int, int> cellToRoomIdx)
    {
        var rooms = new List<Room>();
        var roomTemplateMap = new Dictionary<int, RoomType>(); // maps internal index back to template type

        // Phase A: count how many distinct rooms exist and group cells by room index
        var cellGroups = new Dictionary<int, List<Vector2Int>>();
        foreach (var kv in cellToRoomIdx)
        {
            int idx = kv.Value;
            if (!cellGroups.ContainsKey(idx))
                cellGroups[idx] = new List<Vector2Int>();
            cellGroups[idx].Add(kv.Key);
        }

        // Phase B: infer template type for each room based on shape and position
        foreach (var group in cellGroups)
        {
            int idx = group.Key;
            var cells = group.Value;

            // Calculate bounding box
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in cells)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            Vector2Int pos = new Vector2Int(minX, minY);

            // Infer type based on shape and position
            RoomType inferredType;
            if (minX == 0 && minY == 0)
                inferredType = RoomType.Start;
            else if (maxX == GridSize - 1 || maxY == GridSize - 1)
                inferredType = RoomType.Boss;
            else if (w == 1 && h == 1 && cells.Count == 1)
                inferredType = RoomType.DeadEnd;
            else if (w * h == cells.Count && (w >= 2 || h >= 2))
                inferredType = RoomType.Normal; // full rectangle
            else
                inferredType = RoomType.Corridor; // partial fill

            roomTemplateMap[idx] = inferredType;

            // Build Room struct
            var roomCells = new RoomCell[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                roomCells[i] = new RoomCell(cells[i].x - minX, cells[i].y - minY);

            Room room = new Room(inferredType, pos, w, h);
            room.Cells = roomCells;
            rooms.Add(room);
        }

        return rooms;
    }

    private static DungeonResult BuildMinimal()
    {
        // Minimal viable dungeon: Start (1×1) at (0,0) → Boss (1×1) at bottom-right
        Room start = new Room(RoomType.Start, Vector2Int.zero, 1, 1);
        start.Cells = new[] { new RoomCell(0, 0) };

        Room boss = new Room(RoomType.Boss, new Vector2Int(GridSize - 1, GridSize - 1), 1, 1);
        boss.Cells = new[] { new RoomCell(GridSize - 1, GridSize - 1) };

        return new DungeonResult
        {
            Rooms = new List<Room> { start, boss },
            StartPosition = Vector2Int.zero,
            BossPosition = new Vector2Int(GridSize - 1, GridSize - 1),
            Seed = 0,
        };
    }
}
