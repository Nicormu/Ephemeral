using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Derives and paints every wall tile from each room's own rectangular bounds — never hand-placed,
/// and NOT derived from which interior cells happen to be Floor/Obstacle/Void. Paints two parallel
/// tilemaps: a VISIBLE one that stays continuous even across door gaps (so Rule Tiles never render
/// a false corner next to a door), and an INVISIBLE collision one with a real gap left at each door
/// so the player can walk through. Also tracks which side (North/South/East/West) painted each
/// collision wall cell, so DungeonDecorationPainter can orient decorations per side. Corners are
/// tracked separately and are never eligible for decoration.
///
/// IMPORTANT (fixed): walls used to be derived by looking outward from each Floor/Obstacle cell in
/// room.Cells. Since RoomTemplateSO.GetOccupiedCells skips Void cells entirely, a Void cell placed
/// in a room template's OUTER ring (the row/column directly touching the exterior wall) meant no
/// Floor cell ever existed there to request a wall tile — leaving a literal hole in the boundary
/// wall exactly where that pit touched the edge, with the pit's darkness visually "leaking" through
/// it. Walls are now painted by walking each side of the room's rectangle directly (GridPos to
/// GridPos+Width/Height), so the exterior is always fully enclosed no matter what's inside — Void
/// pits, obstacles, or Floor. This also means internal Void pits (fully inside the room bounds)
/// never get walls painted around them, with no special-casing needed — they're just never visited
/// by this perimeter walk in the first place.
/// </summary>
public class DungeonWallBuilder
{
    private static readonly DoorDirection[] AllDirections =
        { DoorDirection.North, DoorDirection.South, DoorDirection.East, DoorDirection.West };

    private static readonly Matrix4x4 FlipXMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

    private readonly Tilemap _wallTilemap;
    private readonly Tilemap _wallCollisionTilemap;

    /// <summary>Collision-relevant wall cells (door cells excluded, corners excluded), mapped to
    /// the side of the room that painted them. This is what DungeonDecorationPainter iterates.</summary>
    public Dictionary<Vector2Int, DoorDirection> WallPositions { get; private set; }

    /// <summary>Every wall cell painted into the visible tilemap, including door and corner cells
    /// — kept only so painting never double-sets the same cell.</summary>
    public HashSet<Vector2Int> WallVisualPositions { get; private set; }

    /// <summary>The corner cells (one cell beyond the floor rect on each side of the N/S rows).
    /// Never decorated — they don't belong to a single side.</summary>
    public HashSet<Vector2Int> CornerPositions { get; private set; }

    /// <summary>Every grid cell that will get a door — used to skip decorating door cells.</summary>
    public HashSet<Vector2Int> DoorWallCells { get; private set; }

    public DungeonWallBuilder(Tilemap wallTilemap, Tilemap wallCollisionTilemap)
    {
        _wallTilemap = wallTilemap;
        _wallCollisionTilemap = wallCollisionTilemap;
    }

    public void Build(List<Room> rooms)
    {
        WallPositions = new Dictionary<Vector2Int, DoorDirection>();
        WallVisualPositions = new HashSet<Vector2Int>();
        CornerPositions = new HashSet<Vector2Int>();

        ComputeDoorWallCells(rooms);

        if (_wallTilemap == null || rooms == null) return;

        var paintedTop = new HashSet<Vector2Int>();

        // East wall cells need a horizontal flip, but a Rule Tile's neighbor-refresh resets any
        // per-cell transform set mid-loop — painting a NEIGHBORING wall cell later triggers
        // Unity to re-run GetTileData() on already-painted Rule Tile cells nearby, recomputing
        // the transform from scratch. So we only record which cells need flipping here, and
        // apply SetTransformMatrix in one final pass once no more SetTile calls remain.
        var eastFlipVisualPositions = new HashSet<Vector2Int>();
        var eastFlipCollisionPositions = new HashSet<Vector2Int>();

        foreach (var room in rooms)
        {
            foreach (var side in AllDirections)
                PaintWallRun(room, side, paintedTop, eastFlipVisualPositions, eastFlipCollisionPositions);

            PaintNorthWallCorners(room);
            PaintSouthWallCorners(room);
        }

        foreach (var pos in eastFlipVisualPositions)
        {
            Vector3Int tilePos = new Vector3Int(pos.x, pos.y, 0);
            _wallTilemap.SetTileFlags(tilePos, TileFlags.None);
            _wallTilemap.SetTransformMatrix(tilePos, FlipXMatrix);
        }

        if (_wallCollisionTilemap != null)
        {
            foreach (var pos in eastFlipCollisionPositions)
            {
                Vector3Int tilePos = new Vector3Int(pos.x, pos.y, 0);
                _wallCollisionTilemap.SetTileFlags(tilePos, TileFlags.None);
                _wallCollisionTilemap.SetTransformMatrix(tilePos, FlipXMatrix);
            }
        }
    }

