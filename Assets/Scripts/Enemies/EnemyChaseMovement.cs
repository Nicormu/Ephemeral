using UnityEngine;

/// <summary>
/// Moves this enemy straight toward the player at a fixed speed whenever the player is within
/// _detectionRange AND inside this enemy's own room (see EnemyRoomGuard) — a room wall is now a
/// hard detection boundary regardless of how large _detectionRange is set. Add this component to
/// make an enemy "track" — omit it for a stationary enemy (e.g. a turret using only
/// EnemyRangedAttack).
///
/// BUG FIX: chasing never checked whether the enemy was dead, so a bat still slid toward the
/// player for the entire death animation before actually being destroyed. Guarded behind
/// EnemyHealth.IsDead now, same pattern EnemyHazardDetector already uses.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyRoomGuard))]
public class EnemyChaseMovement : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 1.5f;

    [Tooltip("Player must be within this world-space distance for the enemy to start chasing. Set very high to always chase — still only applies while the player is in this enemy's own room, see EnemyRoomGuard.")]
    [SerializeField] private float _detectionRange = 8f;

    [Tooltip("Stops closing the distance once this close, so melee enemies don't jitter on top of the player.")]
    [SerializeField] private float _stoppingDistance = 0.6f;

    private Rigidbody2D _rb;
    private EnemyRoomGuard _roomGuard;
    private EnemyHealth _health; // optional — enemies with no EnemyHealth just never get stopped on death

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _roomGuard = GetComponent<EnemyRoomGuard>();
        _health = GetComponent<EnemyHealth>();
    }

    private void FixedUpdate()
    {
        if (_health != null && _health.IsDead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (PlayerMovement.Instance == null || !_roomGuard.IsPlayerInRoom)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)PlayerMovement.Instance.transform.position - _rb.position;
        float distance = toPlayer.magnitude;

        if (distance > _detectionRange || distance <= _stoppingDistance)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        _rb.linearVelocity = toPlayer.normalized * _moveSpeed;
    }
}