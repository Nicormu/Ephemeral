using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Spawns one door PER ROOM per flagged direction — since rooms are separated by a gap
/// (DungeonGridConstants.RoomGap) instead of touching, a connection between two rooms is two
/// independent physical doors (one on each room's own edge), not one shared tile.
///
/// Only two prefabs are needed, mirroring the wall-tile convention (Top/Bottom/Side — only 3
/// tile slots, Side reused for both E/W): horizontalDoorPrefab is authored for North and reused
/// for South (rotated 180°); verticalDoorPrefab is authored for West and reused for East
/// (rotated 180°).
/// </summary>
public class DungeonDoorSpawner
{
    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private readonly GameObject _topDoorPrefab;
    private readonly GameObject _bottomDoorPrefab;
    private readonly GameObject _verticalDoorPrefab;
    private readonly int _doorSortingOrderOffset;
    private readonly Tilemap _wallTilemap;

    public DungeonDoorSpawner(GameObject topDoorPrefab, GameObject bottomDoorPrefab, GameObject verticalDoorPrefab, int doorSortingOrderOffset, Tilemap wallTilemap)
    {
        _topDoorPrefab = topDoorPrefab;
        _bottomDoorPrefab = bottomDoorPrefab;
        _verticalDoorPrefab = verticalDoorPrefab;
        _doorSortingOrderOffset = doorSortingOrderOffset;
        _wallTilemap = wallTilemap;
    }

    public void SpawnDoors(List<Room> rooms, Transform doorContainer, Dictionary<Vector2Int, RoomController> roomControllers)
    {
        if (rooms == null) return;

        foreach (var room in rooms)
            foreach (var dir in AllDirections)
                if ((room.Doors & dir) != 0)
                    PlaceDoor(room, dir, doorContainer, roomControllers);
    }

    private void PlaceDoor(Room room, DoorDirection dir, Transform doorContainer, Dictionary<Vector2Int, RoomController> roomControllers)
    {
        Vector3 worldPos = DungeonGeometry.GetDoorWorldPosition(room, dir);
        Quaternion rotation;
        GameObject prefabToUse;

        switch (dir)
        {
            case DoorDirection.North:
                rotation = Quaternion.identity; 
                prefabToUse = _topDoorPrefab;
                break;

            case DoorDirection.South:
                // Assuming bottomDoorPrefab is authored specifically for the bottom wall, no rotation needed.
                rotation = Quaternion.identity; 
                prefabToUse = _bottomDoorPrefab;
                break;

            case DoorDirection.West:
                rotation = Quaternion.identity; // verticalDoorPrefab is authored for this
                prefabToUse = _verticalDoorPrefab;
                break;

            case DoorDirection.East:
                // Reusing vertical art for East, flipped 180° to face outward.
                rotation = Quaternion.Euler(0f, 0f, 180f);
                prefabToUse = _verticalDoorPrefab;
                break;

            default:
                return;
        }

        if (prefabToUse == null) return;

        GameObject instance = Object.Instantiate(prefabToUse, worldPos, rotation, doorContainer);
        instance.name = $"Door_{dir}_{room.GridPos.x}_{room.GridPos.y}";

        ApplyDoorSortingOrder(instance);

        var door = instance.GetComponent<Door>();
        if (door == null)
        {
            Debug.LogWarning("[DungeonDoorSpawner] doorPrefab has no Door component — it won't open/close automatically.");
            return;
        }

        Vector2Int neighborGridPos = room.GridPos + DungeonGeometry.DirectionOffset(dir, room.Width, room.Height);

        if (roomControllers.TryGetValue(room.GridPos, out var ownerController))
            door.RegisterRoom(ownerController);

        if (roomControllers.TryGetValue(neighborGridPos, out var neighborController))
        {
            door.RegisterRoom(neighborController);

            Vector3 entryPoint = DungeonGeometry.GetDoorEntryPoint(neighborController.RoomData, DungeonGeometry.Opposite(dir));
            door.SetTeleportDestination(entryPoint);
        }
        else
        {
            Debug.LogWarning($"[DungeonDoorSpawner] Door_{dir}_{room.GridPos.x}_{room.GridPos.y} couldn't find its neighbor room — disabling its entry trigger.");
            door.DisableEntryTrigger();
        }
    }

    /// <summary>Forces every SpriteRenderer on a door instance onto the wall tilemap's Sorting
    /// Layer with a higher Sorting Order, so doors always render on top of the wall art behind
    /// them regardless of scene Sorting Layer setup.</summary>
    private void ApplyDoorSortingOrder(GameObject doorInstance)
    {
        if (_wallTilemap == null) return;

        var wallRenderer = _wallTilemap.GetComponent<TilemapRenderer>();
        if (wallRenderer == null) return;

        var spriteRenderers = doorInstance.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            sr.sortingLayerID = wallRenderer.sortingLayerID;
            sr.sortingOrder = wallRenderer.sortingOrder + _doorSortingOrderOffset;
        }
    }
}