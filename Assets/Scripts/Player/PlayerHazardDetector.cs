using System.Collections;
using UnityEngine;

/// <summary>Unified hazard detector that handles void fall and any walkable-but-damaging
/// obstacle (fire, spikes, etc. — configured per obstacle type in each RoomTemplateSO, not
/// hardcoded here). When the player stands in a Void cell, or an Obstacle cell that doesn't
/// block movement and deals damage, they take that damage, are given brief invulnerability
/// via PlayerHealth, then teleported to the nearest Floor tile within the current room.</summary>
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHazardDetector : MonoBehaviour
{
    [Header("Void Fall Settings")]
    [Tooltip("Damage taken when falling into a Void cell.")]
    [SerializeField] private int _voidFallDamage = 1;

    [Header("Recovery")]
    [Tooltip("Delay before the player is returned to safety after hitting a hazard.")]
    [SerializeField] private float _recoveryDelay = 0.6f;

    [Tooltip("Time between hazard damage triggers (prevents rapid re-hitting).")]
    [SerializeField] private float _damageCooldown = 1.5f;

    private Rigidbody2D _rb;
    private PlayerMovement _movement;
    private float _lastDamageTime;
    private bool _isRecovering;
    private bool _hazardCoroutineRunning;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _movement = GetComponent<PlayerMovement>();
        _lastDamageTime = -999f; // allow immediate first trigger.
    }

    private void FixedUpdate()
    {
        if (_isRecovering || _hazardCoroutineRunning || DungeonManager.Instance == null) return;

        Vector2Int cell = DungeonManager.WorldToGridCell(transform.position);
        CellState state = DungeonManager.Instance.GetCellState(cell);

        int damage = state switch
        {
            CellState.Void => _voidFallDamage,
            // Walkable hazard obstacles (fire, etc.) carry their own damage amount, defined
            // per obstacle type in the RoomTemplateSO — never hardcoded per hazard "kind" here.
            CellState.Obstacle => DungeonManager.Instance.GetObstacleHazardDamage(cell),
            _ => 0
        };

        // Not standing on a hazard this frame — nothing to do. The flag is only ever set
        // right below, immediately before actually starting the coroutine, so it can never
        // get "stuck" true without a matching coroutine to reset it.
        if (damage <= 0) return;

        _hazardCoroutineRunning = true;
        StartCoroutine(TriggerHazard(damage));
    }

    private IEnumerator TriggerHazard(int damageAmount)
    {
        // finally guarantees the gate flags are released on every exit path (cooldown skip,
        // repeat-hit tick, or full recovery), so FixedUpdate can never get permanently blocked.
        try
        {
            // Shared cooldown across all hazard types (Void, Fire, future ones).
            if (Time.time - _lastDamageTime < _damageCooldown)
                yield break;

            bool firstHit = PlayerHealth.Instance != null && !PlayerHealth.Instance.IsInvulnerable;

            if (!firstHit)
            {
                // Already invulnerable — just tick damage without stopping movement or teleporting.
                PlayerHealth.Instance?.TakeDamage(damageAmount);
                yield break;
            }

            _isRecovering = true;

            // Stop player movement and freeze velocity.
            if (_movement != null) _movement.enabled = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            // Apply damage (PlayerHealth handles its own invulnerability window).
            PlayerHealth.Instance?.TakeDamage(damageAmount);

            // Wait for recovery delay, then teleport to the nearest safe floor tile.
            yield return new WaitForSeconds(_recoveryDelay);

            Vector3? safePos = DungeonManager.Instance.FindNearestSafePositionInRoom(transform.position);

            if (safePos.HasValue)
            {
                transform.position = safePos.Value;
            }
            else
            {
                var startRoom = FindStartRoom();
                if (startRoom.HasValue)
                {
                    transform.position = new Vector3(
                        startRoom.Value.GridPos.x + startRoom.Value.Width / 2f,
                        startRoom.Value.GridPos.y + startRoom.Value.Height / 2f,
                        0f);
                }
            }

            _lastDamageTime = Time.time;

            if (_movement != null) _movement.enabled = true;
        }
        finally
        {
            _hazardCoroutineRunning = false;
            _isRecovering = false;
        }
    }

    private Room? FindStartRoom()
    {
        var rooms = DungeonManager.Instance.Rooms;
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i].Type == RoomType.Start)
                return rooms[i];
        return null;
    }
}