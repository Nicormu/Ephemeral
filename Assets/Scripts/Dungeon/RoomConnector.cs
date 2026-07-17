using System.Collections.Generic;
using UnityEngine;

public static class RoomConnector
{
    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    public static bool ValidateConnectivity(List<Room> rooms, out List<Room> disconnectedRooms)
    {
        disconnectedRooms = new List<Room>();
        if (rooms == null || rooms.Count == 0) return false;

        var posToIndex = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < rooms.Count; i++)
            posToIndex[rooms[i].GridPos] = i;

        int startIndex = FindStartRoom(rooms);
        if (startIndex < 0) return false;

        var visited = new bool[rooms.Count];
        var queue = new Queue<int>();
        visited[startIndex] = true;
        queue.Enqueue(startIndex);
        int visitedCount = 0;

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            visitedCount++;
            var room = rooms[idx];

            foreach (var dir in AllDirections)
            {
                if ((room.Doors & dir) == 0) continue;

                Vector2Int neighborPos = room.GridPos + DirectionOffset(dir, room.Width, room.Height);
                if (posToIndex.TryGetValue(neighborPos, out int neighborIdx) && !visited[neighborIdx])
                {
                    visited[neighborIdx] = true;
                    queue.Enqueue(neighborIdx);
                }
            }
        }

        for (int i = 0; i < rooms.Count; i++)
            if (!visited[i]) disconnectedRooms.Add(rooms[i]);

        bool allConnected = visitedCount == rooms.Count;
        if (!allConnected)
        {
            var names = new List<string>(disconnectedRooms.Count);
            foreach (var room in disconnectedRooms)
                names.Add($"Room at ({room.GridPos.x},{room.GridPos.y}) type={room.Type} doors={room.Doors}");
            Debug.LogError($"[RoomConnector] {disconnectedRooms.Count}/{rooms.Count} rooms unreachable:\n" + string.Join("\n", names));
        }

        return allConnected;
    }

    private static Vector2Int DirectionOffset(DoorDirection dir, int width, int height) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, height),
        DoorDirection.South => new Vector2Int(0, -height),
        DoorDirection.East  => new Vector2Int(width, 0),
        DoorDirection.West  => new Vector2Int(-width, 0),
        _ => Vector2Int.zero
    };

    private static int FindStartRoom(IReadOnlyList<Room> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i].Type == RoomType.Start) return i;
        return -1;
    }
}