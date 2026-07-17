using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// MonoBehaviour that lives on an empty GameObject and orchestrates the entire dungeon lifecycle:
/// seed initialization → layout generation → visual spawning → player placement.
///
/// Setup in Unity:
///   1. Create an empty GameObject named "DungeonManager"
///   2. Attach this script
///   3. Assign references (Room Prefabs or Tilemap)
///   4. Call Initialize() at runtime (or enable "Auto-Start")
/// </summary>
public class DungeonManager : MonoBehaviour
{
    [Header("Generation")]
    [Tooltip("Automatically generate a dungeon on Awake? Turn off if you want manual control.")]
    public bool autoStart = true;

    [Tooltip("Override seed (negative = random). See SeedManager for details.")]
    public int overrideSeed = -1;

    [Header("Visual — Prefab Mode")]
    [Tooltip("Assign room prefabs by type. If set, tiles below are ignored.")]
    public RoomPrefabMap prefabMap;

    [Header("Visual — Tilemap Mode") ]
    [Tooltip("Tilemap to draw dungeon floors into.")]
    public Tilemap floorTilemap;

    [Tooltip("Default tile used for floor cells (when no prefab map is set).")]
    public Tile defaultFloorTile;

    [Header("Player Spawn")]
    [Tooltip("Where the player appears after generation. 'RoomCenter' uses the Start room's center.")]
    public PlayerSpawnMode spawnMode = PlayerSpawnMode.RoomCenter;

    [Tooltip("Offset from the spawn position (useful for aligning with character pivot).")]
    public Vector3 spawnOffset = Vector3.zero;

    // — Internal state —
    private FloorLayout.DungeonResult _currentLayout;
    private GameObject _roomContainers;  // parent object for all room visuals
    private bool _isGenerating;

    /// <summary>Current dungeon layout (read-only after generation completes).</summary>
    public FloorLayout.DungeonResult CurrentLayout => _currentLayout;

    /// <summary>All placed rooms in the current dungeon.</summary>
    public Room[] Rooms => _currentLayout.Rooms?.ToArray();

    /// <summary>Grid position where the player should spawn (Start room center).</summary>
    public Vector2Int StartGridPosition => _currentLayout.StartPosition;

    /// <summary>World position where the player should spawn.</summary>
    public Vector3 PlayerSpawnWorldPosition { get; private set; }

    /// <summary>Boss room grid position.</summary>
    public Vector2Int BossGridPosition => _currentLayout.BossPosition;

    void Awake()
    {
        if (autoStart)
            Initialize();
    }

    /// <summary>Initialize the dungeon system: seed → generate → spawn visuals → place player.</summary>
    public void Initialize()
    {
        if (_isGenerating)
        {
            Debug.LogError("[DungeonManager] Generation already in progress!");
            return;
        }

        _isGenerating = true;

        // 1. Initialize seed
        if (overrideSeed >= 0)
            SeedManager.SetSeed(overrideSeed);
        else
            SeedManager.Initialize();

        Debug.Log($"[DungeonManager] Dungeon generation started with seed {SeedManager.CurrentSeed}");

        // 2. Build template pool
        RoomPool.Build();

        // 3. Generate floor layout
        _currentLayout = FloorLayout.Generate(
            SeedManager.Rng,
            RoomPool.GetTemplates(RoomType.Start).ToArray(),
            RoomPool.GetTemplates(RoomType.Normal).ToArray(),
            RoomPool.GetTemplates(RoomType.Treasure).ToArray(),
            RoomPool.GetTemplates(RoomType.Boss).ToArray(),
            RoomPool.GetTemplates(RoomType.DeadEnd).ToArray(),
            RoomPool.GetTemplates(RoomType.Corridor).ToArray()
        );

        if (_currentLayout.Rooms == null || _currentLayout.Rooms.Count == 0)
        {
            Debug.LogError("[DungeonManager] Dungeon generation produced no rooms — check logs for details.");
            _isGenerating = false;
            return;
        }

        // Validate connectivity
        bool connected = RoomConnector.ValidateConnectivity(_currentLayout, out var disconnected);
        if (!connected)
        {
            Debug.LogWarning($"[DungeonManager] {disconnected.Count} rooms are unreachable! Consider increasing generation attempts.");
        }
        else
        {
            Debug.Log($"[DungeonManager] Generation complete: {_currentLayout.Rooms.Count} rooms, all connected.");
        }

        // 4. Spawn visual representation
        SpawnDungeonVisuals();

        // 5. Place player at spawn position
        CalculatePlayerSpawnPosition();

        _isGenerating = false;
    }

    /// <summary>Regenerate the dungeon with a fresh seed (keep same configuration).</summary>
    public void Regenerate()
    {
        SeedManager.Regenerate();
        Initialize();
    }

    // — Visual Spawning —

    private void SpawnDungeonVisuals()
    {
        if (_roomContainers != null)
            Destroy(_roomContainers);

        _roomContainers = new GameObject("DungeonRooms");
        _roomContainers.transform.SetParent(transform);

        foreach (var room in _currentLayout.Rooms)
        {
            SpawnRoom(room);
        }
    }

