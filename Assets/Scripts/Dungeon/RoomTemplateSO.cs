using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewRoomTemplate", menuName = "Dungeon/Room Template")]
public class RoomTemplateSO : ScriptableObject
{
    public static readonly Vector2Int RoomTileSize = new Vector2Int(13, 7); // width, height

    [Header("Identity")]
    public RoomType Type;
    public DoorDirection Doors;

    [System.Serializable]
    private struct TemplateCell
    {
        public CellState State;
        public ObstacleType Obstacle; // only meaningful when State == Obstacle
    }

    [Header("Cell Grid")]
    [Tooltip("Flattened cell grid, row-major (y * width + x). Edit via the custom Inspector grid, not by hand.")]
    [SerializeField] private TemplateCell[] _cellGrid = new TemplateCell[RoomTileSize.x * RoomTileSize.y];

    private static int Index(int x, int y) => y * RoomTileSize.x + x;

    public CellState GetCell(int x, int y) => _cellGrid[Index(x, y)].State;
    public ObstacleType GetObstacle(int x, int y) => _cellGrid[Index(x, y)].Obstacle;

    /// <summary>Set a cell's state. The obstacle reference is only kept when state == Obstacle.</summary>
    public void SetCell(int x, int y, CellState state, ObstacleType obstacle = null)
    {
        int i = Index(x, y);
        _cellGrid[i].State = state;
        _cellGrid[i].Obstacle = state == CellState.Obstacle ? obstacle : null;
    }

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    /// <summary>Every non-Void cell, with its state and obstacle type (if any), in local template space.</summary>
    public (Vector2Int pos, CellState state, ObstacleType obstacle)[] GetOccupiedCells()
    {
        var list = new List<(Vector2Int, CellState, ObstacleType)>();
        for (int y = 0; y < RoomTileSize.y; y++)
            for (int x = 0; x < RoomTileSize.x; x++)
            {
                var cell = _cellGrid[Index(x, y)];
                if (cell.State != CellState.Void)
                    list.Add((new Vector2Int(x, y), cell.State, cell.Obstacle));
            }
        return list.ToArray();
    }

    private void Reset()
    {
        // New assets default to a full Floor rectangle so they're usable immediately.
        _cellGrid = new TemplateCell[RoomTileSize.x * RoomTileSize.y];
        for (int i = 0; i < _cellGrid.Length; i++)
            _cellGrid[i] = new TemplateCell { State = CellState.Floor, Obstacle = null };
    }
}