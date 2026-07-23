using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Generation")]
    public bool autoStart = true;
    public int overrideSeed = -1;

    [Header("Visual — Tilemap containers")]
    [Tooltip("Floor/wall tiles come from each room's RoomStyleSO (via its RoomTemplateSO). This field just holds the scene's Tilemap component to draw into.")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;

    [Header("Visual — Obstacles (blocking)")]
    [Tooltip("Obstacles that physically stop the player (e.g. rocks). Give it a TilemapCollider2D. Each cell uses its own tile from the RoomTemplateSO obstacle palette.")]
    public Tilemap obstacleTilemap;

    [Header("Visual — Hazards (walkable)")]
    [Tooltip("Obstacles the player can walk over but that damage them (e.g. fire). No collider needed — PlayerHazardDetector handles the damage. Each cell uses its own tile from the RoomTemplateSO obstacle palette.")]
    public Tilemap hazardTilemap;

    [Header("Visual — Decoration (optional)")]
    public Tilemap decorationTilemap;
    public TileBase[] decorationTileVariants;
    [Range(0f, 1f)] public float decorationChance = 0.08f;

    [Header("Doors")]
    [Tooltip("Prefab with a Door component. Instantiated once per shared edge between two adjacent rooms.")]
    public GameObject doorPrefab;

    [Header("Player Spawn")]
    public PlayerSpawnMode spawnMode = PlayerSpawnMode.RoomCenter;
    public Vector3 spawnOffset = Vector3.zero;

    private FloorLayout.DungeonResult _currentLayout;
    private GameObject _doorContainer;
    private GameObject _roomLogicContainer;
    private Dictionary<Vector2Int, RoomController> _roomControllers;
    private bool _isGenerating;
    private Dictionary<Vector2Int, CellState> _cellLookup;
    private Dictionary<Vector2Int, int> _obstacleHazardDamage;

    public FloorLayout.DungeonResult CurrentLayout => _currentLayout;
    public Room[] Rooms => _currentLayout.Rooms?.ToArray();
    public Vector2Int StartGridPosition => _currentLayout.StartPosition;
    public Vector3 PlayerSpawnWorldPosition { get; private set; }
    public Vector2Int BossGridPosition => _currentLayout.BossPosition;

    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

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

            BuildCellLookup();
            SpawnDungeonVisuals();
            CalculatePlayerSpawnPosition();
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

    // — Gameplay cell lookup —

    private void BuildCellLookup()
    {
        _cellLookup = new Dictionary<Vector2Int, CellState>();
        _obstacleHazardDamage = new Dictionary<Vector2Int, int>();

        foreach (var room in _currentLayout.Rooms)
        {
            foreach (var cell in room.Cells)
            {
                _cellLookup[cell.CellPos] = cell.State;

                if (cell.State == CellState.Obstacle && !cell.ObstacleBlocksMovement && cell.ObstacleDamage > 0)
                    _obstacleHazardDamage[cell.CellPos] = cell.ObstacleDamage;
            }
        }
    }

    /// <summary>What's at a grid cell. Cells not part of any room (including unpainted "void" cells) return Void.</summary>
    public CellState GetCellState(Vector2Int gridCell) =>
        _cellLookup != null && _cellLookup.TryGetValue(gridCell, out var state) ? state : CellState.Void;

    /// <summary>Damage dealt by standing on this cell, if it's a walkable hazard obstacle (e.g. fire). 0 otherwise.</summary>
    public int GetObstacleHazardDamage(Vector2Int gridCell) =>
        _obstacleHazardDamage != null && _obstacleHazardDamage.TryGetValue(gridCell, out var dmg) ? dmg : 0;

    /// <summary>Converts a world position (1 unit = 1 tile) to a grid cell.</summary>
    public static Vector2Int WorldToGridCell(Vector3 worldPos) =>
        new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

    /// <summary>
    /// Finds the room whose bounds contain worldPos (works even if worldPos itself sits on a
    /// Void cell not present in any room's Cells array), then returns the world-space center
    /// of the closest Floor cell within that room. Returns null if worldPos isn't inside any
    /// room's bounds — callers should fall back to a known-safe location (e.g. Start room).
    /// </summary>
    public Vector3? FindNearestSafePositionInRoom(Vector3 worldPos)
    {
        if (_currentLayout.Rooms == null) return null;

        Room? containingRoom = null;
        foreach (var room in _currentLayout.Rooms)
        {
            Vector3 min = GetRoomCornerWorld(room.GridPos);
            Vector3 max = GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);

            if (worldPos.x >= min.x && worldPos.x < max.x &&
                worldPos.y >= min.y && worldPos.y < max.y)
            {
                containingRoom = room;
                break;
            }
        }

        if (containingRoom == null) return null;

        RoomCell? nearest = null;
        float bestDistSq = float.MaxValue;

        foreach (var cell in containingRoom.Value.Cells)
        {
            if (cell.State != CellState.Floor) continue;

            Vector3 cellCenter = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
            float distSq = (cellCenter - worldPos).sqrMagnitude;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = cell;
            }
        }

        if (nearest == null) return null;

        return new Vector3(nearest.Value.X + 0.5f, nearest.Value.Y + 0.5f, 0f);
    }

    // — Visual + gameplay spawning —

    private void SpawnDungeonVisuals()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        decorationTilemap?.ClearAllTiles();
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

        // Walls are NEVER hand-placed — same philosophy as doors: derived from grid adjacency
        // of actual occupied cells, using each room's own WallFrontTile/WallTopTile style.
        BuildWalls();
        SpawnDecorations();

        // Must run before SpawnDoors(): doors look up rooms by GridPos to know when to open.
        SpawnRoomControllersAndEnemies();
        SpawnDoors();
    }

    private void SpawnRoomFloor(Room room)
    {
        if (room.FloorTile == null)
            Debug.LogWarning($"[DungeonManager] Room at ({room.GridPos.x},{room.GridPos.y}) has no FloorTile — assign a RoomStyleSO on its RoomTemplateSO.");

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

    /// <summary>
    /// Walls are derived per-room, never hand-placed: whatever cell borders an occupied cell but
    /// isn't itself occupied becomes a wall tile, styled using the owning room's WallFrontTile.
    /// On a room's north-facing wall specifically, an extra WallTopTile is stacked one cell
    /// higher to fake the taller "top wall" silhouette (32x32 pieces stacked, not a single
    /// tall sprite).
    /// </summary>
    private void BuildWalls()
    {
        if (wallTilemap == null || _cellLookup == null || _currentLayout.Rooms == null) return;

        var paintedFront = new HashSet<Vector2Int>();
        var paintedTop = new HashSet<Vector2Int>();

        foreach (var room in _currentLayout.Rooms)
        {
            if (room.WallFrontTile == null) continue; // no wall style defined for this room's style

            foreach (var cell in room.Cells)
            {
                Vector2Int cellPos = cell.CellPos;

                foreach (var dir in AllDirections)
                {
                    Vector2Int wallPos = cellPos + UnitOffset(dir);
                    if (_cellLookup.ContainsKey(wallPos)) continue; // occupied — this isn't a wall position

                    if (paintedFront.Add(wallPos))
                        wallTilemap.SetTile(new Vector3Int(wallPos.x, wallPos.y, 0), room.WallFrontTile);

                    if (dir == DoorDirection.North && room.WallTopTile != null)
                    {
                        Vector2Int capPos = wallPos + UnitOffset(DoorDirection.North);
                        if (paintedTop.Add(capPos))
                            wallTilemap.SetTile(new Vector3Int(capPos.x, capPos.y, 0), room.WallTopTile);
                    }
                }
            }
        }
    }

    private void SpawnDecorations()
    {
        if (decorationTilemap == null || decorationTileVariants == null || decorationTileVariants.Length == 0) return;

        foreach (var kv in _cellLookup)
        {
            if (kv.Value != CellState.Floor) continue; // never decorate obstacles/void
            if (SeedManager.Rng.NextDouble() > decorationChance) continue;

            TileBase deco = decorationTileVariants[SeedManager.Rng.Next(decorationTileVariants.Length)];
            decorationTilemap.SetTile(new Vector3Int(kv.Key.x, kv.Key.y, 0), deco);
        }
    }

    /// <summary>Creates one RoomController per room and spawns its enemies at their marked points.</summary>
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

    /// <summary>
    /// Spawns one door per shared edge between two adjacent rooms. ComputeDoors() (in FloorLayout)
    /// always assigns matching opposite doors on both sides of an edge, so acting only on
    /// North/East flags is enough — the South/West side of the same pair is handled by the
    /// neighboring room's own North/East flag. This avoids spawning the same door twice.
    /// </summary>
    private void SpawnDoors()
    {
        if (doorPrefab == null || _currentLayout.Rooms == null) return;

        foreach (var room in _currentLayout.Rooms)
        {
            if ((room.Doors & DoorDirection.North) != 0)
                PlaceDoor(room, DoorDirection.North);

            if ((room.Doors & DoorDirection.East) != 0)
                PlaceDoor(room, DoorDirection.East);
        }
    }

    private void PlaceDoor(Room room, DoorDirection dir)
    {
        Vector3 worldPos;
        Quaternion rotation;

        if (dir == DoorDirection.North)
        {
            worldPos = new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height, 0f);
            rotation = Quaternion.identity; // door spans horizontally
        }
        else // East
        {
            worldPos = new Vector3(room.GridPos.x + room.Width, room.GridPos.y + room.Height / 2f, 0f);
            rotation = Quaternion.Euler(0f, 0f, 90f); // door spans vertically
        }

        GameObject instance = Instantiate(doorPrefab, worldPos, rotation, _doorContainer.transform);
        instance.name = $"Door_{dir}_{room.GridPos.x}_{room.GridPos.y}";

        var door = instance.GetComponent<Door>();
        if (door == null)
        {
            Debug.LogWarning("[DungeonManager] doorPrefab has no Door component — it won't open/close automatically.");
            return;
        }

        Vector2Int neighborGridPos = room.GridPos + DirectionOffset(dir, room.Width, room.Height);

        if (_roomControllers.TryGetValue(room.GridPos, out var ownerController))
            door.RegisterRoom(ownerController);

        if (_roomControllers.TryGetValue(neighborGridPos, out var neighborController))
            door.RegisterRoom(neighborController);
    }

    private void CalculatePlayerSpawnPosition()
    {
        Vector3 spawnPos = GetRoomCornerWorld(_currentLayout.StartPosition);

        if (spawnMode == PlayerSpawnMode.RoomCenter)
        {
            foreach (var room in _currentLayout.Rooms)
            {
                if (room.Type == RoomType.Start)
                {
                    spawnPos = GetRoomCenterWorld(room.GridPos, room.Width, room.Height);
                    break;
                }
            }
        }

        PlayerSpawnWorldPosition = spawnPos + spawnOffset;
    }

    private Vector3 GetRoomCornerWorld(Vector2Int gridPos) => new Vector3(gridPos.x, gridPos.y, 0f);

    private Vector3 GetRoomFarCornerWorld(Vector2Int gridPos, int width, int height) =>
        new Vector3(gridPos.x + width, gridPos.y + height, 0f);

    private Vector3 GetRoomCenterWorld(Vector2Int gridPos, int width, int height) =>
        new Vector3(gridPos.x + width / 2f, gridPos.y + height / 2f, 0f);

    private static Vector2Int UnitOffset(DoorDirection dir) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, 1),
        DoorDirection.South => new Vector2Int(0, -1),
        DoorDirection.East  => new Vector2Int(1, 0),
        DoorDirection.West  => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    private static Vector2Int DirectionOffset(DoorDirection dir, int width, int height) => dir switch
    {
        DoorDirection.North => new Vector2Int(0, height),
        DoorDirection.South => new Vector2Int(0, -height),
        DoorDirection.East  => new Vector2Int(width, 0),
        DoorDirection.West  => new Vector2Int(-width, 0),
        _ => Vector2Int.zero
    };

    public bool IsInsideDungeon(Vector3 worldPos)
    {
        if (_currentLayout.Rooms == null) return false;

        foreach (var room in _currentLayout.Rooms)
        {
            Vector3 min = GetRoomCornerWorld(room.GridPos);
            Vector3 max = GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);

            if (worldPos.x >= min.x && worldPos.x < max.x &&
                worldPos.y >= min.y && worldPos.y < max.y)
                return true;
        }
        return false;
    }

    public Room? GetRoomAtGrid(Vector2Int gridPos)
    {
        if (_currentLayout.Rooms == null) return null;

        foreach (var room in _currentLayout.Rooms)
            foreach (var cell in room.Cells)
                if (cell.X == gridPos.x && cell.Y == gridPos.y)
                    return room;

        return null;
    }

    public Room[] GetConnectedRooms(Room room)
    {
        var connected = new List<Room>();
        if (_currentLayout.Rooms == null) return connected.ToArray();

        foreach (var other in _currentLayout.Rooms)
        {
            if (other.Type == room.Type && other.GridPos == room.GridPos) continue;

            int aMinX = room.GridPos.x - 1;
            int aMaxX = room.GridPos.x + room.Width + 1;
            int aMinY = room.GridPos.y - 1;
            int aMaxY = room.GridPos.y + room.Height + 1;

            int bMinX = other.GridPos.x - 1;
            int bMaxX = other.GridPos.x + other.Width + 1;
            int bMinY = other.GridPos.y - 1;
            int bMaxY = other.GridPos.y + other.Height + 1;

            if (aMaxX >= bMinX && aMinX <= bMaxX && aMaxY >= bMinY && aMinY <= bMaxY)
                connected.Add(other);
        }
        return connected.ToArray();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_currentLayout.Rooms == null) return;

        foreach (var room in _currentLayout.Rooms)
        {
            Gizmos.color = GetRoomColor(room.Type);
            Vector3 min = GetRoomCornerWorld(room.GridPos);
            Vector3 max = GetRoomFarCornerWorld(room.GridPos, room.Width, room.Height);
            Gizmos.DrawWireCube((min + max) / 2f, max - min);

            Vector3 center = (min + max) / 2f;
            Gizmos.color = Color.cyan;
            if ((room.Doors & DoorDirection.North) != 0) Gizmos.DrawLine(new Vector3(center.x, max.y, 0), new Vector3(center.x, max.y - 0.5f, 0));
            if ((room.Doors & DoorDirection.South) != 0) Gizmos.DrawLine(new Vector3(center.x, min.y, 0), new Vector3(center.x, min.y + 0.5f, 0));
            if ((room.Doors & DoorDirection.East)  != 0) Gizmos.DrawLine(new Vector3(max.x, center.y, 0), new Vector3(max.x - 0.5f, center.y, 0));
            if ((room.Doors & DoorDirection.West)  != 0) Gizmos.DrawLine(new Vector3(min.x, center.y, 0), new Vector3(min.x + 0.5f, center.y, 0));

            foreach (var cell in room.Cells)
            {
                if (cell.State != CellState.Obstacle) continue;

                if (cell.ObstacleBlocksMovement)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f), Vector3.one * 0.6f);
                }
                else
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    Gizmos.DrawWireSphere(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f), 0.3f);
                }
            }
        }

        if (_currentLayout.Rooms.Count > 0)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetRoomCenterWorld(_currentLayout.StartPosition, 1, 1), 0.5f);
        }
    }

    private static Color GetRoomColor(RoomType type) => type switch
    {
        RoomType.Start      => Color.green,
        RoomType.Normal     => Color.gray,
        RoomType.Treasure   => Color.yellow,
        RoomType.Boss       => Color.red,
        RoomType.DeadEnd    => Color.magenta,
        RoomType.Corridor   => new Color(0.5f, 0.5f, 0.8f),
        _                   => Color.white,
    };
#endif

    public enum PlayerSpawnMode { GridCorner, RoomCenter }
}