    private void SpawnRoom(Room room)
    {
        if (prefabMap.HasPrefabs)
        {
            // Prefab mode: instantiate a prefab for each room
            GameObject prefab = prefabMap.GetPrefabForType(room.Type);
            if (prefab != null)
            {
                Vector3 worldPos = GridToWorld(room.GridPos, room.Width, room.Height);
                GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity, _roomContainers.transform);
                instance.name = $"{room.Type}_Room_{room.GridPos.x}_{room.GridPos.y}";
            }
        }
        else if (floorTilemap != null && defaultFloorTile != null)
        {
            // Tilemap mode: draw tiles into the tilemap
            foreach (var cell in room.Cells)
            {
                Vector3Int tilePos = new Vector3Int(cell.X, cell.Y, 0);
                floorTilemap.SetTile(tilePos, defaultFloorTile);
            }
        }
        else
        {
            // Fallback: debug gizmo (colored cube in scene view)
            Debug.LogWarning($"[DungeonManager] No visuals configured for room type '{room.Type}' at ({room.GridPos.x},{room.GridPos.y}) — skipping visual spawn.");
        }
    }

    private void CalculatePlayerSpawnPosition()
    {
        Vector3 center = GridToWorld(_currentLayout.StartPosition, 1, 1);

        if (spawnMode == PlayerSpawnMode.RoomCenter)
        {
            // Use actual room center instead of grid corner
            foreach (var room in _currentLayout.Rooms)
            {
                if (room.Type == RoomType.Start)
                {
                    center = GridToWorld(room.GridPos, room.Width, room.Height);
                    break;
                }
            }
        }

        PlayerSpawnWorldPosition = center + spawnOffset;
    }

    /// <summary>Convert grid position to world-space center of the room.</summary>
    private Vector3 GridToWorld(Vector2Int gridPos, int width, int height)
    {
        // Assumes 1 grid unit = 1 world unit. Adjust if your tile size differs.
        return new Vector3(
            gridPos.x + (width - 1f) / 2f,
            gridPos.y + (height - 1f) / 2f,
            0f
        );
    }

    // — Public API for other systems —

    /// <summary>Check if a world position is inside any room floor.</summary>
    public bool IsInsideDungeon(Vector3 worldPos)
    {
        foreach (var room in _currentLayout.Rooms)
        {
            Vector3 min = GridToWorld(room.GridPos, 0, 0);
            Vector3 max = GridToWorld(room.GridPos, room.Width, room.Height);

            if (worldPos.x >= min.x && worldPos.x < max.x &&
                worldPos.y >= min.y && worldPos.y < max.y)
                return true;
        }
        return false;
    }

    /// <summary>Find which room a grid position belongs to.</summary>
    public Room? GetRoomAtGrid(Vector2Int gridPos)
    {
        foreach (var room in _currentLayout.Rooms)
        {
            for (int i = 0; i < room.Cells.Length; i++)
            {
                if (room.Cells[i].X == gridPos.x && room.Cells[i].Y == gridPos.y)
                    return room;
            }
        }
        return null;
    }

    /// <summary>Get the list of rooms connected to a given room by grid adjacency.</summary>
    public Room[] GetConnectedRooms(Room room)
    {
        var connected = new List<Room>();
        foreach (var other in _currentLayout.Rooms)
        {
            if (other.Type == room.Type && other.GridPos == room.GridPos) continue;

            // Check adjacency via expanded bounds
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

    // — Debug helpers —

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_currentLayout.Rooms == null) return;

        foreach (var room in _currentLayout.Rooms)
        {
            // Color by type
            UnityEditor.Handles.color = GetRoomColor(room.Type);
            Vector3 min = GridToWorld(room.GridPos, 0, 0);
            Vector3 max = GridToWorld(room.GridPos, room.Width, room.Height);
            UnityEditor.Handles.DrawWireRectangle(
                new Vector3((min.x + max.x) / 2f, (min.y + max.y) / 2f, 0f),
                max.x - min.x, max.y - min.y
            );
        }

        // Draw player spawn marker
        if (_currentLayout.Rooms.Count > 0)
        {
            UnityEditor.Handles.color = Color.green;
            Vector3 spawnPos = GridToWorld(_currentLayout.StartPosition, 1, 1);
            UnityEditor.Handles.DrawWireDisc(spawnPos, Vector3.forward, 0.5f);
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

    // — Nested types for configuration —

    public enum PlayerSpawnMode
    {
        /// <summary>Spawn at the exact grid corner of the Start room.</summary>
        GridCorner,
        /// <summary>Spawn at the center of the Start room's floor area.</summary>
        RoomCenter,
    }

    [System.Serializable]
    public class RoomPrefabMap
    {
        [Tooltip("Prefab to use for each room type. Leave empty to skip spawning that type.")]
        public GameObject startPrefab;
        public GameObject normalPrefab;
        public GameObject treasurePrefab;
        public GameObject bossPrefab;
        public GameObject deadEndPrefab;
        public GameObject corridorPrefab;

        public bool HasPrefabs => startPrefab != null || normalPrefab != null || treasurePrefab != null || bossPrefab != null;

        public GameObject GetPrefabForType(RoomType type) => type switch
        {
            RoomType.Start      => startPrefab,
            RoomType.Normal     => normalPrefab,
            RoomType.Treasure   => treasurePrefab,
            RoomType.Boss       => bossPrefab,
            RoomType.DeadEnd    => deadEndPrefab,
            RoomType.Corridor   => corridorPrefab,
            _                   => null,
        };
    }
}
