using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Export partial: output paths, Jelly JSON snapshot, level prefab + A* wiring, export validation helpers.
public partial class PixelLevelQuotaComposerWindow
{
    private const string DefaultSaveFolder = "Assets/_Game/Prefabs";
    private const string DefaultLevelsRootFolder = "Assets/_Game/Prefabs/Levels";
    private const string DefaultLevelNamePrefix = "Level";
    private const string DefaultLevelRootPrefabPath = "Assets/_Game/Prefabs/Levels/LevelBase/LevelBase.prefab";
    private const string DefaultKeyPrefabPath = "Assets/_Game/Prefabs/KeyPrefab/KeyPickup.prefab";

    private string _prefabName = "PixelBlockPrefab";
    private string _saveFolder = DefaultSaveFolder;
    private bool _generateAsLevelPrefab = true;
    private GameObject _levelRootPrefab;
    private string _levelsRootFolder = DefaultLevelsRootFolder;
    private int _levelNumber = 1;
    private LevelDifficultyType _levelDifficultyType = LevelDifficultyType.Normal;

    private bool _aStarBuildFromPixelCubeGrid = true;
    private float _aStarGridPositionMatchRadius = 0.75f;

    #region Level prefab + A* wiring

    private void GenerateLevelPackage()
    {
        if (!CanGenerateLevelPackage(out string validationMessage))
        {
            EditorUtility.DisplayDialog("Cannot Generate", validationMessage, "OK");
            return;
        }

        List<PaintedCellData> filled = CollectTopPaintedCells();
        if (filled.Count == 0)
        {
            EditorUtility.DisplayDialog("No Top Cubes", "Paint at least one top cube before generating.", "OK");
            return;
        }

        if (_jellyCellConfigs == null || _jellyCellConfigs.Length != _jellyGridWidth * _jellyGridHeight)
        {
            EditorUtility.DisplayDialog("Missing Jelly Grid", "Initialize the Jelly grid before generating.", "OK");
            return;
        }

        if (!TryGetOutputAssetFolder(out string assetFolderPath, out string outputName, out string folderError))
        {
            EditorUtility.DisplayDialog("Invalid Output", folderError, "OK");
            return;
        }

        EnsureFolderExists(assetFolderPath);

        string blockPrefabPath = AssetDatabase.GenerateUniqueAssetPath($"{assetFolderPath}/{_prefabName}.prefab");
        GameObject generatedBlockRoot = BuildGeneratedBlockRoot(filled, _prefabName);
        PrefabUtility.SaveAsPrefabAsset(generatedBlockRoot, blockPrefabPath);
        DestroyImmediate(generatedBlockRoot);
        DisableSplitClusterGeneration(blockPrefabPath);

        string levelPrefabPath = string.Empty;
        if (_generateAsLevelPrefab)
        {
            GameObject generatedBlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blockPrefabPath);
            if (generatedBlockPrefab == null)
            {
                AssetDatabase.Refresh();
                generatedBlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blockPrefabPath);
            }

            if (generatedBlockPrefab == null)
            {
                EditorUtility.DisplayDialog("Block Prefab Error", $"Could not load the generated block prefab at:\n{blockPrefabPath}", "OK");
                return;
            }

            GameObject levelRootInstance = (GameObject)PrefabUtility.InstantiatePrefab(_levelRootPrefab);
            if (levelRootInstance == null)
            {
                EditorUtility.DisplayDialog("Level Root Clone Failed", "Could not instantiate the assigned Level Root Prefab.", "OK");
                return;
            }

            levelRootInstance.name = outputName;
            GameObject nestedBlockInstance = (GameObject)PrefabUtility.InstantiatePrefab(generatedBlockPrefab);
            if (nestedBlockInstance != null)
            {
                nestedBlockInstance.transform.SetParent(levelRootInstance.transform, false);
                nestedBlockInstance.transform.localPosition = Vector3.zero;
                nestedBlockInstance.transform.localRotation = Quaternion.identity;
            }

            EnsureAStarGridOnLevelRoot(levelRootInstance, nestedBlockInstance);

            levelPrefabPath = AssetDatabase.GenerateUniqueAssetPath($"{assetFolderPath}/{outputName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(levelRootInstance, levelPrefabPath);
            DestroyImmediate(levelRootInstance);
        }

