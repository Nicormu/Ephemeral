using System.Collections.Generic;
using UnityEngine;
 
/// <summary>
/// Generates a procedural dungeon layout using a BSP-like room placement algorithm.
///
/// Pipeline:
/// 1. Create a start room at (0, 0).
/// 2. Extend rooms from existing exits until the target count is reached.
/// 3. Assign room types by weighted probability, then override to guarantee treasure/boss rooms.
/// </summary>
public class RoomGenerator
{
    // -- configuration --
 
    [System.Serializable]
    public struct GeneratorConfig
    {
        [Header("Room Sizes")]
        [Tooltip("Minimum width in cells.")] public int MinWidth;
        [Tooltip("Maximum width in cells.")] public int MaxWidth;
        [Tooltip("Minimum height in cells.")] public int MinHeight;
        [Tooltip("Maximum height in cells.")] public int MaxHeight;
 
        [Header("Layout")]
        [Tooltip("Total number of rooms to generate.")] public int RoomCount;
        [Tooltip("Guaranteed treasure rooms.")] public int GuaranteedTreasureRooms;
        [Tooltip("Guaranteed boss rooms (excluding start).")] public int GuaranteedBossRooms;
 
        [Header("Weights")]
        public float NormalWeight;
        public float TreasureWeight;
        public float BossWeight;
        public float DeadEndWeight;
        public float CorridorWeight;
 
        /// <summary>Returns a GeneratorConfig populated with sensible defaults.</summary>
        public static GeneratorConfig Default => new()
        {
            MinWidth = 4, MaxWidth = 8, MinHeight = 4, MaxHeight = 8,
            RoomCount = 10, GuaranteedTreasureRooms = 2, GuaranteedBossRooms = 1,
            NormalWeight = 5f, TreasureWeight = 2f, BossWeight = 1f,
            DeadEndWeight = 2f, CorridorWeight = 3f,
        };
    }
 
    // Max attempts per room slot before giving up on that slot. Prevents
    // an unlucky run of collisions from looping forever while still letting
    // the generator retry instead of silently under-filling the dungeon.
    private const int MaxPlacementAttemptsPerRoom = 20;
 
    private GeneratorConfig _config;
    private HashSet<Vector2Int> _occupiedCells;
    private List<Room> _rooms;
 
    public RoomGenerator(GeneratorConfig config)
    {
        _config = config;
        _occupiedCells = new HashSet<Vector2Int>();
        _rooms = new List<Room>();
    }
 
    /// <summary>Generate a dungeon. Returns all room data and the config used.</summary>
    public DungeonResult Generate(GeneratorConfig config)
    {
        _config = config;
        _occupiedCells.Clear();
        _rooms.Clear();
 
        CreateStartRoom();
 
        for (int i = 0; i < _config.RoomCount - 1; i++)
        {
            // Retry placement a bounded number of times so a single failed
            // attempt (e.g. picked a parent/offset that collides) doesn't
            // silently shrink the final room count.
            for (int attempt = 0; attempt < MaxPlacementAttemptsPerRoom; attempt++)
            {
                if (PlaceNextRoom(i))
                    break;
            }
        }
 
        ApplyGuaranteedTypes();
        return new DungeonResult(_rooms, _config);
    }
 
    /// <summary>Convenience overload with default config.</summary>
    public DungeonResult Generate(int roomCount = 10) => Generate(new GeneratorConfig { RoomCount = roomCount });
 
    // -- room placement --
 
    private void CreateStartRoom()
    {
        int width  = SeedManager.Rng.Next(_config.MinWidth, _config.MaxWidth + 1);
        int height = SeedManager.Rng.Next(_config.MinHeight, _config.MaxHeight + 1);
        var room   = new Room(RoomType.Start, Vector2Int.zero, width, height);
 
        MarkOccupied(room);
        _rooms.Add(room);
    }
 
