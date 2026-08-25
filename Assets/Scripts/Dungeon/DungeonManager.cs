using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    /// <summary>Sorting Layer every dynamically Y-sorted entity (obstacles, player, enemies) must
    /// use. Must exist in Edit > Project Settings > Tags and Layers > Sorting Layers — create it
    /// there first (this can't be created from script). Kept as a single shared constant so
    /// DungeonManager (obstacles) and YSortRenderer (player/enemies) can never drift out of sync.</summary>
    public const string EntitySortingLayerName = "Entities";

    public const string FlyingEntitySortingLayerName = "FlyingEntities";

    // Base offset keeps every computed order comfortably positive/readable; precision controls
    // how many distinct Order-in-Layer steps exist per world unit of Y. sortingOrder is a short
    // (Unity clamps to roughly -32768..32767), so these two constants are chosen to stay well
    // inside that range even for a dungeon that sprawls a few hundred tiles in any direction.
    private const int YSortBase = 10000;
    private const int YSortPrecision = 20;

    /// <summary>Shared Y-sort formula: higher world Y -> lower Order in Layer (drawn further
    /// back); lower world Y -> higher Order in Layer (drawn further forward, i.e. "closer to
    /// camera" in a top-down room). Used both here (obstacles, computed once at spawn) and by
    /// YSortRenderer (player/enemies, recomputed every frame since they move).</summary>
    public static int CalculateYSortOrder(float worldY) =>
        YSortBase - Mathf.RoundToInt(worldY * YSortPrecision);

    [Header("Generation")]
    public bool autoStart = true;
    public int overrideSeed = -1;

    [Header("Room Style")]
    [Tooltip("Pool of available visual themes. One is chosen at random (seeded, so it's reproducible per seed) each generation and applied to every room.")]
    public RoomStyleSO[] availableStyles;

    [Header("Visual — Tilemap containers")]
    public Tilemap floorTilemap;

    [Tooltip("VISIBLE wall tiles. Painted CONTINUOUSLY along every wall run — including across door gaps — so wall Rule Tiles never render a false corner next to a door. No collider needed.")]
    public Tilemap wallTilemap;

    [Tooltip("INVISIBLE collision-only wall tilemap. Same shape as wallTilemap but with a real gap at each door. Needs a TilemapCollider2D (+ Rigidbody2D, Static), Tilemap Renderer disabled.")]
    public Tilemap wallCollisionTilemap;

    [Header("Visual — Void")]
    public Tilemap voidTilemap;

    [Header("Visual — Decoration (optional, floor)")]
    public Tilemap decorationTilemap;
    public TileBase[] decorationTileVariants;
    [Range(0f, 1f)] public float decorationChance = 0.08f;

    [Header("Visual — Wall Decoration (optional)")]
    [Tooltip("Rotated per side (North upright, South 180°, East/West ±90°) — use rotation-agnostic art (cracks, moss, rubble), not directional props.")]
    public Tilemap wallDecorationTilemap;
    public TileBase[] wallDecorationTileVariants;
    [Range(0f, 1f)] public float wallDecorationChance = 0.08f;

    [Header("Obstacles")]
    [Tooltip("Optional safety net only — normally each ObstacleType asset (Rock, Fire, ...) supplies its own Prefab, so obstacle kinds can have different collider sizes/shapes. This is instantiated ONLY when a cell's ObstacleType has no Prefab assigned (a misconfigured/empty ObstacleType). Needs a SpriteRenderer + Collider2D like any obstacle prefab. Leave empty to just log a warning and skip spawning misconfigured obstacles instead.")]
    public GameObject fallbackObstaclePrefab;

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
    private GameObject _obstacleContainer;
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

    /// <summary>Fired once a generation (initial Initialize() or a later Regenerate()) has
    /// finished successfully — after rooms, visuals, room controllers/enemies, doors, and player
    /// spawn positioning are all in place. Anything that caches its own copy of dungeon data
    /// (today: MinimapController) should subscribe to this and rebuild from Rooms/CurrentLayout
    /// instead of only reading them once at Start() — otherwise a Regenerate() (R-key reset, or
    /// the post-death reset) leaves it pointing at destroyed Room data from the previous dungeon.
    /// Deliberately NOT fired if generation fails (e.g. FloorLayout produced zero rooms).</summary>
    public event System.Action OnDungeonGenerated;

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

            // Must happen before SpawnDungeonVisuals() creates the new RoomControllers below —
            // see RoomCamera.ResetTracking()'s doc comment for exactly why. In short: without
            // this, a fresh RoomController can mistake RoomCamera's stale "current room" from the
            // PREVIOUS dungeon (same GridPos, new room) as the player already standing in it,
            // permanently locking that room's doors before the player ever actually entered.
            RoomCamera.Instance?.ResetTracking();

            SpawnDungeonVisuals();
            _playerSpawner.CalculateSpawnPosition(_currentLayout.Rooms, _currentLayout.StartPosition);
            _playerSpawner.PositionPlayerAtSpawn();

            OnDungeonGenerated?.Invoke();
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

    /// <summary>Whether a flying entity is immune to a given hazard cell's damage — sourced from
    /// that cell's ObstacleType.IgnoredByFlyingEntities. Used by EnemyHazardDetector (and could
    /// be used by a future flying-player mechanic) alongside GetObstacleHazardDamage.</summary>
    public bool GetObstacleIgnoredByFlight(Vector2Int gridCell) => _cellQuery.GetObstacleIgnoredByFlight(gridCell);

    public Vector3? FindNearestSafePositionInRoom(Vector3 worldPos) => _cellQuery.FindNearestSafePositionInRoom(worldPos);
    public bool IsInsideDungeon(Vector3 worldPos) => _cellQuery.IsInsideDungeon(worldPos);
    public Room? GetRoomAtGrid(Vector2Int gridPos) => _cellQuery.GetRoomAtGrid(gridPos);
    public Room[] GetConnectedRooms(Room room) => _cellQuery.GetConnectedRooms(room);

    /// <summary>Marks a cell as Floor at runtime. Called by DestructibleObstacle.Break() so the
    /// cell becomes walkable/pathable the instant an obstacle is destroyed, without regenerating
    /// the dungeon.</summary>
    public void FreeCellToFloor(Vector2Int gridCell) => _cellQuery.SetCellToFloor(gridCell);

    /// <summary>Converts a world position (1 unit = 1 tile) to a grid cell.</summary>
    public static Vector2Int WorldToGridCell(Vector3 worldPos) =>
        new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

    // — Visual + gameplay spawning —

    private void SpawnDungeonVisuals()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        wallCollisionTilemap?.ClearAllTiles();
        voidTilemap?.ClearAllTiles();
        decorationTilemap?.ClearAllTiles();
        wallDecorationTilemap?.ClearAllTiles();

        if (_doorContainer != null) Destroy(_doorContainer);
        _doorContainer = new GameObject("DungeonDoors");
        _doorContainer.transform.SetParent(transform);

        if (_roomLogicContainer != null) Destroy(_roomLogicContainer);
        _roomLogicContainer = new GameObject("DungeonRoomLogic");
        _roomLogicContainer.transform.SetParent(transform);

        if (_obstacleContainer != null) Destroy(_obstacleContainer);
        _obstacleContainer = new GameObject("DungeonObstacles");
        _obstacleContainer.transform.SetParent(transform);

        if (floorTilemap == null)
        {
            Debug.LogWarning("[DungeonManager] No floor tilemap assigned — skipping visual spawn.");
            return;
        }

        foreach (var room in _currentLayout.Rooms)
            SpawnRoomFloor(room);

        // NOTE: the gap between two connected rooms' doors (DungeonGridConstants.RoomGap) is
        // deliberately left unpainted — no corridor floor tile is drawn there anymore. It's
        // still registered as CellState.Floor in DungeonCellQuery (see DoorCorridorCells), so
        // the player can't fall through it mid-transition; it just doesn't render anything.

        SpawnVoidTiles();

        // Walls are never hand-placed — derived from adjacency, using the chosen style's tiles.
        _wallBuilder.Build(_currentLayout.Rooms);

        _decorationPainter.SpawnFloorDecorations(_cellQuery.CellLookup);
        _decorationPainter.SpawnWallDecorations(_wallBuilder.WallPositions, _wallBuilder.DoorWallCells);

        // Must run before door spawning: doors look up rooms by GridPos to know when to open.
        SpawnRoomControllersAndEnemies();
        _doorSpawner.SpawnDoors(_currentLayout.Rooms, _doorContainer.transform, _roomControllers);
    }

    /// <summary>Paints one floor tile per cell. If the chosen style has Floor Tile Variants
    /// assigned, a random variant (seeded via SeedManager.Rng, so reproducible per seed) is
    /// picked independently for EACH cell — this is what breaks up the repeated-single-sprite
    /// "grid" look. Falls back to the style's single FloorTile when no variants are assigned,
    /// same as before.</summary>
    private void SpawnRoomFloor(Room room)
    {
        bool hasVariants = _chosenStyle != null && _chosenStyle.FloorTileVariants != null && _chosenStyle.FloorTileVariants.Length > 0;

        if (!hasVariants && room.FloorTile == null)
            Debug.LogWarning($"[DungeonManager] Room at ({room.GridPos.x},{room.GridPos.y}) has no FloorTile — assign at least one RoomStyleSO to 'Available Styles'.");

        foreach (var cell in room.Cells)
        {
            Vector3Int tilePos = new Vector3Int(cell.X, cell.Y, 0);

            TileBase floorTile = hasVariants
                ? _chosenStyle.FloorTileVariants[SeedManager.Rng.Next(_chosenStyle.FloorTileVariants.Length)]
                : room.FloorTile;

            if (floorTile != null)
                floorTilemap.SetTile(tilePos, floorTile);

            if (cell.State == CellState.Obstacle)
                SpawnObstacleInstance(cell);
        }
    }

    /// <summary>Spawns the runtime GameObject for one obstacle cell — blocking (rocks) or walkable
    /// hazard (fire) alike. The prefab comes from THIS CELL's own ObstacleType.Prefab (falling
    /// back to fallbackObstaclePrefab only if that's unset), so different obstacle kinds can use
    /// differently sized/shaped prefabs instead of all sharing one generic prefab. Assigns this
    /// cell's resolved sprite variant to the prefab's SpriteRenderer, toggles the prefab's own
    /// Collider2D between solid/trigger to match whether the obstacle blocks movement, and
    /// attaches a DestructibleObstacle if this cell's obstacle type is destructible. Using a real
    /// GameObject (instead of a Tilemap tile) is what lets these obstacles participate in normal
    /// Transparency Sort Axis Y-sorting against the player and each other — Tilemap tiles can't
    /// reliably sort against each other once their sprites are taller than one cell and overlap
    /// into neighboring cells.
    ///
    /// SAFETY NET: forces Sorting Layer to EntitySortingLayerName and computes Order in Layer
    /// directly from this cell's Y position (see CalculateYSortOrder), instead of trusting the
    /// prefab's own Inspector values. This guarantees every obstacle sorts correctly against
    /// every other obstacle regardless of what a given ObstacleType prefab happens to have set —
    /// a single mismatched prefab can no longer silently break Y-sorting for the whole room.
    ///
    /// NOTE: this does NOT set the instance's Physics2D Layer — that still comes straight from
    /// whatever Layer is baked into the prefab. For flying enemies to pass through obstacles
    /// (see FlightComponent / EnemyHazardDetector), every obstacle prefab's Layer must be set to
    /// "Obstacle" by hand in its own prefab asset, with that layer's collision against
    /// "FlyingEntity" disabled in Project Settings > Physics 2D.</summary>
    private void SpawnObstacleInstance(RoomCell cell)
    {
        GameObject prefabToUse = cell.ObstaclePrefab != null ? cell.ObstaclePrefab : fallbackObstaclePrefab;

        if (prefabToUse == null)
        {
            Debug.LogWarning($"[DungeonManager] Obstacle at ({cell.X},{cell.Y}) has no Prefab assigned on its ObstacleType, and no Fallback Obstacle Prefab is set — skipping spawn. Assign a Prefab on the relevant ObstacleType asset (Create > Dungeon/Obstacle Type).");
            return;
        }

        Vector3 pos = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
        GameObject instance = Instantiate(prefabToUse, pos, Quaternion.identity, _obstacleContainer.transform);
        instance.name = $"Obstacle_{cell.X}_{cell.Y}";

        var sr = instance.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = cell.ObstacleSprite;
            if (cell.ObstacleSprite == null)
                Debug.LogWarning($"[DungeonManager] Obstacle at ({cell.X},{cell.Y}) has no sprite — check that its ObstacleType has Sprite Variants assigned.");

            // Force consistent Y-sort regardless of whatever this prefab's own SpriteRenderer
            // Inspector values were — see method doc above.
            sr.sortingLayerName = EntitySortingLayerName;
            sr.sortingOrder = CalculateYSortOrder(pos.y);
        }
        else
        {
            Debug.LogWarning("[DungeonManager] The obstacle prefab used at this cell has no SpriteRenderer — it won't be visible.");
        }

        var col = instance.GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = !cell.ObstacleBlocksMovement;
        else
            Debug.LogWarning("[DungeonManager] The obstacle prefab used at this cell has no Collider2D — it won't block/detect the player.");

        if (cell.ObstacleIsDestructible)
        {
            var destructible = instance.GetComponent<DestructibleObstacle>();
            if (destructible == null) destructible = instance.AddComponent<DestructibleObstacle>();
            destructible.Initialize(cell.ObstacleMaxHealth, cell.ObstacleBreakEffectPrefab, new Vector2Int(cell.X, cell.Y));
        }
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