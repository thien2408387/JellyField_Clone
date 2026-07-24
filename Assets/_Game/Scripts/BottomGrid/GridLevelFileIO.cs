using UnityEngine;
using System;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;


public static class GridLevelFileIO
{
    public static bool TrySaveLevelData(
        string savePath,
        string levelName,
        int width,
        int height,
        float cellSize,
        CellConfig[] cellConfigs,
        out string fullPath,
        out string errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = string.Empty;

        if (cellConfigs == null)
        {
            errorMessage = "Initialize the grid first!";
            return false;
        }

        try
        {
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            LevelSaveData data = new LevelSaveData
            {
                FormatVersion = 1,
                Width = width,
                Height = height,
                CellSize = cellSize,
                CellConfigs = cellConfigs,
                DifficultyType = LevelDifficultyType.Normal,
            };

            return TrySaveLevelDataInternal(savePath, levelName, data, out fullPath, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = "Failed to save level data.\n" + ex.Message;
            return false;
        }
    }

    public static bool TryLoadLevelData(
        string fullPath,
        out LevelSaveData data,
        out string errorMessage)
    {
        data = default;
        errorMessage = string.Empty;

        if (string.IsNullOrEmpty(fullPath))
        {
            errorMessage = "No file selected.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            errorMessage = $"File not found:\n{fullPath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            data = JsonUtility.FromJson<LevelSaveData>(json);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "Failed to load level data.\n" + ex.Message;
            return false;
        }
    }

    public static bool TrySaveLevelData(
        string savePath,
        string levelName,
        LevelSaveData data,
        out string fullPath,
        out string errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = string.Empty;

        if (data.CellConfigs == null)
        {
            errorMessage = "Initialize the grid first!";
            return false;
        }

        try
        {
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            return TrySaveLevelDataInternal(savePath, levelName, data, out fullPath, out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = "Failed to save level data.\n" + ex.Message;
            return false;
        }
    }

    private static bool TrySaveLevelDataInternal(
        string savePath,
        string levelName,
        LevelSaveData data,
        out string fullPath,
        out string errorMessage)
    {
        fullPath = string.Empty;
        errorMessage = string.Empty;

        try
        {
            string json = JsonUtility.ToJson(data, true);
            fullPath = Path.Combine(savePath, levelName + ".json");
            File.WriteAllText(fullPath, json);
            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "Failed to save level data.\n" + ex.Message;
            return false;
        }
    }
}

#endif

[Serializable]
public struct ColorQuotaSaveData
{
    public PixelCubeColor Color;
    public int TargetCount;
}

[Serializable]
public struct LevelSaveData
{
    /// <summary>1 = jelly grid only (legacy). 2 = includes quotas/top grid. 3 = legacy top cube layers. 4 = adds Jelly stack items. 5 = adds Stack helper-cell direction (HelperDx/HelperDy on Stack-type cells). 6 = adds top-cell key markers (TopCellIsKeyFlat). 7 = adds A* pixel-cube grid export fields (AStarBuildFromPixelCubeGrid, AStarBlockedCubeColor, AStarGridPositionMatchRadius). 8 = adds per-cell Jelly TimingSeconds and TopCellTimingSecondsFlat. 9 = adds TopCellPrefabAssetPath. 10 = adds DifficultyType.</summary>
    public int FormatVersion;

    public int Width;
    public int Height;
    public float CellSize;
    public CellConfig[] CellConfigs;
    public LevelDifficultyType DifficultyType;

    public ColorQuotaSaveData[] ColorQuotas;

    public int TopGridWidth;
    public int TopGridHeight;
    public int[] TopCellColorsFlat;
    public int[] TopCellIsKeyFlat;
    public int[] TopCellTimingSecondsFlat;

    public float CubeSize;
    public float CubeSpacing;
    public bool CenterPivot;
    public string TopCellPrefabAssetPath;

    public float JellyDisplaySize;

    public string BlockPrefabName;
    public string SaveFolder;
    public string LevelsRootFolder;
    public bool GenerateAsLevel;
    public int LevelNumber;
    public string LevelRootPrefabAssetPath;

    public bool AStarBuildFromPixelCubeGrid;
    public int AStarBlockedCubeColor;
    public float AStarGridPositionMatchRadius;
}
