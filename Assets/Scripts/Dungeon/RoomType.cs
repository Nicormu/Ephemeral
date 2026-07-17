using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Possible room types in the dungeon.
/// </summary>
public enum RoomType
{
    /// <summary>Starting room — player spawns here.</summary>
    Start,

    /// <summary>Regular gameplay room with enemies and loot.</summary>
    Normal,

    /// <summary>Treasure room with items or power-ups.</summary>
    Treasure,

    /// <summary>Boss room with a stronger encounter.</summary>
    Boss,

    /// <summary>Dead-end room (no exit on one side).</summary>
    DeadEnd,

    /// <summary>Narrow corridor used to connect rooms.</summary>
    Corridor
}

/// <summary>A single grid cell that composes a room floor.</summary>
[System.Serializable]
public struct RoomCell
{
    public int X;
    public int Y;
    public Vector2Int CellPos => new Vector2Int(X, Y);

    public RoomCell(int x, int y) { X = x; Y = y; }
}

/// <summary>Describes a single generated room with its type, size, and grid position.</summary>
[System.Serializable]
public struct Room
{
    public RoomType Type;
    public Vector2Int GridPos;
    public int Width;
    public int Height;

    /// <summary>All grid cells the room occupies (floor area).</summary>
    public RoomCell[] Cells;

    /// <summary>Exit portal position in grid coords, relative to dungeon origin.</summary>
    public Vector2Int ExitPos;

    // -- runtime connectivity state (reset each generation/validation pass) --
    internal bool _isConnected;
    internal List<Room> _connectedRooms;

    public Room(RoomType type, Vector2Int gridPos, int width, int height)
    {
        Type = type;
        GridPos = gridPos;
        Width = width;
        Height = height;
        _isConnected = false;
        _connectedRooms = new List<Room>();

        // Fill Cells array with every floor tile.
        int total = width * height;
        Cells = new RoomCell[total];
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Cells[idx++] = new RoomCell(gridPos.x + x, gridPos.y + y);

        // Default exit: center of the right wall.
        ExitPos = new Vector2Int(gridPos.x + width, gridPos.y + height / 2);
    }
}
