using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Generation")]
    public bool autoStart = true;
    public int overrideSeed = -1;

    [Header("Visual — Prefab Mode")]
    [Tooltip("Whole-room prefabs (already include their own obstacles/walls baked in).")]
    public RoomPrefabMap prefabMap;

    [Header("Visual — Tilemap Mode")]
    public Tilemap floorTilemap;
    public Tile defaultFloorTile;

    [Tooltip("Separate tilemap for obstacles. Give it a TilemapCollider2D in the scene so obstacles physically block the player.")]
    public Tilemap obstacleTilemap;
    public Tile obstacleTile;

    [Header("Player Spawn")]
    public PlayerSpawnMode spawnMode = PlayerSpawnMode.RoomCenter;
    public Vector3 spawnOffset = Vector3.zero;

    private FloorLayout.DungeonResult _currentLayout;
    private GameObject _roomContainers;
    private bool _isGenerating;
    private Dictionary<Vector2Int, CellState> _cellLookup;

    public FloorLayout.DungeonResult CurrentLayout => _currentLayout;
    public Room[] Rooms => _currentLayout.Rooms?.ToArray();
    public Vector2Int StartGridPosition => _currentLayout.StartPosition;
    public Vector3 PlayerSpawnWorldPosition { get; private set; }
    public Vector2Int BossGridPosition => _currentLayout.BossPosition;

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
        foreach (var room in _currentLayout.Rooms)
            foreach (var cell in room.Cells)
                _cellLookup[cell.CellPos] = cell.State;
    }

    /// <summary>What's at a grid cell. Cells not part of any room (including unpainted "void" cells) return Void.</summary>
    public CellState GetCellState(Vector2Int gridCell) =>
        _cellLookup != null && _cellLookup.TryGetValue(gridCell, out var state) ? state : CellState.Void;

    /// <summary>Converts a world position (1 unit = 1 tile) to a grid cell.</summary>
    public static Vector2Int WorldToGridCell(Vector3 worldPos) =>
        new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

    // — Visual Spawning —

    private void SpawnDungeonVisuals()
    {
        if (_roomContainers != null)
            Destroy(_roomContainers);

        _roomContainers = new GameObject("DungeonRooms");
        _roomContainers.transform.SetParent(transform);

        foreach (var room in _currentLayout.Rooms)
            SpawnRoom(room);
    }

    private void SpawnRoom(Room room)
    {
        if (prefabMap.HasPrefabs)
        {
            // Whole-room prefab — hand-crafted art already contains its own obstacles/colliders.
            GameObject prefab = prefabMap.GetPrefabForType(room.Type);
            if (prefab != null)
            {
                Vector3 worldPos = GetRoomCenterWorld(room.GridPos, room.Width, room.Height);
                GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity, _roomContainers.transform);
                instance.name = $"{room.Type}_Room_{room.GridPos.x}_{room.GridPos.y}";
            }
        }
        else if (floorTilemap != null && defaultFloorTile != null)
        {
            foreach (var cell in room.Cells)
            {
                Vector3Int tilePos = new Vector3Int(cell.X, cell.Y, 0);

                // Floor always goes down first — Obstacle cells are Floor + something on top, so
                // "below every obstacle there's floor" is guaranteed by construction.
                floorTilemap.SetTile(tilePos, defaultFloorTile);

                if (cell.State == CellState.Obstacle && obstacleTilemap != null && obstacleTile != null)
                    obstacleTilemap.SetTile(tilePos, obstacleTile);
            }
        }
        else
        {
            Debug.LogWarning($"[DungeonManager] No visuals configured for room type '{room.Type}' at ({room.GridPos.x},{room.GridPos.y}) — skipping visual spawn.");
        }
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

            Gizmos.color = Color.red;
            foreach (var cell in room.Cells)
                if (cell.State == CellState.Obstacle)
                    Gizmos.DrawWireCube(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f), Vector3.one * 0.6f);
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

    [System.Serializable]
    public class RoomPrefabMap
    {
        public GameObject startPrefab;
        public GameObject normalPrefab;
        public GameObject treasurePrefab;
        public GameObject bossPrefab;
        public GameObject deadEndPrefab;
        public GameObject corridorPrefab;

        public bool HasPrefabs => startPrefab != null || normalPrefab != null || treasurePrefab != null ||
                                   bossPrefab != null || deadEndPrefab != null || corridorPrefab != null;

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