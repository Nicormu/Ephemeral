using System.Collections;
using UnityEngine;

/// <summary>
/// Fires a Projectile prefab at the player at a fixed interval, whenever the player is within
/// range AND inside this enemy's own room (see EnemyRoomGuard) — a room wall is a hard detection
/// boundary regardless of how large _detectionRange is set. Now includes a windup: entering the
/// attack cycle plays the Attack animation via EnemyAnimator, and the projectile only actually
/// launches _windupDuration seconds later (tune this to match the clip's "release frame"), with
/// a re-check that the player is still in range/room at that moment. Works whether or not the
/// enemy also has EnemyChaseMovement — a stationary turret and a "chase and shoot" enemy both
/// use this same component.
///
/// Implements IEnemyResettable so EnemyRoomGuard clears the fire cooldown AND cancels any
/// in-progress windup on reset — otherwise an enemy reset mid-windup could still fire a shot from
/// its old position/target a moment later.
/// </summary>
[RequireComponent(typeof(EnemyRoomGuard))]
[RequireComponent(typeof(EnemyAnimator))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyResettable
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint; // leave empty to fire from this transform
    [SerializeField] private float _fireInterval = 1.5f;
    [SerializeField] private float _detectionRange = 6f;

    [Tooltip("Seconds from the Attack trigger firing until the projectile is actually launched — should match the 'release frame' of the Attack animation clip.")]
    [SerializeField] private float _windupDuration = 0.25f;

    private EnemyRoomGuard _roomGuard;
    private EnemyAnimator _enemyAnimator;
    private float _lastFireTime = -999f;
    private bool _isWindingUp;
    private Coroutine _windupRoutine;

    private void Awake()
    {
        _roomGuard = GetComponent<EnemyRoomGuard>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
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
        _windupRoutine = StartCoroutine(WindupThenFire());
    }

    private IEnumerator WindupThenFire()
    {
        yield return new WaitForSeconds(_windupDuration);

        // Re-check the player is still in range/room — they may have fled or left mid-windup.
        if (PlayerMovement.Instance != null && _roomGuard.IsPlayerInRoom)
        {
            Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
            Vector3 toPlayer = PlayerMovement.Instance.transform.position - origin;

            if (toPlayer.magnitude <= _detectionRange)
                Fire(origin, toPlayer.normalized);
        }

        _isWindingUp = false;
        _windupRoutine = null;
    }

    private void Fire(Vector3 origin, Vector2 direction)
    {
        GameObject instance = Instantiate(_projectilePrefab, origin, Quaternion.identity);
        var projectile = instance.GetComponent<Projectile>();
        if (projectile != null)
            projectile.Launch(direction);
    }

    /// <summary>Called by EnemyRoomGuard on reset, and by EnemyHealth on death — clears the fire
    /// cooldown and cancels any in-progress windup coroutine.</summary>
    public void ResetEnemyState()
    {
        _lastFireTime = -999f;

        if (_windupRoutine != null)
        {
            StopCoroutine(_windupRoutine);
            _windupRoutine = null;
        }

        _isWindingUp = false;
    }
}