        if (!TryConvertAssetPathToFullPath(assetFolderPath, out string outputDirectory))
        {
            EditorUtility.DisplayDialog("Output Path Error", $"Could not convert asset path to disk path:\n{assetFolderPath}", "OK");
            return;
        }

        GUI.FocusControl(null);
        SanitizeJellyCellConfigs();

        LevelSaveData jsonSnapshot = BuildLevelSaveDataSnapshot();
        if (!GridLevelFileIO.TrySaveLevelData(
                outputDirectory,
                outputName,
                jsonSnapshot,
                out string jsonFullPath,
                out string errorMessage))
        {
            EditorUtility.DisplayDialog("JSON Save Failed", errorMessage, "OK");
            return;
        }

        string cellTypeSummary = BuildCellTypeSaveSummary();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string dialogMessage = $"Created block prefab at:\n{blockPrefabPath}\n\nSaved Jelly JSON at:\n{jsonFullPath}\n\n{cellTypeSummary}";
        if (!string.IsNullOrEmpty(levelPrefabPath))
        {
            dialogMessage = $"Created block prefab at:\n{blockPrefabPath}\n\nCreated level prefab at:\n{levelPrefabPath}\n\nSaved Jelly JSON at:\n{jsonFullPath}\n\n{cellTypeSummary}";
        }

