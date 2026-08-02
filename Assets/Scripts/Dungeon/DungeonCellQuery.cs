using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gameplay-facing read API over the generated dungeon: what's at a given grid cell, hazard
/// damage lookups, and "find me a safe floor tile" queries. Built once per generation by
/// DungeonManager (see Build()) and consumed by PlayerHazardDetector, RoomCamera, etc.
/// </summary>
public class DungeonCellQuery
{
    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private Dictionary<Vector2Int, CellState> _cellLookup;
    private Dictionary<Vector2Int, int> _obstacleHazardDamage;
    private List<Room> _rooms;
    private HashSet<Vector2Int> _doorCorridorCells;

    /// <summary>Raw cell lookup, exposed for DungeonDecorationPainter's floor-decoration pass.</summary>
    public IReadOnlyDictionary<Vector2Int, CellState> CellLookup => _cellLookup;

    /// <summary>Every gap-tile between two connected rooms' doors, registered as Floor. Exposed so
    /// DungeonManager can paint an actual floor tile there too (visually, it used to be empty
    /// space — "nothing" — between rooms, which is also what let the player fall through it).</summary>
    public IReadOnlyCollection<Vector2Int> DoorCorridorCells => _doorCorridorCells;

    /// <summary>Rebuilds the lookup tables from the current room list. Call once per generation,
    /// after ApplyRandomRoomStyle and before anything queries cell state.</summary>
    public void Build(List<Room> rooms)
    {
        _rooms = rooms;
        _cellLookup = new Dictionary<Vector2Int, CellState>();
        _obstacleHazardDamage = new Dictionary<Vector2Int, int>();
        _doorCorridorCells = new HashSet<Vector2Int>();

        foreach (var room in rooms)
        {
            foreach (var cell in room.Cells)
            {
                _cellLookup[cell.CellPos] = cell.State;

                if (cell.State == CellState.Obstacle && !cell.ObstacleBlocksMovement && cell.ObstacleDamage > 0)
                    _obstacleHazardDamage[cell.CellPos] = cell.ObstacleDamage;
            }

            // The gap between two connected rooms (DungeonGridConstants.RoomGap tiles wide) sits
            // entirely outside every room's Cells array, so without this it reads as Void the
            // whole way across. That let the player fall through mid-door-crossing (RoomCamera
            // lerps the player's position straight across this gap while transitioning, and
            // PlayerHazardDetector keeps checking cell state every FixedUpdate the whole time —
            // it isn't paused during the transition). Registering the FULL corridor — not just
            // the single door-threshold tile — as Floor keeps the entire crossing walkable.
            foreach (var dir in AllDirections)
            {
                if ((room.Doors & dir) == 0) continue;

                foreach (var corridorCell in GetCorridorCells(room, dir))
                {
                    _cellLookup[corridorCell] = CellState.Floor;
                    _doorCorridorCells.Add(corridorCell);
                }
            }
        }
    }

    /// <summary>The run of cells from a room's own door-wall tile, extending outward across the
    /// gap toward the connected room's door-wall tile — DungeonGridConstants.RoomGap cells long.</summary>
    private static IEnumerable<Vector2Int> GetCorridorCells(Room room, DoorDirection dir)
    {
        Vector2Int cursor = DungeonGeometry.GetDoorWallCell(room, dir);
        Vector2Int step = DungeonGeometry.UnitOffset(dir);

        for (int i = 0; i < DungeonGridConstants.RoomGap; i++)
        {
            yield return cursor;
            cursor += step;
        }
    }

    /// <summary>What's at a grid cell. Cells not part of any room return Void.</summary>
    public CellState GetCellState(Vector2Int gridCell) =>
        _cellLookup != null && _cellLookup.TryGetValue(gridCell, out var state) ? state : CellState.Void;

    /// <summary>Damage dealt by standing on this cell, if it's a walkable hazard obstacle (e.g. fire). 0 otherwise.</summary>
    public int GetObstacleHazardDamage(Vector2Int gridCell) =>
        _obstacleHazardDamage != null && _obstacleHazardDamage.TryGetValue(gridCell, out var dmg) ? dmg : 0;

    /// <summary>
    /// Finds the room whose bounds contain worldPos, then returns the world-space center of the
    /// closest Floor cell within that room. Null if worldPos isn't inside any room's bounds.
    /// </summary>
    public Vector3? FindNearestSafePositionInRoom(Vector3 worldPos)
    {
        if (_rooms == null) return null;

        Room? containingRoom = null;
        foreach (var room in _rooms)
        {
            Vector3 min = DungeonGeometry.GetRoomCornerWorld(room.GridPos);
            Vector3 max = DungeonGeometry.GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);

            if (worldPos.x >= min.x && worldPos.x < max.x &&
                worldPos.y >= min.y && worldPos.y < max.y)
            {
                containingRoom = room;
                break;
            }
        }

        if (containingRoom == null) return null;

        RoomCell? nearest = null;
        float bestDistSq = float.MaxValue;

        foreach (var cell in containingRoom.Value.Cells)
        {
            if (cell.State != CellState.Floor) continue;

            Vector3 cellCenter = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
            float distSq = (cellCenter - worldPos).sqrMagnitude;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = cell;
            }
        }

        if (nearest == null) return null;

        return new Vector3(nearest.Value.X + 0.5f, nearest.Value.Y + 0.5f, 0f);
    }

    public bool IsInsideDungeon(Vector3 worldPos)
    {
        if (_rooms == null) return false;

        foreach (var room in _rooms)
        {
            Vector3 min = DungeonGeometry.GetRoomCornerWorld(room.GridPos);
            Vector3 max = DungeonGeometry.GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);

            if (worldPos.x >= min.x && worldPos.x < max.x &&
                worldPos.y >= min.y && worldPos.y < max.y)
                return true;
        }
        return false;
    }

    public Room? GetRoomAtGrid(Vector2Int gridPos)
    {
        if (_rooms == null) return null;

        foreach (var room in _rooms)
            foreach (var cell in room.Cells)
                if (cell.X == gridPos.x && cell.Y == gridPos.y)
                    return room;

        return null;
    }

    // NOTE: this bounding-box "touching" heuristic (expanding each room's rect by only 1 tile)
    // assumed rooms sat flush against each other. With DungeonGridConstants.RoomGap now
    // separating rooms, this won't detect true neighbors unless RoomGap <= 1. Not used
    // internally — flag it if you hook it up elsewhere (e.g. a minimap).
    public Room[] GetConnectedRooms(Room room)
    {
        var connected = new List<Room>();
        if (_rooms == null) return connected.ToArray();

        foreach (var other in _rooms)
        {
            if (other.Type == room.Type && other.GridPos == room.GridPos) continue;

            int aMinX = room.GridPos.x - 1;
            int aMaxX = room.GridPos.x + room.Width + 1;
            int aMinY = room.GridPos.y - 1;
            int aMaxY = room.GridPos.y + room.Height + 1;

            int bMinX = other.GridPos.x - 1;
            int bMaxX = other.GridPos.x + other.Width + 1;
            int bMinY = other.GridPos.y - 1;
            int bMaxY = other.GridPos.y + other.Height + 1;

            if (aMaxX >= bMinX && aMinX <= bMaxX && aMaxY >= bMinY && aMinY <= bMaxY)
                connected.Add(other);
        }
        return connected.ToArray();
    }
}