#if UNITY_EDITOR
using KingCat.Base.Assets;
using UnityEditor;
using UnityEngine;

public static class CellConfigDrawer
{
    private const float SwatchSize = 32f;
    private const int SwatchesPerRow = 8;
    private const float SelectionBorderThickness = 2f;
    private const int MaxFreezeValue = 4;

    public static void Draw(CellConfig[] configs, int selectedIndex, int gridWidth, MaterialColorDatabaseSO colorDatabase)
    {
        if (configs == null || selectedIndex < 0 || selectedIndex >= configs.Length)
        {
            EditorGUILayout.HelpBox("Click a playable cell to configure it.", MessageType.Info);
            return;
        }

        int x = selectedIndex % gridWidth;
        int y = selectedIndex / gridWidth;

        EditorGUILayout.LabelField($"Cell ({x}, {y})", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        configs[selectedIndex].BulletNum = EditorGUILayout.IntField("Bullet Count", configs[selectedIndex].BulletNum);

        DrawColorSwatches(configs, selectedIndex, colorDatabase);

        PlayableCellType previousType = configs[selectedIndex].Type;
        configs[selectedIndex].Type =
            (PlayableCellType)EditorGUILayout.EnumPopup("Cell Type", configs[selectedIndex].Type);
        if (previousType != PlayableCellType.Freeze &&
            configs[selectedIndex].Type == PlayableCellType.Freeze &&
            configs[selectedIndex].FreezeValue <= 0)
        {
            configs[selectedIndex].FreezeValue = 1;
        }

        configs[selectedIndex].TimingSeconds =
            Mathf.Max(0, EditorGUILayout.IntField("Timing Seconds", configs[selectedIndex].TimingSeconds));

        if (configs[selectedIndex].Type == PlayableCellType.Link)
        {
            configs[selectedIndex].LinkGroupId =
                Mathf.Max(0, EditorGUILayout.IntField("Link Group Id", configs[selectedIndex].LinkGroupId));
        }
        else
        {
            configs[selectedIndex].LinkGroupId = -1;
        }

        if (configs[selectedIndex].Type == PlayableCellType.Freeze)
        {
            configs[selectedIndex].FreezeValue =
                Mathf.Clamp(
                    EditorGUILayout.IntField("Freeze Value", configs[selectedIndex].FreezeValue),
                    1,
                    MaxFreezeValue);
        }
        else
        {
            configs[selectedIndex].FreezeValue = 0;
        }

        if (configs[selectedIndex].Type != PlayableCellType.Stack)
        {
            configs[selectedIndex].StackItems = null;
        }

        EditorGUI.indentLevel--;
    }

    private static void DrawColorSwatches(CellConfig[] configs, int selectedIndex, MaterialColorDatabaseSO database)
    {
        EditorGUILayout.LabelField("Cell Color");

        if (database == null)
        {
            EditorGUILayout.HelpBox("Assign a Material Color Database to enable color selection.", MessageType.Info);
            return;
        }

        int count = 0;
        EditorGUILayout.BeginHorizontal();

        foreach (MaterialColorDatabaseSO.MaterialColorEntry entry in database.Entries)
        {
            if (entry == null || entry.material == null)
            {
                continue;
            }

            if (count > 0 && count % SwatchesPerRow == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            DrawSwatch(configs, selectedIndex, entry);
            count++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawSwatch(CellConfig[] configs, int selectedIndex, MaterialColorDatabaseSO.MaterialColorEntry entry)
    {
        Rect rect = GUILayoutUtility.GetRect(
            SwatchSize,
            SwatchSize,
            GUILayout.Width(SwatchSize),
            GUILayout.Height(SwatchSize));

        EditorGUI.DrawRect(rect, GetMaterialColor(entry.material));

        bool isSelected = configs[selectedIndex].CellColor == entry.colorType;
        if (isSelected)
        {
            DrawSelectionBorder(rect, SelectionBorderThickness, Color.white);
        }

        if (GUI.Button(rect, new GUIContent(string.Empty, entry.colorType.ToString()), GUIStyle.none))
        {
            configs[selectedIndex].CellColor = entry.colorType;
        }
    }

    private static void DrawSelectionBorder(Rect rect, float thickness, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    public static Color GetMaterialColor(Material material)
    {
        if (material == null)
        {
            return Color.gray;
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.color;
        }

        return Color.gray;
    }
}
#endif
