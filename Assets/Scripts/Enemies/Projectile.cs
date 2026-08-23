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

    [Tooltip("Physics2D Layer this projectile is forced onto at spawn — must have collision with itself DISABLED in Project Settings > Physics 2D > Layer Collision Matrix, so two projectiles never trigger against each other. Create this layer first (it can't be created from script).")]
    [SerializeField] private string _projectileLayerName = "Projectile";

    // Lazily created the first time any Projectile spawns — keeps every projectile organized
    // under one Hierarchy object instead of cluttering the scene root. Static, so it's shared
    // across every Projectile instance/prefab and only ever created once per scene.
    private static Transform _projectilesContainer;

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

        // Force the physics layer too — a projectile prefab left on "Default" (or any layer
        // that collides with itself) is what let two projectiles trigger against and destroy
        // each other. This guarantees every projectile ends up on the correct layer regardless
        // of what a given prefab's Inspector value happens to be, same reasoning as the
        // sorting-layer force above.
        if (!string.IsNullOrEmpty(_projectileLayerName))
        {
            int resolved = LayerMask.NameToLayer(_projectileLayerName);
            if (resolved < 0)
                Debug.LogWarning($"[Projectile] Layer '{_projectileLayerName}' doesn't exist — create it under Project Settings > Tags and Layers, and disable its self-collision in Physics 2D > Layer Collision Matrix. Projectile-vs-projectile collisions won't be prevented until that's fixed.");
            else
                gameObject.layer = resolved;
        }

        transform.SetParent(GetProjectilesContainer(), worldPositionStays: true);

        Destroy(gameObject, _lifetime);
    }

    private static Transform GetProjectilesContainer()
    {
        if (_projectilesContainer == null)
        {
            var go = new GameObject("Projectiles");
            _projectilesContainer = go.transform;
        }
        return _projectilesContainer;
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

        // Rotate to face the travel direction so the sprite points where it's actually going,
        // instead of always rendering at its default prefab orientation.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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