using System.Collections.Generic;
using KingCat.Base.Assets;
using UnityEditor;
using UnityEngine;

public partial class PixelLevelQuotaComposerWindow
{
    private const float BrushSwatchSize = 24f;
    private const int BrushSwatchesPerRow = 8;
    private const float MinJellyDisplaySize = 20f;
    private const float MaxJellyDisplaySize = 48f;

    private bool _showJellySection = true;
    private bool _showSelectedJellyCell = true;
    private bool _jellyIsPainting = true;
    private PixelCubeColor _selectedJellyBrushColor = PixelCubeColor.Red;

    private void DrawJellySection()
    {
        _showJellySection = EditorGUILayout.Foldout(_showJellySection, "Jelly Grid", true, EditorStyles.foldoutHeader);
        if (!_showJellySection)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        int newWidth = EditorGUILayout.IntSlider("Jelly Width", _jellyGridWidth, MinJellyGridSize, MaxJellyGridWidth);
        int newHeight = EditorGUILayout.IntSlider("Jelly Height", _jellyGridHeight, MinJellyGridSize, MaxJellyGridHeight);
        _jellyDisplaySize = EditorGUILayout.Slider("Jelly Display Size", _jellyDisplaySize, MinJellyDisplaySize, MaxJellyDisplaySize);
        _jellyCellSize = EditorGUILayout.Slider("Jelly Cell Size", _jellyCellSize, 0.2f, 5f);

        if (EditorGUI.EndChangeCheck())
        {
            newWidth = Mathf.Clamp(newWidth, MinJellyGridSize, MaxJellyGridWidth);
            newHeight = Mathf.Clamp(newHeight, MinJellyGridSize, MaxJellyGridHeight);
            _jellyCellSize = Mathf.Max(0.01f, _jellyCellSize);
            _jellyDisplaySize = Mathf.Clamp(_jellyDisplaySize, MinJellyDisplaySize, MaxJellyDisplaySize);
            ResizeJellyGrid(newWidth, newHeight);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Initialize Jelly Grid"))
        {
            InitializeJellyGrid();
        }

        if (GUILayout.Button("Clear Jelly Grid"))
        {
            InitializeJellyGrid();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _jellyIsPainting = GUILayout.Toggle(_jellyIsPainting, "Paint", "Button");
        _jellyIsPainting = !GUILayout.Toggle(!_jellyIsPainting, "Erase", "Button");
        if (GUILayout.Button("Load Jelly JSON"))
        {
            LoadJellyJson();
        }
        if (GUILayout.Button("Save Jelly JSON Draft"))
        {
            SaveJellyJsonDraft();
        }
        EditorGUILayout.EndHorizontal();

        DrawJellyBrushPicker();
        HandleJellyBrushHotkeys();

        DrawJellyGrid();
        EditorGUILayout.Space(6f);
        DrawSelectedJellyCellSection();
    }

    private void DrawJellyBrushPicker()
    {
        EditorGUILayout.LabelField("Jelly Brush Color");
        EditorGUILayout.HelpBox(
            "Paint assigns this color and 1 bullet (clamped to remaining quota). Total bullets per color must match Shared Color Quotas — same rule as Top cube count vs Target.",
            MessageType.None);

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

            bool hasMapping = _colorDatabase != null && TryGetPrimaryColorTypeForBrush(color, out _);
            EditorGUI.DrawRect(swatchRect, hasMapping ? GetDisplayColor(color) : new Color(0.25f, 0.25f, 0.25f));
            if (_selectedJellyBrushColor == color)
            {
                DrawSelectionBorder(swatchRect, 2f, Color.white);
            }

            if (GUI.Button(swatchRect, new GUIContent(string.Empty, $"{color} (Tab to cycle)"), GUIStyle.none))
            {
                _selectedJellyBrushColor = color;
            }

            GUI.Label(countRect, hasMapping ? GetJellyRemaining(color).ToString() : "—", countStyle);
            count++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void HandleJellyBrushHotkeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
        {
            return;
        }

        if (e.keyCode == KeyCode.Tab)
        {
            int idx = System.Array.IndexOf(ManagedColors, _selectedJellyBrushColor);
            if (idx < 0)
            {
                idx = 0;
            }

            idx = e.shift ? (idx + ManagedColors.Length - 1) % ManagedColors.Length : (idx + 1) % ManagedColors.Length;
            _selectedJellyBrushColor = ManagedColors[idx];
            e.Use();
            Repaint();
        }
    }

    private void DrawJellyGrid()
    {
        if (_jellyCellConfigs == null || _jellyCellConfigs.Length != _jellyGridWidth * _jellyGridHeight)
        {
            EditorGUILayout.HelpBox("Initialize the Jelly grid to start editing.", MessageType.Info);
            return;
        }

        _jellyCellRects.Clear();
        GUIStyle badgeStyle = BuildBadgeStyle();
        Dictionary<int, int> helperOwners = ComputeStackHelperOwners();
        for (int y = _jellyGridHeight - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int x = 0; x < _jellyGridWidth; x++)
            {
                int index = y * _jellyGridWidth + x;
                CellConfig config = _jellyCellConfigs[index];
                bool isSelected = _selectedJellyCellIndex == index;
                bool isHelper = helperOwners.ContainsKey(index);
                Color background = GetJellyCellBackgroundColor(config, isSelected);
                if (isHelper)
                {
                    background = Color.Lerp(background, new Color(0.95f, 0.75f, 0.2f), 0.55f);
                }

                Color previousBackground = GUI.backgroundColor;
                Color previousContent = GUI.contentColor;
                GUI.backgroundColor = background;
                GUI.contentColor = GetContrastColor(background);

                string label = config.IsPlayable ? GetJellyCellLabel(config) : string.Empty;
                if (GUILayout.Button(label, GUILayout.Width(_jellyDisplaySize), GUILayout.Height(_jellyDisplaySize)))
                {
                    OnJellyCellClicked(index);
                }
                _jellyCellRects[index] = GUILayoutUtility.GetLastRect();

                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;

                if (Event.current.type == EventType.Repaint)
                {
                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    if (config.IsPlayable && config.Type != PlayableCellType.Normal)
                    {
                        DrawCellTypeBadge(lastRect, config.Type, badgeStyle);
                    }

                    if (isHelper)
                    {
                        DrawHelperCellBadge(lastRect, badgeStyle);
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        DrawJellyLinkOverlay();
    }

    private static void DrawHelperCellBadge(Rect cellRect, GUIStyle badgeStyle)
    {
        const float badgeSize = 14f;
        const float padding = 2f;
        Rect badgeRect = new Rect(cellRect.xMax - badgeSize - padding, cellRect.y + padding, badgeSize, badgeSize);
        EditorGUI.DrawRect(badgeRect, new Color(0.6f, 0.4f, 0f));

        Color previous = GUI.contentColor;
        GUI.contentColor = Color.white;
        GUI.Label(badgeRect, "H", badgeStyle);
        GUI.contentColor = previous;
    }

    private void DrawSelectedJellyCellSection()
    {
        _showSelectedJellyCell = EditorGUILayout.Foldout(_showSelectedJellyCell, "Selected Jelly Cell", true, EditorStyles.foldoutHeader);
        if (!_showSelectedJellyCell)
        {
            return;
        }

        if (!HasValidSelectedJellyCell())
        {
            EditorGUILayout.HelpBox("Click a playable Jelly cell to configure it.", MessageType.Info);
            return;
        }

        int x = _selectedJellyCellIndex % _jellyGridWidth;
        int y = _selectedJellyCellIndex / _jellyGridWidth;
        CellConfig config = _jellyCellConfigs[_selectedJellyCellIndex];

        EditorGUILayout.LabelField($"Cell ({x}, {y})", EditorStyles.boldLabel);

        bool isStackCell = config.Type == PlayableCellType.Stack;
        if (!isStackCell && TryMapColorTypeToPixelColor(config.CellColor, out PixelCubeColor mappedColor))
        {
            int maxAllowed = GetMaxBulletCountForCell(_selectedJellyCellIndex, config.CellColor);
            EditorGUILayout.HelpBox(
                $"Quota Color: {mappedColor} | Current Ammo: {config.BulletNum} | Max Allowed For This Cell: {maxAllowed}",
                MessageType.None);
        }
        else if (!isStackCell)
        {
            EditorGUILayout.HelpBox("Pick a quota-supported color before entering ammo.", MessageType.Warning);
        }

        if (!isStackCell)
        {
            int newBulletCount = EditorGUILayout.IntField("Bullet Count", config.BulletNum);
            config.BulletNum = ClampBulletCountForCell(_selectedJellyCellIndex, config.CellColor, newBulletCount);

            DrawJellyColorPicker(ref config);
        }

        PlayableCellType previousType = config.Type;
        int previousHelperDx = config.HelperDx;
        int previousHelperDy = config.HelperDy;
        config.Type = (PlayableCellType)EditorGUILayout.EnumPopup("Cell Type", config.Type);
        if (previousType != PlayableCellType.Freeze &&
            config.Type == PlayableCellType.Freeze &&
            config.FreezeValue <= 0)
        {
            config.FreezeValue = 1;
        }

        config.TimingSeconds = Mathf.Max(0, EditorGUILayout.IntField("Timing Seconds", config.TimingSeconds));
        if (previousType != PlayableCellType.Stack && config.Type == PlayableCellType.Stack)
        {
            EnsureStackItemsInitialized(ref config, _selectedJellyCellIndex);
            if (TryPickDefaultHelperDirection(_selectedJellyCellIndex, out int dx, out int dy))
            {
                config.HelperDx = dx;
                config.HelperDy = dy;
                int ax0 = _selectedJellyCellIndex % _jellyGridWidth;
                int ay0 = _selectedJellyCellIndex / _jellyGridWidth;
                ReserveHelperCell((ay0 + dy) * _jellyGridWidth + (ax0 + dx));
            }
            else
            {
                config.HelperDx = 0;
                config.HelperDy = 0;
            }
        }
        else if (previousType == PlayableCellType.Stack && config.Type != PlayableCellType.Stack)
        {
            int ax0 = _selectedJellyCellIndex % _jellyGridWidth;
            int ay0 = _selectedJellyCellIndex / _jellyGridWidth;
            int hx = ax0 + previousHelperDx;
            int hy = ay0 + previousHelperDy;
            if (Mathf.Abs(previousHelperDx) + Mathf.Abs(previousHelperDy) == 1 &&
                hx >= 0 && hx < _jellyGridWidth && hy >= 0 && hy < _jellyGridHeight)
            {
                FreeHelperCell(hy * _jellyGridWidth + hx);
            }
            config.HelperDx = 0;
            config.HelperDy = 0;
        }

        if (config.Type == PlayableCellType.Link)
        {
            config.LinkGroupId = Mathf.Max(0, EditorGUILayout.IntField("Link Group Id", config.LinkGroupId));
        }
        else
        {
            config.LinkGroupId = -1;
        }

        if (config.Type == PlayableCellType.Freeze)
        {
            config.FreezeValue = Mathf.Clamp(
                EditorGUILayout.IntField("Freeze Value", config.FreezeValue),
                1,
                MaxFreezeValue);
        }
        else
        {
            config.FreezeValue = 0;
        }

        if (config.Type == PlayableCellType.Stack)
        {
            DrawStackItemsSection(ref config);
            DrawHelperDirectionPicker(ref config);
        }
        else
        {
            config.StackItems = null;
        }

        _jellyCellConfigs[_selectedJellyCellIndex] = config;
    }

    private void DrawHelperDirectionPicker(ref CellConfig config)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Helper Cell Direction", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Direction (from this anchor) of the cell where the stack spawns the next normal jelly block. The helper cell is reserved and cannot be edited directly.",
            MessageType.None);

        Dictionary<int, int> owners = ComputeStackHelperOwners();

        int[] dirsDx = { 0, 0, -1, 1 };
        int[] dirsDy = { 1, -1, 0, 0 };
        string[] dirsLabel = { "Up", "Down", "Left", "Right" };

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < dirsDx.Length; i++)
        {
            int dx = dirsDx[i];
            int dy = dirsDy[i];
            bool isCurrent = config.HelperDx == dx && config.HelperDy == dy;
            bool isValid = IsHelperDirectionValid(_selectedJellyCellIndex, dx, dy, owners);

            Color prevBg = GUI.backgroundColor;
            if (isCurrent)
            {
                GUI.backgroundColor = new Color(0.4f, 0.85f, 0.45f);
            }

            EditorGUI.BeginDisabledGroup(!isValid && !isCurrent);
            if (GUILayout.Button(dirsLabel[i]))
            {
                int ax = _selectedJellyCellIndex % _jellyGridWidth;
                int ay = _selectedJellyCellIndex / _jellyGridWidth;

                int oldHx = ax + config.HelperDx;
                int oldHy = ay + config.HelperDy;
                if (Mathf.Abs(config.HelperDx) + Mathf.Abs(config.HelperDy) == 1 &&
                    oldHx >= 0 && oldHx < _jellyGridWidth && oldHy >= 0 && oldHy < _jellyGridHeight)
                {
                    FreeHelperCell(oldHy * _jellyGridWidth + oldHx);
                }

                config.HelperDx = dx;
                config.HelperDy = dy;
                ReserveHelperCell((ay + dy) * _jellyGridWidth + (ax + dx));
            }
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();

        bool currentValid = IsHelperDirectionValid(_selectedJellyCellIndex, config.HelperDx, config.HelperDy, owners);
        if (!currentValid)
        {
            EditorGUILayout.HelpBox(
                "This Stack has no valid helper neighbor. Pick a direction (in-grid, not another Stack, not another anchor's helper).",
                MessageType.Warning);
        }
    }

    private void DrawJellyColorPicker(ref CellConfig config)
    {
        EditorGUILayout.LabelField("Cell Color");

        if (_colorDatabase == null)
        {
            EditorGUILayout.HelpBox("Assign a Material Color Database to enable color selection.", MessageType.Info);
            return;
        }

        GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };

        int count = 0;
        EditorGUILayout.BeginHorizontal();

        foreach (MaterialColorDatabaseSO.MaterialColorEntry entry in _colorDatabase.Entries)
        {
            if (entry == null || entry.material == null)
            {
                continue;
            }

            if (!TryMapColorTypeToPixelColor(entry.colorType, out PixelCubeColor mappedColor))
            {
                continue;
            }

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

            EditorGUI.DrawRect(swatchRect, CellConfigDrawer.GetMaterialColor(entry.material));
            if (config.CellColor == entry.colorType)
            {
                DrawSelectionBorder(swatchRect, 2f, Color.white);
            }

            if (GUI.Button(swatchRect, new GUIContent(string.Empty, $"{entry.colorType} -> {mappedColor}"), GUIStyle.none))
            {
                config.CellColor = entry.colorType;
                config.BulletNum = ClampBulletCountForCell(_selectedJellyCellIndex, config.CellColor, config.BulletNum);
            }

            GUI.Label(countRect, GetJellyRemaining(mappedColor).ToString(), countStyle);
            count++;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("The number below each swatch is the remaining Jelly ammo quota for that mapped shooter color.", MessageType.None);
    }

    private void DrawStackItemsSection(ref CellConfig config)
    {
        EnsureStackItemsInitialized(ref config, _selectedJellyCellIndex);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Stack Items", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Stack order is top-to-bottom: item 1 spawns first, then item 2, then item 3...", MessageType.None);

        for (int i = 0; i < config.StackItems.Length; i++)
        {
            JellyStackItem item = config.StackItems[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28f));
            item.CellColor = DrawStackItemColorPopup(item.CellColor);
            item.BulletNum = Mathf.Max(0, EditorGUILayout.IntField(item.BulletNum, GUILayout.Width(60f)));

            bool moveUp = GUILayout.Button("Up", GUILayout.Width(36f));
            bool moveDown = GUILayout.Button("Down", GUILayout.Width(48f));
            bool remove = GUILayout.Button("-", GUILayout.Width(24f));
            EditorGUILayout.EndHorizontal();

            config.StackItems[i] = item;

            if (moveUp && i > 0)
            {
                SwapStackItems(config.StackItems, i, i - 1);
            }

            if (moveDown && i < config.StackItems.Length - 1)
            {
                SwapStackItems(config.StackItems, i, i + 1);
            }

            if (remove)
            {
                RemoveStackItem(ref config, i);
                break;
            }
        }

        if (GUILayout.Button("Add Stack Item"))
        {
            AddStackItem(ref config);
        }

        config.BulletNum = GetStackTotalBulletCount(config);
        if (config.StackItems.Length > 0)
        {
            config.CellColor = config.StackItems[0].CellColor;
        }

        EditorGUILayout.LabelField($"Total Stack Ammo: {config.BulletNum}");
    }

    private ColorType DrawStackItemColorPopup(ColorType currentColor)
    {
        List<ColorType> colorTypes = new List<ColorType>();
        List<string> labels = new List<string>();
        int selectedIndex = -1;

        for (int i = 0; i < ManagedColors.Length; i++)
        {
            PixelCubeColor pixelColor = ManagedColors[i];
            if (!TryGetPrimaryColorTypeForBrush(pixelColor, out ColorType colorType))
            {
                continue;
            }

            colorTypes.Add(colorType);
            labels.Add(pixelColor.ToString());
            if (colorType == currentColor)
            {
                selectedIndex = colorTypes.Count - 1;
            }
        }

        if (colorTypes.Count == 0)
        {
            return (ColorType)EditorGUILayout.EnumPopup(currentColor);
        }

        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        int nextIndex = EditorGUILayout.Popup(selectedIndex, labels.ToArray());
        return colorTypes[Mathf.Clamp(nextIndex, 0, colorTypes.Count - 1)];
    }

    private void DrawJellyLinkOverlay()
    {
        if (Event.current.type != EventType.Repaint || _jellyCellRects.Count == 0 || _jellyCellConfigs == null)
        {
            return;
        }

        Dictionary<int, List<int>> groups = BuildLinkGroups();
        if (groups.Count == 0)
        {
            return;
        }

        GUIStyle idStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        idStyle.normal.textColor = Color.white;

        Handles.BeginGUI();
        foreach (KeyValuePair<int, List<int>> group in groups)
        {
            int groupId = group.Key;
            List<int> cells = group.Value;
            if (cells.Count <= 1)
            {
                continue;
            }

            Handles.color = GetLinkGroupDisplayColor(groupId);
            if (cells.Count == 2)
            {
                DrawLinkBetweenCells(cells[0], cells[1]);
            }
            else
            {
                int root = cells[0];
                for (int i = 1; i < cells.Count; i++)
                {
                    DrawLinkBetweenCells(root, cells[i]);
                }
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (!_jellyCellRects.TryGetValue(cells[i], out Rect rect))
                {
                    continue;
                }

                Rect badgeRect = new Rect(rect.xMax - 16f, rect.yMax - 14f, 14f, 12f);
                EditorGUI.DrawRect(badgeRect, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(badgeRect, groupId.ToString(), idStyle);
            }
        }

        Handles.EndGUI();
    }

    private void DrawLinkBetweenCells(int indexA, int indexB)
    {
        if (!_jellyCellRects.TryGetValue(indexA, out Rect rectA) || !_jellyCellRects.TryGetValue(indexB, out Rect rectB))
        {
            return;
        }

        Vector3 a = new Vector3(rectA.center.x, rectA.center.y, 0f);
        Vector3 b = new Vector3(rectB.center.x, rectB.center.y, 0f);
        Handles.DrawAAPolyLine(2f, a, b);
    }

    private static Color GetLinkGroupDisplayColor(int groupId)
    {
        float hue = Mathf.Repeat(groupId * 0.173f, 1f);
        Color color = Color.HSVToRGB(hue, 0.8f, 1f);
        color.a = 0.95f;
        return color;
    }

    private Color GetJellyCellBackgroundColor(CellConfig config, bool isSelected)
    {
        if (!config.IsPlayable)
        {
            return new Color(0.15f, 0.15f, 0.25f);
        }

        Color background = GetDisplayColorFromColorType(config.CellColor);
        if (isSelected)
        {
            background = Color.Lerp(background, Color.white, 0.35f);
        }

        return background;
    }

    private static string GetJellyCellLabel(CellConfig config)
    {
        int bulletCount = config.Type == PlayableCellType.Stack
            ? GetStackTotalBulletCount(config)
            : Mathf.Max(0, config.BulletNum);

        int timingSeconds = Mathf.Max(0, config.TimingSeconds);
        return timingSeconds > 0
            ? $"{bulletCount}\n{timingSeconds}s"
            : bulletCount.ToString();
    }

    private Color GetDisplayColorFromColorType(ColorType colorType)
    {
        if (_colorDatabase != null)
        {
            foreach (MaterialColorDatabaseSO.MaterialColorEntry entry in _colorDatabase.Entries)
            {
                if (entry != null && entry.colorType == colorType && entry.material != null)
                {
                    return CellConfigDrawer.GetMaterialColor(entry.material);
                }
            }
        }

        return new Color(0.3f, 0.4f, 0.8f);
    }

    private static void DrawCellTypeBadge(Rect cellRect, PlayableCellType type, GUIStyle badgeStyle)
    {
        const float badgeSize = 14f;
        const float padding = 2f;
        Rect badgeRect = new Rect(cellRect.x + padding, cellRect.y + padding, badgeSize, badgeSize);
        EditorGUI.DrawRect(badgeRect, new Color(0.15f, 0.15f, 0.15f));

        Color previous = GUI.contentColor;
        GUI.contentColor = Color.white;
        GUI.Label(badgeRect, GetCellTypeBadgeLabel(type), badgeStyle);
        GUI.contentColor = previous;
    }

    private static string GetCellTypeBadgeLabel(PlayableCellType type)
    {
        switch (type)
        {
            case PlayableCellType.Hidden: return "H";
            case PlayableCellType.Link: return "L";
            case PlayableCellType.Freeze: return "F";
            case PlayableCellType.Locked: return "L";
            case PlayableCellType.Stack: return "S";
            default: return string.Empty;
        }
    }
}