        EditorUtility.DisplayDialog("Level Package Generated", dialogMessage, "OK");
        Selection.activeObject = !string.IsNullOrEmpty(levelPrefabPath)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(levelPrefabPath)
            : AssetDatabase.LoadAssetAtPath<GameObject>(blockPrefabPath);
    }

    private void EnsureAStarGridOnLevelRoot(GameObject levelRootInstance, GameObject pixelCubeRoot)
    {
        float cellStep = Mathf.Max(0.0001f, _cubeSize + _cubeSpacing);
        AStarGridLevelRootUtility.EnsureSingleAStarGridOnLevelRoot(
            levelRootInstance,
            pixelCubeRoot,
            cellStep,
            _aStarBuildFromPixelCubeGrid,
            _aStarGridPositionMatchRadius);
    }

    #endregion

    #region Jelly JSON + level data snapshot

    private void SaveJellyJsonDraft()
    {
        if (_jellyCellConfigs == null || _jellyCellConfigs.Length != _jellyGridWidth * _jellyGridHeight)
        {
            EditorUtility.DisplayDialog("Missing Jelly Grid", "Initialize the Jelly grid before saving JSON.", "OK");
            return;
        }

        if (!TryGetOutputAssetFolder(out string assetFolderPath, out string outputName, out string folderError))
        {
            EditorUtility.DisplayDialog("Invalid Output", folderError, "OK");
            return;
        }

        EnsureFolderExists(assetFolderPath);

        string selectedAssetPath = EditorUtility.SaveFilePanelInProject(
            "Save Jelly JSON Draft",
            outputName,
            "json",
            "Choose where to save the Jelly JSON draft.",
            assetFolderPath);

        if (string.IsNullOrEmpty(selectedAssetPath))
        {
            return;
        }

        string selectedAssetDirectory = Path.GetDirectoryName(selectedAssetPath)?.Replace('\\', '/') ?? assetFolderPath;
        string selectedFileName = Path.GetFileNameWithoutExtension(selectedAssetPath);

        if (!TryConvertAssetPathToFullPath(selectedAssetDirectory, out string outputDirectory))
        {
            EditorUtility.DisplayDialog("Output Path Error", $"Could not convert asset path to disk path:\n{selectedAssetDirectory}", "OK");
            return;
        }

        GUI.FocusControl(null);
        SanitizeJellyCellConfigs();

        LevelSaveData jsonSnapshot = BuildLevelSaveDataSnapshot();
        if (!GridLevelFileIO.TrySaveLevelData(
                outputDirectory,
                selectedFileName,
                jsonSnapshot,
                out string jsonFullPath,
                out string errorMessage))
        {
            EditorUtility.DisplayDialog("JSON Save Failed", errorMessage, "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Jelly JSON Saved",
            $"Saved Jelly JSON at:\n{jsonFullPath}\n\n{BuildCellTypeSaveSummary()}",
            "OK");
    }

    private void LoadJellyJson()
    {
        string fullPath = EditorUtility.OpenFilePanel("Load Jelly JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(fullPath))
        {
            return;
        }

        if (!GridLevelFileIO.TryLoadLevelData(fullPath, out LevelSaveData data, out string errorMessage))
        {
            EditorUtility.DisplayDialog("Load Failed", errorMessage, "OK");
            return;
        }

        int expectedLength = data.Width * data.Height;
        if (data.CellConfigs == null || data.CellConfigs.Length != expectedLength)
        {
            EditorUtility.DisplayDialog("Load Failed", $"Invalid cell data count. Expected {expectedLength}, got {data.CellConfigs?.Length ?? 0}.", "OK");
            return;
        }

        _jellyGridWidth = data.Width;
        _jellyGridHeight = data.Height;
        _jellyCellSize = data.CellSize;
        _jellyCellConfigs = data.CellConfigs;
        SanitizeJellyCellConfigs();
        _selectedJellyCellIndex = -1;

        ApplyLevelSaveDataSnapshot(data);

        Repaint();
    }

    private LevelSaveData BuildLevelSaveDataSnapshot()
    {
        EnsureColorQuotas();
        InitializeTopGridIfNeeded();

        var quotas = new ColorQuotaSaveData[_colorQuotas.Length];
        for (int i = 0; i < _colorQuotas.Length; i++)
        {
            quotas[i] = new ColorQuotaSaveData
            {
                Color = _colorQuotas[i].Color,
                TargetCount = _colorQuotas[i].TargetCount,
            };
        }

        int tw = _topGridWidth;
        int th = _topGridHeight;
        int[] topFlat = new int[tw * th];
        int[] topKeyFlat = new int[tw * th];
        int[] topTimingFlat = new int[tw * th];
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                int index = y * tw + x;
                topFlat[index] = (int)_topCellColors[x, y];
                topKeyFlat[index] = (_topCellIsKey != null && _topCellIsKey[x, y]) ? 1 : 0;
                topTimingFlat[index] = _topCellColors[x, y] == PixelCubeColor.None || _topCellTimingSeconds == null
                    ? 0
                    : Mathf.Max(0, _topCellTimingSeconds[x, y]);
            }
        }

        string prefabPath = _topCellPrefab != null ? AssetDatabase.GetAssetPath(_topCellPrefab) : string.Empty;
        string levelRootPath = _levelRootPrefab != null ? AssetDatabase.GetAssetPath(_levelRootPrefab) : string.Empty;

        return new LevelSaveData
        {
            FormatVersion = 10,
            Width = _jellyGridWidth,
            Height = _jellyGridHeight,
            CellSize = _jellyCellSize,
            CellConfigs = _jellyCellConfigs,
            DifficultyType = _levelDifficultyType,
            ColorQuotas = quotas,
            TopGridWidth = tw,
            TopGridHeight = th,
            TopCellColorsFlat = topFlat,
            TopCellIsKeyFlat = topKeyFlat,
            TopCellTimingSecondsFlat = topTimingFlat,
            CubeSize = _cubeSize,
            CubeSpacing = _cubeSpacing,
            CenterPivot = _centerPivot,
            TopCellPrefabAssetPath = prefabPath,
            JellyDisplaySize = _jellyDisplaySize,
            BlockPrefabName = _prefabName,
            SaveFolder = _saveFolder,
            LevelsRootFolder = _levelsRootFolder,
            GenerateAsLevel = _generateAsLevelPrefab,
            LevelNumber = _levelNumber,
            LevelRootPrefabAssetPath = levelRootPath,
            AStarBuildFromPixelCubeGrid = _aStarBuildFromPixelCubeGrid,
            AStarBlockedCubeColor = (int)PixelCubeColor.Red,
            AStarGridPositionMatchRadius = _aStarGridPositionMatchRadius,
        };
    }

    private void ApplyLevelSaveDataSnapshot(LevelSaveData data)
    {
        EnsureColorQuotas();

        if (data.ColorQuotas != null && data.ColorQuotas.Length > 0)
        {
            for (int i = 0; i < _colorQuotas.Length; i++)
            {
                PixelCubeColor c = _colorQuotas[i].Color;
                for (int j = 0; j < data.ColorQuotas.Length; j++)
                {
                    if (data.ColorQuotas[j].Color == c)
                    {
                        _colorQuotas[i].TargetCount = Mathf.Max(0, data.ColorQuotas[j].TargetCount);
                        break;
                    }
                }
            }
        }

        if (data.TopGridWidth > 0 &&
            data.TopGridHeight > 0 &&
            data.TopCellColorsFlat != null &&
            data.TopCellColorsFlat.Length == data.TopGridWidth * data.TopGridHeight)
        {
            _topGridWidth = data.TopGridWidth;
            _topGridHeight = data.TopGridHeight;
            _topCellColors = new PixelCubeColor[_topGridWidth, _topGridHeight];
            _topCellTimingSeconds = new int[_topGridWidth, _topGridHeight];
            _topCellIsKey = new bool[_topGridWidth, _topGridHeight];
            bool hasKeyFlat = data.TopCellIsKeyFlat != null &&
                data.TopCellIsKeyFlat.Length == data.TopGridWidth * data.TopGridHeight;
            bool hasTimingFlat = data.TopCellTimingSecondsFlat != null &&
                data.TopCellTimingSecondsFlat.Length == data.TopGridWidth * data.TopGridHeight;
            for (int y = 0; y < _topGridHeight; y++)
            {
                for (int x = 0; x < _topGridWidth; x++)
                {
                    int index = y * _topGridWidth + x;
                    int raw = data.TopCellColorsFlat[index];
                    _topCellColors[x, y] = SanitizeLoadedPixelCubeColor(raw);
                    _topCellTimingSeconds[x, y] = hasTimingFlat && _topCellColors[x, y] != PixelCubeColor.None
                        ? Mathf.Max(0, data.TopCellTimingSecondsFlat[index])
                        : 0;

                    _topCellIsKey[x, y] = hasKeyFlat && data.TopCellIsKeyFlat[index] != 0;
                }
            }
        }

        _levelDifficultyType = data.FormatVersion >= 10 &&
            Enum.IsDefined(typeof(LevelDifficultyType), data.DifficultyType)
            ? data.DifficultyType
            : LevelDifficultyType.Normal;

        if (data.FormatVersion < 2)
        {
            return;
        }

        if (data.CubeSize > 0.001f)
        {
            _cubeSize = data.CubeSize;
        }

        _cubeSpacing = data.CubeSpacing >= 0f ? data.CubeSpacing : _cubeSpacing;
        _centerPivot = data.CenterPivot;

        if (data.JellyDisplaySize > 0f)
        {
            _jellyDisplaySize = Mathf.Clamp(data.JellyDisplaySize, MinJellyDisplaySize, MaxJellyDisplaySize);
        }

        if (!string.IsNullOrEmpty(data.TopCellPrefabAssetPath))
        {
            GameObject loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(data.TopCellPrefabAssetPath);
            if (loadedPrefab != null)
            {
                _topCellPrefab = loadedPrefab;
            }
        }

        if (!string.IsNullOrEmpty(data.BlockPrefabName))
        {
            _prefabName = data.BlockPrefabName;
        }

        if (!string.IsNullOrEmpty(data.SaveFolder))
        {
            _saveFolder = data.SaveFolder;
        }

        if (!string.IsNullOrEmpty(data.LevelsRootFolder))
        {
            _levelsRootFolder = data.LevelsRootFolder;
        }

        _generateAsLevelPrefab = data.GenerateAsLevel;
        if (data.LevelNumber > 0)
        {
            _levelNumber = data.LevelNumber;
        }

        if (!string.IsNullOrEmpty(data.LevelRootPrefabAssetPath))
        {
            GameObject loadedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(data.LevelRootPrefabAssetPath);
            if (loadedRoot != null)
            {
                _levelRootPrefab = loadedRoot;
            }
        }

        if (data.FormatVersion >= 7)
        {
            _aStarBuildFromPixelCubeGrid = data.AStarBuildFromPixelCubeGrid;
            float radius = data.AStarGridPositionMatchRadius;
            _aStarGridPositionMatchRadius = radius >= 0.01f ? radius : 0.75f;
        }
    }

    #endregion

    #region Export validation & filesystem

    private static PixelCubeColor SanitizeLoadedPixelCubeColor(int raw)
    {
        if (Enum.IsDefined(typeof(PixelCubeColor), raw))
        {
            return (PixelCubeColor)raw;
        }

        return PixelCubeColor.None;
    }

    private bool CanGenerateLevelPackage(out string message)
    {
        if (CollectTopPaintedCells().Count == 0)
        {
            message = "Paint at least one top cube before generating.";
            return false;
        }

        List<string> issues = new List<string>();
        for (int i = 0; i < ManagedColors.Length; i++)
        {
            PixelCubeColor color = ManagedColors[i];
            int target = GetTargetCount(color);
            int topUsed = CountTopCellsForColor(color);
            int jellyUsed = CountJellyAmmoForColor(color);

            if (topUsed != target || jellyUsed != target)
            {
                issues.Add($"{color}: target {target}, top {topUsed}, jelly {jellyUsed}");
            }
        }

        if (issues.Count > 0)
        {
            message = "Every color must satisfy Target = Top = Jelly before export.\n" + string.Join("\n", issues);
            return false;
        }

        int keyCount = CountKeyAnchors();
        int lockCount = CountLockedJellyCells();
        if (keyCount > lockCount)
        {
            int missing = keyCount - lockCount;
            message = $"Keys ({keyCount}) exceed Locked Jelly cells ({lockCount}). Add {missing} more Locked jelly cell(s) or remove excess keys.";
            return false;
        }

        List<string> linkIssues = CollectLinkValidationIssues();
        if (linkIssues.Count > 0)
        {
            message = "Every Link group must contain exactly 2 cells.\n" + string.Join("\n", linkIssues);
            return false;
        }

        if (_generateAsLevelPrefab)
        {
            if (_levelRootPrefab == null)
            {
                TryAutoAssignLevelRootPrefab();
            }

            if (_levelRootPrefab == null)
            {
                message = "Assign a Level Root Prefab before generating a level package.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private bool TryGetOutputAssetFolder(out string assetFolderPath, out string outputName, out string errorMessage)
    {
        outputName = _generateAsLevelPrefab ? $"{DefaultLevelNamePrefix}{Mathf.Max(1, _levelNumber)}" : _prefabName;
        assetFolderPath = _generateAsLevelPrefab ? $"{_levelsRootFolder}/{outputName}" : _saveFolder;

        if (string.IsNullOrWhiteSpace(outputName))
        {
            errorMessage = "Output name cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(assetFolderPath) || !assetFolderPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            errorMessage = "Output folder must be an Assets-relative path.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static void EnsureFolderExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string normalized = assetPath.Replace("\\", "/");
        string[] segments = normalized.Split('/');
        if (segments.Length == 0 || segments[0] != "Assets")
        {
            return;
        }

        string current = "Assets";
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }

    private static bool TryConvertAssetPathToFullPath(string assetPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            return false;
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return false;
        }

        fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        return true;
    }

    #endregion

    #region Editor defaults

    private void TryAutoAssignLevelRootPrefab()
    {
        if (_levelRootPrefab != null)
        {
            return;
        }

        _levelRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLevelRootPrefabPath);
    }

    private void TryAutoAssignKeyPrefab()
    {
        if (_keyPrefab != null)
        {
            return;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultKeyPrefabPath);
        if (asset != null)
        {
            _keyPrefab = asset.GetComponent<KeyPickup>();
        }
    }

    private static Material GetMaterialForColor(PixelCubeColor color)
    {
        if (color == PixelCubeColor.None)
        {
            return null;
        }

        string materialPath;
        switch (color)
        {
            case PixelCubeColor.Red: materialPath = "ColorMaterials/Red"; break;
            case PixelCubeColor.Blue: materialPath = "ColorMaterials/Blue"; break;
            case PixelCubeColor.Yellow: materialPath = "ColorMaterials/Yellow"; break;
            case PixelCubeColor.Green: materialPath = "ColorMaterials/Green"; break;
            case PixelCubeColor.Purple: materialPath = "ColorMaterials/Purple"; break;
            case PixelCubeColor.Orange: materialPath = "ColorMaterials/Orange"; break;
            case PixelCubeColor.Black: materialPath = "ColorMaterials/Black"; break;
            case PixelCubeColor.White: materialPath = "ColorMaterials/White"; break;
            case PixelCubeColor.Brown: materialPath = "ColorMaterials/Brown"; break;
            case PixelCubeColor.Beige: materialPath = "ColorMaterials/Beige"; break;
            case PixelCubeColor.DarkPurple: materialPath = "ColorMaterials/DarkPurple"; break;
            case PixelCubeColor.SkyBlue: materialPath = "ColorMaterials/SkyBlue"; break;
            case PixelCubeColor.DarkGreen: materialPath = "ColorMaterials/DarkGreen"; break;
            case PixelCubeColor.Pink: materialPath = "ColorMaterials/Pink"; break;
            case PixelCubeColor.Set2Beige: materialPath = "ColorMaterials2/Beige"; break;
            case PixelCubeColor.Set2Black_1: materialPath = "ColorMaterials2/Black_1"; break;
            case PixelCubeColor.Set2Black_2: materialPath = "ColorMaterials2/Black_2"; break;
            case PixelCubeColor.Set2Brown: materialPath = "ColorMaterials2/Brown"; break;
            case PixelCubeColor.Set2Cyan_1: materialPath = "ColorMaterials2/Cyan_1"; break;
            case PixelCubeColor.Set2DarkGreen: materialPath = "ColorMaterials2/DarkGreen"; break;
            case PixelCubeColor.Set2DarkGreen_1: materialPath = "ColorMaterials2/DarkGreen_1"; break;
            case PixelCubeColor.Set2Green_1: materialPath = "ColorMaterials2/Green_1"; break;
            case PixelCubeColor.Set2Green_2: materialPath = "ColorMaterials2/Green_2"; break;
            case PixelCubeColor.Set2Green_3: materialPath = "ColorMaterials2/Green_3"; break;
            case PixelCubeColor.Set2Green_3_1: materialPath = "ColorMaterials2/Green_3_1"; break;
            case PixelCubeColor.Set2Green_4: materialPath = "ColorMaterials2/Green_4"; break;
            case PixelCubeColor.Set2Green_5: materialPath = "ColorMaterials2/Green_5"; break;
            case PixelCubeColor.Set2Purple_1: materialPath = "ColorMaterials2/Purple_1"; break;
            case PixelCubeColor.Set2Purple_2: materialPath = "ColorMaterials2/Purple_2"; break;
            case PixelCubeColor.Set2Purple_3: materialPath = "ColorMaterials2/Purple_3"; break;
            case PixelCubeColor.Set2Red_1: materialPath = "ColorMaterials2/Red_1"; break;
            case PixelCubeColor.Set2Red_1_1: materialPath = "ColorMaterials2/Red_1_1"; break;
            case PixelCubeColor.Set2Red_1_3: materialPath = "ColorMaterials2/Red_1_3"; break;
            case PixelCubeColor.Set2Red_2: materialPath = "ColorMaterials2/Red_2"; break;
            case PixelCubeColor.Set2Red_3: materialPath = "ColorMaterials2/Red_3"; break;
            case PixelCubeColor.Set2Red_4: materialPath = "ColorMaterials2/Red_4"; break;
            case PixelCubeColor.Set2Red_5: materialPath = "ColorMaterials2/Red_5"; break;
            case PixelCubeColor.Set2Red_6: materialPath = "ColorMaterials2/Red_6"; break;
            case PixelCubeColor.Set2Red_7: materialPath = "ColorMaterials2/Red_7"; break;
            case PixelCubeColor.Set2SkyBlue: materialPath = "ColorMaterials2/SkyBlue"; break;
            case PixelCubeColor.Set2Teal: materialPath = "ColorMaterials2/Teal"; break;
            case PixelCubeColor.Set2Yellow2: materialPath = "ColorMaterials2/Yellow2"; break;
            case PixelCubeColor.Set2Yellow_1: materialPath = "ColorMaterials2/Yellow_1"; break;
            case PixelCubeColor.Set2Yellow_1_1: materialPath = "ColorMaterials2/Yellow_1_1"; break;
            case PixelCubeColor.Set2Yellow_3: materialPath = "ColorMaterials2/Yellow_3"; break;
            case PixelCubeColor.Set2Yellow_4: materialPath = "ColorMaterials2/Yellow_4"; break;
            case PixelCubeColor.Set2Yellow_5: materialPath = "ColorMaterials2/Yellow_5"; break;
            default: return null;
        }

        return Resources.Load<Material>(materialPath);
    }

    #endregion
}
