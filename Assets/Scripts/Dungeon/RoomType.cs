using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

/// <summary>
/// What a single dungeon cell contains. Void cells simply aren't part of a room's Cells array.
/// Obstacle covers both blocking hazards (rocks) and walkable damaging hazards (fire) —
/// see RoomCell.ObstacleBlocksMovement / ObstacleDamage.
/// </summary>
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

    /// <summary>Which obstacle visual to use. Only meaningful when State == Obstacle.</summary>
    public TileBase ObstacleTile;

    /// <summary>Whether this obstacle physically blocks the player. Only meaningful when State == Obstacle.</summary>
    public bool ObstacleBlocksMovement;

    /// <summary>Damage dealt if the player stands here. Only meaningful when State == Obstacle and ObstacleBlocksMovement is false.</summary>
    public int ObstacleDamage;

    public Vector2Int CellPos => new Vector2Int(X, Y);

    public RoomCell(int x, int y, CellState state = CellState.Floor, TileBase obstacleTile = null,
        bool obstacleBlocksMovement = true, int obstacleDamage = 0)
    {
        X = x; Y = y; State = state;
        ObstacleTile = obstacleTile;
        ObstacleBlocksMovement = obstacleBlocksMovement;
        ObstacleDamage = obstacleDamage;
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
    public EnemySpawn[] EnemySpawns;

    // -- Visual style, resolved from this room's originating RoomTemplateSO -> RoomStyleSO --
    public TileBase FloorTile;
    public TileBase WallFrontTile;
    public TileBase WallTopTile;

    [System.Serializable]
    public struct EnemySpawn
    {
        public Vector2Int WorldCell;
        public GameObject Prefab;
    }

    public Room(RoomType type, Vector2Int gridPos, int width, int height, DoorDirection doors = DoorDirection.None)
    {
        Type = type;
        GridPos = gridPos;
        Width = width;
        Height = height;
        Doors = DoorDirection.None;
        EnemySpawns = System.Array.Empty<EnemySpawn>();
        FloorTile = null;
        WallFrontTile = null;
        WallTopTile = null;

        int total = width * height;
        Cells = new RoomCell[total];
        int idx = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Cells[idx++] = new RoomCell(gridPos.x + x, gridPos.y + y); // defaults to Floor
    }
}