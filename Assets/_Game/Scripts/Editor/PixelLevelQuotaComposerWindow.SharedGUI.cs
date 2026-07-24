using System.Collections.Generic;
using KingCat.Base.Assets;
using UnityEditor;
using UnityEngine;

public partial class PixelLevelQuotaComposerWindow
{
    private bool showQuotaSection = true;

    private void DrawQuotaSection()
    {
        showQuotaSection = EditorGUILayout.Foldout(showQuotaSection, "Shared Color Quotas", true, EditorStyles.foldoutHeader);
        if (!showQuotaSection)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Each color target is shared by both flows. Top cube count and total Jelly ammo for that color must both match the target before export.",
            MessageType.Info);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawQuotaHeader();

        for (int i = 0; i < _colorQuotas.Length; i++)
        {
            PixelCubeColor color = _colorQuotas[i].Color;
            int topUsed = CountTopCellsForColor(color);
            int jellyUsed = CountJellyAmmoForColor(color);
            int target = _colorQuotas[i].TargetCount;

            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch(color, GUILayout.Width(18f), GUILayout.Height(18f));
            GUILayout.Label(color.ToString(), GUILayout.Width(70f));

            int newTarget = EditorGUILayout.IntField(target, GUILayout.Width(70f));
            _colorQuotas[i].TargetCount = Mathf.Max(0, newTarget);

            GUILayout.Label(topUsed.ToString(), GUILayout.Width(60f));
            DrawDeltaLabel(_colorQuotas[i].TargetCount - topUsed, GUILayout.Width(70f));
            GUILayout.Label(jellyUsed.ToString(), GUILayout.Width(70f));
            DrawDeltaLabel(_colorQuotas[i].TargetCount - jellyUsed, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawQuotaHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(94f);
        GUILayout.Label("Target", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Top", EditorStyles.miniBoldLabel, GUILayout.Width(60f));
        GUILayout.Label("Top Left", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Jelly", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Jelly Left", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawDeltaLabel(int value, params GUILayoutOption[] options)
    {
        Color previous = GUI.color;
        GUI.color = value == 0 ? new Color(0.45f, 0.9f, 0.45f) : (value > 0 ? new Color(0.95f, 0.82f, 0.35f) : new Color(1f, 0.45f, 0.45f));
        GUILayout.Label(value.ToString(), EditorStyles.miniBoldLabel, options);
        GUI.color = previous;
    }

    private void DrawValidationSection()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool isValid = true;
        List<string> issues = new List<string>();

        for (int i = 0; i < ManagedColors.Length; i++)
        {
            PixelCubeColor color = ManagedColors[i];
            int target = GetTargetCount(color);
            int topUsed = CountTopCellsForColor(color);
            int jellyUsed = CountJellyAmmoForColor(color);

            EditorGUILayout.BeginHorizontal();
            DrawColorSwatch(color, GUILayout.Width(18f), GUILayout.Height(18f));
            GUILayout.Label(color.ToString(), GUILayout.Width(70f));
            GUILayout.Label($"Target: {target}", GUILayout.Width(90f));
            GUILayout.Label($"Top: {topUsed}", GUILayout.Width(80f));
            GUILayout.Label($"Jelly: {jellyUsed}", GUILayout.Width(90f));

            bool rowValid = topUsed == target && jellyUsed == target;
            Color previous = GUI.color;
            GUI.color = rowValid ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 0.5f, 0.4f);
            GUILayout.Label(rowValid ? "OK" : "Mismatch", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            GUI.color = previous;
            EditorGUILayout.EndHorizontal();

            if (!rowValid)
            {
                isValid = false;
                issues.Add($"{color}: target {target}, top {topUsed}, jelly {jellyUsed}");
            }
        }

        if (!isValid)
        {
            EditorGUILayout.HelpBox("Quota mismatch:\n" + string.Join("\n", issues), MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        int keyCount = CountKeyAnchors();
        int lockCount = CountLockedJellyCells();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Keys vs Locks", GUILayout.Width(110f));
        GUILayout.Label($"Keys: {keyCount}", GUILayout.Width(90f));
        GUILayout.Label($"Locked Jellies: {lockCount}", GUILayout.Width(150f));
        bool keysValid = keyCount <= lockCount;
        Color keyPrevColor = GUI.color;
        GUI.color = keysValid ? new Color(0.45f, 0.9f, 0.45f) : new Color(1f, 0.5f, 0.4f);
        GUILayout.Label(keysValid ? "OK" : "Mismatch", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUI.color = keyPrevColor;
        EditorGUILayout.EndHorizontal();

        if (!keysValid)
        {
            int missing = keyCount - lockCount;
            EditorGUILayout.HelpBox($"Keys ({keyCount}) exceed Locked Jelly cells ({lockCount}). {missing} key(s) will stay pending forever. Add {missing} more Locked jelly cell(s) or remove excess keys.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        List<string> linkIssues = CollectLinkValidationIssues();
        if (linkIssues.Count > 0)
        {
            EditorGUILayout.HelpBox("Link mismatch:\n" + string.Join("\n", linkIssues), MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("All colors and link groups are valid. The package is ready to export.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBottomActions()
    {
        DrawOutputSettings();

        bool canGenerate = CanGenerateLevelPackage(out string validationMessage);
        using (new EditorGUI.DisabledScope(!canGenerate))
        {
            if (GUILayout.Button("Generate Level Package", GUILayout.Height(34f)))
            {
                GenerateLevelPackage();
            }
        }

        DrawToolPackExportAction();

        if (!canGenerate)
        {
            EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
        }
    }

    private static void DrawToolPackExportAction()
    {
        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Export This Tool As Pack", GUILayout.Height(26f)))
        {
            PixelLevelQuotaComposerPackExporter.ExportPack();
        }
    }

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _prefabName = EditorGUILayout.TextField("Block Prefab Name", _prefabName);
        _saveFolder = EditorGUILayout.TextField("Save Folder", _saveFolder);
        _generateAsLevelPrefab = EditorGUILayout.Toggle("Generate As Level", _generateAsLevelPrefab);

        using (new EditorGUI.DisabledScope(!_generateAsLevelPrefab))
        {
            _levelRootPrefab = (GameObject)EditorGUILayout.ObjectField("Level Root Prefab", _levelRootPrefab, typeof(GameObject), false);
            _levelsRootFolder = EditorGUILayout.TextField("Levels Root Folder", _levelsRootFolder);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Level Name");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(DefaultLevelNamePrefix, GUILayout.Width(64f));
            }
            _levelNumber = Mathf.Max(1, EditorGUILayout.IntField(_levelNumber));
            EditorGUILayout.EndHorizontal();
            _levelDifficultyType = (LevelDifficultyType)EditorGUILayout.EnumPopup("Difficulty Type", _levelDifficultyType);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("A* Pixel Cube Grid (level prefab + JSON)", EditorStyles.boldLabel);
            _aStarBuildFromPixelCubeGrid = EditorGUILayout.Toggle("Build From Pixel Cube Grid", _aStarBuildFromPixelCubeGrid);
            _aStarGridPositionMatchRadius = EditorGUILayout.Slider(
                "Grid Position Match Radius",
                _aStarGridPositionMatchRadius,
                0.01f,
                3f);
        }

        if (string.IsNullOrWhiteSpace(_prefabName))
        {
            _prefabName = "PixelBlockPrefab";
        }

        if (string.IsNullOrWhiteSpace(_saveFolder))
        {
            _saveFolder = DefaultSaveFolder;
        }

        if (string.IsNullOrWhiteSpace(_levelsRootFolder))
        {
            _levelsRootFolder = DefaultLevelsRootFolder;
        }
    }

    private void EnsureColorQuotas()
    {
        if (_colorQuotas != null && _colorQuotas.Length == ManagedColors.Length)
        {
            bool isValid = true;
            for (int i = 0; i < ManagedColors.Length; i++)
            {
                if (_colorQuotas[i].Color != ManagedColors[i])
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
            {
                return;
            }
        }

        _colorQuotas = new ColorQuotaEntry[ManagedColors.Length];
        for (int i = 0; i < ManagedColors.Length; i++)
        {
            _colorQuotas[i] = new ColorQuotaEntry
            {
                Color = ManagedColors[i],
                TargetCount = 0,
            };
        }
    }

    private int GetTargetCount(PixelCubeColor color)
    {
        for (int i = 0; i < _colorQuotas.Length; i++)
        {
            if (_colorQuotas[i].Color == color)
            {
                return Mathf.Max(0, _colorQuotas[i].TargetCount);
            }
        }

        return 0;
    }

    private Color GetDisplayColor(PixelCubeColor color)
    {
        if (color == PixelCubeColor.None)
        {
            return new Color(0.18f, 0.18f, 0.18f);
        }

        if (_colorDatabase != null && TryMapPixelColorToColorType(color, out ColorType colorType))
        {
            foreach (MaterialColorDatabaseSO.MaterialColorEntry entry in _colorDatabase.Entries)
            {
                if (entry != null && entry.colorType == colorType && entry.material != null)
                {
                    return CellConfigDrawer.GetMaterialColor(entry.material);
                }
            }
        }

        return Color.gray;
    }

    private static void DrawColorSwatch(PixelCubeColor color, params GUILayoutOption[] options)
    {
        Rect rect = GUILayoutUtility.GetRect(16f, 16f, options);
        EditorGUI.DrawRect(rect, GetFallbackDisplayColor(color));
    }

    private static Color GetFallbackDisplayColor(PixelCubeColor color)
    {
        switch (color)
        {
            case PixelCubeColor.Red: return new Color(0.88f, 0.2f, 0.2f);
            case PixelCubeColor.Blue: return new Color(0.2f, 0.45f, 0.95f);
            case PixelCubeColor.Yellow: return new Color(0.95f, 0.82f, 0.2f);
            case PixelCubeColor.Green: return new Color(0.2f, 0.75f, 0.28f);
            case PixelCubeColor.Purple: return new Color(0.6f, 0.3f, 0.85f);
            case PixelCubeColor.Orange: return new Color(0.95f, 0.5f, 0.18f);
            case PixelCubeColor.Black: return new Color(0.15f, 0.15f, 0.15f);
            case PixelCubeColor.White: return new Color(0.9f, 0.9f, 0.9f);
            case PixelCubeColor.Brown: return new Color(0.45f, 0.27f, 0.16f);
            case PixelCubeColor.Beige: return new Color(0.93f, 0.85f, 0.7f);
            case PixelCubeColor.DarkPurple: return new Color(0.32f, 0.15f, 0.48f);
            case PixelCubeColor.SkyBlue: return new Color(0.45f, 0.75f, 0.95f);
            case PixelCubeColor.DarkGreen: return new Color(0.1f, 0.4f, 0.2f);
            case PixelCubeColor.Pink: return new Color(0.98f, 0.55f, 0.75f);
            case PixelCubeColor.Set2Beige: return new Color(0.84f, 0.74f, 0.58f);
            case PixelCubeColor.Set2Black_1: return new Color(0.08f, 0.08f, 0.1f);
            case PixelCubeColor.Set2Black_2: return new Color(0.14f, 0.13f, 0.16f);
            case PixelCubeColor.Set2Brown: return new Color(0.36f, 0.2f, 0.12f);
            case PixelCubeColor.Set2Cyan_1: return new Color(0.1f, 0.75f, 0.85f);
            case PixelCubeColor.Set2DarkGreen: return new Color(0.08f, 0.3f, 0.14f);
            case PixelCubeColor.Set2DarkGreen_1: return new Color(0.14f, 0.42f, 0.2f);
            case PixelCubeColor.Set2Green_1: return new Color(0.18f, 0.65f, 0.22f);
            case PixelCubeColor.Set2Green_2: return new Color(0.35f, 0.78f, 0.18f);
            case PixelCubeColor.Set2Green_3: return new Color(0.55f, 0.85f, 0.2f);
            case PixelCubeColor.Set2Green_3_1: return new Color(0.42f, 0.7f, 0.18f);
            case PixelCubeColor.Set2Green_4: return new Color(0.12f, 0.55f, 0.28f);
            case PixelCubeColor.Set2Green_5: return new Color(0.24f, 0.86f, 0.42f);
            case PixelCubeColor.Set2Purple_1: return new Color(0.48f, 0.24f, 0.78f);
            case PixelCubeColor.Set2Purple_2: return new Color(0.62f, 0.28f, 0.88f);
            case PixelCubeColor.Set2Purple_3: return new Color(0.74f, 0.42f, 0.95f);
            case PixelCubeColor.Set2Red_1: return new Color(0.82f, 0.12f, 0.12f);
            case PixelCubeColor.Set2Red_1_1: return new Color(0.92f, 0.18f, 0.16f);
            case PixelCubeColor.Set2Red_1_3: return new Color(0.7f, 0.1f, 0.12f);
            case PixelCubeColor.Set2Red_2: return new Color(0.95f, 0.28f, 0.18f);
            case PixelCubeColor.Set2Red_3: return new Color(0.9f, 0.35f, 0.2f);
            case PixelCubeColor.Set2Red_4: return new Color(0.72f, 0.16f, 0.18f);
            case PixelCubeColor.Set2Red_5: return new Color(0.88f, 0.2f, 0.28f);
            case PixelCubeColor.Set2Red_6: return new Color(0.95f, 0.45f, 0.32f);
            case PixelCubeColor.Set2Red_7: return new Color(0.66f, 0.08f, 0.08f);
            case PixelCubeColor.Set2SkyBlue: return new Color(0.38f, 0.72f, 0.95f);
            case PixelCubeColor.Set2Teal: return new Color(0.08f, 0.55f, 0.52f);
            case PixelCubeColor.Set2Yellow2: return new Color(0.96f, 0.75f, 0.16f);
            case PixelCubeColor.Set2Yellow_1: return new Color(0.98f, 0.86f, 0.18f);
            case PixelCubeColor.Set2Yellow_1_1: return new Color(0.9f, 0.72f, 0.12f);
            case PixelCubeColor.Set2Yellow_3: return new Color(0.95f, 0.62f, 0.12f);
            case PixelCubeColor.Set2Yellow_4: return new Color(0.82f, 0.58f, 0.08f);
            case PixelCubeColor.Set2Yellow_5: return new Color(1f, 0.9f, 0.28f);
            default: return Color.gray;
        }
    }

    private static Color GetContrastColor(Color background)
    {
        float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
        return luminance > 0.5f ? Color.black : Color.white;
    }

    private static GUIStyle BuildBadgeStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = Color.white;
        return style;
    }

    private static void DrawSelectionBorder(Rect rect, float thickness, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static bool TryMapPixelColorToColorType(PixelCubeColor pixelColor, out ColorType colorType)
    {
        switch (pixelColor)
        {
            case PixelCubeColor.Red: colorType = ColorType.Red; return true;
            case PixelCubeColor.Blue: colorType = ColorType.Blue; return true;
            case PixelCubeColor.Yellow: colorType = ColorType.Yellow; return true;
            case PixelCubeColor.Green: colorType = ColorType.Green; return true;
            case PixelCubeColor.Purple: colorType = ColorType.Purple; return true;
            case PixelCubeColor.Orange: colorType = ColorType.Orange; return true;
            case PixelCubeColor.Black: colorType = ColorType.Black; return true;
            case PixelCubeColor.White: colorType = ColorType.White; return true;
            case PixelCubeColor.Brown: colorType = ColorType.Brown; return true;
            case PixelCubeColor.Beige: colorType = ColorType.Beige; return true;
            case PixelCubeColor.DarkPurple: colorType = ColorType.DarkPurple; return true;
            case PixelCubeColor.SkyBlue: colorType = ColorType.SkyBlue; return true;
            case PixelCubeColor.DarkGreen: colorType = ColorType.DarkGreen; return true;
            case PixelCubeColor.Pink: colorType = ColorType.Pink; return true;
            case PixelCubeColor.Set2Beige: colorType = ColorType.Set2Beige; return true;
            case PixelCubeColor.Set2Black_1: colorType = ColorType.Set2Black_1; return true;
            case PixelCubeColor.Set2Black_2: colorType = ColorType.Set2Black_2; return true;
            case PixelCubeColor.Set2Brown: colorType = ColorType.Set2Brown; return true;
            case PixelCubeColor.Set2Cyan_1: colorType = ColorType.Set2Cyan_1; return true;
            case PixelCubeColor.Set2DarkGreen: colorType = ColorType.Set2DarkGreen; return true;
            case PixelCubeColor.Set2DarkGreen_1: colorType = ColorType.Set2DarkGreen_1; return true;
            case PixelCubeColor.Set2Green_1: colorType = ColorType.Set2Green_1; return true;
            case PixelCubeColor.Set2Green_2: colorType = ColorType.Set2Green_2; return true;
            case PixelCubeColor.Set2Green_3: colorType = ColorType.Set2Green_3; return true;
            case PixelCubeColor.Set2Green_3_1: colorType = ColorType.Set2Green_3_1; return true;
            case PixelCubeColor.Set2Green_4: colorType = ColorType.Set2Green_4; return true;
            case PixelCubeColor.Set2Green_5: colorType = ColorType.Set2Green_5; return true;
            case PixelCubeColor.Set2Purple_1: colorType = ColorType.Set2Purple_1; return true;
            case PixelCubeColor.Set2Purple_2: colorType = ColorType.Set2Purple_2; return true;
            case PixelCubeColor.Set2Purple_3: colorType = ColorType.Set2Purple_3; return true;
            case PixelCubeColor.Set2Red_1: colorType = ColorType.Set2Red_1; return true;
            case PixelCubeColor.Set2Red_1_1: colorType = ColorType.Set2Red_1_1; return true;
            case PixelCubeColor.Set2Red_1_3: colorType = ColorType.Set2Red_1_3; return true;
            case PixelCubeColor.Set2Red_2: colorType = ColorType.Set2Red_2; return true;
            case PixelCubeColor.Set2Red_3: colorType = ColorType.Set2Red_3; return true;
            case PixelCubeColor.Set2Red_4: colorType = ColorType.Set2Red_4; return true;
            case PixelCubeColor.Set2Red_5: colorType = ColorType.Set2Red_5; return true;
            case PixelCubeColor.Set2Red_6: colorType = ColorType.Set2Red_6; return true;
            case PixelCubeColor.Set2Red_7: colorType = ColorType.Set2Red_7; return true;
            case PixelCubeColor.Set2SkyBlue: colorType = ColorType.Set2SkyBlue; return true;
            case PixelCubeColor.Set2Teal: colorType = ColorType.Set2Teal; return true;
            case PixelCubeColor.Set2Yellow2: colorType = ColorType.Set2Yellow2; return true;
            case PixelCubeColor.Set2Yellow_1: colorType = ColorType.Set2Yellow_1; return true;
            case PixelCubeColor.Set2Yellow_1_1: colorType = ColorType.Set2Yellow_1_1; return true;
            case PixelCubeColor.Set2Yellow_3: colorType = ColorType.Set2Yellow_3; return true;
            case PixelCubeColor.Set2Yellow_4: colorType = ColorType.Set2Yellow_4; return true;
            case PixelCubeColor.Set2Yellow_5: colorType = ColorType.Set2Yellow_5; return true;
            default:
                colorType = default;
                return false;
        }
    }

    private bool TryGetPrimaryColorTypeForBrush(PixelCubeColor pixelColor, out ColorType colorType)
    {
        colorType = default;
        if (_colorDatabase == null)
        {
            return false;
        }

        foreach (MaterialColorDatabaseSO.MaterialColorEntry entry in _colorDatabase.Entries)
        {
            if (entry == null || entry.material == null)
            {
                continue;
            }

            if (!TryMapColorTypeToPixelColor(entry.colorType, out PixelCubeColor mapped))
            {
                continue;
            }

            if (mapped == pixelColor)
            {
                colorType = entry.colorType;
                return true;
            }
        }

        return false;
    }

    private static bool TryMapColorTypeToPixelColor(ColorType colorType, out PixelCubeColor pixelColor)
    {
        switch (colorType)
        {
            case ColorType.Red:
                pixelColor = PixelCubeColor.Red;
                return true;
            case ColorType.Blue:
            case ColorType.DarkBlue:
            case ColorType.Cyan:
                pixelColor = PixelCubeColor.Blue;
                return true;
            case ColorType.Yellow:
                pixelColor = PixelCubeColor.Yellow;
                return true;
            case ColorType.Green:
                pixelColor = PixelCubeColor.Green;
                return true;
            case ColorType.Purple:
                pixelColor = PixelCubeColor.Purple;
                return true;
            case ColorType.Pink:
                pixelColor = PixelCubeColor.Pink;
                return true;
            case ColorType.Orange:
                pixelColor = PixelCubeColor.Orange;
                return true;
            case ColorType.Black:
            case ColorType.Grey:
                pixelColor = PixelCubeColor.Black;
                return true;
            case ColorType.White:
                pixelColor = PixelCubeColor.White;
                return true;
            case ColorType.Brown:
                pixelColor = PixelCubeColor.Brown;
                return true;
            case ColorType.Beige:
                pixelColor = PixelCubeColor.Beige;
                return true;
            case ColorType.DarkPurple:
                pixelColor = PixelCubeColor.DarkPurple;
                return true;
            case ColorType.SkyBlue:
                pixelColor = PixelCubeColor.SkyBlue;
                return true;
            case ColorType.DarkGreen:
                pixelColor = PixelCubeColor.DarkGreen;
                return true;
            case ColorType.Set2Beige:
                pixelColor = PixelCubeColor.Set2Beige;
                return true;
            case ColorType.Set2Black_1:
                pixelColor = PixelCubeColor.Set2Black_1;
                return true;
            case ColorType.Set2Black_2:
                pixelColor = PixelCubeColor.Set2Black_2;
                return true;
            case ColorType.Set2Brown:
                pixelColor = PixelCubeColor.Set2Brown;
                return true;
            case ColorType.Set2Cyan_1:
                pixelColor = PixelCubeColor.Set2Cyan_1;
                return true;
            case ColorType.Set2DarkGreen:
                pixelColor = PixelCubeColor.Set2DarkGreen;
                return true;
            case ColorType.Set2DarkGreen_1:
                pixelColor = PixelCubeColor.Set2DarkGreen_1;
                return true;
            case ColorType.Set2Green_1:
                pixelColor = PixelCubeColor.Set2Green_1;
                return true;
            case ColorType.Set2Green_2:
                pixelColor = PixelCubeColor.Set2Green_2;
                return true;
            case ColorType.Set2Green_3:
                pixelColor = PixelCubeColor.Set2Green_3;
                return true;
            case ColorType.Set2Green_3_1:
                pixelColor = PixelCubeColor.Set2Green_3_1;
                return true;
            case ColorType.Set2Green_4:
                pixelColor = PixelCubeColor.Set2Green_4;
                return true;
            case ColorType.Set2Green_5:
                pixelColor = PixelCubeColor.Set2Green_5;
                return true;
            case ColorType.Set2Purple_1:
                pixelColor = PixelCubeColor.Set2Purple_1;
                return true;
            case ColorType.Set2Purple_2:
                pixelColor = PixelCubeColor.Set2Purple_2;
                return true;
            case ColorType.Set2Purple_3:
                pixelColor = PixelCubeColor.Set2Purple_3;
                return true;
            case ColorType.Set2Red_1:
                pixelColor = PixelCubeColor.Set2Red_1;
                return true;
            case ColorType.Set2Red_1_1:
                pixelColor = PixelCubeColor.Set2Red_1_1;
                return true;
            case ColorType.Set2Red_1_3:
                pixelColor = PixelCubeColor.Set2Red_1_3;
                return true;
            case ColorType.Set2Red_2:
                pixelColor = PixelCubeColor.Set2Red_2;
                return true;
            case ColorType.Set2Red_3:
                pixelColor = PixelCubeColor.Set2Red_3;
                return true;
            case ColorType.Set2Red_4:
                pixelColor = PixelCubeColor.Set2Red_4;
                return true;
            case ColorType.Set2Red_5:
                pixelColor = PixelCubeColor.Set2Red_5;
                return true;
            case ColorType.Set2Red_6:
                pixelColor = PixelCubeColor.Set2Red_6;
                return true;
            case ColorType.Set2Red_7:
                pixelColor = PixelCubeColor.Set2Red_7;
                return true;
            case ColorType.Set2SkyBlue:
                pixelColor = PixelCubeColor.Set2SkyBlue;
                return true;
            case ColorType.Set2Teal:
                pixelColor = PixelCubeColor.Set2Teal;
                return true;
            case ColorType.Set2Yellow2:
                pixelColor = PixelCubeColor.Set2Yellow2;
                return true;
            case ColorType.Set2Yellow_1:
                pixelColor = PixelCubeColor.Set2Yellow_1;
                return true;
            case ColorType.Set2Yellow_1_1:
                pixelColor = PixelCubeColor.Set2Yellow_1_1;
                return true;
            case ColorType.Set2Yellow_3:
                pixelColor = PixelCubeColor.Set2Yellow_3;
                return true;
            case ColorType.Set2Yellow_4:
                pixelColor = PixelCubeColor.Set2Yellow_4;
                return true;
            case ColorType.Set2Yellow_5:
                pixelColor = PixelCubeColor.Set2Yellow_5;
                return true;
            default:
                pixelColor = PixelCubeColor.None;
                return false;
        }
    }
}
