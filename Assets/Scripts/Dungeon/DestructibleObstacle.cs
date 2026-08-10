using UnityEngine;

/// <summary>
/// Runtime instance of a single destructible obstacle cell. Added as a component onto the same
/// GameObject DungeonManager spawns from obstaclePrefab (see DungeonManager.SpawnObstacleInstance)
/// whenever its ObstacleType.IsDestructible is true — no longer paints/clears a separate tile.
/// Exposes a single public TakeDamage(int) entry point so any damage source can break it — today
/// that's PlayerObstacleContactDamage (bump-to-break while the player has no weapon yet); later, a
/// weapon/projectile system can call the same method with its own damage value without this
/// script needing to change.
///
/// On break: tells DungeonManager to mark its cell as Floor (so hazard/pathing checks treat it as
/// walkable), optionally spawns a VFX/loot prefab in its place, and destroys the whole GameObject
/// (sprite + collider go with it — nothing to clean up on a Tilemap anymore).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DestructibleObstacle : MonoBehaviour
{
    private int _currentHealth;
    private GameObject _breakEffectPrefab;
    private Vector2Int _gridCell;
    private bool _isBroken;

    /// <summary>Called once by DungeonManager right after this GameObject is created.</summary>
    public void Initialize(int maxHealth, GameObject breakEffectPrefab, Vector2Int gridCell)
    {
        _currentHealth = Mathf.Max(1, maxHealth);
        _breakEffectPrefab = breakEffectPrefab;
        _gridCell = gridCell;
    }

    /// <summary>Universal damage entry point — safe to call from contact, melee, projectiles,
    /// bombs, etc. Breaks the obstacle once health reaches zero.</summary>
    public void TakeDamage(int amount)
    {
        if (_isBroken || amount <= 0) return;

        _currentHealth -= amount;
        if (_currentHealth <= 0)
            Break();
    }

    private void Break()
    {
        if (_isBroken) return;
        _isBroken = true;

        if (DungeonManager.Instance != null)
            DungeonManager.Instance.FreeCellToFloor(_gridCell);

        if (_breakEffectPrefab != null)
            Instantiate(_breakEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}