    /// <summary>Paints every wall cell along ONE side of a room's rectangle — walked directly from
    /// GridPos/Width/Height, deliberately ignoring whatever CellState (Floor/Obstacle/Void) sits
    /// just inside that boundary. This is what guarantees the room is always fully enclosed.</summary>
    private void PaintWallRun(Room room, DoorDirection side, HashSet<Vector2Int> paintedTop,
        HashSet<Vector2Int> eastFlipVisualPositions, HashSet<Vector2Int> eastFlipCollisionPositions)
    {
        TileBase wallTile = side switch
        {
            DoorDirection.North => room.TopWallTile,
            DoorDirection.South => room.BottomWallTile,
            DoorDirection.East  => room.SideWallTile,
            DoorDirection.West  => room.SideWallTile,
            _ => null
        };
        if (wallTile == null) return;

        foreach (Vector2Int wallPos in GetSidePositions(room, side))
        {
            bool isDoorCell = IsRoomDoorCell(room, wallPos, side);

            if (WallVisualPositions.Add(wallPos))
            {
                var tilePos = new Vector3Int(wallPos.x, wallPos.y, 0);
                _wallTilemap.SetTile(tilePos, wallTile);

                if (side == DoorDirection.East)
                    eastFlipVisualPositions.Add(wallPos);
            }

            if (!isDoorCell)
            {
                WallPositions[wallPos] = side;

                if (_wallCollisionTilemap != null)
                {
                    var tilePos = new Vector3Int(wallPos.x, wallPos.y, 0);
                    _wallCollisionTilemap.SetTile(tilePos, wallTile);

                    if (side == DoorDirection.East)
                        eastFlipCollisionPositions.Add(wallPos);
                }
            }

            if (side == DoorDirection.North && room.WallTopTile != null)
            {
                Vector2Int capPos = wallPos + DungeonGeometry.UnitOffset(DoorDirection.North);
                if (paintedTop.Add(capPos))
                    _wallTilemap.SetTile(new Vector3Int(capPos.x, capPos.y, 0), room.WallTopTile);
            }
        }
    }

    /// <summary>The run of exterior wall cell positions along one side of a room's rectangle,
    /// excluding corners (corners are handled separately by PaintNorthWallCorners/PaintSouthWallCorners).</summary>
    private static IEnumerable<Vector2Int> GetSidePositions(Room room, DoorDirection side)
    {
        switch (side)
        {
            case DoorDirection.North:
            {
                int y = room.GridPos.y + room.Height;
                for (int x = room.GridPos.x; x < room.GridPos.x + room.Width; x++)
                    yield return new Vector2Int(x, y);
                break;
            }
            case DoorDirection.South:
            {
                int y = room.GridPos.y - 1;
                for (int x = room.GridPos.x; x < room.GridPos.x + room.Width; x++)
                    yield return new Vector2Int(x, y);
                break;
            }
            case DoorDirection.East:
            {
                int x = room.GridPos.x + room.Width;
                for (int y = room.GridPos.y; y < room.GridPos.y + room.Height; y++)
                    yield return new Vector2Int(x, y);
                break;
            }
            case DoorDirection.West:
            {
                int x = room.GridPos.x - 1;
                for (int y = room.GridPos.y; y < room.GridPos.y + room.Height; y++)
                    yield return new Vector2Int(x, y);
                break;
            }
        }
    }

    private static bool IsRoomDoorCell(Room room, Vector2Int wallPos, DoorDirection dir)
    {
        if ((room.Doors & dir) == 0) return false;
        return wallPos == DungeonGeometry.GetDoorWallCell(room, dir);
    }

    private void PaintNorthWallCorners(Room room)
    {
        if (room.TopWallTile == null) return;

        int wallY = room.GridPos.y + room.Height;
        Vector2Int leftCorner  = new Vector2Int(room.GridPos.x - 1, wallY);
        Vector2Int rightCorner = new Vector2Int(room.GridPos.x + room.Width, wallY);

        PaintCornerCellIfEmpty(leftCorner, room.TopWallTile);
        PaintCornerCellIfEmpty(rightCorner, room.TopWallTile);
    }

    private void PaintSouthWallCorners(Room room)
    {
        if (room.BottomWallTile == null) return;

        int wallY = room.GridPos.y - 1;
        Vector2Int leftCorner  = new Vector2Int(room.GridPos.x - 1, wallY);
        Vector2Int rightCorner = new Vector2Int(room.GridPos.x + room.Width, wallY);

        PaintCornerCellIfEmpty(leftCorner, room.BottomWallTile);
        PaintCornerCellIfEmpty(rightCorner, room.BottomWallTile);
    }

    /// <summary>Corners are painted onto both tilemaps (they still need to block movement) but
    /// deliberately kept OUT of WallPositions — this is what makes them automatically ineligible
    /// for wall decoration without the decoration painter needing any corner-specific check.</summary>
    private void PaintCornerCellIfEmpty(Vector2Int pos, TileBase tile)
    {
        if (!CornerPositions.Add(pos)) return; // already painted

        var tilePos = new Vector3Int(pos.x, pos.y, 0);

        if (WallVisualPositions.Add(pos))
            _wallTilemap.SetTile(tilePos, tile);

        if (_wallCollisionTilemap != null)
            _wallCollisionTilemap.SetTile(tilePos, tile);
    }

    private void ComputeDoorWallCells(List<Room> rooms)
    {
        DoorWallCells = new HashSet<Vector2Int>();
        if (rooms == null) return;

        foreach (var room in rooms)
            foreach (var dir in AllDirections)
                if ((room.Doors & dir) != 0)
                    DoorWallCells.Add(DungeonGeometry.GetDoorWallCell(room, dir));
    }
}