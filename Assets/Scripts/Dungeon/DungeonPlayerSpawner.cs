using System.Collections.Generic;
using UnityEngine;

/// <summary>Computes and applies the player's spawn position inside the Start room.</summary>
public class DungeonPlayerSpawner
{
    private readonly DungeonManager.PlayerSpawnMode _spawnMode;
    private readonly Vector3 _spawnOffset;

    public Vector3 SpawnWorldPosition { get; private set; }

    public DungeonPlayerSpawner(DungeonManager.PlayerSpawnMode spawnMode, Vector3 spawnOffset)
    {
        _spawnMode = spawnMode;
        _spawnOffset = spawnOffset;
    }

    public void CalculateSpawnPosition(List<Room> rooms, Vector2Int startGridPosition)
    {
        Vector3 spawnPos = DungeonGeometry.GetRoomCornerWorld(startGridPosition);

        if (_spawnMode == DungeonManager.PlayerSpawnMode.RoomCenter)
        {
            foreach (var room in rooms)
            {
                if (room.Type == RoomType.Start)
                {
                    spawnPos = DungeonGeometry.GetRoomCenterWorld(room.GridPos, room.Width, room.Height);
                    break;
                }
            }
        }

        SpawnWorldPosition = spawnPos + _spawnOffset;
    }

    /// <summary>Moves the player (if present in the scene) to the calculated spawn position.</summary>
    public void PositionPlayerAtSpawn()
    {
        if (PlayerMovement.Instance == null)
        {
            Debug.LogWarning("[DungeonPlayerSpawner] No PlayerMovement.Instance found in the scene — "
                + "can't move the player to the spawn point. Make sure the Player object is active "
                + "before dungeon generation runs.");
            return;
        }

        PlayerMovement.Instance.TeleportTo(SpawnWorldPosition);
    }
}