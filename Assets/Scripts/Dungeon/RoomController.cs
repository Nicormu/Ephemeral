using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime logic for one generated room: spawns its enemies (defined per RoomTemplateSO, at
/// manually marked points) and reports when the room is cleared — either because the last
/// enemy died, or because it never had any enemies to begin with.
/// </summary>
public class RoomController : MonoBehaviour
{
    public Room RoomData { get; private set; }
    public bool IsCleared { get; private set; }

    public event Action OnCleared;

    private readonly List<GameObject> _activeEnemies = new();

    public void Initialize(Room roomData)
    {
        RoomData = roomData;
    }

    /// <summary>Spawns each enemy at its manually-marked spawn point (Room.EnemySpawns).</summary>
    public void SpawnEnemies(Room.EnemySpawn[] spawns)
    {
        if (spawns == null || spawns.Length == 0)
        {
            MarkClearedIfEmpty();
            return;
        }

        foreach (var spawn in spawns)
        {
            if (spawn.Prefab == null) continue;

            Vector3 pos = new Vector3(spawn.WorldCell.x + 0.5f, spawn.WorldCell.y + 0.5f, 0f);
            GameObject enemy = Instantiate(spawn.Prefab, pos, Quaternion.identity, transform);
            _activeEnemies.Add(enemy);
        }

        MarkClearedIfEmpty();
    }

    private void Update()
    {
        if (IsCleared || _activeEnemies.Count == 0) return;

        _activeEnemies.RemoveAll(e => e == null); // enemies destroyed elsewhere (death, pooling, etc.)
        MarkClearedIfEmpty();
    }

    private void MarkClearedIfEmpty()
    {
        if (IsCleared || _activeEnemies.Count > 0) return;

        IsCleared = true;
        OnCleared?.Invoke();
    }
}