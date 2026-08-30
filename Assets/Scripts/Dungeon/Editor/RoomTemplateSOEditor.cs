using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomTemplateSO))]
public class RoomTemplateSOEditor : Editor
{
    private const float CellSize = 20f;

    // Only these 4 bits are ever valid — anything else (notably -1 / "Everything", the value
    // Unity's built-in EnumFlagsField widget can snap to even when every individual checkbox
    // was ticked one by one) gets masked away by DoorsMask.
    private const int DoorsMask = (int)(DoorDirection.North | DoorDirection.South | DoorDirection.East | DoorDirection.West);

    private enum PaintMode { Cells, EnemySpawnPoints }
    private PaintMode _paintMode = PaintMode.Cells;
    private int _activeObstacleType = 0;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw every default field EXCEPT Doors — Doors gets its own toggle-based UI below
        // instead of Unity's default flags widget, which is what causes the "Everything" (-1)
        // corruption in the first place. See DrawDoorsField for details.
        DrawPropertiesExcluding(serializedObject, "Doors");
        DrawDoorsField();

        serializedObject.ApplyModifiedProperties();

        var template = (RoomTemplateSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Editor", EditorStyles.boldLabel);

        _paintMode = (PaintMode)EditorGUILayout.EnumPopup("Paint Mode", _paintMode);

        if (_paintMode == PaintMode.Cells)
        {
            DrawObstaclePalette(template);
            EditorGUILayout.LabelField("Click: Void → Floor → Obstacle (active type) → Void", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Click a Floor cell to add/remove an enemy spawn point. Click a Void cell to add/remove a FLYING-only spawn point (blue).", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();
        DrawGrid(template);

        if (_paintMode == PaintMode.EnemySpawnPoints)
            DrawVoidSpawnPointWarnings(template);
    }

    /// <summary>
    /// Custom replacement for the default [Flags] enum widget. Unity's built-in EnumFlagsField
    /// (what DrawDefaultInspector() renders for the Doors property) can write -1 ("Everything")
    /// to the serialized int the instant every individually-representable flag happens to be
    /// selected — even if you ticked each checkbox one at a time and never touched an
    /// "Everything" entry yourself. That -1 then fails RoomTemplate.FromSO's validity check
    /// (any bits outside North|South|East|West), which is what produced the warning you saw.
    ///
    /// This draws 4 plain toggles bound directly to the individual bits, so the serialized value
    /// can only ever be some combination of 1/2/4/8 — never -1. It also SELF-HEALS: if this
    /// asset already has a corrupted -1 stored (from before this fix), the moment this Inspector
    /// draws, `sanitized` masks it back down to the correct value and immediately writes it back
    /// via doorsProp.intValue below — so just selecting the asset once in the Project window
    /// permanently fixes it (see the batch menu command further down for fixing many at once
    /// without opening each one).
    /// </summary>
    private void DrawDoorsField()
    {
        var doorsProp = serializedObject.FindProperty("Doors");
        if (doorsProp == null) return;

        int current = doorsProp.intValue & DoorsMask; // sanitize on every draw — heals old -1 values

        EditorGUILayout.LabelField("Doors", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        current = ToggleFlag(current, (int)DoorDirection.North, "North");
        current = ToggleFlag(current, (int)DoorDirection.South, "South");
        current = ToggleFlag(current, (int)DoorDirection.East, "East");
        current = ToggleFlag(current, (int)DoorDirection.West, "West");
        EditorGUILayout.EndHorizontal();

        doorsProp.intValue = current;
    }

    private int ToggleFlag(int current, int flag, string label)
    {
        bool has = (current & flag) != 0;
        bool newVal = EditorGUILayout.ToggleLeft(label, has, GUILayout.Width(65));
        return newVal ? (current | flag) : (current & ~flag);
    }

    /// <summary>
    /// One-click fix for every RoomTemplateSO in the project at once — no need to open each
    /// asset in the Inspector individually to trigger the self-heal in DrawDoorsField above.
    /// </summary>
    [MenuItem("Tools/Dungeon/Fix Corrupted Door Masks On All Room Templates")]
    private static void FixAllDoorMasks()
    {
        string[] guids = AssetDatabase.FindAssets("t:RoomTemplateSO");
        int fixedCount = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(path);
            if (so == null) continue;

            int sanitized = (int)so.Doors & DoorsMask;
            if (sanitized != (int)so.Doors)
            {
                Undo.RecordObject(so, "Fix Door Mask");
                so.Doors = (DoorDirection)sanitized;
                EditorUtility.SetDirty(so);
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomTemplateSOEditor] Fixed {fixedCount} RoomTemplateSO asset(s) with a corrupted Doors mask.");
        }
        else
        {
            Debug.Log("[RoomTemplateSOEditor] No RoomTemplateSO assets needed fixing — all Doors masks were already valid.");
        }
    }

    private void DrawObstaclePalette(RoomTemplateSO template)
    {
        if (template.ObstacleTypes == null || template.ObstacleTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("Add at least one entry to 'Obstacle Types' above to paint obstacles. Drag in ObstacleType assets (Create > Dungeon/Obstacle Type) — set 'Blocks Movement' off + a Damage value on the asset to make one act like fire.", MessageType.Info);
            _activeObstacleType = 0;
            return;
        }

        var labels = new string[template.ObstacleTypes.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            var def = template.ObstacleTypes[i];
            if (def == null)
            {
                labels[i] = $"{i}: (empty slot)";
                continue;
            }
            string suffix = def.BlocksMovement ? "" : $" (hazard, {def.Damage} dmg)";
            labels[i] = $"{i}: {def.name}{suffix}";
        }

        _activeObstacleType = Mathf.Clamp(_activeObstacleType, 0, labels.Length - 1);
        _activeObstacleType = EditorGUILayout.Popup("Active Obstacle Type", _activeObstacleType, labels);

        if (template.ObstacleTypes[_activeObstacleType] == null)
            EditorGUILayout.HelpBox("The active slot is empty — assign an ObstacleType asset to it above before painting.", MessageType.Warning);
    }

    private void DrawGrid(RoomTemplateSO template)
    {
        for (int y = RoomTemplateSO.RoomTileSize.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < RoomTemplateSO.RoomTileSize.x; x++)
            {
                CellState current = template.GetCell(x, y);
                var pos = new Vector2Int(x, y);
                bool isSpawnPoint = template.EnemySpawnPoints.Contains(pos);
                bool isVoidSpawn = isSpawnPoint && current == CellState.Void;

                GUI.backgroundColor = (_paintMode == PaintMode.EnemySpawnPoints && isSpawnPoint)
                    ? (isVoidSpawn ? new Color(0.25f, 0.55f, 0.95f) : Color.red) // blue = void/flying-only, red = floor
                    : ColorForState(template, x, y, current);

                string label = "";
                if (_paintMode == PaintMode.Cells && current == CellState.Obstacle)
                    label = template.GetObstacleTypeIndex(x, y).ToString();
                else if (_paintMode == PaintMode.EnemySpawnPoints && isSpawnPoint)
                    label = template.EnemySpawnPoints.IndexOf(pos).ToString();

                if (GUILayout.Button(label, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    Undo.RecordObject(template, "Edit Room Grid");

                    if (_paintMode == PaintMode.Cells)
                        HandleCellClick(template, x, y, current);
                    else
                        HandleSpawnPointClick(template, x, y, current);

                    EditorUtility.SetDirty(template);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
    }

    private void HandleCellClick(RoomTemplateSO template, int x, int y, CellState current)
    {
        switch (current)
        {
            case CellState.Void:
                template.SetCell(x, y, CellState.Floor);
                break;

            case CellState.Floor:
                // A cell can't be both a spawn point and an obstacle.
                RemoveSpawnPointIfPresent(template, x, y);
                template.SetCell(x, y, CellState.Obstacle);
                template.SetObstacleTypeIndex(x, y, _activeObstacleType);
                break;

            case CellState.Obstacle:
                template.SetCell(x, y, CellState.Void);
                template.SetObstacleTypeIndex(x, y, -1);
                break;
        }
    }

    /// <summary>Spawn points are now allowed on Floor OR Void cells — Void ones are meant for
    /// flying enemies (see DrawVoidSpawnPointWarnings, which flags a Void spawn point whose
    /// assigned prefab has no FlightComponent). Obstacle cells are still rejected — a spawn point
    /// there would either not fit or immediately conflict with the obstacle's own collider.</summary>
    private void HandleSpawnPointClick(RoomTemplateSO template, int x, int y, CellState current)
    {
        if (current == CellState.Obstacle)
        {
            Debug.LogWarning("[RoomTemplateSOEditor] Enemy spawn points can't be placed on Obstacle cells.");
            return;
        }

        var pos = new Vector2Int(x, y);
        if (template.EnemySpawnPoints.Contains(pos))
            RemoveSpawnPointIfPresent(template, x, y);
        else
            template.EnemySpawnPoints.Add(pos);
    }

    private void RemoveSpawnPointIfPresent(RoomTemplateSO template, int x, int y)
    {
        var pos = new Vector2Int(x, y);
        int index = template.EnemySpawnPoints.IndexOf(pos);
        if (index < 0) return;

        template.EnemySpawnPoints.RemoveAt(index);

        foreach (var entry in template.EnemySpawnEntries)
        {
            if (entry.SpawnPointIndex == index) entry.SpawnPointIndex = -1;
            else if (entry.SpawnPointIndex > index) entry.SpawnPointIndex--;
        }
    }

    /// <summary>Warns (doesn't block) when a Void spawn point's assigned enemy prefab has no
    /// FlightComponent — that enemy would spawn floating over the pit with no fall/hazard
    /// handling (EnemyHazardDetector for flight-aware hazard damage is still on the roadmap).
    /// Purely informational — you can still assign a grounded prefab there if you want.</summary>
    private void DrawVoidSpawnPointWarnings(RoomTemplateSO template)
    {
        var voidIndices = new List<int>();
        for (int i = 0; i < template.EnemySpawnPoints.Count; i++)
        {
            var pos = template.EnemySpawnPoints[i];
            if (template.GetCell(pos.x, pos.y) == CellState.Void)
                voidIndices.Add(i);
        }

        if (voidIndices.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Void Spawn Point Checks", EditorStyles.boldLabel);

        foreach (int index in voidIndices)
        {
            var entry = template.EnemySpawnEntries.FirstOrDefault(e => e.SpawnPointIndex == index);

            if (entry == null || entry.EnemyPrefab == null)
            {
                EditorGUILayout.HelpBox($"Spawn point {index} sits on a Void cell but has no enemy assigned yet in 'Enemy Spawn Entries' above.", MessageType.Info);
                continue;
            }

            bool canFly = entry.EnemyPrefab.GetComponent<FlightComponent>() != null;
            if (!canFly)
            {
                EditorGUILayout.HelpBox(
                    $"Spawn point {index} (Void cell) uses '{entry.EnemyPrefab.name}', which has no FlightComponent. " +
                    "It'll spawn floating over the pit with no fall handling. Add a FlightComponent (Starts Flying = true) " +
                    "to the prefab, or move this spawn point to a Floor cell.",
                    MessageType.Warning);
            }
        }
    }

    private static Color ColorForState(RoomTemplateSO template, int x, int y, CellState state)
    {
        if (state == CellState.Obstacle)
        {
            int idx = template.GetObstacleTypeIndex(x, y);
            if (idx >= 0 && idx < template.ObstacleTypes.Count && template.ObstacleTypes[idx] != null && !template.ObstacleTypes[idx].BlocksMovement)
                return new Color(0.95f, 0.4f, 0.1f); // hazard (walkable) — orange

            return new Color(0.7f, 0.35f, 0.1f); // blocking obstacle — brown
        }

        return state switch
        {
            CellState.Void  => new Color(0.25f, 0.25f, 0.25f),
            CellState.Floor => new Color(0.3f, 0.75f, 0.3f),
            _ => Color.white
        };
    }
}