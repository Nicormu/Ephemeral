using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomTemplateSO))]
public class RoomTemplateSOEditor : Editor
{
    private const float CellSize = 20f;

    // Which obstacle asset gets assigned when you click a cell into the Obstacle state.
    private ObstacleType _paintObstacle;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var template = (RoomTemplateSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cell Grid — click to cycle Void → Floor → Obstacle", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Shift-click an Obstacle cell to repaint its obstacle type without cycling.", EditorStyles.miniLabel);

        _paintObstacle = (ObstacleType)EditorGUILayout.ObjectField("Obstacle To Paint", _paintObstacle, typeof(ObstacleType), false);

        EditorGUILayout.Space();

        for (int y = RoomTemplateSO.RoomTileSize.y - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < RoomTemplateSO.RoomTileSize.x; x++)
            {
                CellState current = template.GetCell(x, y);
                ObstacleType currentObstacle = template.GetObstacle(x, y);
                GUI.backgroundColor = ColorForState(current);

                string label = (current == CellState.Obstacle && currentObstacle != null)
                    ? currentObstacle.name.Substring(0, Mathf.Min(3, currentObstacle.name.Length))
                    : "";

                if (GUILayout.Button(label, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    Undo.RecordObject(template, "Edit Room Cell");

                    bool repaintOnly = Event.current != null && Event.current.shift && current == CellState.Obstacle;

                    if (repaintOnly)
                    {
                        template.SetCell(x, y, CellState.Obstacle, _paintObstacle);
                    }
                    else
                    {
                        CellState next = NextState(current);
                        ObstacleType obstacleForCell = next == CellState.Obstacle ? _paintObstacle : null;
                        template.SetCell(x, y, next, obstacleForCell);
                    }

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