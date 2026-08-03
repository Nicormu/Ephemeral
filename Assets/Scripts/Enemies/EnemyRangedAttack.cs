using UnityEngine;

/// <summary>
/// Fires a Projectile prefab at the player at a fixed interval, whenever the player is within
/// range. Works whether or not the enemy also has EnemyChaseMovement — a stationary turret and
/// a "chase and shoot" enemy both use this same component.
/// </summary>
public class EnemyRangedAttack : MonoBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint; // leave empty to fire from this transform
    [SerializeField] private float _fireInterval = 1.5f;
    [SerializeField] private float _detectionRange = 6f;

    private float _lastFireTime = -999f;

    private void Update()
    {
        if (PlayerMovement.Instance == null || _projectilePrefab == null) return;
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
}