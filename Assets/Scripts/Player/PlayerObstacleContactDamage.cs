using UnityEngine;

/// <summary>
/// Temporary damage source for destructible obstacles while the player has no weapon system
/// yet: bumping into a DestructibleObstacle deals a fixed amount of damage, with a per-target
/// cooldown so standing against it doesn't chain-break it instantly. Mirrors
/// EnemyMeleeContactDamage's dual OnCollisionStay2D/OnTriggerStay2D pattern (blocking vs.
/// walkable obstacles use different collider types), aimed the other direction.
///
/// Once weapons exist, obstacle-breaking should move to whatever deals the hit (melee swing,
/// projectile, bomb) calling DestructibleObstacle.TakeDamage() directly with its own damage
/// value — this component can then be removed, or kept as a secondary "bump" source.
/// </summary>
public class PlayerObstacleContactDamage : MonoBehaviour
{
    [Tooltip("Damage dealt to a DestructibleObstacle per contact. Placeholder until weapons exist.")]
    [SerializeField] private int _damage = 1;

    [SerializeField] private float _damageCooldown = 0.5f;

    private float _lastHitTime = -999f;

    private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

    private void TryDamage(Collider2D other)
    {
        if (Time.time - _lastHitTime < _damageCooldown) return;

        var obstacle = other.GetComponent<DestructibleObstacle>();
        if (obstacle == null) return;

        obstacle.TakeDamage(_damage);
        _lastHitTime = Time.time;
    }
}