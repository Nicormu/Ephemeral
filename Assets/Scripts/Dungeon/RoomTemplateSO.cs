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

    [Header("Obstacles")]
    [Tooltip("Palette of obstacle types available when painting the grid below. Drag in ObstacleType assets (Create > Dungeon/Obstacle Type).")]
    public List<ObstacleType> ObstacleTypes = new();

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

    /// <summary>Every non-Void cell, with its state and resolved obstacle data, in local template
    /// space. obstacleTileVariants/obstacleSpriteVariants is the full pool for that cell's obstacle
    /// type — the actual per-instance tile/sprite is picked later, once per physical room in the
    /// generated dungeon (see FloorLayout.BuildAbsoluteCells), so two rooms using the same template
    /// don't always show the exact same variant in the exact same spot. obstaclePrefab and
    /// obstacleIgnoredByFlight both come straight from the cell's ObstacleType asset — single
    /// values per obstacle KIND, defined once and shared by every RoomTemplateSO/room using that
    /// same ObstacleType.</summary>
    public (Vector2Int pos, CellState state, TileBase[] obstacleTileVariants, Sprite[] obstacleSpriteVariants,
        bool obstacleBlocksMovement, int obstacleDamage, bool obstacleIsDestructible, int obstacleMaxHealth,
        GameObject obstacleBreakEffectPrefab, GameObject obstaclePrefab, bool obstacleIgnoredByFlight)[] GetOccupiedCells()
    {
        var list = new List<(Vector2Int, CellState, TileBase[], Sprite[], bool, int, bool, int, GameObject, GameObject, bool)>();
        for (int y = 0; y < RoomTileSize.y; y++)
            for (int x = 0; x < RoomTileSize.x; x++)
            {
                var state = GetCell(x, y);
                if (state == CellState.Void) continue;

                TileBase[] obstacleTileVariants = null;
                Sprite[] obstacleSpriteVariants = null;
                bool blocksMovement = true;
                int damage = 0;
                bool isDestructible = false;
                int maxHealth = 1;
                GameObject breakEffectPrefab = null;
                GameObject obstaclePrefab = null;
                bool ignoredByFlight = false;

                if (state == CellState.Obstacle)
                {
                    int idx = GetObstacleTypeIndex(x, y);
                    if (idx >= 0 && idx < ObstacleTypes.Count && ObstacleTypes[idx] != null)
                    {
                        var def = ObstacleTypes[idx];
                        obstacleSpriteVariants = def.SpriteVariants;
                        blocksMovement = def.BlocksMovement;
                        damage = def.Damage;
                        isDestructible = def.IsDestructible;
                        maxHealth = def.MaxHealth;
                        breakEffectPrefab = def.BreakEffectPrefab;
                        obstaclePrefab = def.Prefab;
                        ignoredByFlight = def.IgnoredByFlyingEntities;

                        if (obstacleSpriteVariants == null || obstacleSpriteVariants.Length == 0)
                            Debug.LogWarning($"[RoomTemplateSO] '{name}' obstacle type '{def.name}' has no Sprite Variants assigned — cell at ({x},{y}) will spawn without a sprite.");

                        if (obstaclePrefab == null)
                            Debug.LogWarning($"[RoomTemplateSO] '{name}' obstacle type '{def.name}' has no Prefab assigned — cell at ({x},{y}) will fall back to DungeonManager's Fallback Obstacle Prefab, or spawn nothing if that's empty too.");
                    }
                    else
                    {
                        Debug.LogWarning($"[RoomTemplateSO] '{name}' has an Obstacle cell at ({x},{y}) pointing to an empty/missing ObstacleTypes slot (index {idx}) — treating it as a plain blocking obstacle with no sprite/prefab.");
                    }
                }

                list.Add((new Vector2Int(x, y), state, obstacleTileVariants, obstacleSpriteVariants, blocksMovement, damage, isDestructible, maxHealth, breakEffectPrefab, obstaclePrefab, ignoredByFlight));
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
public class EnemySpawnEntry
{
    public GameObject EnemyPrefab;

    [Tooltip("Index into EnemySpawnPoints — which marked point this enemy spawns at. Set via the grid editor.")]
    public int SpawnPointIndex = -1;
}