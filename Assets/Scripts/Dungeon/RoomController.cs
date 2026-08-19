using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime logic for one generated room: spawns its enemies (defined per RoomTemplateSO, at
/// manually marked points), tracks whether the player has actually entered it yet, and reports
/// when the room is cleared — either because the last enemy died, or because it never had any
/// enemies to begin with.
///
/// PlayerHasEntered exists so Door can distinguish "this room has active enemies but the player
/// hasn't walked in yet" (doors stay open — the player needs to be able to reach the room at
/// all) from "the player is inside a room with active enemies right now" (doors lock, Isaac-
/// style, until it's cleared). Without this, IsCleared alone would already be false the instant
/// a room is generated, locking every door touching it before the player ever arrives.
/// </summary>
public class RoomController : MonoBehaviour
{
    public Room RoomData { get; private set; }
    public bool IsCleared { get; private set; }

    /// <summary>True once the player has entered this room at least once (via
    /// RoomCamera.OnRoomEntered matching this room's GridPos). Never resets back to false —
    /// re-entering an already-visited-but-not-cleared room should still lock the doors, which
    /// ShouldLockDoors already handles via !IsCleared, so this only needs to flip on once.</summary>
    public bool PlayerHasEntered { get; private set; }

    /// <summary>Whether this room's doors should currently be locked: the player is/has been
    /// inside and it isn't cleared yet. Door.Reevaluate reads this instead of IsCleared alone.</summary>
    public bool ShouldLockDoors => PlayerHasEntered && !IsCleared;

    /// <summary>Fired when the room's enemy count reaches zero.</summary>
    public event Action OnCleared;

    /// <summary>Fired whenever ShouldLockDoors may have changed — on both PlayerHasEntered
    /// flipping true and on OnCleared — so Door only needs to subscribe to one event to stay
    /// in sync with both triggers.</summary>
    public event Action OnLockStateChanged;

    private readonly List<GameObject> _activeEnemies = new();

    public void Initialize(Room roomData)
    {
        RoomData = roomData;

        if (RoomCamera.Instance != null)
        {
            RoomCamera.Instance.OnRoomEntered += HandleRoomEntered;

            // Covers the edge case where RoomCamera already reports the player standing in this
            // room's cell by the time this controller spawns (e.g. the Start room, if generation
            // order ever puts room-controller creation after the player's first camera snap).
            if (RoomCamera.Instance.CurrentRoom.HasValue)
                HandleRoomEntered(RoomCamera.Instance.CurrentRoom.Value);
        }
        else
        {
            Debug.LogWarning($"[RoomController] '{name}' initialized before RoomCamera.Instance existed — "
                + "PlayerHasEntered will never be set for this room, so its doors will stay permanently unlocked. "
                + "Check script execution order if this happens consistently.");
        }
    }

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

    private void HandleRoomEntered(Room enteredRoom)
    {
        if (PlayerHasEntered) return; // only need to flip this once
        if (enteredRoom.GridPos != RoomData.GridPos) return;

        PlayerHasEntered = true;
        OnLockStateChanged?.Invoke();
    }

    private void MarkClearedIfEmpty()
    {
        if (IsCleared || _activeEnemies.Count > 0) return;

        IsCleared = true;
        OnCleared?.Invoke();
        OnLockStateChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.OnRoomEntered -= HandleRoomEntered;
    }
}