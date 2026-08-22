using UnityEngine;

/// <summary>
/// Straight-line projectile fired by EnemyRangedAttack. Damages the player on contact and
/// self-destructs on hit or after _lifetime seconds (so a shot that never hits anything doesn't
/// live forever).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _lifetime = 4f;

    private Rigidbody2D _rb;
    private SpriteRenderer _sr;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        // Same safety net DungeonManager uses for obstacles — force this onto the shared
        // "Entities" sorting layer so it can never render behind the floor tilemap regardless
        // of what the prefab's own Inspector value happens to be.
        _sr.sortingLayerName = DungeonManager.EntitySortingLayerName;

        Destroy(gameObject, _lifetime);
    }

    private void LateUpdate()
    {
        // Recompute every frame — same formula YSortRenderer uses for the player/enemies —
        // since a projectile crosses in front of / behind obstacles as it travels.
        _sr.sortingOrder = DungeonManager.CalculateYSortOrder(transform.position.y);
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