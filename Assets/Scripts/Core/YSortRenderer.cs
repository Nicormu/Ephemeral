using UnityEngine;

/// <summary>
/// Keeps this GameObject's SpriteRenderer correctly Y-sorted against everything else on the
/// shared "Entities" Sorting Layer (obstacles, other movers) by recomputing Order in Layer every
/// frame from the current world Y position — using the exact same formula
/// 
/// Should be used in player and entities that move around the map.
///
/// FLYING: if a FlightComponent exists on this GameObject OR any of its parents, this sprite's
/// Sorting Layer switches to DungeonManager.FlyingEntitySortingLayerName whenever
/// FlightComponent.IsFlying is true, and back to DungeonManager.EntitySortingLayerName when
/// grounded. Uses GetComponentInParent (not GetComponent) specifically because many enemy
/// prefabs split physics/collision (root, holds FlightComponent) from the animated visual
/// (child, holds SpriteRenderer + this script) — GetComponent alone would never find a
/// parent's FlightComponent, silently leaving the sprite stuck on "Entities" forever regardless
/// of actual flight state. Sorting Layer order always beats Order-in-Layer in Unity, so this
/// guarantees a flying entity renders above every obstacle regardless of world Y position — a
/// numeric offset alone couldn't guarantee that, since Order-in-Layer values here are derived
/// from world Y and this dungeon can generate rooms at negative Y (south/west of the origin).
/// FlightComponent is optional — entities without one anywhere in their hierarchy (e.g. most
/// enemies, the player until a flight mechanic exists) just always stay on "Entities".
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YSortRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("Extra constant nudge applied after the Y-based order. Leave at 0 for normal entities. Useful only for special cases — e.g. forcing a shadow child sprite to always render just behind its owner despite sharing the same Y.")]
    [SerializeField] private int _orderOffset = 0;

    [Tooltip("Y position sampled for sorting. Leave empty to use this GameObject's own transform — override only if the sprite's visual 'feet'/base is offset from the transform's origin (e.g. a child sprite object).")]
    [SerializeField] private Transform _sortReference;

    private FlightComponent _flightComponent; // optional — null means "never flying, always on Entities layer"
    private bool _isCurrentlyOnFlyingLayer;

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_sortReference == null) _sortReference = transform;

        // GetComponentInParent (not GetComponent) — see class doc. Covers both layouts: a
        // single-GameObject entity where FlightComponent and this script sit together, AND a
        // split rig where FlightComponent is on a physics/collider root and this script is on a
        // separate visual child.
        _flightComponent = GetComponentInParent<FlightComponent>();

        _spriteRenderer.sortingLayerName = DungeonManager.EntitySortingLayerName;
        _isCurrentlyOnFlyingLayer = false;
    }

    // LateUpdate — after movement code (PlayerMovement/EnemyChaseMovement run in FixedUpdate,
    // and any camera/animation code in Update) has already applied this frame's final position.
    private void LateUpdate()
    {
        bool wantsFlyingLayer = _flightComponent != null && _flightComponent.IsFlying;

        // Only touch sortingLayerName when the state actually changes — SpriteRenderer property
        // writes aren't free, and this runs every frame for every Y-sorted entity in the scene.
        if (wantsFlyingLayer != _isCurrentlyOnFlyingLayer)
        {
            _spriteRenderer.sortingLayerName = wantsFlyingLayer
                ? DungeonManager.FlyingEntitySortingLayerName
                : DungeonManager.EntitySortingLayerName;
            _isCurrentlyOnFlyingLayer = wantsFlyingLayer;
        }

        _spriteRenderer.sortingOrder = DungeonManager.CalculateYSortOrder(_sortReference.position.y) + _orderOffset;
    }
}