using UnityEditor;
using UnityEngine;

public partial class PixelLevelQuotaComposerWindow
{
    private bool _showTopSection = true;

    private void DrawTopSection()
    {
        _showTopSection = EditorGUILayout.Foldout(_showTopSection, "Top Cubes", true, EditorStyles.foldoutHeader);
        if (!_showTopSection)
        {
            return;
        }

        if (_topGridWidth != FixedTopGridWidth)
        {
            ResizeTopGrid(FixedTopGridWidth, _topGridHeight);
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Top Grid Width", FixedTopGridWidth.ToString());
        _topGridHeight = EditorGUILayout.IntSlider("Top Grid Height", _topGridHeight, MinTopGridSize, MaxTopGridHeight);
        _cubeSize = EditorGUILayout.Slider("Cube Size", _cubeSize, 0.1f, 3f);
        _cubeSpacing = EditorGUILayout.Slider("Cube Spacing", _cubeSpacing, 0f, 1f);
        _selectedTopTimingSeconds = Mathf.Max(0, EditorGUILayout.IntField("Timing Seconds", _selectedTopTimingSeconds));
        EditorGUILayout.HelpBox(
            "Top cubes are placed on a strict integer lattice (GridPosition × (cube size + spacing)). " +
            "A* manual grid bounds are generated from pixel cells so path nodes align with PixelCubeCell.",
            MessageType.None);
        _centerPivot = EditorGUILayout.Toggle("Center Pivot", _centerPivot);
        _topCellPrefab = (GameObject)EditorGUILayout.ObjectField("Block Prefab (optional)", _topCellPrefab, typeof(GameObject), false);
        if (_topCellPrefab != null)
        {
            EditorGUILayout.HelpBox("Block Prefab is assigned, so generated top cells will use the prefab.", MessageType.Info);
        }
        _keyPrefab = (KeyPickup)EditorGUILayout.ObjectField("Key Prefab (for 2x2 key markers)", _keyPrefab, typeof(KeyPickup), false);

        if (EditorGUI.EndChangeCheck())
        {
            _topGridWidth = FixedTopGridWidth;
            _topGridHeight = Mathf.Clamp(_topGridHeight, MinTopGridSize, MaxTopGridHeight);
            _cubeSize = Mathf.Max(0.01f, _cubeSize);
            _cubeSpacing = Mathf.Max(0f, _cubeSpacing);
            ResizeTopGrid(_topGridWidth, _topGridHeight);
        }

        DrawTopBrushPicker();
        DrawTopToolbar();
        DrawTopGrid();
    }

    private void DrawTopBrushPicker()
    {
        EditorGUILayout.LabelField("Top Brush Color");

        GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };

        int count = 0;
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < ManagedColors.Length; i++)
        {
            PixelCubeColor color = ManagedColors[i];
            if (count > 0 && count % BrushSwatchesPerRow == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            Rect totalRect = GUILayoutUtility.GetRect(
                BrushSwatchSize,
                BrushSwatchSize + 14f,
                GUILayout.Width(BrushSwatchSize),
                GUILayout.Height(BrushSwatchSize + 14f));

            Rect swatchRect = new Rect(totalRect.x, totalRect.y, BrushSwatchSize, BrushSwatchSize);
            Rect countRect = new Rect(totalRect.x, totalRect.y + BrushSwatchSize + 1f, BrushSwatchSize, 12f);

            EditorGUI.DrawRect(swatchRect, GetDisplayColor(color));
            if (_selectedTopBrushColor == color)
            {
                DrawSelectionBorder(swatchRect, 2f, Color.white);
            }

            if (GUI.Button(swatchRect, new GUIContent(string.Empty, color.ToString()), GUIStyle.none))
            {
                _selectedTopBrushColor = color;
            }

            GUI.Label(countRect, GetTopRemaining(color).ToString(), countStyle);
            count++;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("The number below each swatch is the remaining top-cube quota for that color.", MessageType.None);
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Toggle(_topEditTool == TopEditTool.Paint, "Paint", "Button"))
        {
            _topEditTool = TopEditTool.Paint;
        }
        if (GUILayout.Toggle(_topEditTool == TopEditTool.Erase, "Erase", "Button"))
        {
            _topEditTool = TopEditTool.Erase;
        }
        if (GUILayout.Toggle(_topEditTool == TopEditTool.Key, "Key", "Button"))
        {
            _topEditTool = TopEditTool.Key;
        }

        if (GUILayout.Button("Fill Empty"))
        {
            FillTopEmptyCellsWithSelectedColor();
        }

        if (GUILayout.Button("Clear"))
        {
            ClearTopGrid();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopGrid()
    {
        float drawSize = Mathf.Clamp((position.width - 60f) / Mathf.Max(_topGridWidth, _topGridHeight), 12f, 28f);
        Rect gridRect = EditorGUILayout.GetControlRect(false, _topGridHeight * drawSize + 6f);
        Event currentEvent = Event.current;

        Handles.BeginGUI();
        for (int y = 0; y < _topGridHeight; y++)
        {
            for (int x = 0; x < _topGridWidth; x++)
            {
                Rect cellRect = new Rect(
                    gridRect.x + 2f + x * drawSize,
                    gridRect.y + 2f + (_topGridHeight - 1 - y) * drawSize,
                    drawSize - 2f,
                    drawSize - 2f);

                PixelCubeColor color = _topCellColors[x, y];
                EditorGUI.DrawRect(cellRect, color == PixelCubeColor.None ? new Color(0.18f, 0.18f, 0.18f) : GetDisplayColor(color));
                int timingSeconds = _topCellTimingSeconds != null ? Mathf.Max(0, _topCellTimingSeconds[x, y]) : 0;
                if (color != PixelCubeColor.None && timingSeconds > 0)
                {
                    GUIStyle timingStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        alignment = TextAnchor.LowerRight,
                    };
                    timingStyle.normal.textColor = Color.white;
                    GUI.Label(cellRect, $"{timingSeconds}s", timingStyle);
                }

                Handles.color = new Color(0f, 0f, 0f, 0.25f);
                Handles.DrawAAPolyLine(
                    1.5f,
                    new Vector3(cellRect.xMin, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMin));

                bool clickedOnCell =
                    (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag) &&
                    currentEvent.button == 0 &&
                    cellRect.Contains(currentEvent.mousePosition);

                if (!clickedOnCell)
                {
                    continue;
                }

                switch (_topEditTool)
                {
                    case TopEditTool.Paint:
                        TryPaintTopCell(x, y, _selectedTopBrushColor);
                        break;
                    case TopEditTool.Erase:
                        TryPaintTopCell(x, y, PixelCubeColor.None);
                        break;
                    case TopEditTool.Key:
                        TryToggleTopCellKey(x, y);
                        break;
                }
                currentEvent.Use();
            }
        }

        if (_topCellIsKey != null)
        {
            GUIStyle keyStyle = new GUIStyle(EditorStyles.boldLabel);
            keyStyle.alignment = TextAnchor.MiddleCenter;
            keyStyle.normal.textColor = Color.white;
            keyStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(drawSize * 1.4f));

            for (int y = 0; y < _topGridHeight; y++)
            {
                for (int x = 0; x < _topGridWidth; x++)
                {
                    if (!_topCellIsKey[x, y])
                    {
                        continue;
                    }

                    float left = gridRect.x + 2f + x * drawSize;
                    float top = gridRect.y + 2f + (_topGridHeight - 1 - (y + 1)) * drawSize;
                    float badgeW = 2f * drawSize - 2f;
                    float badgeH = 2f * drawSize - 2f;
                    Rect keyRect = new Rect(left, top, badgeW, badgeH);
                    GUI.Label(keyRect, "K", keyStyle);
                }
            }
        }

        Handles.EndGUI();
    }
}
