using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hand-crafted room definition: shape (which cells are floor), type, and portal positions.
/// Used as a template — the FloorLayout picks templates for each cell during generation.
/// </summary>
[System.Serializable]
public class RoomTemplate
{
    public RoomType Type { get; set; }

    /// <summary>Which cells in the template's local grid are walkable floor tiles.</summary>
    public Vector2Int[] FloorCells { get; set; }

    public int Width
    {
        get
        {
            int max = 0;
            for (int i = 0; i < FloorCells.Length; i++)
                if (FloorCells[i].x > max) max = FloorCells[i].x;
            return max + 1;
        }
    }

    public int Height
    {
        get
        {
            int max = 0;
            for (int i = 0; i < FloorCells.Length; i++)
                if (FloorCells[i].y > max) max = FloorCells[i].y;
            return max + 1;
        }
    }

    /// <summary>Where a connected room can enter this room (normalized in template space, x/y in [0,1]).</summary>
    public Vector2 EntrancePortal { get; set; } = new Vector2(0.5f, 1f); // bottom by default

    /// <summary>Where this room exits into the next room (normalized in template space, x/y in [0,1]).</summary>
    public Vector2 ExitPortal { get; set; } = new Vector2(0.5f, 0f);     // top by default

    /// <summary>Create a simple RoomTemplate from a set of floor cells and type.</summary>
    public static RoomTemplate Create(RoomType type, params (int x, int y)[] cellCoords)
    {
        var cells = new Vector2Int[cellCoords.Length];
        for (int i = 0; i < cellCoords.Length; i++)
            cells[i] = new Vector2Int(cellCoords[i].x, cellCoords[i].y);

        RoomTemplate template = new RoomTemplate();
        template.Type = type;
        template.FloorCells = cells;
        return template;
    }

    /// <summary>Clone with custom entrance/exit portals for directional rooms.</summary>
    public static RoomTemplate Create(RoomType type, Vector2 entrancePortal, Vector2 exitPortal, params (int x, int y)[] cellCoords)
    {
        RoomTemplate template = Create(type, cellCoords);
        template.EntrancePortal = entrancePortal;
        template.ExitPortal = exitPortal;
        return template;
    }
}

/// <summary>Collection of templates grouped by room type for random selection during generation.</summary>
public static class RoomPool
{
    private static readonly Dictionary<RoomType, List<RoomTemplate>> _templates = new Dictionary<RoomType, List<RoomTemplate>>();

    /// <summary>Initialize the template pool with all available room shapes.</summary>
    public static void Build()
    {
        _templates.Clear();

        // — Start: single-cell room, accessible from below (spawn is at the top of a corridor) —
        Add(RoomType.Start, RoomTemplate.Create(
            RoomType.Start,
            (0, 0)));

        // — Normal: multiple shapes for variety —
        Add(RoomType.Normal, RoomTemplate.Create(
            RoomType.Normal,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), // entrance bottom, exit top (vertical)
            (0, 0), (1, 0))); // horizontal corridor (2x1)

        Add(RoomType.Normal, RoomTemplate.Create(
            RoomType.Normal,
            new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), // entrance right, exit left
            (0, 0), (0, 1))); // vertical corridor (1x2)

        Add(RoomType.Normal, RoomTemplate.Create(
            RoomType.Normal,
            (0, 0))); // simple room (1x1)

        Add(RoomType.Normal, RoomTemplate.Create(
            RoomType.Normal,
            (0, 0), (1, 0), (0, 1), (1, 1))); // square room (2x2)

        // — Treasure: chamber that can be entered from top and exited right —
        Add(RoomType.Treasure, RoomTemplate.Create(
            RoomType.Treasure,
            new Vector2(0.5f, 1f), new Vector2(1f, 0.5f),
            (0, 0), (1, 0)));

        Add(RoomType.Treasure, RoomTemplate.Create(
            RoomType.Treasure,
            new Vector2(0.5f, 1f), new Vector2(1f, 0.5f),
            (0, 0), (1, 0), (0, 1), (1, 1)));

        // — Boss: single-cell chamber with entrance and exit —
        Add(RoomType.Boss, RoomTemplate.Create(
            RoomType.Boss,
            new Vector2(0.5f, 1f), new Vector2(1f, 0.5f),
            (0, 0)));

        // — DeadEnd: small chamber at the end of a path —
        Add(RoomType.DeadEnd, RoomTemplate.Create(
            RoomType.DeadEnd,
            (0, 0))); // 1x1

        Add(RoomType.DeadEnd, RoomTemplate.Create(
            RoomType.DeadEnd,
            new Vector2(1f, 0.5f), Vector2Int.zero, // no exit (dead end)
            (0, 0), (0, 1))); // 2x1 vertical

        Add(RoomType.DeadEnd, RoomTemplate.Create(
            RoomType.DeadEnd,
            new Vector2(1f, 0.5f), Vector2Int.zero,
            (0, 0), (1, 0), (2, 0))); // 3x1 horizontal

        // — Corridor: thin connecting room with aligned entrance/exit —
        Add(RoomType.Corridor, RoomTemplate.Create(
            RoomType.Corridor,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            (0, 0), (1, 0))); // horizontal

        Add(RoomType.Corridor, RoomTemplate.Create(
            RoomType.Corridor,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
            (0, 0), (0, 1))); // vertical

        Add(RoomType.Corridor, RoomTemplate.Create(
            RoomType.Corridor,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            (0, 0), (1, 0), (2, 0))); // long horizontal
    }

    /// <summary>Get the list of templates for a given room type.</summary>
    public static List<RoomTemplate> GetTemplates(RoomType type)
    {
        if (_templates.TryGetValue(type, out var list))
            return list;
        return new List<RoomTemplate>();
    }

    private static void Add(RoomType type, RoomTemplate template)
    {
        if (!_templates.ContainsKey(type))
            _templates[type] = new List<RoomTemplate>();
        _templates[type].Add(template);
    }
}

/// <summary>Represents a room placed on the dungeon grid during generation.</summary>
public class PlacedRoom
{
    public RoomTemplate Template { get; private set; }
    public Vector2Int GridPos { get; private set; }
    public RoomType Type => Template.Type;

    /// <summary>Which direction this room connects toward an adjacent placed room (1=right, -1=left, etc.).</summary>
    public List<Vector2Int> Connections { get; } = new List<Vector2Int>();

    public PlacedRoom(RoomTemplate template, Vector2Int gridPos)
    {
        Template = template;
        GridPos = gridPos;
    }

    /// <summary>Add a connection to an adjacent placed room. Direction in grid-space (+/- 1 on one axis).</summary>
    public void AddConnection(Vector2Int direction, PlacedRoom connectedTo)
    {
        Connections.Add(direction);
    }

    public PlacedRoom GetConnectedRoom(int index)
    {
        // We store a reference alongside the direction; simplified to return by direction index.
        return null; // placeholder — filled at generation time via DungeonLayoutManager
    }
}
