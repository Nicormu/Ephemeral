using UnityEngine;

/// <summary>
/// Fires a Projectile prefab at the player at a fixed interval, whenever the player is within
/// range AND inside this enemy's own room (see EnemyRoomGuard) — a room wall is a hard detection
/// boundary regardless of how large _detectionRange is set. Entering the attack cycle plays the
/// Attack animation via EnemyAnimator, and the projectile is spawned when EnemyAnimator reports
/// the Attack animation's release point (EnemyAnimator.OnAttackReleased — fired either by an
/// Animation Event on the clip's release frame, or by EnemyAnimator's own fallback timer if that
/// event was never wired up), instead of a separately-tuned windup timer that could drift out of
/// sync with the actual animation. Works whether or not the enemy also has EnemyChaseMovement —
/// a stationary turret and a "chase and shoot" enemy both use this same component.
///
/// Implements IEnemyResettable so EnemyRoomGuard clears the fire cooldown AND cancels any
/// in-progress windup on reset — otherwise an enemy reset mid-windup could still fire a shot from
/// its old position/target once the (now-stale) OnAttackReleased event eventually arrives.
/// </summary>
[RequireComponent(typeof(EnemyRoomGuard))]
[RequireComponent(typeof(EnemyAnimator))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyResettable
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint; // leave empty to fire from this transform
    [SerializeField] private float _fireInterval = 1.5f;
    [SerializeField] private float _detectionRange = 6f;

    private EnemyRoomGuard _roomGuard;
    private EnemyAnimator _enemyAnimator;
    private float _lastFireTime = -999f;
    private bool _isWindingUp;

    private void Awake()
    {
        _roomGuard = GetComponent<EnemyRoomGuard>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
    }

    private void OnEnable()
    {
        if (_enemyAnimator != null)
            _enemyAnimator.OnAttackReleased += HandleAttackReleased;
    }

    private void OnDisable()
    {
        if (_enemyAnimator != null)
            _enemyAnimator.OnAttackReleased -= HandleAttackReleased;
    }

    private void Update()
    {
        if (_isWindingUp) return; // already committed to this attack cycle
        if (PlayerMovement.Instance == null || _projectilePrefab == null) return;
        if (!_roomGuard.IsPlayerInRoom) return;
        if (Time.time - _lastFireTime < _fireInterval) return;

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 toPlayer = PlayerMovement.Instance.transform.position - origin;

        if (toPlayer.magnitude > _detectionRange) return;

        StartAttack();
    }

    private void StartAttack()
    {
        _isWindingUp = true;
        _lastFireTime = Time.time;

        _enemyAnimator.PlayAttack();
    }

    /// <summary>Called by EnemyAnimator.OnAttackReleased — either right on the Attack clip's
    /// release frame (via its Animation Event) or its fallback timer. Re-checks the player is
    /// still in range/room before actually firing, since time has passed since StartAttack() and
    /// they may have fled or left mid-windup. Guarded by _isWindingUp so a stray/late event after
    /// a reset (see ResetEnemyState) can't fire a shot from stale state.</summary>
    private void HandleAttackReleased()
    {
        if (!_isWindingUp) return;
        _isWindingUp = false;

        if (PlayerMovement.Instance == null || !_roomGuard.IsPlayerInRoom) return;

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 toPlayer = PlayerMovement.Instance.transform.position - origin;

        if (toPlayer.magnitude <= _detectionRange)
            Fire(origin, toPlayer.normalized);
    }

    private void Fire(Vector3 origin, Vector2 direction)
    {
        GameObject instance = Instantiate(_projectilePrefab, origin, Quaternion.identity);
        var projectile = instance.GetComponent<Projectile>();
        if (projectile != null)
            projectile.Launch(direction);
    }

    /// <summary>Called by EnemyRoomGuard on reset, and by EnemyHealth on death — clears the fire
    /// cooldown and marks any in-progress windup as cancelled. If EnemyAnimator's OnAttackReleased
    /// still fires later (fallback timer or a queued Animation Event), HandleAttackReleased's
    /// _isWindingUp guard will simply no-op instead of firing a stale shot.</summary>
    public void ResetEnemyState()
    {
        _lastFireTime = -999f;
        _isWindingUp = false;
    }
}