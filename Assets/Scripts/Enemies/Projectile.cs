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

    // Lazily-created shared parent for every spawned projectile, purely for Hierarchy
    // organization — keeps them out of the scene root regardless of which script (today just
    // EnemyRangedAttack, potentially a future player weapon) does the spawning. Unity's
    // overridden == on UnityEngine.Object means this null-check also catches the container
    // having been destroyed (e.g. a scene reload), so a fresh one is created automatically
    // instead of silently reparenting under a dead Transform.
    private static Transform _projectilesContainer;

    private static Transform GetProjectilesContainer()
    {
        if (_projectilesContainer == null)
            _projectilesContainer = new GameObject("Projectiles").transform;

        return _projectilesContainer;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();

        // worldPositionStays: true — this runs after Instantiate() already placed us at our
        // launch position, and the container sits at the world origin with no rotation/scale,
        // so this is purely a Hierarchy move, never a visual/positional one.
        transform.SetParent(GetProjectilesContainer(), worldPositionStays: true);

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

        // Rotate the sprite to face its travel direction. Assumes the sprite's default artwork
        // points along +X (Vector2.right) — if your art faces a different default direction,
        // add/subtract the appropriate offset to the angle below (e.g. -90f if the art faces up).
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