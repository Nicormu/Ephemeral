using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Paints optional floor decoration (cracks, moss, debris — cosmetic, random per Floor cell)
/// and wall decoration (same idea, per wall cell). Wall decorations are rotated to match the
/// side of the room they sit on — North upright, South flipped 180°, East/West rotated 90° —
/// so a single rotation-agnostic tile set can dress all four sides without separate per-side
/// art. Only suitable for direction-agnostic decoration (cracks, moss, rubble); avoid this for
/// gravity-anchored props like torch brackets, which would render sideways/upside-down.
/// Corner cells are never decorated — DungeonWallBuilder never puts them in WallPositions.
/// </summary>
public class DungeonDecorationPainter
{
    // North omitted deliberately — identity rotation, nothing to apply.
    private static readonly Dictionary<DoorDirection, Matrix4x4> RotationBySide = new Dictionary<DoorDirection, Matrix4x4>
    {
        { DoorDirection.South, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 180f)) },
        { DoorDirection.East,  Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f)) },
        { DoorDirection.West,  Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)) },
    };

    private readonly Tilemap _decorationTilemap;
    private readonly TileBase[] _decorationTileVariants;
    private readonly float _decorationChance;

    private readonly Tilemap _wallDecorationTilemap;
    private readonly TileBase[] _wallDecorationTileVariants;
    private readonly float _wallDecorationChance;

    public DungeonDecorationPainter(
        Tilemap decorationTilemap, TileBase[] decorationTileVariants, float decorationChance,
        Tilemap wallDecorationTilemap, TileBase[] wallDecorationTileVariants, float wallDecorationChance)
    {
        _decorationTilemap = decorationTilemap;
        _decorationTileVariants = decorationTileVariants;
        _decorationChance = decorationChance;

        _wallDecorationTilemap = wallDecorationTilemap;
        _wallDecorationTileVariants = wallDecorationTileVariants;
        _wallDecorationChance = wallDecorationChance;
    }

    public void SpawnFloorDecorations(IReadOnlyDictionary<Vector2Int, CellState> cellLookup)
    {
        if (_decorationTilemap == null || _decorationTileVariants == null || _decorationTileVariants.Length == 0) return;
        if (cellLookup == null) return;

        foreach (var kv in cellLookup)
        {
            if (kv.Value != CellState.Floor) continue; // never decorate obstacles/void
            if (SeedManager.Rng.NextDouble() > _decorationChance) continue;

            TileBase deco = _decorationTileVariants[SeedManager.Rng.Next(_decorationTileVariants.Length)];
            _decorationTilemap.SetTile(new Vector3Int(kv.Key.x, kv.Key.y, 0), deco);
        }
    }

    /// <summary>
    /// wallPositions comes from DungeonWallBuilder.WallPositions (side per cell, corners already
    /// excluded); doorWallCells comes from DungeonWallBuilder.DoorWallCells (skip door cells).
    /// </summary>
    public void SpawnWallDecorations(IReadOnlyDictionary<Vector2Int, DoorDirection> wallPositions, HashSet<Vector2Int> doorWallCells)
    {
        if (_wallDecorationTilemap == null || _wallDecorationTileVariants == null || _wallDecorationTileVariants.Length == 0) return;
        if (wallPositions == null) return;

        // Painted first, rotation applied in a final pass — same deferred-transform approach
        // DungeonWallBuilder uses for the East wall flip, in case this tilemap ever uses Rule
        // Tiles that would otherwise reset a per-cell transform on neighbor refresh.
        var toRotate = new List<(Vector2Int pos, DoorDirection dir)>();

        foreach (var kv in wallPositions)
        {
            Vector2Int wallPos = kv.Key;
            DoorDirection side = kv.Value;

            if (doorWallCells != null && doorWallCells.Contains(wallPos)) continue; // reserved for a door
            if (SeedManager.Rng.NextDouble() > _wallDecorationChance) continue;

            TileBase deco = _wallDecorationTileVariants[SeedManager.Rng.Next(_wallDecorationTileVariants.Length)];
            _wallDecorationTilemap.SetTile(new Vector3Int(wallPos.x, wallPos.y, 0), deco);

            if (side != DoorDirection.North)
                toRotate.Add((wallPos, side));
        }

        foreach (var (pos, side) in toRotate)
        {
            var tilePos = new Vector3Int(pos.x, pos.y, 0);
            _wallDecorationTilemap.SetTileFlags(tilePos, TileFlags.None);
            _wallDecorationTilemap.SetTransformMatrix(tilePos, RotationBySide[side]);
        }
    }
}