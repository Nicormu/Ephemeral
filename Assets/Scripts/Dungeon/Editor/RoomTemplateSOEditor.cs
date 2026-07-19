using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomTemplateSO))]
public class RoomTemplateSOEditor : Editor
{
    private const float CellSize = 20f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var template = (RoomTemplateSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cell Grid — click to cycle Void → Floor → Obstacle", EditorStyles.boldLabel);

        for (int y = RoomTemplateSO.RoomTileSize.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < RoomTemplateSO.RoomTileSize.x; x++)
            {
                CellState current = template.GetCell(x, y);
                GUI.backgroundColor = ColorForState(current);

                if (GUILayout.Button("", GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    Undo.RecordObject(template, "Toggle Room Cell");
                    template.SetCell(x, y, NextState(current));
                    EditorUtility.SetDirty(template);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
    }

    private static CellState NextState(CellState current) => current switch
    {
        CellState.Void     => CellState.Floor,
        CellState.Floor    => CellState.Obstacle,
        CellState.Obstacle => CellState.Void,
        _ => CellState.Void
    };

    private static Color ColorForState(CellState state) => state switch
    {
        CellState.Void     => new Color(0.25f, 0.25f, 0.25f),
        CellState.Floor    => new Color(0.3f, 0.75f, 0.3f),
        CellState.Obstacle => new Color(0.7f, 0.35f, 0.1f),
        _ => Color.white
    };
}