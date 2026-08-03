using UnityEngine;

/// <summary>
/// Damages the player on physical contact, with a per-target cooldown so standing inside the
/// enemy doesn't chain-damage every frame. Add this alongside EnemyChaseMovement for a classic
/// melee chaser, or on its own for a stationary contact hazard.
/// </summary>
public class EnemyMeleeContactDamage : MonoBehaviour
{
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _damageCooldown = 1f;

    private float _lastHitTime = -999f;

    private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

    private void TryDamage(Collider2D other)
    {
        if (Time.time - _lastHitTime < _damageCooldown) return;

        var movement = other.GetComponent<PlayerMovement>();
        if (movement == null) return;

        PlayerHealth.Instance?.TakeDamage(_damage);
        _lastHitTime = Time.time;
    }
}