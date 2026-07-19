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

[System.Flags]
public enum DoorDirection
{
    None  = 0,
    North = 1 << 0,
    South = 1 << 1,
    East  = 1 << 2,
    West  = 1 << 3,
}

/// <summary>What a single dungeon cell contains. Void cells simply aren't part of a room's Cells array.</summary>
public enum CellState
{
    Void,
    Floor,
    Obstacle
}

[System.Serializable]
public struct RoomCell
{
    public int X;
    public int Y;
    public CellState State;
    public Vector2Int CellPos => new Vector2Int(X, Y);

    public RoomCell(int x, int y, CellState state = CellState.Floor)
    {
        X = x; Y = y; State = state;
    }
}

[System.Serializable]
public struct Room
{
    public RoomType Type;
    public Vector2Int GridPos;   // tile-space bottom-left corner
    public int Width;
    public int Height;
    public DoorDirection Doors;

    public RoomCell[] Cells;

    /// <summary>
    /// Create a room with default floor cells. Use the object initializer to set Doors
    /// after construction (e.g., { Doors = node.Doors }) or pass it directly here.
    /// </summary>
    public Room(RoomType type, Vector2Int gridPos, int width, int height, DoorDirection doors = DoorDirection.None)
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
                Cells[idx++] = new RoomCell(gridPos.x + x, gridPos.y + y); // defaults to Floor
    }
}