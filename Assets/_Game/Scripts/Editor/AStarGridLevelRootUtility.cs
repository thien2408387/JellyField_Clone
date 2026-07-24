using UnityEditor;
using UnityEngine;

public static class AStarGridLevelRootUtility
{
    private const string AStarGridObjectName = "AStarGrid";

    public static AStarGrid EnsureSingleAStarGridOnLevelRoot(
        GameObject levelRootInstance,
        GameObject pixelCubeRoot,
        float cellStep,
        bool buildFromPixelCubeGrid,
        float gridPositionMatchRadius)
    {
        if (levelRootInstance == null || pixelCubeRoot == null)
        {
            return null;
        }

        Transform pixelRootTransform = pixelCubeRoot.transform;
        RemoveNestedPixelRootGrids(levelRootInstance, pixelRootTransform);

        AStarGrid aStarGrid = ResolveLevelRootGrid(levelRootInstance, pixelRootTransform);
        if (aStarGrid == null)
        {
            GameObject aStarGridObject = new GameObject(AStarGridObjectName);
            aStarGridObject.transform.SetParent(levelRootInstance.transform, false);
            aStarGrid = aStarGridObject.AddComponent<AStarGrid>();
        }

        if (aStarGrid.gameObject != levelRootInstance)
        {
            aStarGrid.transform.SetParent(levelRootInstance.transform, false);
        }

        aStarGrid.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        aStarGrid.transform.localScale = Vector3.one;

        float effectiveCellStep = ResolveEffectiveCellStep(pixelRootTransform, cellStep);
        float effectiveMatchRadius = ResolveEffectiveMatchRadius(
            gridPositionMatchRadius,
            cellStep,
            effectiveCellStep);

        SerializedObject serializedGrid = new SerializedObject(aStarGrid);
        AStarGridEditorSerializationUtility.ApplyCellSizeStep(serializedGrid, effectiveCellStep);
        AStarGridEditorSerializationUtility.ApplyPixelCubeGridSettings(
            serializedGrid,
            pixelRootTransform,
            buildFromPixelCubeGrid,
            PixelCubeColor.Red,
            effectiveMatchRadius);
        AStarGridEditorSerializationUtility.ApplyDefaultCenteredManualGridSettings(serializedGrid);
        AStarGridEditorSerializationUtility.ApplyLevelPrimaryRegistrationPriority(serializedGrid);
        serializedGrid.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(aStarGrid);

        serializedGrid.Update();
        if (AStarGridEditorSerializationUtility.TrySnapManualGridWorldOffsetToPixels(
                serializedGrid,
                aStarGrid.transform,
                pixelRootTransform))
        {
            serializedGrid.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(aStarGrid);
        }

        return aStarGrid;
    }

    private static float ResolveEffectiveCellStep(Transform pixelRootTransform, float authoredCellStep)
    {
        Vector3 lossyScale = pixelRootTransform != null ? pixelRootTransform.lossyScale : Vector3.one;
        float planarScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        return Mathf.Max(0.0001f, authoredCellStep * Mathf.Max(0.0001f, planarScale));
    }

    private static float ResolveEffectiveMatchRadius(float authoredMatchRadius, float authoredCellStep, float effectiveCellStep)
    {
        float ratio = effectiveCellStep / Mathf.Max(0.0001f, authoredCellStep);
        return Mathf.Max(0.01f, authoredMatchRadius * ratio);
    }

    private static void RemoveNestedPixelRootGrids(GameObject levelRootInstance, Transform pixelRootTransform)
    {
        AStarGrid[] grids = levelRootInstance.GetComponentsInChildren<AStarGrid>(true);
        for (int i = grids.Length - 1; i >= 0; i--)
        {
            AStarGrid grid = grids[i];
            if (grid == null || grid.transform == null || !grid.transform.IsChildOf(pixelRootTransform))
            {
                continue;
            }

            if (grid.transform == pixelRootTransform)
            {
                Object.DestroyImmediate(grid, true);
                continue;
            }

            Object.DestroyImmediate(grid.gameObject, true);
        }
    }

    private static AStarGrid ResolveLevelRootGrid(GameObject levelRootInstance, Transform pixelRootTransform)
    {
        AStarGrid[] grids = levelRootInstance.GetComponentsInChildren<AStarGrid>(true);
        AStarGrid selectedGrid = null;
        for (int i = 0; i < grids.Length; i++)
        {
            AStarGrid grid = grids[i];
            if (grid == null || grid.transform == null || grid.transform.IsChildOf(pixelRootTransform))
            {
                continue;
            }

            if (selectedGrid == null)
            {
                selectedGrid = grid;
                continue;
            }

            Object.DestroyImmediate(grid.gameObject, true);
        }

        return selectedGrid;
    }
}
