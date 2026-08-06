using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Generation")]
    public bool autoStart = true;
    public int overrideSeed = -1;

    [Header("Room Style")]
    [Tooltip("Pool of available visual themes. One is chosen at random (seeded, so it's reproducible per seed) each generation and applied to every room.")]
    public RoomStyleSO[] availableStyles;

    [Header("Visual — Tilemap containers")]
    public Tilemap floorTilemap;

    [Tooltip("Separate, Rule-Tile-agnostic tilemap for the floor painted across door-to-door corridors (the gap between rooms). MUST be a different Tilemap than floorTilemap: if the corridor floor lives on the same Tilemap as the room's own floor, the room's Floor Rule Tile sees continuous floor through the doorway and stops rendering its border variant right at the door, rendering the 'middle' variant instead (Rule Tile neighbor matching only looks within its own Tilemap). Same Sorting Layer/Order as floorTilemap, no collider needed.")]
    public Tilemap corridorFloorTilemap;

    [Tooltip("VISIBLE wall tiles. Painted CONTINUOUSLY along every wall run — including across door gaps — so wall Rule Tiles never render a false corner next to a door. No collider needed.")]
    public Tilemap wallTilemap;

    [Tooltip("INVISIBLE collision-only wall tilemap. Same shape as wallTilemap but with a real gap at each door. Needs a TilemapCollider2D (+ Rigidbody2D, Static), Tilemap Renderer disabled.")]
    public Tilemap wallCollisionTilemap;

    [Header("Visual — Void")]
    public Tilemap voidTilemap;

    [Header("Visual — Obstacles (blocking)")]
    public Tilemap obstacleTilemap;

    [Header("Visual — Hazards (walkable)")]
    public Tilemap hazardTilemap;

    [Header("Visual — Decoration (optional, floor)")]
    public Tilemap decorationTilemap;
    public TileBase[] decorationTileVariants;
    [Range(0f, 1f)] public float decorationChance = 0.08f;

    [Header("Visual — Wall Decoration (optional)")]
    [Tooltip("Rotated per side (North upright, South 180°, East/West ±90°) — use rotation-agnostic art (cracks, moss, rubble), not directional props.")]
    public Tilemap wallDecorationTilemap;
    public TileBase[] wallDecorationTileVariants;
    [Range(0f, 1f)] public float wallDecorationChance = 0.08f;

    [Header("Doors")]
    [Tooltip("Door art for the North (Upper) wall.")]
    public GameObject topDoorPrefab;

    [Tooltip("Door art for the South (Lower) wall.")]
    public GameObject bottomDoorPrefab;

    [Tooltip("Door art for the West wall — reused for East, rotated 180°.")]
    public GameObject verticalDoorPrefab;

    [Tooltip("Added to the wall tilemap's Sorting Order to decide each door's Sorting Order.")]
    public int doorSortingOrderOffset = 1;

    [Header("Player Spawn")]
    public PlayerSpawnMode spawnMode = PlayerSpawnMode.RoomCenter;
    public Vector3 spawnOffset = Vector3.zero;

    private FloorLayout.DungeonResult _currentLayout;
    private GameObject _doorContainer;
    private GameObject _roomLogicContainer;
    private Dictionary<Vector2Int, RoomController> _roomControllers;
    private bool _isGenerating;
    private RoomStyleSO _chosenStyle;

    private DungeonCellQuery _cellQuery;
    private DungeonWallBuilder _wallBuilder;
    private DungeonDoorSpawner _doorSpawner;
    private DungeonDecorationPainter _decorationPainter;
    private DungeonPlayerSpawner _playerSpawner;

    public FloorLayout.DungeonResult CurrentLayout => _currentLayout;
    public Room[] Rooms => _currentLayout.Rooms?.ToArray();
    public Vector2Int StartGridPosition => _currentLayout.StartPosition;
    public Vector3 PlayerSpawnWorldPosition => _playerSpawner.SpawnWorldPosition;
    public Vector2Int BossGridPosition => _currentLayout.BossPosition;
    public RoomStyleSO ChosenStyle => _chosenStyle;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _cellQuery = new DungeonCellQuery();
        _wallBuilder = new DungeonWallBuilder(wallTilemap, wallCollisionTilemap);
        
        // Pass the three new prefabs here
        _doorSpawner = new DungeonDoorSpawner(topDoorPrefab, bottomDoorPrefab, verticalDoorPrefab, doorSortingOrderOffset, wallTilemap);
        
        _decorationPainter = new DungeonDecorationPainter(decorationTilemap, decorationTileVariants, decorationChance, wallDecorationTilemap, wallDecorationTileVariants, wallDecorationChance);
        _playerSpawner = new DungeonPlayerSpawner(spawnMode, spawnOffset);

        if (autoStart)
            Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Initialize()
    {
        if (_isGenerating)
        {
            Debug.LogError("[DungeonManager] Generation already in progress!");
            return;
        }

        _isGenerating = true;

        try
        {
            if (overrideSeed >= 0)
                SeedManager.SetSeed(overrideSeed);
            else
                SeedManager.Initialize();

            Debug.Log($"[DungeonManager] Dungeon generation started with seed {SeedManager.CurrentSeed}");

            RoomPool.Build();
            _currentLayout = FloorLayout.Generate(SeedManager.Rng);

            if (_currentLayout.Rooms == null || _currentLayout.Rooms.Count == 0)
            {
                Debug.LogError("[DungeonManager] Dungeon generation produced no rooms — check logs for details.");
                return;
            }

            bool connected = RoomConnector.ValidateConnectivity(_currentLayout.Rooms, out var disconnected);
            if (!connected)
                Debug.LogWarning($"[DungeonManager] {disconnected.Count} rooms are unreachable!");
            else
                Debug.Log($"[DungeonManager] Generation complete: {_currentLayout.Rooms.Count} rooms, all connected.");

            ApplyRandomRoomStyle();
            _cellQuery.Build(_currentLayout.Rooms);
            SpawnDungeonVisuals();
            _playerSpawner.CalculateSpawnPosition(_currentLayout.Rooms, _currentLayout.StartPosition);
            _playerSpawner.PositionPlayerAtSpawn();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DungeonManager] Generation threw an exception: {ex}");
        }
        finally
        {
            _isGenerating = false;
        }
    }

    public void Regenerate()
    {
        SeedManager.Regenerate();
        Initialize();
    }

    private void ApplyRandomRoomStyle()
    {
        if (availableStyles == null || availableStyles.Length == 0)
        {
            Debug.LogWarning("[DungeonManager] No Room Styles assigned in 'Available Styles' — "
                + "rooms will render without floor/wall tiles.");
            _chosenStyle = null;
            return;
        }

        _chosenStyle = availableStyles[SeedManager.Rng.Next(availableStyles.Length)];
        Debug.Log($"[DungeonManager] Room style chosen: {_chosenStyle.name}");

        for (int i = 0; i < _currentLayout.Rooms.Count; i++)
        {
            Room room = _currentLayout.Rooms[i];
            room.FloorTile = _chosenStyle.FloorTile;
            room.TopWallTile = _chosenStyle.TopWallTile;
            room.BottomWallTile = _chosenStyle.BottomWallTile;
            room.SideWallTile = _chosenStyle.SideWallTile;
            _currentLayout.Rooms[i] = room;
        }
    }

    // — Gameplay cell lookup (delegated to DungeonCellQuery) —
    public CellState GetCellState(Vector2Int gridCell) => _cellQuery.GetCellState(gridCell);
    public int GetObstacleHazardDamage(Vector2Int gridCell) => _cellQuery.GetObstacleHazardDamage(gridCell);
    public Vector3? FindNearestSafePositionInRoom(Vector3 worldPos) => _cellQuery.FindNearestSafePositionInRoom(worldPos);
    public bool IsInsideDungeon(Vector3 worldPos) => _cellQuery.IsInsideDungeon(worldPos);
    public Room? GetRoomAtGrid(Vector2Int gridPos) => _cellQuery.GetRoomAtGrid(gridPos);
    public Room[] GetConnectedRooms(Room room) => _cellQuery.GetConnectedRooms(room);

    /// <summary>Converts a world position (1 unit = 1 tile) to a grid cell.</summary>
    public static Vector2Int WorldToGridCell(Vector3 worldPos) =>
        new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

    // — Visual + gameplay spawning —

    private void SpawnDungeonVisuals()
    {
        floorTilemap?.ClearAllTiles();
        corridorFloorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        wallCollisionTilemap?.ClearAllTiles();
        voidTilemap?.ClearAllTiles();
        decorationTilemap?.ClearAllTiles();
        wallDecorationTilemap?.ClearAllTiles();
        obstacleTilemap?.ClearAllTiles();
        hazardTilemap?.ClearAllTiles();

        if (_doorContainer != null) Destroy(_doorContainer);
        _doorContainer = new GameObject("DungeonDoors");
        _doorContainer.transform.SetParent(transform);

        if (_roomLogicContainer != null) Destroy(_roomLogicContainer);
        _roomLogicContainer = new GameObject("DungeonRoomLogic");
        _roomLogicContainer.transform.SetParent(transform);

        if (floorTilemap == null)
        {
            Debug.LogWarning("[DungeonManager] No floor tilemap assigned — skipping visual spawn.");
            return;
        }

        foreach (var room in _currentLayout.Rooms)
            SpawnRoomFloor(room);

        // Fills the gap between two connected rooms' doors with the same floor tile — painted on
        // its OWN tilemap (corridorFloorTilemap), deliberately separate from floorTilemap. If this
        // used floorTilemap, the room's Floor Rule Tile would see continuous floor running through
        // the doorway (Rule Tile neighbor matching only looks within its own Tilemap) and would
        // stop rendering its border variant right at the door, showing the 'middle' variant
        // instead — same root cause documented for walls (rooms must have true exterior edges).
        // Must run after _cellQuery.Build(), which already happened before SpawnDungeonVisuals()
        // was called from Initialize().
        SpawnDoorCorridorFloors();

        SpawnVoidTiles();

        // Walls are never hand-placed — derived from adjacency, using the chosen style's tiles.
        _wallBuilder.Build(_currentLayout.Rooms);

        _decorationPainter.SpawnFloorDecorations(_cellQuery.CellLookup);
        _decorationPainter.SpawnWallDecorations(_wallBuilder.WallPositions, _wallBuilder.DoorWallCells);

        // Must run before door spawning: doors look up rooms by GridPos to know when to open.
        SpawnRoomControllersAndEnemies();
        _doorSpawner.SpawnDoors(_currentLayout.Rooms, _doorContainer.transform, _roomControllers);
    }

    private void SpawnRoomFloor(Room room)
    {
        if (room.FloorTile == null)
            Debug.LogWarning($"[DungeonManager] Room at ({room.GridPos.x},{room.GridPos.y}) has no FloorTile — assign at least one RoomStyleSO to 'Available Styles'.");

        foreach (var cell in room.Cells)
        {
            Vector3Int tilePos = new Vector3Int(cell.X, cell.Y, 0);

            if (room.FloorTile != null)
                floorTilemap.SetTile(tilePos, room.FloorTile);

            if (cell.State == CellState.Obstacle && cell.ObstacleTile != null)
            {
                if (cell.ObstacleBlocksMovement)
                {
                    if (obstacleTilemap != null)
                        obstacleTilemap.SetTile(tilePos, cell.ObstacleTile);
                }
                else
                {
                    if (hazardTilemap != null)
                        hazardTilemap.SetTile(tilePos, cell.ObstacleTile);
                }
            }
        }
    }

    /// <summary>Paints the same floor tile used by the dungeon's chosen style across every
    /// door-to-door corridor cell (see DungeonCellQuery.DoorCorridorCells), so the gap between
    /// rooms reads as a walkable connector instead of empty space. Painted on corridorFloorTilemap
    /// — NOT floorTilemap — so it never counts as a neighbor for the room's Floor Rule Tile (see
    /// the comment on corridorFloorTilemap's declaration above for why that separation matters).</summary>
    private void SpawnDoorCorridorFloors()
    {
        if (_chosenStyle == null || _chosenStyle.FloorTile == null) return;

        Tilemap targetTilemap = corridorFloorTilemap != null ? corridorFloorTilemap : floorTilemap;

        if (corridorFloorTilemap == null)
            Debug.LogWarning("[DungeonManager] No Corridor Floor Tilemap assigned — falling back to "
                + "floorTilemap for door corridors. This will cause the room's Floor Rule Tile to "
                + "render its 'middle' variant instead of its border variant right at doorways. "
                + "Assign a separate Tilemap to 'Corridor Floor Tilemap' to fix this.");

        foreach (var cell in _cellQuery.DoorCorridorCells)
            targetTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), _chosenStyle.FloorTile);
    }

    private void SpawnVoidTiles()
    {
        if (voidTilemap == null || _chosenStyle == null || _chosenStyle.VoidTile == null) return;
        if (_currentLayout.Rooms == null) return;

        var painted = new HashSet<Vector2Int>();

        foreach (var room in _currentLayout.Rooms)
        {
            for (int y = 0; y < room.Height; y++)
            {
                for (int x = 0; x < room.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(room.GridPos.x + x, room.GridPos.y + y);

                    if (_cellQuery.CellLookup.ContainsKey(cellPos)) continue; // Floor or Obstacle
                    if (!painted.Add(cellPos)) continue;

                    voidTilemap.SetTile(new Vector3Int(cellPos.x, cellPos.y, 0), _chosenStyle.VoidTile);
                }
            }
        }
    }

    private void SpawnRoomControllersAndEnemies()
    {
        _roomControllers = new Dictionary<Vector2Int, RoomController>();

        foreach (var room in _currentLayout.Rooms)
        {
            var go = new GameObject($"Room_{room.Type}_{room.GridPos.x}_{room.GridPos.y}");
            go.transform.SetParent(_roomLogicContainer.transform);

            var controller = go.AddComponent<RoomController>();
            controller.Initialize(room);
            controller.SpawnEnemies(room.EnemySpawns);

            _roomControllers[room.GridPos] = controller;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        DungeonGizmos.Draw(_currentLayout);
    }
#endif

    public enum PlayerSpawnMode { GridCorner, RoomCenter }
}