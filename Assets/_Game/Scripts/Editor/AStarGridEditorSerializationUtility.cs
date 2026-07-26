using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes <see cref="AStarGrid"/> serialized fields for the Pixel Cube Grid foldout (editor / prefab gen).
/// </summary>
public static class AStarGridEditorSerializationUtility
{
    public const int LevelExportedRegistrationPriority = 100;

    /// <summary>Level-gen / export standard manual slab: 100x100 cells with indices starting at (-50,-50).</summary>
    public static readonly Vector2Int DefaultCenteredGridMin = new Vector2Int(-50, -50);

    public static readonly Vector2Int DefaultCenteredGridSize = new Vector2Int(100, 100);

    public static readonly Vector3 DefaultManualWorldOffset = new Vector3(-50f, -50f, 0f);

    public static void ApplyPixelCubeGridSettings(
        SerializedObject serializedGrid,
        Transform pixelCubeRoot,
        bool buildFromPixelCubeGrid,
        PixelCubeColor blockedCubeColor,
        float gridPositionMatchRadius)
    {
        if (serializedGrid == null)
        {
            return;
        }

        SerializedProperty pixelCubeRootProperty = serializedGrid.FindProperty("_pixelCubeRoot");
        if (pixelCubeRootProperty != null)
        {
            pixelCubeRootProperty.objectReferenceValue = pixelCubeRoot;
        }

        SerializedProperty buildFromPixelCubeGridProperty = serializedGrid.FindProperty("_buildFromPixelCubeGrid");
        if (buildFromPixelCubeGridProperty != null)
        {
            buildFromPixelCubeGridProperty.boolValue = buildFromPixelCubeGrid;
        }

        SerializedProperty blockedCubeColorProperty = serializedGrid.FindProperty("_blockedCubeColor");
        if (blockedCubeColorProperty != null)
        {
            blockedCubeColorProperty.intValue = (int)blockedCubeColor;
        }

        SerializedProperty gridPositionMatchRadiusProperty = serializedGrid.FindProperty("_gridPositionMatchRadius");
        if (gridPositionMatchRadiusProperty != null)
        {
            gridPositionMatchRadiusProperty.floatValue = Mathf.Max(0.01f, gridPositionMatchRadius);
        }
    }

    public static void ApplyLevelPrimaryRegistrationPriority(SerializedObject serializedGrid)
    {
        if (serializedGrid == null)
        {
            return;
        }

        SerializedProperty priorityProperty = serializedGrid.FindProperty("_providerRegistrationPriority");
        if (priorityProperty != null)
        {
            priorityProperty.intValue = LevelExportedRegistrationPriority;
        }
    }

    public static void ApplyDefaultCenteredManualGridSettings(SerializedObject serializedGrid)
    {
        if (serializedGrid == null)
        {
            return;
        }

        SerializedProperty gridMinProperty = serializedGrid.FindProperty("_gridMin");
        if (gridMinProperty != null)
        {
            gridMinProperty.vector2IntValue = DefaultCenteredGridMin;
        }

        SerializedProperty gridSizeProperty = serializedGrid.FindProperty("_gridSize");
        if (gridSizeProperty != null)
        {
            gridSizeProperty.vector2IntValue = DefaultCenteredGridSize;
        }

        SerializedProperty worldOffsetProperty = serializedGrid.FindProperty("_worldOffset");
        if (worldOffsetProperty != null)
        {
            worldOffsetProperty.vector3Value = DefaultManualWorldOffset;
        }
    }

    public static void ApplyGridBounds(SerializedObject serializedGrid, Vector2Int gridMin, Vector2Int gridSize)
    {
        if (serializedGrid == null)
        {
            return;
        }

        SerializedProperty gridMinProperty = serializedGrid.FindProperty("_gridMin");
        if (gridMinProperty != null)
        {
            gridMinProperty.vector2IntValue = gridMin;
        }

        SerializedProperty gridSizeProperty = serializedGrid.FindProperty("_gridSize");
        if (gridSizeProperty != null)
        {
            gridSizeProperty.vector2IntValue = new Vector2Int(
                Mathf.Max(1, gridSize.x),
                Mathf.Max(1, gridSize.y));
        }
    }

    public static void ApplyCellSizeStep(SerializedObject serializedGrid, float step)
    {
        if (serializedGrid == null)
        {
            return;
        }

        float safeStep = Mathf.Max(0.0001f, step);
        SerializedProperty cellSizeProperty = serializedGrid.FindProperty("_cellSize");
        if (cellSizeProperty != null)
        {
            cellSizeProperty.vector2Value = new Vector2(safeStep, safeStep);
        }
    }

    /// <summary>
    /// After fixed manual slab (<see cref="ApplyDefaultCenteredManualGridSettings"/>), aligns serialized <c>_worldOffset</c>
    /// so cell centers match <see cref="PixelCubeCell"/> world positions for indices inside the slab.
    /// </summary>
    public static bool TrySnapManualGridWorldOffsetToPixels(
        SerializedObject serializedGrid,
        Transform aStarGridTransform,
        Transform pixelCubeRoot)
    {
        return AStarGridPixelBlockPlacementUtility.TrySnapSerializedGridWorldOffsetToPixelCells(
            serializedGrid,
            aStarGridTransform,
            pixelCubeRoot);
    }
}
