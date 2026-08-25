using System.Collections.Generic;
using UnityEngine;

public static class RoomPool
{
    private static readonly Dictionary<(RoomType, DoorDirection), List<RoomTemplate>> _exact = new();
    private static readonly Dictionary<RoomType, List<RoomTemplate>> _anyDoorFallback = new();

    public static void Build()
    {
        _exact.Clear();
        _anyDoorFallback.Clear();

        var loaded = Resources.LoadAll<RoomTemplateSO>("RoomTemplates");
        if (loaded.Length == 0)
            Debug.LogWarning("[RoomPool] No RoomTemplateSO assets found in Resources/RoomTemplates — " +
                "create assets via CreateAssetMenu > Dungeon/Room Template, then place them in that folder.");

        foreach (var so in loaded)
            Add(RoomTemplate.FromSO(so));
    }

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
        if (!_exact.TryGetValue(key, out var list)) { list = new List<RoomTemplate>(); _exact[key] = list; }
        list.Add(template);

        if (!_anyDoorFallback.TryGetValue(template.Type, out var anyList)) { anyList = new List<RoomTemplate>(); _anyDoorFallback[template.Type] = anyList; }
        anyList.Add(template);
    }
}