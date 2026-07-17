using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hand-crafted room shape for one exact (RoomType, DoorDirection) combination.
/// All templates share a uniform tile size so rooms tile cleanly on the room-grid, Isaac-style.
/// </summary>
[System.Serializable]
public class RoomTemplate
{
    /// <summary>Uniform room size in tiles. Every template's floor cells must fit inside this.</summary>
    public static readonly Vector2Int RoomTileSize = new Vector2Int(11, 9);

    public RoomType Type { get; set; }
    public DoorDirection Doors { get; set; }

    /// <summary>Local floor cells (relative to template origin, 0..RoomTileSize-1).</summary>
    public Vector2Int[] FloorCells { get; set; }

    public int Width => RoomTileSize.x;
    public int Height => RoomTileSize.y;

    /// <summary>Simple full-rectangle template — used as the automatic fallback for every combo.</summary>
    public static RoomTemplate CreateDefault(RoomType type, DoorDirection doors)
    {
        var cells = new List<Vector2Int>(RoomTileSize.x * RoomTileSize.y);
        for (int y = 0; y < RoomTileSize.y; y++)
            for (int x = 0; x < RoomTileSize.x; x++)
                cells.Add(new Vector2Int(x, y));

        return new RoomTemplate { Type = type, Doors = doors, FloorCells = cells.ToArray() };
    }

    /// <summary>Hand-craft a custom floor shape (L-shape, narrow alcove, etc.) for a specific door combo.</summary>
    public static RoomTemplate CreateCustom(RoomType type, DoorDirection doors, params (int x, int y)[] cellCoords)
    {
        var cells = new Vector2Int[cellCoords.Length];
        for (int i = 0; i < cellCoords.Length; i++)
            cells[i] = new Vector2Int(cellCoords[i].x, cellCoords[i].y);

        return new RoomTemplate { Type = type, Doors = doors, FloorCells = cells };
    }
}

/// <summary>
/// Templates grouped by exact (RoomType, DoorDirection). The generator always knows exactly
/// which doors a cell needs before requesting a template, so lookup is exact-match first.
/// </summary>
public static class RoomPool
{
    private static readonly Dictionary<(RoomType, DoorDirection), List<RoomTemplate>> _exact = new();
    private static readonly Dictionary<RoomType, List<RoomTemplate>> _anyDoorFallback = new();

    public static void Build()
    {
        _exact.Clear();
        _anyDoorFallback.Clear();

        // Auto-generate a default rectangular template for every (type, door-combo) pair.
        // Guarantees the generator always has something to place, even with zero hand-crafted art.
        foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            foreach (var doors in AllDoorCombinations())
                Add(RoomTemplate.CreateDefault(type, doors));

        // --- Hand-crafted variety — add more of these per (type, doors) combo you want variation on ---

        // Alternate DeadEnd shape for a South-only door (small alcove instead of full rectangle).
        Add(RoomTemplate.CreateCustom(RoomType.DeadEnd, DoorDirection.South,
            (3,0),(4,0),(5,0),(6,0),(7,0),
            (3,1),(4,1),(5,1),(6,1),(7,1),
            (5,2)));

        // Alternate straight Corridor shape, thinner than the default full rectangle.
        Add(RoomTemplate.CreateCustom(RoomType.Corridor, DoorDirection.East | DoorDirection.West,
            (0,4),(1,4),(2,4),(3,4),(4,4),(5,4),(6,4),(7,4),(8,4),(9,4),(10,4)));
    }

    /// <summary>Get a random template matching the exact door configuration, or a warned same-type fallback.</summary>
    public static RoomTemplate GetTemplate(RoomType type, DoorDirection doors, System.Random rng)
    {
        if (_exact.TryGetValue((type, doors), out var exact) && exact.Count > 0)
            return exact[rng.Next(exact.Count)];

        if (_anyDoorFallback.TryGetValue(type, out var any) && any.Count > 0)
        {
            Debug.LogWarning($"[RoomPool] No template for {type} with doors {doors} — using a same-type fallback (doors won't visually line up).");
            return any[rng.Next(any.Count)];
        }

        return null;
    }

    private static void Add(RoomTemplate template)
    {
        var key = (template.Type, template.Doors);
        if (!_exact.TryGetValue(key, out var list))
        {
            list = new List<RoomTemplate>();
            _exact[key] = list;
        }
        list.Add(template);

        if (!_anyDoorFallback.TryGetValue(template.Type, out var anyList))
        {
            anyList = new List<RoomTemplate>();
            _anyDoorFallback[template.Type] = anyList;
        }
        anyList.Add(template);
    }

    private static IEnumerable<DoorDirection> AllDoorCombinations()
    {
        for (int mask = 1; mask <= 15; mask++) // 1..15 = every nonzero combination of the 4 door flags
            yield return (DoorDirection)mask;
    }
}