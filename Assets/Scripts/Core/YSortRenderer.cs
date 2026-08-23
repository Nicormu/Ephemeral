using UnityEngine;

/// <summary>
/// Keeps this GameObject's SpriteRenderer correctly Y-sorted against everything else on the
/// shared "Entities" Sorting Layer (obstacles, other movers) by recomputing Order in Layer every
/// frame from the current world Y position — using the exact same formula
/// 
/// Should be used in player and entities that move around the map.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YSortRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("Extra constant nudge applied after the Y-based order. Leave at 0 for normal entities. Useful only for special cases — e.g. forcing a shadow child sprite to always render just behind its owner despite sharing the same Y.")]
    [SerializeField] private int _orderOffset = 0;

    [Tooltip("Y position sampled for sorting. Leave empty to use this GameObject's own transform — override only if the sprite's visual 'feet'/base is offset from the transform's origin (e.g. a child sprite object).")]
    [SerializeField] private Transform _sortReference;

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_sortReference == null) _sortReference = transform;

        _spriteRenderer.sortingLayerName = DungeonManager.EntitySortingLayerName;
    }

    // LateUpdate — after movement code (PlayerMovement/EnemyChaseMovement run in FixedUpdate,
    // and any camera/animation code in Update) has already applied this frame's final position.
    private void LateUpdate()
    {
        _spriteRenderer.sortingOrder = DungeonManager.CalculateYSortOrder(_sortReference.position.y) + _orderOffset;
    }
}