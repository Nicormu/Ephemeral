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
    [Tooltip("Single timer that does two jobs: (1) how long the player hangs in the void before being teleported back to safety, and (2) how long the hazard system waits afterward before it will trigger on this player again.")]
    [SerializeField] private float _hazardRecoveryTime = 0.6f;

    [Header("Detection")]
    [Tooltip("Point sampled for hazard/void cell checks. Leave empty to use this GameObject's own transform (the sprite's pivot). Override with a child positioned at the character's visual base/feet if the sprite's pivot sits higher (e.g. center-of-sprite for a tall character) — otherwise the player only 'falls' once the pivot itself crosses into a Void cell, well after the feet visually reach the edge. Same idea as YSortRenderer's Sort Reference field — reuse that same child here if one already exists, so there's a single 'feet' point instead of two separately configured ones.")]
    [SerializeField] private Transform _feetReference;

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

        if (_feetReference == null) _feetReference = transform;
    }

    private void FixedUpdate()
    {
        if (_isRecovering || _hazardCoroutineRunning || DungeonManager.Instance == null) return;

        Vector2Int cell = DungeonManager.WorldToGridCell(_feetReference.position);
        CellState state = DungeonManager.Instance.GetCellState(cell);

        int damage = state switch
        {
            CellState.Void => _voidFallDamage,
            // Walkable hazard obstacles (fire, etc.) carry their own damage amount, defined
            // per obstacle type in the RoomTemplateSO — never hardcoded per hazard "kind" here.
            CellState.Obstacle => DungeonManager.Instance.GetObstacleHazardDamage(cell),
            _ => 0
        };

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
            // Single cooldown across all hazard types (Void, Fire, future ones) — reused as
            // the recovery wait below, so there's only one number to tune per hazard cycle.
            if (Time.time - _lastDamageTime < _hazardRecoveryTime)
                yield break;

            bool firstHit = PlayerHealth.Instance != null && !PlayerHealth.Instance.IsInvulnerable;

            if (!firstHit)
            {
                // Already invulnerable — just tick damage without stopping movement or teleporting.
                PlayerHealth.Instance?.TakeDamage(damageAmount);
                yield break;
            }

            _isRecovering = true;

            // Lock player input (not the whole component — see PlayerMovement.SetInputEnabled)
            // so HandleMovement keeps running and settles into Idle with zero velocity instead
            // of freezing whatever state/direction was active the instant input was cut. That's
            // what keeps PlayerAnimator's IsMoving/Facing updating correctly through the fall
            // instead of freezing on the last animation frame. Disabling the whole PlayerMovement
            // component (the old approach) also nulled out PlayerMovement.Instance via OnDisable,
            // which is what caused the freeze in the first place — never disable the component
            // itself for this.
            if (_movement != null) _movement.SetInputEnabled(false);

            // Apply damage (PlayerHealth handles its own invulnerability window).
            PlayerHealth.Instance?.TakeDamage(damageAmount);

            // Wait for recovery delay, then teleport to the nearest safe floor tile.
            yield return new WaitForSeconds(_hazardRecoveryTime);

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

            if (_movement != null) _movement.SetInputEnabled(true);
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