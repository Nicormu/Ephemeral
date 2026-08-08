using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Runtime instance of a single destructible obstacle cell. Spawned by DungeonManager next to
/// the obstacle's own tile whenever its ObstacleType.IsDestructible is true. Exposes a single
/// public TakeDamage(int) entry point so any damage source can break it — today that's
/// PlayerObstacleContactDamage (bump-to-break while the player has no weapon yet); later, a
/// weapon/projectile system can call the same method with its own damage value without this
/// script needing to change.
///
/// On break: removes its own tile from the tilemap it was painted on, tells DungeonManager to
/// mark its cell as Floor (so hazard/pathing checks treat it as walkable), optionally spawns a
/// VFX/loot prefab in its place, and destroys itself.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DestructibleObstacle : MonoBehaviour
{
    private int _currentHealth;
    private GameObject _breakEffectPrefab;
    private Tilemap _ownerTilemap;
    private Vector3Int _tilePos;
    private Vector2Int _gridCell;
    private bool _isBroken;

    /// <summary>Called once by DungeonManager right after this GameObject is created.</summary>
    public void Initialize(int maxHealth, GameObject breakEffectPrefab, Tilemap ownerTilemap, Vector3Int tilePos, Vector2Int gridCell)
    {
        _currentHealth = Mathf.Max(1, maxHealth);
        _breakEffectPrefab = breakEffectPrefab;
        _ownerTilemap = ownerTilemap;
        _tilePos = tilePos;
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

        if (_ownerTilemap != null)
            _ownerTilemap.SetTile(_tilePos, null);

        if (DungeonManager.Instance != null)
            DungeonManager.Instance.FreeCellToFloor(_gridCell);

        if (_breakEffectPrefab != null)
            Instantiate(_breakEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}