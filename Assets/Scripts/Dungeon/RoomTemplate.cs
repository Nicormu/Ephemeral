using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomTemplate
{
    public static readonly Vector2Int RoomTileSize = RoomTemplateSO.RoomTileSize;

    // The only door bits FloorLayout.ComputeDoors() can ever request are these four — it's a
    // plain OR of North/South/East/West, so the result is always in 0..15. If a RoomTemplateSO's
    // Doors field was set via the Inspector's "Everything" entry instead of ticking each
    // checkbox, Unity's default Flags-enum mask popup stores -1 (all 32 bits) rather than 15 —
    // which would never equal FloorLayout's requested mask, causing RoomPool to silently miss
    // the exact match and fall back to a same-type template with mismatched doors. Masking here,
    // the single point where an SO's raw Doors value enters the runtime pipeline, makes matching
    // correct regardless of how the value ended up serialized.
    private const DoorDirection AllValidDoorBits =
        DoorDirection.North | DoorDirection.South | DoorDirection.East | DoorDirection.West;

    public RoomType Type { get; set; }
    public DoorDirection Doors { get; set; }
    public (Vector2Int pos, CellState state, TileBase[] obstacleTileVariants, Sprite[] obstacleSpriteVariants,
        bool obstacleBlocksMovement, int obstacleDamage, bool obstacleIsDestructible, int obstacleMaxHealth,
        GameObject obstacleBreakEffectPrefab, GameObject obstaclePrefab, bool obstacleIgnoredByFlight)[] Cells { get; set; }
    public (Vector2Int pos, GameObject prefab)[] EnemySpawns { get; set; }

    // Populated later by DungeonManager after a style is randomly chosen for the whole dungeon —
    // not sourced from the template itself anymore. See DungeonManager.ApplyRandomRoomStyle().
    public TileBase FloorTile { get; set; }
    public TileBase TopWallTile { get; set; }
    public TileBase BottomWallTile { get; set; }
    public TileBase SideWallTile { get; set; }
    public TileBase WallTopTile { get; set; }

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    public static RoomTemplate FromSO(RoomTemplateSO so)
    {
        var enemySpawns = new List<(Vector2Int, GameObject)>();

        if (so.EnemySpawnEntries != null)
        {
            foreach (var entry in so.EnemySpawnEntries)
            {
                if (entry.EnemyPrefab == null) continue;

                if (entry.SpawnPointIndex < 0 || entry.SpawnPointIndex >= so.EnemySpawnPoints.Count)
                {
                    Debug.LogWarning($"[RoomTemplate] '{so.name}' has an enemy entry with an invalid spawn point index — skipping.");
                    continue;
                }

                enemySpawns.Add((so.EnemySpawnPoints[entry.SpawnPointIndex], entry.EnemyPrefab));
            }
        }

        DoorDirection sanitizedDoors = so.Doors & AllValidDoorBits;

        if (sanitizedDoors != so.Doors)
        {
            Debug.LogWarning($"[RoomTemplate] '{so.name}' has a Doors value of {(int)so.Doors}, which includes bits " +
                $"outside North|South|East|West (max valid value is 15). This happens when the Doors field was set via " +
                $"the Inspector's \"Everything\" option instead of ticking each checkbox individually — Unity's mask " +
                $"popup writes -1 for \"Everything\" rather than the sum of the four flags. Sanitized to " +
                $"{(int)sanitizedDoors} ({sanitizedDoors}) so RoomPool matching works correctly.");
        }

        return new RoomTemplate
        {
            Type = so.Type,
            Doors = sanitizedDoors,
            Cells = so.GetOccupiedCells(),
            EnemySpawns = enemySpawns.ToArray(),
            FloorTile = null,
            TopWallTile = null,
            BottomWallTile = null,
            SideWallTile = null,
            WallTopTile = null
        };
    }
}