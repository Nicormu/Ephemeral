using UnityEngine;

public class RoomTemplate
{
    public static readonly Vector2Int RoomTileSize = RoomTemplateSO.RoomTileSize;

    public RoomType Type { get; set; }
    public DoorDirection Doors { get; set; }
    public (Vector2Int pos, CellState state)[] Cells { get; set; }

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    public static RoomTemplate FromSO(RoomTemplateSO so) => new RoomTemplate
    {
        Type = so.Type,
        Doors = so.Doors,
        Cells = so.GetOccupiedCells()
    };
}