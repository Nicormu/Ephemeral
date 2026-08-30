using UnityEngine;

/// <summary>
/// Fires a Projectile prefab at the player at a fixed interval, whenever the player is within
/// range, inside this enemy's own room (see EnemyRoomGuard), AND in clear line of sight (no wall
/// or blocking obstacle between the enemy and the player — see _lineOfSightBlockingLayers).
/// Entering the attack cycle plays the Attack animation via EnemyAnimator, and the projectile is
/// spawned when EnemyAnimator reports the Attack animation's release point (EnemyAnimator.
/// OnAttackReleased — fired either by an Animation Event on the clip's release frame, or by
/// EnemyAnimator's own fallback timer if that event was never wired up), instead of a
/// separately-tuned windup timer that could drift out of sync with the actual animation. Works
/// whether or not the enemy also has EnemyChaseMovement — a stationary turret and a "chase and
/// shoot" enemy both use this same component.
///
/// STAYING STILL WHILE FIRING: if this enemy also has an EnemyChaseMovement component, it's
/// disabled (and the Rigidbody2D's velocity zeroed) for the entire windup, then re-enabled once
/// the attack resolves (fired, cancelled, or reset) — same disable/re-enable pattern
/// EnemyKnockback already uses to stop chase logic from fighting a knockback impulse.
///
/// Implements IEnemyResettable so EnemyRoomGuard clears the fire cooldown AND cancels any
/// in-progress windup on reset — otherwise an enemy reset mid-windup could still fire a shot from
/// its old position/target once the (now-stale) OnAttackReleased event eventually arrives.
///
/// BUG FIX: neither the "start a new attack" check nor the windup-release handler looked at
/// EnemyHealth.IsDead, so an enemy that died mid-windup could still fire a shot during its death
/// animation, and a fresh windup could even start while already dead. Both are now guarded —
/// same pattern EnemyChaseMovement/EnemyHazardDetector use.
/// </summary>
[RequireComponent(typeof(EnemyRoomGuard))]
[RequireComponent(typeof(EnemyAnimator))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyResettable
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint; // leave empty to fire from this transform
    [SerializeField] private float _fireInterval = 1.5f;
    [SerializeField] private float _detectionRange = 6f;

    [Header("Line of Sight")]
    [Tooltip("Physics2D layers checked between the enemy and the player before winding up AND before releasing a shot. Should include your wall-collision layer and any BLOCKING obstacle layer (e.g. \"Default\" + \"Obstacle\"). Do NOT include layers used by walkable/trigger hazards (fire, spikes), the Player, enemies, projectiles, or FlyingEntity — a shot should still fire over/through those.")]
    [SerializeField] private LayerMask _lineOfSightBlockingLayers;

    private static readonly RaycastHit2D[] _losHitsBuffer = new RaycastHit2D[1];

    private EnemyRoomGuard _roomGuard;
    private EnemyAnimator _enemyAnimator;
    private EnemyHealth _health; // optional — enemies with no EnemyHealth just never get gated on death
    private Rigidbody2D _rb; // optional — used to stop residual motion the instant windup starts
    private EnemyChaseMovement _chaseMovement; // optional — null for stationary turret-style enemies
    private float _lastFireTime = -999f;
    private bool _isWindingUp;

    private void Awake()
    {
        _roomGuard = GetComponent<EnemyRoomGuard>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
        _health = GetComponent<EnemyHealth>();
        _rb = GetComponent<Rigidbody2D>();
        _chaseMovement = GetComponent<EnemyChaseMovement>();
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
        if (_health != null && _health.IsDead) return;
        if (PlayerMovement.Instance == null || _projectilePrefab == null) return;
        if (!_roomGuard.IsPlayerInRoom) return;
        if (Time.time - _lastFireTime < _fireInterval) return;

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 playerPos = PlayerMovement.Instance.transform.position;
        Vector3 toPlayer = playerPos - origin;

        if (toPlayer.magnitude > _detectionRange) return;
        if (!HasLineOfSight(origin, playerPos)) return;

        StartAttack();
    }

    private void StartAttack()
    {
        _isWindingUp = true;
        _lastFireTime = Time.time;

        // Stay still for the whole windup — same mechanism EnemyKnockback uses to stop chase
        // logic from immediately overwriting velocity on the next physics step.
        if (_chaseMovement != null) _chaseMovement.enabled = false;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        _enemyAnimator.PlayAttack();
    }

    /// <summary>Called by EnemyAnimator.OnAttackReleased — either right on the Attack clip's
    /// release frame (via its Animation Event) or its fallback timer. Re-checks the player is
    /// still in range/room/line-of-sight before actually firing, since time has passed since
    /// StartAttack() and they may have fled, left mid-windup, or ducked behind something. Guarded
    /// by _isWindingUp so a stray/late event after a reset (see ResetEnemyState) can't fire a
    /// shot from stale state. Also re-checks IsDead — a windup that started before the killing
    /// blow landed shouldn't still launch a projectile once the death animation is playing.</summary>
    private void HandleAttackReleased()
    {
        if (!_isWindingUp) return;
        _isWindingUp = false;

        // Windup is over either way — hand movement back.
        if (_chaseMovement != null) _chaseMovement.enabled = true;

        if (_health != null && _health.IsDead) return;
        if (PlayerMovement.Instance == null || !_roomGuard.IsPlayerInRoom) return;

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 playerPos = PlayerMovement.Instance.transform.position;
        Vector3 toPlayer = playerPos - origin;

        if (toPlayer.magnitude > _detectionRange) return;
        if (!HasLineOfSight(origin, playerPos)) return;

        Fire(origin, toPlayer.normalized);
    }

    /// <summary>True if nothing on _lineOfSightBlockingLayers sits between origin and target.
    /// Trigger colliders (walkable hazards) never block, regardless of layer — only solid
    /// (non-trigger) colliders count, since a hazard tile is walkable ground clutter, not a wall.</summary>
    private bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(_lineOfSightBlockingLayers);
        filter.useTriggers = false;

        int hitCount = Physics2D.Linecast(origin, target, filter, _losHitsBuffer);
        return hitCount == 0;
    }

    private void Fire(Vector3 origin, Vector2 direction)
    {
        GameObject instance = Instantiate(_projectilePrefab, origin, Quaternion.identity);
        var projectile = instance.GetComponent<Projectile>();
        if (projectile != null)
            projectile.Launch(direction);
    }

    /// <summary>Called by EnemyRoomGuard on reset, and by EnemyHealth on death — clears the fire
    /// cooldown, marks any in-progress windup as cancelled, and hands movement back in case a
    /// reset interrupts a windup mid-flight.</summary>
    public void ResetEnemyState()
    {
        _lastFireTime = -999f;

        if (_isWindingUp && _chaseMovement != null)
            _chaseMovement.enabled = true;

        _isWindingUp = false;
    }
}