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
    [Tooltip("The single floor tile used for every Floor cell in rooms using this style. Floors stay visually simple on purpose — variety comes from the Decoration tilemap, not multiple floor tiles.")]
    public TileBase FloorTile;

    [Tooltip("Rule Tile used for the room's North (top) wall — handles straight runs, corners, and door gaps.")]
    public TileBase TopWallTile;

    [Tooltip("Rule Tile used for the room's South (bottom) wall — handles straight runs, corners, and door gaps.")]
    public TileBase BottomWallTile;

    [Tooltip("Rule Tile used for the room's East and West (side) walls. East is rendered as a horizontal mirror of this same tile at paint time — no separate asset needed.")]
    public TileBase SideWallTile;
    
    [Tooltip("Tile drawn for Void cells within a room's bounds (pits/chasms the player can fall into — see PlayerHazardDetector). Leave empty to leave Void cells unrendered, as before.")]
    public TileBase VoidTile;
}