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

    [Tooltip("Wall piece used for the wall's visible face — the 'front'/lower part of the wall.")]
    public TileBase WallFrontTile;

    [Tooltip("Optional cap piece stacked directly above WallFrontTile, only on a room's north-facing wall, to fake the taller 'top wall' silhouette (two 32x32 pieces stacked instead of one tall sprite). Leave empty to skip the cap.")]
    public TileBase WallTopTile;

    [Tooltip("Tile drawn for Void cells within a room's bounds (pits/chasms the player can fall into — see PlayerHazardDetector). Leave empty to leave Void cells unrendered, as before.")]
    public TileBase VoidTile;
}