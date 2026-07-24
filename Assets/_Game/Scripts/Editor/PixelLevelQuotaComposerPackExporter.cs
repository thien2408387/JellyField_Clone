using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PixelLevelQuotaComposerPackExporter
{
    private const string DefaultPackageFileName = "PixelLevelQuotaComposerPack.unitypackage";

    private static readonly string[] ExportRoots =
    {
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.SharedGUI.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.Top.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.TopGUI.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.Jelly.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.JellyGUI.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerWindow.Export.cs",
        "Assets/_Game/Scripts/Editor/PixelLevelQuotaComposerPackExporter.cs",
        "Assets/_Game/Scripts/Editor/AStarGridEditorSerializationUtility.cs",
        "Assets/_Game/Scripts/Editor/AStarGridLevelRootUtility.cs",
        "Assets/_Game/Scripts/Editor/AStarGridPixelBlockPlacementUtility.cs",
        "Assets/_Game/Scripts/BottomGrid/GridLevelFileIO.cs",
        "Assets/_Game/Scripts/BottomGrid/CellConfig.cs",
        "Assets/_Game/Scripts/BottomGrid/CellConfigDrawer.cs",
        "Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGrid.cs",
        "Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGridGizmos.cs",
        "Assets/_Game/Scripts/Gameplay/JellyCapsule/AStarGridProvider.cs",
        "Assets/_Game/Scripts/Gameplay/PixelClusterComponent/PixelCubeColor.cs",
        "Assets/_Game/Scripts/Gameplay/PixelClusterComponent/PixelColorMaterialProvider.cs",
        "Assets/_Game/Scripts/Gameplay/PixelClusterComponent/PixelCubeCell.cs",
        "Assets/_Game/Scripts/Gameplay/PixelClusterComponent/PixelClusterRigidbody.cs",
        "Assets/_Game/Scripts/Keys/KeyPickup.cs",
        "Assets/_Game/Prefabs/KeyPrefab/KeyPickup.prefab",
        "Assets/_Game/Prefabs/Levels/LevelBase/LevelBase.prefab",
        "Assets/_Game/Prefabs/LevelsNew/LevelBase/LevelBase.prefab",
        "Assets/_ThirdPartty/Arts/3D/Meshs/CubeMatch/TM_cube.asset",
        "Assets/_ThirdPartty/Resources/ColorMaterials",
        "Assets/_SDK/Template/Scripts/AssetHelper/MaterialColor/Scripts/MaterialColorDatabaseSO.cs",
        "Assets/_SDK/Template/Scripts/AssetHelper/MaterialColor/Scripts/MaterialColorController.cs",
        "Assets/_SDK/Template/Scripts/AssetHelper/MaterialColor/SO/MaterialColorDatabase.asset",
        "Assets/_Game/Scripts/Utilities",
        "Assets/_Game/Docs/LevelQuotaComposerPack_README.md",
    };

    [MenuItem("Tools/Pixel Fever/Export/Level Quota Composer Pack")]
    public static void ExportPack()
    {
        string exportPath = EditorUtility.SaveFilePanel(
            "Export Level Quota Composer Pack",
            string.Empty,
            DefaultPackageFileName,
            "unitypackage");

        if (string.IsNullOrEmpty(exportPath))
        {
            return;
        }

        List<string> assetPaths = CollectExistingExportAssets();
        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Export Failed", "No export assets were found.", "OK");
            return;
        }

        AssetDatabase.ExportPackage(
            assetPaths.ToArray(),
            exportPath,
            ExportPackageOptions.Interactive | ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

        Debug.Log($"Level Quota Composer pack exported to: {exportPath}");
        EditorUtility.DisplayDialog("Export Complete", $"Pack created at:\n{exportPath}", "OK");
    }

    public static void ExportPackToPath(string exportPath)
    {
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            Debug.LogError("Level Quota Composer export failed: output path is empty.");
            return;
        }

        List<string> assetPaths = CollectExistingExportAssets();
        if (assetPaths.Count == 0)
        {
            Debug.LogError("Level Quota Composer export failed: no export assets were found.");
            return;
        }

        string folderPath = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrWhiteSpace(folderPath) && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        AssetDatabase.ExportPackage(
            assetPaths.ToArray(),
            exportPath,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

        Debug.Log($"Level Quota Composer pack exported to: {exportPath}");
    }

    private static List<string> CollectExistingExportAssets()
    {
        List<string> collected = new List<string>(ExportRoots.Length);
        CollectExistingAssets(ExportRoots, collected);
        return collected;
    }

    private static void CollectExistingAssets(IReadOnlyList<string> assetPaths, ICollection<string> collected)
    {
        for (int i = 0; i < assetPaths.Count; i++)
        {
            string assetPath = assetPaths[i];
            if (AssetDatabase.IsValidFolder(assetPath) || AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                collected.Add(assetPath);
                continue;
            }

            Debug.LogWarning($"Skipped missing pack asset: {assetPath}");
        }
    }
}
