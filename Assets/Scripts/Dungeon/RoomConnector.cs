using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates and annotates room connectivity in a generated dungeon using BFS.
///
/// Call Validate() after generation to ensure every room is reachable from the Start room.
/// Unreachable rooms are logged as errors so you can detect dead dungeons during development.
/// </summary>
public static class RoomConnector
{
    /// <summary>
    /// Validate that all rooms in the dungeon are connected to the Start room via BFS.
    /// Returns true if every room is reachable.
    /// </summary>
    public static bool ValidateConnectivity(DungeonResult result, out List<Room> disconnectedRooms)
    {
        var rooms = result.Rooms;
        if (rooms == null || rooms.Count == 0)
        {
            disconnectedRooms = new List<Room>();
            return false;
        }

        // Separate arrays for connectivity tracking — avoids mutating Room structs directly.
        var isConnected = new bool[rooms.Count];
        var connectedFrom = new List<int>[rooms.Count]; // store indices of connected neighbors

        // BFS from Start room.
        var queue = new Queue<int>();
        int startIndex = FindStartRoom(rooms);
        if (startIndex < 0)
        {
            disconnectedRooms = new List<Room>();
            return false;
        }

        isConnected[startIndex] = true;
        queue.Enqueue(startIndex);
        int visitedCount = 0;

        while (queue.Count > 0)
        {
            int currentIdx = queue.Dequeue();
            visitedCount++;

            var currentRoom = rooms[currentIdx];
            for (int i = 0; i < rooms.Count; i++)
            {
                if (isConnected[i]) continue; // already visited.
                if (IsAdjacent(currentRoom, rooms[i]))
                {
                    isConnected[i] = true;
                    connectedFrom[i] = new List<int> { currentIdx };
                    connectedFrom[currentIdx].Add(i);
                    queue.Enqueue(i);
                }
            }
        }

        disconnectedRooms = new List<Room>();
        for (int i = 0; i < rooms.Count; i++)
        {
            if (!isConnected[i])
                disconnectedRooms.Add(rooms[i]);
        }

        bool allConnected = visitedCount == rooms.Count;
        if (!allConnected)
        {
            var names = new List<string>(disconnectedRooms.Count);
            foreach (var room in disconnectedRooms)
                names.Add($"Room at ({room.GridPos.x},{room.GridPos.y}) type={room.Type}");
            Debug.LogError($"[RoomConnector] {disconnectedRooms.Count}/{rooms.Count} rooms unreachable:\n" + string.Join("\n", names));
        }

        return allConnected;
    }

    /// <summary>
    /// Two rooms are considered adjacent if their grid bounding boxes overlap or touch (expanded by 1 cell for exits).
    /// </summary>
    private static bool IsAdjacent(Room a, Room b)
    {
        // Each room's bounds expanded by 1 cell (tolerance for exit portals).
        int aMinX = a.GridPos.x - 1;
        int aMaxX = a.GridPos.x + a.Width + 1;
        int aMinY = a.GridPos.y - 1;
        int aMaxY = a.GridPos.y + a.Height + 1;

        int bMinX = b.GridPos.x - 1;
        int bMaxX = b.GridPos.x + b.Width + 1;
        int bMinY = b.GridPos.y - 1;
        int bMaxY = b.GridPos.y + b.Height + 1;

        // Two rectangles overlap if neither is completely to the left/right or above/below the other.
        return aMaxX >= bMinX && aMinX <= bMaxX && aMaxY >= bMinY && aMinY <= bMaxY;
    }

    private static int FindStartRoom(IReadOnlyList<Room> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Type == RoomType.Start) return i;
        }
        // Fallback: assume index 0 is start.
        return 0;
    }
}
