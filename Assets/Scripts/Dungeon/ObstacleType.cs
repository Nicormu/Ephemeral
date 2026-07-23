using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Defines one kind of obstacle asset (e.g. "Rock", "Unbreakable Pillar") that a room
/// template cell can reference. Create one asset per obstacle variety.
/// </summary>
[CreateAssetMenu(fileName = "NewObstacleType", menuName = "Dungeon/Obstacle Type")]
public class ObstacleType : ScriptableObject
{
    [Header("Visual")]
    [Tooltip("Tile drawn for this obstacle when rendered as a tilemap. Accepts a normal Tile or an Animated Tile.")]
    public TileBase VisualTile;

    [Header("Behavior")]
    [Tooltip("If true, this obstacle can never be destroyed. If false, it can be broken by gameplay logic.")]
    public bool IsUnbreakable = true;

    [Tooltip("Optional VFX/prefab spawned when this obstacle breaks. Only used when IsUnbreakable is false.")]
    public GameObject BreakEffectPrefab;
}