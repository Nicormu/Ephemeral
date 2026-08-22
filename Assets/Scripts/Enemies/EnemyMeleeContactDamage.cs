using UnityEngine;

/// <summary>
/// Damages the player on physical contact, with a per-target cooldown so standing inside the
/// enemy doesn't chain-damage every frame. Tracks the player via Enter/Exit instead of relying
/// on OnCollisionStay2D/OnTriggerStay2D firing every physics step — Unity's 2D physics engine
/// puts non-moving Rigidbody2D pairs to sleep, which stops Stay callbacks from firing while both
/// the enemy and the player are stationary. Damage is now ticked from Update() using Time.time,
/// so it keeps applying on cooldown regardless of physics sleep state.
/// </summary>
public class EnemyMeleeContactDamage : MonoBehaviour
{
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _damageCooldown = 1f;

    private float _lastHitTime = -999f;
    private PlayerMovement _playerInRange;

    private void OnCollisionEnter2D(Collision2D collision) => TryTrack(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => Untrack(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryTrack(other);
    private void OnTriggerExit2D(Collider2D other) => Untrack(other);

    private void TryTrack(Collider2D other)
    {
        var movement = other.GetComponent<PlayerMovement>();
        if (movement == null) return; // e.g. the player's Visuals collider, which has no PlayerMovement
        _playerInRange = movement;
    }

    private void Untrack(Collider2D other)
    {
        var movement = other.GetComponent<PlayerMovement>();
        if (movement == null || movement != _playerInRange) return;
        _playerInRange = null;
    }

    private void Update()
    {
        if (_playerInRange == null) return;
        if (Time.time - _lastHitTime < _damageCooldown) return;

        PlayerHealth.Instance?.TakeDamage(_damage);
        _lastHitTime = Time.time;
    }
}