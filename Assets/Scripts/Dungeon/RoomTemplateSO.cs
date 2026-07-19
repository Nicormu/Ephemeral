using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRoomTemplate", menuName = "Dungeon/Room Template")]
public class RoomTemplateSO : ScriptableObject
{
    public static readonly Vector2Int RoomTileSize = new Vector2Int(11, 9);

    [Header("Identity")]
    public RoomType Type;
    public DoorDirection Doors;

    [Header("Cell Grid")]
    [Tooltip("Flattened CellState grid, row-major (y * width + x). Edit via the custom Inspector grid, not by hand.")]
    [SerializeField] private CellState[] _cellGrid = new CellState[RoomTileSize.x * RoomTileSize.y];

    public CellState GetCell(int x, int y) => _cellGrid[y * RoomTileSize.x + x];
    public void SetCell(int x, int y, CellState state) => _cellGrid[y * RoomTileSize.x + x] = state;

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    /// <summary>Every non-Void cell, with its state (Floor or Obstacle), in local template space.</summary>
    public (Vector2Int pos, CellState state)[] GetOccupiedCells()
    {
        var list = new List<(Vector2Int, CellState)>();
        for (int y = 0; y < RoomTileSize.y; y++)
            for (int x = 0; x < RoomTileSize.x; x++)
            {
                var state = GetCell(x, y);
                if (state != CellState.Void)
                    list.Add((new Vector2Int(x, y), state));
            }
        return list.ToArray();
    }

    private void Reset()
    {
        // New assets default to a full Floor rectangle so they're usable immediately.
        _cellGrid = new CellState[RoomTileSize.x * RoomTileSize.y];
        for (int i = 0; i < _cellGrid.Length; i++)
            _cellGrid[i] = CellState.Floor;
    }
}