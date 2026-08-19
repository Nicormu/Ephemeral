using UnityEngine;

/// <summary>
/// Fires a Projectile prefab at the player at a fixed interval, whenever the player is within
/// range AND inside this enemy's own room (see EnemyRoomGuard) — a room wall is now a hard
/// detection boundary regardless of how large _detectionRange is set. Works whether or not the
/// enemy also has EnemyChaseMovement — a stationary turret and a "chase and shoot" enemy both
/// use this same component.
///
/// Implements IEnemyResettable so EnemyRoomGuard clears the fire cooldown on reset — otherwise
/// an enemy that was mid-cooldown when the player left would still remember _lastFireTime and
/// could fire instantly the moment the player re-enters, instead of starting fresh.
/// </summary>
[RequireComponent(typeof(EnemyRoomGuard))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyResettable
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint; // leave empty to fire from this transform
    [SerializeField] private float _fireInterval = 1.5f;
    [SerializeField] private float _detectionRange = 6f;

    private EnemyRoomGuard _roomGuard;
    private float _lastFireTime = -999f;

    private void Awake()
    {
        _roomGuard = GetComponent<EnemyRoomGuard>();
    }

    private void Update()
    {
        if (PlayerMovement.Instance == null || _projectilePrefab == null) return;
        if (!_roomGuard.IsPlayerInRoom) return;
        if (Time.time - _lastFireTime < _fireInterval) return;

        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 toPlayer = PlayerMovement.Instance.transform.position - origin;

        if (toPlayer.magnitude > _detectionRange) return;

        Fire(origin, toPlayer.normalized);
        _lastFireTime = Time.time;
    }

    private void Fire(Vector3 origin, Vector2 direction)
    {
        GameObject instance = Instantiate(_projectilePrefab, origin, Quaternion.identity);
        var projectile = instance.GetComponent<Projectile>();
        if (projectile != null)
            projectile.Launch(direction);
    }

    /// <summary>Called by EnemyRoomGuard when it resets this enemy to spawn — clears the
    /// cooldown so the enemy doesn't fire instantly the moment the player re-enters.</summary>
    public void ResetEnemyState()
    {
        _lastFireTime = -999f;
    }
}