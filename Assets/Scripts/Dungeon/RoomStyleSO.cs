using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "NewRoomStyle", menuName = "Dungeon/Room Style")]
public class RoomStyleSO : ScriptableObject
{
    [Tooltip("Display name, for your own reference (e.g. 'Deteriorated').")]
    public string StyleName;

    [Header("Floor")]
    [Tooltip("Floor tile variants. One is picked randomly PER CELL for subtle texture variation, same style overall.")]
    public TileBase[] FloorTiles;

    [Header("Walls")]
    [Tooltip("Wall tile — assign a Rule Tile here so corners/edges/junctions auto-select. See notes below.")]
    public TileBase WallTile;
}