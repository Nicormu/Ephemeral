using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "NewRoomTemplate", menuName = "Dungeon/Room Template")]
public class RoomTemplateSO : ScriptableObject
{
    public static readonly Vector2Int RoomTileSize = new Vector2Int(13, 7);

    [Header("Identity")]
    public RoomType Type;
    public DoorDirection Doors;

    [Header("Visual Style")]
    [Tooltip("Reusable style asset (floor tile + wall tiles). Multiple templates can share the same RoomStyleSO to keep a consistent theme.")]
    public RoomStyleSO Style;

    [Header("Obstacles")]
    [Tooltip("Palette of obstacle types available when painting the grid below. Each type defines its own visual, whether it blocks movement, and damage dealt if it doesn't (e.g. fire).")]
    public List<ObstacleTypeDefinition> ObstacleTypes = new();

    [Header("Enemies")]
    [Tooltip("Spawn points marked on the grid below (Paint Mode: Enemy Spawn Points), in the order they were placed.")]
    public List<Vector2Int> EnemySpawnPoints = new();

    [Tooltip("Which enemy prefab spawns at which marked point. SpawnPointIndex refers to the list above.")]
    public List<EnemySpawnEntry> EnemySpawnEntries = new();

    [Header("Cell Grid")]
    [Tooltip("Flattened CellState grid, row-major (y * width + x). Edit via the custom Inspector grid, not by hand.")]
    [SerializeField] private CellState[] _cellGrid = new CellState[RoomTileSize.x * RoomTileSize.y];

    [Tooltip("Flattened obstacle-type index grid (index into ObstacleTypes, -1 = none). Edit via the custom Inspector grid.")]
    [SerializeField] private int[] _obstacleTypeGrid = InitObstacleGrid();

    public CellState GetCell(int x, int y) => _cellGrid[y * RoomTileSize.x + x];
    public void SetCell(int x, int y, CellState state) => _cellGrid[y * RoomTileSize.x + x] = state;

    public int GetObstacleTypeIndex(int x, int y) => _obstacleTypeGrid[y * RoomTileSize.x + x];
    public void SetObstacleTypeIndex(int x, int y, int index) => _obstacleTypeGrid[y * RoomTileSize.x + x] = index;

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    private static int[] InitObstacleGrid()
    {
        var arr = new int[RoomTileSize.x * RoomTileSize.y];
        for (int i = 0; i < arr.Length; i++) arr[i] = -1;
        return arr;
    }

    /// <summary>Every non-Void cell, with its state and resolved obstacle data, in local template space.</summary>
    public (Vector2Int pos, CellState state, TileBase obstacleTile, bool obstacleBlocksMovement, int obstacleDamage)[] GetOccupiedCells()
    {
        var list = new List<(Vector2Int, CellState, TileBase, bool, int)>();
        for (int y = 0; y < RoomTileSize.y; y++)
            for (int x = 0; x < RoomTileSize.x; x++)
            {
                var state = GetCell(x, y);
                if (state == CellState.Void) continue;

                TileBase obstacleTile = null;
                bool blocksMovement = true;
                int damage = 0;

                if (state == CellState.Obstacle)
                {
                    int idx = GetObstacleTypeIndex(x, y);
                    if (idx >= 0 && idx < ObstacleTypes.Count)
                    {
                        var def = ObstacleTypes[idx];
                        obstacleTile = def.Tile;
                        blocksMovement = def.BlocksMovement;
                        damage = def.Damage;
                    }
                }

                list.Add((new Vector2Int(x, y), state, obstacleTile, blocksMovement, damage));
            }
        return list.ToArray();
    }

    private void Reset()
    {
        _cellGrid = new CellState[RoomTileSize.x * RoomTileSize.y];
        for (int i = 0; i < _cellGrid.Length; i++)
            _cellGrid[i] = CellState.Floor;

        _obstacleTypeGrid = InitObstacleGrid();
    }

    private void OnValidate()
    {
        int expected = RoomTileSize.x * RoomTileSize.y;
        if (_obstacleTypeGrid == null || _obstacleTypeGrid.Length != expected)
        {
            var resized = InitObstacleGrid();
            if (_obstacleTypeGrid != null)
                System.Array.Copy(_obstacleTypeGrid, resized, Mathf.Min(_obstacleTypeGrid.Length, resized.Length));
            _obstacleTypeGrid = resized;
        }
    }
}

[System.Serializable]
public class ObstacleTypeDefinition
{
    [Tooltip("Label shown in the palette dropdown. Purely for your own organization.")]
    public string Name = "Obstacle";

    public TileBase Tile;

    [Tooltip("If true (default), this obstacle physically blocks the player (e.g. a rock). If false, the player can walk over it — use this for hazards like fire.")]
    public bool BlocksMovement = true;

    [Tooltip("Damage dealt if the player stands on this obstacle. Only relevant when Blocks Movement is off.")]
    public int Damage = 0;
}

[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject EnemyPrefab;

    [Tooltip("Index into EnemySpawnPoints — which marked point this enemy spawns at. Set via the grid editor.")]
    public int SpawnPointIndex = -1;
}