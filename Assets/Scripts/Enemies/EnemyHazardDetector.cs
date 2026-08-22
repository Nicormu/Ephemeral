using UnityEngine;

/// <summary>
/// Enemy-side counterpart to PlayerHazardDetector. Unlike the player, an enemy gets no
/// teleport-to-safety recovery step — hazard damage just ticks in on FixedUpdate, naturally
/// rate-limited by HealthComponent's own invulnerability window (the same guard EnemyHealth
/// already relies on for TakeDamage), so standing in a hazard doesn't deal damage every single
/// physics frame.
///
/// Reads an optional FlightComponent on the same GameObject:
///   - While IsFlying is true: Void cells deal NO damage — that's the whole point of spawning a
///     flying enemy over a Void (see RoomTemplateSOEditor's Void-cell spawn points). Obstacle-
///     type hazards (Fire, etc.) still deal damage UNLESS that specific ObstacleType has
///     IgnoredByFlyingEntities checked (e.g. Spikes could be flagged to not affect fliers).
///   - Without a FlightComponent, or while not currently flying: Void and hazard Obstacles both
///     deal damage exactly like they do to the player. This also acts as a safety net for a
///     grounded enemy that wanders into a Void cell while chasing — EnemyChaseMovement has no
///     concept of "safe ground" today, so without this an enemy could freely walk over pits.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHazardDetector : MonoBehaviour
{
    [Tooltip("Damage taken per hazard tick while standing in a Void cell. Only relevant while NOT flying — a flying enemy takes zero Void damage, see class doc.")]
    [SerializeField] private int _voidDamage = 1;

    [Tooltip("Point sampled for hazard/void cell checks. Leave empty to use this GameObject's own transform. Override with a child positioned at the sprite's visual base if the transform's origin sits higher than the feet — same idea as PlayerHazardDetector's Feet Reference / YSortRenderer's Sort Reference.")]
    [SerializeField] private Transform _feetReference;

    private EnemyHealth _health;
    private FlightComponent _flight;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        _flight = GetComponent<FlightComponent>(); // optional — null means "never flying"

        if (_feetReference == null) _feetReference = transform;
    }

    private void FixedUpdate()
    {
        if (DungeonManager.Instance == null || _health == null || _health.IsDead) return;

        bool isFlying = _flight != null && _flight.IsFlying;

        Vector2Int cell = DungeonManager.WorldToGridCell(_feetReference.position);
        CellState state = DungeonManager.Instance.GetCellState(cell);

        int damage = 0;

        switch (state)
        {
            case CellState.Void:
                if (!isFlying) damage = _voidDamage;
                break;

            case CellState.Obstacle:
                int hazardDamage = DungeonManager.Instance.GetObstacleHazardDamage(cell);
                if (hazardDamage <= 0) break;

                bool ignoredByFlight = isFlying && DungeonManager.Instance.GetObstacleIgnoredByFlight(cell);
                if (!ignoredByFlight) damage = hazardDamage;
                break;
        }

        if (damage > 0)
            _health.TakeDamage(damage);
    }
}