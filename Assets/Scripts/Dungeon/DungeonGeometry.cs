using UnityEngine;

/// <summary>
/// Pure, stateless room/door math shared across wall building, door spawning, cell queries,
/// and gizmo drawing — one source of truth instead of near-duplicate copies in each system.
/// </summary>
public static class DungeonGeometry
{
    /// <summary>How far inward (toward the room interior) the door prefab's spawn point is
    /// pulled from the dead center of its wall tile (0.5f from the tile's edge). 0 = exact
    /// center of the wall tile (the old behavior). Only affects GetDoorWorldPosition — i.e. the
    /// door prefab's visual/physical position (sprite, collider, and the EntryTrigger child,
    /// since it inherits this offset as the door's parent transform). It deliberately does NOT
    /// change which grid cell counts as "the door" for wall building, corridor floor
    /// registration, or Rule Tile matching (see GetDoorWallCell) — those stay purely cell-based.</summary>
    public const float DoorInwardOffset = 0.10f;

    public static Vector3 GetRoomCornerWorld(Vector2Int gridPos) => new Vector3(gridPos.x, gridPos.y, 0f);

    public static Vector3 GetRoomFarCornerWorld(Vector2Int gridPos, int width, int height) =>
        new Vector3(gridPos.x + width, gridPos.y + height, 0f);

    public static Vector3 GetRoomCenterWorld(Vector2Int gridPos, int width, int height) =>
        new Vector3(gridPos.x + width / 2f, gridPos.y + height / 2f, 0f);

    public static Vector2Int UnitOffset(DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, 1),
        DoorDirection.South => new Vector2Int(0, -1),
        DoorDirection.East  => new Vector2Int(1, 0),
        DoorDirection.West  => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    // Includes DungeonGridConstants.RoomGap: rooms are separated by a gap, so the neighbor
    // in a given direction sits (width/height + gap) tiles away, not just (width/height) away.
    public static Vector2Int DirectionOffset(DoorDirection dir, int width, int height) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, height + DungeonGridConstants.RoomGap),
        DoorDirection.South => new Vector2Int(0, -(height + DungeonGridConstants.RoomGap)),
        DoorDirection.East  => new Vector2Int(width + DungeonGridConstants.RoomGap, 0),
        DoorDirection.West  => new Vector2Int(-(width + DungeonGridConstants.RoomGap), 0),
        _ => Vector2Int.zero
    };

    public static DoorDirection Opposite(DoorDirection dir) => dir switch
    {
        DoorDirection.North => DoorDirection.South,
        DoorDirection.South => DoorDirection.North,
        DoorDirection.East  => DoorDirection.West,
        DoorDirection.West  => DoorDirection.East,
        _ => DoorDirection.None
    };

    /// <summary>
    /// The single grid cell, in absolute world-tile coordinates, that a room's door on side dir
    /// occupies — the wall tile immediately outside the room's floor rect. Shared by wall
    /// building, wall-decoration exclusion, door-threshold Floor registration, and door
    /// spawning, so they all agree on exactly where a door sits. Purely cell-based — NOT affected
    /// by DoorInwardOffset, which only nudges the door prefab's visual spawn point within this cell.
    /// </summary>
    public static Vector2Int GetDoorWallCell(Room room, DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector2Int(room.GridPos.x + room.Width / 2, room.GridPos.y + room.Height),
        DoorDirection.South => new Vector2Int(room.GridPos.x + room.Width / 2, room.GridPos.y - 1),
        DoorDirection.East  => new Vector2Int(room.GridPos.x + room.Width, room.GridPos.y + room.Height / 2),
        DoorDirection.West  => new Vector2Int(room.GridPos.x - 1, room.GridPos.y + room.Height / 2),
        _ => room.GridPos
    };

    /// <summary>
    /// World-space spawn position for a room's door on side dir. Along the wall's own length
    /// this lands on the tile's center because room dimensions are odd (13x7). Across the wall
    /// — the single-tile-thick axis — a Tilemap cell coordinate is its BOTTOM-LEFT corner, not
    /// its center, so the base offset is ±0.5f to center the door inside that wall tile; that
    /// 0.5f is then reduced by DoorInwardOffset to pull the door slightly toward the room
    /// interior instead of sitting dead-center in the wall tile. Everything parented to the
    /// spawned door prefab (sprite, Collider2D, the EntryTrigger child) moves with it.
    /// </summary>
    public static Vector3 GetDoorWorldPosition(Room room, DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height + (0.5f - DoorInwardOffset), 0f),
        DoorDirection.South => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y - (0.5f - DoorInwardOffset), 0f),
        DoorDirection.East  => new Vector3(room.GridPos.x + room.Width + (0.5f - DoorInwardOffset), room.GridPos.y + room.Height / 2f, 0f),
        DoorDirection.West  => new Vector3(room.GridPos.x - (0.5f - DoorInwardOffset), room.GridPos.y + room.Height / 2f, 0f),
        _ => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height / 2f, 0f)
    };

    /// <summary>World-space position of the first Floor tile inside a room, right next to its
    /// own door on side sideWithDoor — where the player lands after using the door on the OTHER
    /// side of that connection. Deliberately left untouched by DoorInwardOffset: this is a
    /// landing spot inside the room, not the source door's own visual position.</summary>
    public static Vector3 GetDoorEntryPoint(Room room, DoorDirection sideWithDoor) => sideWithDoor switch
    {
        DoorDirection.North => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height - 0.5f, 0f),
        DoorDirection.South => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + 0.5f, 0f),
        DoorDirection.East  => new Vector3(room.GridPos.x + room.Width - 0.5f, room.GridPos.y + room.Height / 2f, 0f),
        DoorDirection.West  => new Vector3(room.GridPos.x + 0.5f, room.GridPos.y + room.Height / 2f, 0f),
        _ => new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height / 2f, 0f)
    };
}