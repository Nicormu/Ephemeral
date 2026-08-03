using UnityEngine;

/// <summary>
/// Straight-line projectile fired by EnemyRangedAttack. Damages the player on contact and
/// self-destructs on hit or after _lifetime seconds (so a shot that never hits anything doesn't
/// live forever).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _lifetime = 4f;

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, _lifetime);
    }

    public void Launch(Vector2 direction)
    {
        _rb.linearVelocity = direction * _speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var movement = other.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            PlayerHealth.Instance?.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }

        // Hitting a wall also ends the projectile. Adjust the tag/layer check to match your setup.
        if (other.gameObject.layer == LayerMask.NameToLayer("Default") && other.GetComponent<PlayerMovement>() == null)
            Destroy(gameObject);
    }
}