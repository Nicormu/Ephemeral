using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Defines one kind of obstacle asset (e.g. "Rock", "Fire", "Spikes") that any RoomTemplateSO
/// can drag into its own ObstacleTypes palette. Centralizing these here means editing one asset
/// updates every room template that references it, instead of re-entering the same values by
/// hand. Create one asset per obstacle variety via Create > Dungeon/Obstacle Type.
/// </summary>
[CreateAssetMenu(fileName = "NewObstacleType", menuName = "Dungeon/Obstacle Type")]
public class ObstacleType : ScriptableObject
{
    [Header("Visual - Prefab")]
    [Tooltip("GameObject prefab spawned for every obstacle cell using this type (see DungeonManager.SpawnObstacleInstance). Needs a SpriteRenderer (sprite assigned per-instance from Sprite Variants below) and a Collider2D sized/shaped for THIS obstacle type — code only toggles isTrigger based on Blocks Movement. Moving the prefab here (instead of one shared prefab on DungeonManager) lets each obstacle kind own its own collider size/shape, defined once and reused by every template that references this asset.")]
    public GameObject Prefab;

    [Header("Visual - Sprite Variants")]
    [Tooltip("Sprite variants drawn for this obstacle's spawned GameObject. If more than one is assigned, a random variant is picked independently for each obstacle cell using this type (e.g. Rock1/Rock2/Rock3) — same idea as the legacy Tiles array below, but for the SpriteRenderer-based obstacle system.")]
    public Sprite[] SpriteVariants;

    [Header("Behavior")]
    [Tooltip("If true (default), this obstacle physically blocks the player (e.g. a rock). If false, the player can walk over it — use this for hazards like fire or spikes.")]
    public bool BlocksMovement = true;

    [Tooltip("Damage dealt if the player stands on this obstacle. Only relevant when Blocks Movement is off.")]
    public int Damage = 0;

    [Tooltip("If true, an entity with an active FlightComponent (IsFlying == true) takes NO damage from this hazard and never triggers its effect — e.g. Spikes should ignore flying entities. If false (default), this hazard affects flying AND grounded entities alike — e.g. Fire still burns you even while flying over it. Only relevant when Blocks Movement is off (i.e. this is a walkable hazard, not a solid obstacle).")]
    public bool IgnoredByFlyingEntities = false;

    [Header("Destruction")]
    [Tooltip("If true, this obstacle has HP and can be broken (contact damage today, weapons later) instead of being permanent.")]
    public bool IsDestructible = false;

    [Tooltip("Hit points before this obstacle breaks. Only relevant when Is Destructible is on.")]
    public int MaxHealth = 1;

    [Tooltip("Optional VFX/loot prefab spawned in this obstacle's place when it breaks. Only relevant when Is Destructible is on.")]
    public GameObject BreakEffectPrefab;
}