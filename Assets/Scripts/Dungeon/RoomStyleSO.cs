using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Reusable visual theme for rooms: floor, wall pieces, and an optional void tile.
/// Multiple RoomTemplateSO assets share the same look via one randomly chosen RoomStyleSO
/// per dungeon generation (see DungeonManager.ApplyRandomRoomStyle) — never assigned by hand
/// on individual templates.
/// </summary>
[CreateAssetMenu(fileName = "NewRoomStyle", menuName = "Dungeon/Room Style")]
public class RoomStyleSO : ScriptableObject
{
    [Tooltip("Fallback single floor tile, used when Floor Tile Variants below is empty. Kept for backward compatibility with existing styles — assign at least one of these two floor fields.")]
    public TileBase FloorTile;

    [Tooltip("Pool of floor sprite variants. If this has one or more entries, DungeonManager picks a random variant (seeded, so it's reproducible per seed) for EVERY floor cell instead of repeating a single sprite — use this to break up the 'grid' look a single repeated floor tile produces. Leave empty to fall back to the single Floor Tile above.")]
    public TileBase[] FloorTileVariants;

    [Tooltip("Rule Tile used for the room's North (top) wall — handles straight runs, corners, and door gaps.")]
    public TileBase TopWallTile;

    [Tooltip("Rule Tile used for the room's South (bottom) wall — handles straight runs, corners, and door gaps.")]
    public TileBase BottomWallTile;

    [Tooltip("Rule Tile used for the room's East and West (side) walls. East is rendered as a horizontal mirror of this same tile at paint time — no separate asset needed.")]
    public TileBase SideWallTile;

    [Tooltip("Tile drawn for Void cells within a room's bounds (pits/chasms the player can fall into — see PlayerHazardDetector). Leave empty to leave Void cells unrendered, as before.")]
    public TileBase VoidTile;
}