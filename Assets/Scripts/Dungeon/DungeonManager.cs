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
    [Tooltip("Pool of available visual themes. One is chosen at random (seeded, so it's reproducible per seed) each generation and applied to every room — you no longer assign a Style per RoomTemplateSO.")]
    public RoomStyleSO[] availableStyles;

    [Header("Visual — Tilemap containers")]
    [Tooltip("Floor/wall tiles come from the randomly chosen RoomStyleSO. This field just holds the scene's Tilemap component to draw into.")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;

    [Header("Visual — Void")]
    [Tooltip("Tilemap for Void cells (pits/chasms within a room's bounds) — painted with the chosen RoomStyleSO's VoidTile. Give it a spot below your floor tilemap in the Sorting Layer order. No collider needed — falling in is handled by PlayerHazardDetector via cell state, not physics.")]
    public Tilemap voidTilemap;

    [Header("Visual — Obstacles (blocking)")]
    [Tooltip("Obstacles that physically stop the player (e.g. rocks). Give it a TilemapCollider2D. Each cell uses its own tile from the RoomTemplateSO obstacle palette.")]
    public Tilemap obstacleTilemap;

    [Header("Visual — Hazards (walkable)")]
    [Tooltip("Obstacles the player can walk over but that damage them (e.g. fire). No collider needed — PlayerHazardDetector handles the damage. Each cell uses its own tile from the RoomTemplateSO obstacle palette.")]
    public Tilemap hazardTilemap;

    [Header("Visual — Decoration (optional, floor)")]
    public Tilemap decorationTilemap;
    public TileBase[] decorationTileVariants;
    [Range(0f, 1f)] public float decorationChance = 0.08f;

    [Header("Visual — Wall Decoration (optional)")]
    [Tooltip("Separate tilemap for wall decorations (torches, cracks, moss, etc.) — kept apart from floor decoration since the art and sorting layer needs are usually different.")]
    public Tilemap wallDecorationTilemap;
    [Tooltip("Tile variants painted onto wall cells. One is picked at random per decorated wall cell.")]
    public TileBase[] wallDecorationTileVariants;
    [Tooltip("Chance, per exterior wall cell, that a decoration is painted there. Cells where a door will spawn are always skipped.")]
    [Range(0f, 1f)] public float wallDecorationChance = 0.08f;

    [Header("Doors")]
    [Tooltip("Prefab with a Door component, drawn for a HORIZONTAL wall segment (room's North/South edge — door spans left-to-right). Instantiated once per shared horizontal edge between two adjacent rooms.")]
    public GameObject horizontalDoorPrefab;

    [Tooltip("Prefab with a Door component, drawn for a VERTICAL wall segment (room's East/West edge — door spans top-to-bottom). Instantiated once per shared vertical edge between two adjacent rooms. Leave empty to fall back to doorPrefab rotated 90°.")]
    public GameObject verticalDoorPrefab;

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
    private RoomStyleSO _chosenStyle;
    private HashSet<Vector2Int> _wallPositions;
    private HashSet<Vector2Int> _doorWallCells;

    public FloorLayout.DungeonResult CurrentLayout => _currentLayout;
    public Room[] Rooms => _currentLayout.Rooms?.ToArray();
    public Vector2Int StartGridPosition => _currentLayout.StartPosition;
    public Vector3 PlayerSpawnWorldPosition { get; private set; }
    public Vector2Int BossGridPosition => _currentLayout.BossPosition;

    /// <summary>The RoomStyleSO randomly chosen for the current dungeon generation.</summary>
    public RoomStyleSO ChosenStyle => _chosenStyle;

    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private static readonly Matrix4x4 FlipXMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

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

            ApplyRandomRoomStyle();
            BuildCellLookup();
            SpawnDungeonVisuals();
            CalculatePlayerSpawnPosition();
            PositionPlayerAtSpawn();
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

    /// <summary>
    /// Picks one RoomStyleSO from availableStyles using the seeded RNG (so the same seed always
    /// picks the same style) and applies its floor/wall tiles to every room in the current layout.
    /// This replaces manually assigning a Style per RoomTemplateSO.
    /// </summary>
    private void ApplyRandomRoomStyle()
    {
        if (availableStyles == null || availableStyles.Length == 0)
        {
            Debug.LogWarning("[DungeonManager] No Room Styles assigned in 'Available Styles' — "
                + "rooms will render without floor/wall tiles. Assign at least one RoomStyleSO asset.");
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

        SpawnVoidTiles();

        // Walls are NEVER hand-placed — same philosophy as doors: derived from grid adjacency
        // of actual occupied cells, using the randomly chosen style's directional wall Rule Tiles.
        BuildWalls();

        // Doors are computed (not yet spawned) here so wall decoration can avoid those cells;
        // the actual Door GameObjects are still created later, in SpawnDoors().
        ComputeDoorWallCells();

        SpawnDecorations();
        SpawnWallDecorations();

        // Must run before SpawnDoors(): doors look up rooms by GridPos to know when to open.
        SpawnRoomControllersAndEnemies();
        SpawnDoors();
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

    /// <summary>
    /// Paints the chosen style's VoidTile onto every Void cell that falls within a room's
    /// rectangular bounds (pits/chasms inside a room's template, not the gaps between rooms —
    /// those are never in camera view since RoomCamera frames exactly one room at a time).
    /// A cell counts as Void here if it's inside the room's Width x Height rectangle but wasn't
    /// painted as Floor/Obstacle in SpawnRoomFloor (i.e. it's absent from _cellLookup).
    /// </summary>
    private void SpawnVoidTiles()
    {
        if (voidTilemap == null || _chosenStyle == null || _chosenStyle.VoidTile == null) return;
        if (_currentLayout.Rooms == null || _cellLookup == null) return;

        var painted = new HashSet<Vector2Int>();

        foreach (var room in _currentLayout.Rooms)
        {
            for (int y = 0; y < room.Height; y++)
            {
                for (int x = 0; x < room.Width; x++)
                {
                    Vector2Int cellPos = new Vector2Int(room.GridPos.x + x, room.GridPos.y + y);

                    if (_cellLookup.ContainsKey(cellPos)) continue; // Floor or Obstacle — not void
                    if (!painted.Add(cellPos)) continue; // already painted (room rectangles don't overlap, but stay safe)

                    voidTilemap.SetTile(new Vector3Int(cellPos.x, cellPos.y, 0), _chosenStyle.VoidTile);
                }
            }
        }
    }

    /// <summary>
    /// Walls are derived per-room, never hand-placed: for each room, whatever cell borders one
    /// of THAT ROOM'S OWN occupied cells but isn't itself one of that room's own cells becomes a
    /// wall — UNLESS it's exactly that room's door position for that side, which is left open.
    ///
    /// Deliberately checked against the room's own cell set rather than the dungeon-wide
    /// _cellLookup: two rooms can sit directly adjacent on the grid without being connected by a
    /// door, and _cellLookup would report that boundary as "occupied" (by the neighboring room),
    /// silently skipping the wall along the whole shared edge instead of just the door's single
    /// tile. Using each room's own cells avoids that and paints a full wall except exactly where
    /// a door belongs.
    ///
    /// Which Rule Tile gets used depends on which side of the room the wall is on — North uses
    /// TopWallTile, South uses BottomWallTile, and East/West both use SideWallTile (East is
    /// painted as a horizontal mirror of the same asset, so only one side tileset needs to be
    /// authored). On a room's north-facing wall specifically, an extra WallTopTile is stacked one
    /// cell higher to fake the taller "top wall" silhouette (32x32 pieces stacked, not a single
    /// tall sprite).
    /// Also records every painted wall cell into _wallPositions, so SpawnWallDecorations() knows
    /// exactly which cells are eligible for a wall decoration.
    /// </summary>
    private void BuildWalls()
    {
        _wallPositions = new HashSet<Vector2Int>();

        if (wallTilemap == null || _currentLayout.Rooms == null) return;

        var paintedTop = new HashSet<Vector2Int>();

        // East wall cells need a horizontal flip (see below), but a Rule Tile's neighbor-refresh
        // resets any per-cell transform we set mid-loop — painting a NEIGHBORING wall cell later
        // triggers Unity to re-run GetTileData() on already-painted Rule Tile cells nearby, which
        // recomputes the transform from scratch (identity) and silently discards our flip. So we
        // only record which cells need flipping here, and apply SetTransformMatrix in one final
        // pass at the very end of this method, once no more SetTile calls remain to re-trigger a
        // refresh.
        var eastFlipPositions = new HashSet<Vector2Int>();

        foreach (var room in _currentLayout.Rooms)
        {
            // Built from THIS room's own cells only — deliberately NOT the dungeon-wide
            // _cellLookup. Two rooms can sit directly adjacent on the grid without being
            // connected by a door, and _cellLookup would report that shared boundary as
            // "occupied" (by the neighboring room's floor), silently skipping the wall along
            // the whole shared edge instead of just the door's single tile. Using each room's
            // own cells means a wall is painted along the full edge except exactly where this
            // room's own door belongs (see IsRoomDoorCell below).
            var roomCells = new HashSet<Vector2Int>();
            foreach (var cell in room.Cells)
                roomCells.Add(cell.CellPos);

            foreach (var cell in room.Cells)
            {
                Vector2Int cellPos = cell.CellPos;

                foreach (var dir in AllDirections)
                {
                    Vector2Int wallPos = cellPos + UnitOffset(dir);

                    // Interior to this same room — not a wall position.
                    if (roomCells.Contains(wallPos)) continue;

                    // Leave exactly this room's door gap open on this side, regardless of
                    // whether a neighboring room's floor happens to sit beyond it.
                    if (IsRoomDoorCell(room, wallPos, dir)) continue;

                    TileBase wallTile = dir switch
                    {
                        DoorDirection.North => room.TopWallTile,
                        DoorDirection.South => room.BottomWallTile,
                        DoorDirection.East  => room.SideWallTile,
                        DoorDirection.West  => room.SideWallTile,
                        _ => null
                    };
                    if (wallTile == null) continue; // no wall style resolved for this side

                    if (_wallPositions.Add(wallPos))
                    {
                        var tilePos = new Vector3Int(wallPos.x, wallPos.y, 0);
                        wallTilemap.SetTile(tilePos, wallTile);

                        // East is the same art as West, mirrored horizontally — but don't set the
                        // transform here, see eastFlipPositions comment above.
                        if (dir == DoorDirection.East)
                            eastFlipPositions.Add(wallPos);
                    }

                    if (dir == DoorDirection.North && room.WallTopTile != null)
                    {
                        Vector2Int capPos = wallPos + UnitOffset(DoorDirection.North);
                        if (paintedTop.Add(capPos))
                            wallTilemap.SetTile(new Vector3Int(capPos.x, capPos.y, 0), room.WallTopTile);
                    }
                }
            }

            // North wall corners: one cell BEYOND the room's floor width on each side, so the
            // North wall row ends up Width + 2 cells wide (corner + Width normal cells + corner)
            // instead of the corners eating into the Width cells directly above the floor.
            // These two cells are never produced by the per-floor-cell loop above (they aren't
            // directly north of any floor cell — they're diagonally outside the room's rectangle),
            // so they're painted explicitly here. The corner sprite itself is still resolved by
            // TopWallTile's own Rule Tile ("no neighbor to the west/east" rule) — same asset, same
            // rules, just now applied to the two outermost cells of a 15-wide row instead of a
            // 13-wide one.
            PaintNorthWallCorners(room);
            PaintSouthWallCorners(room);
        }

        // Now that every wall cell across every room has been painted — and nothing else will
        // call SetTile on wallTilemap after this — it's safe to flip the East wall cells without
        // a later Rule Tile refresh silently resetting them back to identity.
        foreach (var pos in eastFlipPositions)
        {
            Vector3Int tilePos = new Vector3Int(pos.x, pos.y, 0);
            wallTilemap.SetTileFlags(tilePos, TileFlags.None);
            wallTilemap.SetTransformMatrix(tilePos, FlipXMatrix);
        }
    }

    /// <summary>
    /// True if wallPos is exactly this room's door tile on side dir (a single tile, centered on
    /// that side, matching where SpawnDoors/PlaceDoor puts the actual Door GameObject). Doesn't
    /// check whether a neighboring room actually exists there — ComputeDoors() (FloorLayout) only
    /// ever sets a door flag when there IS a connected neighbor, so this is safe on its own.
    /// </summary>
    private static bool IsRoomDoorCell(Room room, Vector2Int wallPos, DoorDirection dir)
    {
        if ((room.Doors & dir) == 0) return false;

        Vector2Int doorCell = dir switch
        {
            DoorDirection.North => new Vector2Int(room.GridPos.x + room.Width / 2, room.GridPos.y + room.Height),
            DoorDirection.South => new Vector2Int(room.GridPos.x + room.Width / 2, room.GridPos.y - 1),
            DoorDirection.East  => new Vector2Int(room.GridPos.x + room.Width, room.GridPos.y + room.Height / 2),
            DoorDirection.West  => new Vector2Int(room.GridPos.x - 1, room.GridPos.y + room.Height / 2),
            _ => wallPos + Vector2Int.one // never matches
        };

        return wallPos == doorCell;
    }

    /// <summary>
    /// Paints the two North-wall corner cells for a room, one cell outside the floor rectangle
    /// on each side (see BuildWalls). Skipped if that exact cell was already painted — e.g. by a
    /// horizontally-adjacent room's own wall — so two neighboring rooms never overwrite each
    /// other's tile at the shared boundary. Once side walls exist, that shared cell is meant to
    /// resolve to the same corner sprite regardless of which room "claims" it first, since both
    /// walls use the same TopWallTile asset with the same rules.
    /// </summary>
    private void PaintNorthWallCorners(Room room)
    {
        if (room.TopWallTile == null) return;

        int wallY = room.GridPos.y + room.Height;
        Vector2Int leftCorner  = new Vector2Int(room.GridPos.x - 1, wallY);
        Vector2Int rightCorner = new Vector2Int(room.GridPos.x + room.Width, wallY);

        PaintWallCellIfEmpty(leftCorner, room.TopWallTile);
        PaintWallCellIfEmpty(rightCorner, room.TopWallTile);
    }

    /// <summary>
    /// Paints the two South-wall corner cells for a room, one cell outside the floor rectangle
    /// on each side. Uses BottomWallTile. Skipped if that exact cell was already painted.
    /// </summary>
    private void PaintSouthWallCorners(Room room)
    {
        if (room.BottomWallTile == null) return;

        // South walls are placed one tile below the room's bottom edge (y - 1)
        int wallY = room.GridPos.y - 1; 
        
        Vector2Int leftCorner  = new Vector2Int(room.GridPos.x - 1, wallY);
        Vector2Int rightCorner = new Vector2Int(room.GridPos.x + room.Width, wallY);

        PaintWallCellIfEmpty(leftCorner, room.BottomWallTile);
        PaintWallCellIfEmpty(rightCorner, room.BottomWallTile);
    }

    private void PaintWallCellIfEmpty(Vector2Int pos, TileBase tile)
    {
        if (!_wallPositions.Add(pos)) return; // already painted — leave whatever's there

        wallTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), tile);
    }

    /// <summary>
    /// Precomputes the grid cell each door will occupy (mirrors the math in PlaceDoor) without
    /// actually spawning anything. Only North/East flags are used — same reasoning as SpawnDoors:
    /// a South/West door is always the other side of some neighboring room's North/East door, so
    /// it's already covered when that neighbor is processed. Used by SpawnWallDecorations() to
    /// skip cells that are about to get a door.
    /// </summary>
    private void ComputeDoorWallCells()
    {
        _doorWallCells = new HashSet<Vector2Int>();
        if (_currentLayout.Rooms == null) return;

        foreach (var room in _currentLayout.Rooms)
        {
            if ((room.Doors & DoorDirection.North) != 0)
                _doorWallCells.Add(new Vector2Int(room.GridPos.x + room.Width / 2, room.GridPos.y + room.Height));

            if ((room.Doors & DoorDirection.East) != 0)
                _doorWallCells.Add(new Vector2Int(room.GridPos.x + room.Width, room.GridPos.y + room.Height / 2));
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

    /// <summary>
    /// Paints wall decorations (torches, cracks, moss, etc.) onto a random subset of exterior
    /// wall cells, using their own tilemap/variant list/chance so they never mix with floor
    /// decoration art. Cells reserved for a door (see ComputeDoorWallCells) are always skipped.
    /// </summary>
    private void SpawnWallDecorations()
    {
        if (wallDecorationTilemap == null || wallDecorationTileVariants == null || wallDecorationTileVariants.Length == 0) return;
        if (_wallPositions == null) return;

        foreach (var wallPos in _wallPositions)
        {
            if (_doorWallCells != null && _doorWallCells.Contains(wallPos)) continue; // reserved for a door
            if (SeedManager.Rng.NextDouble() > wallDecorationChance) continue;

            TileBase deco = wallDecorationTileVariants[SeedManager.Rng.Next(wallDecorationTileVariants.Length)];
            wallDecorationTilemap.SetTile(new Vector3Int(wallPos.x, wallPos.y, 0), deco);
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
        if (horizontalDoorPrefab == null || _currentLayout.Rooms == null) return;

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
        GameObject prefabToUse;

        if (dir == DoorDirection.North)
        {
            worldPos = new Vector3(room.GridPos.x + room.Width / 2f, room.GridPos.y + room.Height, 0f);
            rotation = Quaternion.identity; // horizontal wall — horizontalDoorPrefab is already drawn for this
            prefabToUse = horizontalDoorPrefab;
        }
        else // East
        {
            worldPos = new Vector3(room.GridPos.x + room.Width, room.GridPos.y + room.Height / 2f, 0f);

            if (verticalDoorPrefab != null)
            {
                // Dedicated vertical art — no rotation needed, it's drawn for this orientation.
                rotation = Quaternion.identity;
                prefabToUse = verticalDoorPrefab;
            }
            else
            {
                // Fallback: rotate the horizontal prefab 90° (only looks right for symmetric art).
                rotation = Quaternion.Euler(0f, 0f, 90f);
                prefabToUse = horizontalDoorPrefab;
            }
        }

        GameObject instance = Instantiate(prefabToUse, worldPos, rotation, _doorContainer.transform);
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

    /// <summary>Moves the player (if present in the scene) to the calculated spawn position.
    /// Called right after CalculatePlayerSpawnPosition() during Initialize()/Regenerate().</summary>
    private void PositionPlayerAtSpawn()
    {
        if (PlayerMovement.Instance == null)
        {
            Debug.LogWarning("[DungeonManager] No PlayerMovement.Instance found in the scene — "
                + "can't move the player to the spawn point. Make sure the Player object is active "
                + "before dungeon generation runs.");
            return;
        }

        PlayerMovement.Instance.TeleportTo(PlayerSpawnWorldPosition);
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