    /// <summary>Attempts to place one room. Returns true on success, false if this attempt failed.</summary>
    private bool PlaceNextRoom(int index)
    {
        var candidates = new List<Room>();
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].Type == RoomType.Start || _rooms[i].Type == RoomType.Normal)
                candidates.Add(_rooms[i]);
        }
        if (candidates.Count == 0) return false;
 
        var parent    = candidates[SeedManager.Rng.Next(candidates.Count)];
        var exitPoint = new Vector2Int(parent.GridPos.x + parent.Width, parent.GridPos.y + parent.Height / 2);
 
        // Decide the room's type first, then derive its size from that type,
        // so a room can't end up corridor-sized but typed Boss/Treasure (or
        // vice versa) from two independent rolls.
        var type        = DetermineType(index);
        bool isCorridor = type == RoomType.Corridor;
 
        int width  = isCorridor ? SeedManager.Range(SeedManager.Rng, _config.MinWidth / 2, _config.MinWidth)
                                 : SeedManager.Rng.Next(_config.MinWidth, _config.MaxWidth + 1);
        int height = isCorridor ? SeedManager.Range(SeedManager.Rng, _config.MinHeight / 2, _config.MinHeight)
                                 : SeedManager.Rng.Next(_config.MinHeight, _config.MaxHeight + 1);
 
        var gridPos = new Vector2Int(
            exitPoint.x + SeedManager.Range(SeedManager.Rng, 0, width + 3),
            exitPoint.y + SeedManager.Range(SeedManager.Rng, -2 * height / 3, height)
        );
 
        if (!CanPlace(gridPos, width, height)) return false;
 
        var room = new Room(type, gridPos, width, height);
 
        MarkOccupied(room);
        _rooms.Add(room);
        return true;
    }
 
    private bool CanPlace(Vector2Int pos, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (_occupiedCells.Contains(pos + new Vector2Int(x, y)))
                    return false;
        return true;
    }
 
    private void MarkOccupied(Room room)
    {
        foreach (var cell in room.Cells)
            _occupiedCells.Add(cell.CellPos);
    }
 
    // -- room type helpers --
 
    private float TotalWeight() =>
        _config.NormalWeight + _config.TreasureWeight + _config.BossWeight + _config.DeadEndWeight + _config.CorridorWeight;
 
    private RoomType DetermineType(int index)
    {
        if (index < 2) return RoomType.Normal; // first few rooms are normal.
 
        float total = TotalWeight();
        float roll  = SeedManager.NextFloat(SeedManager.Rng, 0f, total);
        float cum   = _config.CorridorWeight;
        if (roll < cum) return RoomType.Corridor;
        cum += _config.DeadEndWeight;
        if (roll < cum) return RoomType.DeadEnd;
        cum += _config.NormalWeight;
        if (roll < cum) return RoomType.Normal;
        cum += _config.TreasureWeight;
        if (roll < cum) return RoomType.Treasure;
        return RoomType.Boss;
    }
 
    /// <summary>
    /// Converts a random selection of existing Normal rooms into guaranteed
    /// Treasure/Boss rooms. Only Normal rooms are eligible (so a corridor- or
    /// dead-end-sized room never becomes a treasure/boss room), and each room
    /// index can only be converted once so guarantees never collide or
    /// silently overwrite one another.
    /// </summary>
    private void ApplyGuaranteedTypes()
    {
        var usedIndices = new HashSet<int>();
 
        AssignGuaranteed(RoomType.Treasure, _config.GuaranteedTreasureRooms, usedIndices);
        AssignGuaranteed(RoomType.Boss, _config.GuaranteedBossRooms, usedIndices);
    }
 
    private void AssignGuaranteed(RoomType type, int count, HashSet<int> usedIndices)
    {
        // Build the pool of eligible room indices once: Normal rooms
        // (never the Start room) that haven't already been converted by an
        // earlier guarantee.
        var eligible = new List<int>();
        for (int i = 1; i < _rooms.Count; i++)
        {
            if (_rooms[i].Type == RoomType.Normal && !usedIndices.Contains(i))
                eligible.Add(i);
        }
 
        int toAssign = Mathf.Min(count, eligible.Count);
        for (int i = 0; i < toAssign; i++)
        {
            int pick = SeedManager.Rng.Next(eligible.Count);
            int idx  = eligible[pick];
            eligible.RemoveAt(pick);
 
            usedIndices.Add(idx);
            _rooms[idx] = new Room(type, _rooms[idx].GridPos, _rooms[idx].Width, _rooms[idx].Height);
        }
    }
}
 
/// <summary>Result of a dungeon generation pass.</summary>
public class DungeonResult
{
    public List<Room> Rooms   { get; }
    public RoomGenerator.GeneratorConfig Config { get; }
    public int TotalRooms => Rooms.Count;
 
    public DungeonResult(List<Room> rooms, RoomGenerator.GeneratorConfig config)
    {
        Rooms = rooms;
        Config = config;
    }
 
    /// <summary>Debug print a summary of the generated dungeon.</summary>
    public void DebugLogSummary()
    {
        string list = "";
        for (int i = 0; i < Rooms.Count; i++)
        {
            var r = Rooms[i];
            list += $"  [{i}] {r.Type} at ({r.GridPos.x},{r.GridPos.y}) size={r.Width}x{r.Height}";
            if (r.Type == RoomType.Start)   list += " <-- SPAWN";
            if (r.Type == RoomType.Treasure) list += " <-- TREASURE";
            if (r.Type == RoomType.Boss)     list += " <-- BOSS";
            list += "\n";
        }
        Debug.Log($"[DungeonResult] {TotalRooms} rooms:\n{list}");
    }
}
