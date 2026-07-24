using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomTemplate
{
    public static readonly Vector2Int RoomTileSize = RoomTemplateSO.RoomTileSize;

    public RoomType Type { get; set; }
    public DoorDirection Doors { get; set; }
    public (Vector2Int pos, CellState state, TileBase obstacleTile, bool obstacleBlocksMovement, int obstacleDamage)[] Cells { get; set; }
    public (Vector2Int pos, GameObject prefab)[] EnemySpawns { get; set; }

    // Populated later by DungeonManager after a style is randomly chosen for the whole dungeon —
    // not sourced from the template itself anymore. See DungeonManager.ApplyRandomRoomStyle().
    public TileBase FloorTile { get; set; }
    public TileBase WallFrontTile { get; set; }
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

        return new RoomTemplate
        {
            Type = so.Type,
            Doors = so.Doors,
            Cells = so.GetOccupiedCells(),
            EnemySpawns = enemySpawns.ToArray(),
            FloorTile = null,
            WallFrontTile = null,
            WallTopTile = null
        };
    }
}