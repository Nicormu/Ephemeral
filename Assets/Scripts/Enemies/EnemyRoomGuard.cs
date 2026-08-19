using UnityEngine;

/// <summary>
/// Confines an enemy's awareness of the player to the room it was spawned in, and resets the
/// enemy back to its spawn transform (+ notifies other components to reset their own state) if
/// the player leaves that room while the enemy is still alive (i.e. the room wasn't cleared).
///
/// Sits alongside EnemyChaseMovement / EnemyRangedAttack — those components should read
/// IsPlayerInRoom before doing any distance-based detection, instead of checking distance alone.
/// This is what makes room walls a hard detection boundary regardless of how large a component's
/// own _detectionRange is set to.
///
/// Room membership comes from the enemy's own parent hierarchy: RoomController.SpawnEnemies
/// instantiates every enemy as a child of that room's logic GameObject (see DungeonManager.
/// SpawnRoomControllersAndEnemies -> RoomController.SpawnEnemies), so GetComponentInParent
/// resolves it with zero manual Inspector wiring — this component works purely by being present
/// on the enemy prefab.
///
/// "Current room" tracking piggybacks on RoomCamera.OnRoomEntered — the same event
/// MinimapController already subscribes to — so every enemy doesn't need its own per-frame grid
/// query; they just compare the room they belong to against the room RoomCamera last reported.
/// </summary>
[DisallowMultipleComponent]
public class EnemyRoomGuard : MonoBehaviour
{
    [Tooltip("If true (default), this enemy resets to its spawn transform (and asks other components to reset their own runtime state, e.g. attack cooldowns) the moment the player leaves its room without clearing it. Turn off for enemies that should keep their state regardless — e.g. a boss.")]
    [SerializeField] private bool _resetOnPlayerExit = true;

    private RoomController _ownerRoom;
    private Rigidbody2D _rb;

    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    private bool _playerInRoom;
    private IEnemyResettable[] _resettables;

    /// <summary>True only while the player is inside this enemy's own room. EnemyChaseMovement /
    /// EnemyRangedAttack should gate their existing distance checks behind this.</summary>
    public bool IsPlayerInRoom => _playerInRoom;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>(); // optional — enemies without one just skip velocity reset
        _ownerRoom = GetComponentInParent<RoomController>();
        _resettables = GetComponents<IEnemyResettable>();

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        if (_ownerRoom == null)
            Debug.LogWarning($"[EnemyRoomGuard] '{name}' has no RoomController in its parent hierarchy — it will never detect the player (IsPlayerInRoom stays false). Make sure enemies are spawned as children of a room's logic GameObject.");
    }

    private void OnEnable()
    {
        if (RoomCamera.Instance != null)
        {
            RoomCamera.Instance.OnRoomEntered += HandleRoomEntered;

            if (RoomCamera.Instance.CurrentRoom.HasValue)
                HandleRoomEntered(RoomCamera.Instance.CurrentRoom.Value);
        }
    }

    private void OnDisable()
    {
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.OnRoomEntered -= HandleRoomEntered;
    }

    private void HandleRoomEntered(Room enteredRoom)
    {
        bool wasInRoom = _playerInRoom;
        _playerInRoom = _ownerRoom != null && enteredRoom.GridPos == _ownerRoom.RoomData.GridPos;

        // Player just left this enemy's room (was in, now isn't) — reset if the room wasn't
        // cleared. If it WAS cleared, RoomController already reports IsCleared and there's
        // nothing left to reset (enemy is dead/removed by then anyway in the normal case, but
        // this guards a future "cleared but enemy somehow survives" scenario too).
        if (wasInRoom && !_playerInRoom && _resetOnPlayerExit)
        {
            if (_ownerRoom == null || !_ownerRoom.IsCleared)
                ResetToSpawn();
        }
    }

    private void ResetToSpawn()
    {
        transform.position = _spawnPosition;
        transform.rotation = _spawnRotation;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        foreach (var resettable in _resettables)
            resettable.ResetEnemyState();
    }
}

/// <summary>Optional interface for any enemy component that holds runtime state (cooldown
/// timers, wind-up flags, etc.) that should snap back to a fresh/idle value when EnemyRoomGuard
/// resets the enemy — e.g. EnemyRangedAttack's fire cooldown. Components that don't need this
/// (e.g. EnemyChaseMovement, which is fully driven by transform + Rigidbody every frame) don't
/// need to implement it.</summary>
public interface IEnemyResettable
{
    void ResetEnemyState();
}