#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Editor-only gizmo drawing for the current dungeon layout — room bounds, door gaps, and
/// obstacle markers. Called from DungeonManager.OnDrawGizmos (Unity requires that callback to
/// live on the MonoBehaviour itself, so this is a one-line hook from there).
/// </summary>
public static class DungeonGizmos
{
    public static void Draw(FloorLayout.DungeonResult layout)
    {
        if (layout.Rooms == null) return;

        foreach (var room in layout.Rooms)
        {
            Gizmos.color = GetRoomColor(room.Type);
            Vector3 min = DungeonGeometry.GetRoomCornerWorld(room.GridPos);
            Vector3 max = DungeonGeometry.GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);
            Gizmos.DrawWireCube((min + max) / 2f, max - min);

            Vector3 center = (min + max) / 2f;
            Gizmos.color = Color.cyan;
            if ((room.Doors & DoorDirection.North) != 0) Gizmos.DrawLine(new Vector3(center.x, max.y, 0), new Vector3(center.x, max.y - 0.5f, 0));
            if ((room.Doors & DoorDirection.South) != 0) Gizmos.DrawLine(new Vector3(center.x, min.y, 0), new Vector3(center.x, min.y + 0.5f, 0));
            if ((room.Doors & DoorDirection.East)  != 0) Gizmos.DrawLine(new Vector3(max.x, center.y, 0), new Vector3(max.x - 0.5f, center.y, 0));
            if ((room.Doors & DoorDirection.West)  != 0) Gizmos.DrawLine(new Vector3(min.x, center.y, 0), new Vector3(min.x + 0.5f, center.y, 0));

            foreach (var cell in room.Cells)
            {
                if (cell.State != CellState.Obstacle) continue;

                if (cell.ObstacleBlocksMovement)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f), Vector3.one * 0.6f);
                }
                else
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawWireSphere(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f), 0.3f);
                }
            }
        }

        if (layout.Rooms.Count > 0)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(DungeonGeometry.GetRoomCenterWorld(layout.StartPosition, 1, 1), 0.5f);
        }
    }

    private static Color GetRoomColor(RoomType type) => type switch
    {
        RoomType.Start      => Color.green,
        RoomType.Normal     => Color.gray,
        RoomType.Treasure   => Color.yellow,
        RoomType.Boss       => Color.red,
        RoomType.DeadEnd    => Color.magenta,
        _                   => Color.white,
    };
}
#endif