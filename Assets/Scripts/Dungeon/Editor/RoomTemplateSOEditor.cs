using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomTemplateSO))]
public class RoomTemplateSOEditor : Editor
{
    private const float CellSize = 20f;

    private enum PaintMode { Cells, EnemySpawnPoints }
    private PaintMode _paintMode = PaintMode.Cells;
    private int _activeObstacleType = 0;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

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
            EditorGUILayout.LabelField("Click a Floor cell to add/remove an enemy spawn point.", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();
        DrawGrid(template);
    }

    private void DrawObstaclePalette(RoomTemplateSO template)
    {
        if (template.ObstacleTypes == null || template.ObstacleTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("Add at least one entry to 'Obstacle Types' above to paint obstacles. Set 'Blocks Movement' off + a Damage value to make one act like fire.", MessageType.Info);
            _activeObstacleType = 0;
            return;
        }

        var labels = new string[template.ObstacleTypes.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            var def = template.ObstacleTypes[i];
            string suffix = def.BlocksMovement ? "" : $" (hazard, {def.Damage} dmg)";
            labels[i] = $"{i}: {def.Name}{suffix}";
        }

        _activeObstacleType = Mathf.Clamp(_activeObstacleType, 0, labels.Length - 1);
        _activeObstacleType = EditorGUILayout.Popup("Active Obstacle Type", _activeObstacleType, labels);
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

                GUI.backgroundColor = (_paintMode == PaintMode.EnemySpawnPoints && isSpawnPoint)
                    ? Color.red
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

    private void HandleSpawnPointClick(RoomTemplateSO template, int x, int y, CellState current)
    {
        if (current != CellState.Floor)
        {
            Debug.LogWarning("[RoomTemplateSOEditor] Enemy spawn points can only be placed on Floor cells.");
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

    private static Color ColorForState(RoomTemplateSO template, int x, int y, CellState state)
    {
        if (state == CellState.Obstacle)
        {
            int idx = template.GetObstacleTypeIndex(x, y);
            if (idx >= 0 && idx < template.ObstacleTypes.Count && !template.ObstacleTypes[idx].BlocksMovement)
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