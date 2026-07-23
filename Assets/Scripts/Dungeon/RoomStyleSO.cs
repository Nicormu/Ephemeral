using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Reusable visual theme for rooms: one floor tile plus the two wall pieces (front face +
/// optional top cap for the false-depth look). Multiple RoomTemplateSO assets can reference
/// the same RoomStyleSO so a whole floor (cave, dungeon, cathedral, etc.) shares one look
/// without re-assigning tiles on every template.
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
}