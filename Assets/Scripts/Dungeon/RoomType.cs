using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Start,
    Normal,
    Treasure,
    Boss,
    DeadEnd,
    Corridor
}

/// <summary>Which walls of a room have a door connecting to a neighboring room.</summary>
[System.Flags]
public enum DoorDirection
{
    None  = 0,
    North = 1 << 0,
    South = 1 << 1,
    East  = 1 << 2,
    West  = 1 << 3,
}

[System.Serializable]
public struct RoomCell
{
    public int X;
    public int Y;
    public Vector2Int CellPos => new Vector2Int(X, Y);

    public RoomCell(int x, int y) { X = x; Y = y; }
}

/// <summary>A single generated room: type, tile-space origin, size, and which walls connect to neighbors.</summary>
[System.Serializable]
public struct Room
{
    public RoomType Type;
    public Vector2Int GridPos;   // tile-space bottom-left corner
    public int Width;
    public int Height;
    public DoorDirection Doors;  // which walls have a door to a neighboring room

    public RoomCell[] Cells;

    public Room(RoomType type, Vector2Int gridPos, int width, int height)
    {
        Type = type;
        GridPos = gridPos;
        Width = width;
        Height = height;
        Doors = DoorDirection.None;

        int total = width * height;
        Cells = new RoomCell[total];
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Cells[idx++] = new RoomCell(gridPos.x + x, gridPos.y + y);
    }
}