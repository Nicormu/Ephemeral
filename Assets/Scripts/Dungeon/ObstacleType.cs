using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Defines one kind of obstacle asset (e.g. "Rock", "Fire") that any RoomTemplateSO can drag
/// into its own ObstacleTypes palette. Centralizing these here means editing one asset updates
/// every room template that references it, instead of re-entering the same values by hand.
/// Create one asset per obstacle variety via Create > Dungeon/Obstacle Type.
/// </summary>
[CreateAssetMenu(fileName = "NewObstacleType", menuName = "Dungeon/Obstacle Type")]
public class ObstacleType : ScriptableObject
{
    [Header("Visual")]
    [Tooltip("Tile variants drawn for this obstacle. If more than one is assigned, a random variant is picked independently for each obstacle cell painted with this type (e.g. Rock1/Rock2/Rock3) — purely visual, same BlocksMovement/Damage/Destructible behavior for all variants.")]
    public TileBase[] Tiles;

    [Header("Behavior")]
    [Tooltip("If true (default), this obstacle physically blocks the player (e.g. a rock). If false, the player can walk over it — use this for hazards like fire.")]
    public bool BlocksMovement = true;

    [Tooltip("Damage dealt if the player stands on this obstacle. Only relevant when Blocks Movement is off.")]
    public int Damage = 0;

    [Header("Destruction")]
    [Tooltip("If true, this obstacle has HP and can be broken (contact damage today, weapons later) instead of being permanent.")]
    public bool IsDestructible = false;

    [Tooltip("Hit points before this obstacle breaks. Only relevant when Is Destructible is on.")]
    public int MaxHealth = 1;

    [Tooltip("Optional VFX/loot prefab spawned in this obstacle's place when it breaks. Only relevant when Is Destructible is on.")]
    public GameObject BreakEffectPrefab;
}