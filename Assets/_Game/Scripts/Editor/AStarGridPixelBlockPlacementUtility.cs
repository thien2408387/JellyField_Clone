using UnityEditor;
using UnityEngine;

public static class AStarGridPixelBlockPlacementUtility
{
    /// <summary>
    /// Sets serialized <see cref="AStarGrid"/> <c>_worldOffset</c> so manual grid cell centers match
    /// <see cref="PixelCubeCell"/> world positions.
    /// </summary>
    public static bool TrySnapSerializedGridWorldOffsetToPixelCells(
        SerializedObject serializedGrid,
        Transform aStarGridTransform,
        Transform pixelCubeRoot)
    {
        if (serializedGrid == null || aStarGridTransform == null || pixelCubeRoot == null)
        {
            return false;
        }

        SerializedProperty gridMinProperty = serializedGrid.FindProperty("_gridMin");
        SerializedProperty gridSizeProperty = serializedGrid.FindProperty("_gridSize");
        SerializedProperty cellSizeProperty = serializedGrid.FindProperty("_cellSize");
        SerializedProperty worldOffsetProperty = serializedGrid.FindProperty("_worldOffset");
        if (gridMinProperty == null ||
            gridSizeProperty == null ||
            cellSizeProperty == null ||
            worldOffsetProperty == null)
        {
            return false;
        }

        Vector2Int gridMin = gridMinProperty.vector2IntValue;
        Vector2Int gridSize = gridSizeProperty.vector2IntValue;
        if (gridSize.x <= 0 || gridSize.y <= 0)
        {
            return false;
        }

        RectInt bounds = new RectInt(gridMin.x, gridMin.y, gridSize.x, gridSize.y);
        Vector2 cellSize = cellSizeProperty.vector2Value;
        if (cellSize.x <= 0f || cellSize.y <= 0f)
        {
            return false;
        }

        PixelCubeCell[] cells = pixelCubeRoot.GetComponentsInChildren<PixelCubeCell>(true);
        PixelCubeCell referenceCell = null;
        PixelCubeCell fallbackLexMin = null;
        for (int i = 0; i < cells.Length; i++)
        {
            PixelCubeCell cell = cells[i];
            if (cell == null)
            {
                continue;
            }

            if (fallbackLexMin == null ||
                cell.GridPosition.y < fallbackLexMin.GridPosition.y ||
                (cell.GridPosition.y == fallbackLexMin.GridPosition.y && cell.GridPosition.x < fallbackLexMin.GridPosition.x))
            {
                fallbackLexMin = cell;
            }

            if (!bounds.Contains(cell.GridPosition))
            {
                continue;
            }

            if (referenceCell == null ||
                cell.GridPosition.y < referenceCell.GridPosition.y ||
                (cell.GridPosition.y == referenceCell.GridPosition.y && cell.GridPosition.x < referenceCell.GridPosition.x))
            {
                referenceCell = cell;
            }
        }

        if (referenceCell == null)
        {
            referenceCell = fallbackLexMin;
            if (referenceCell != null && !bounds.Contains(referenceCell.GridPosition))
            {
                Debug.LogWarning(
                    $"[AStarGrid] SnapWorldOffset: no {nameof(PixelCubeCell)}.{nameof(PixelCubeCell.GridPosition)} inside manual slab " +
                    $"{bounds.xMin},{bounds.yMin} .. {bounds.xMax - 1},{bounds.yMax - 1}. Using fallback '{referenceCell.name}' at " +
                    $"{referenceCell.GridPosition}. Align _gridMin/_gridSize with painted indices or regenerate.",
                    pixelCubeRoot.gameObject);
            }
        }

        if (referenceCell == null)
        {
            return false;
        }

        Vector2Int gridPosition = referenceCell.GridPosition;
        Vector3 cellLocal = aStarGridTransform.InverseTransformPoint(referenceCell.GetLatticeSnapReferenceWorldCenter());
        Vector2Int localGrid = gridPosition - new Vector2Int(bounds.xMin, bounds.yMin);
        Vector3 halfCell = new Vector3(cellSize.x * 0.5f, cellSize.y * 0.5f, 0f);
        Vector3 lattice = new Vector3(localGrid.x * cellSize.x, localGrid.y * cellSize.y, 0f);
        Vector3 neededOffset = cellLocal - halfCell - lattice;
        Vector3 currentOffset = worldOffsetProperty.vector3Value;
        Vector3 newOffset = new Vector3(neededOffset.x, neededOffset.y, cellLocal.z);
        if ((newOffset - currentOffset).sqrMagnitude < 1e-10f)
        {
            return false;
        }

        worldOffsetProperty.vector3Value = newOffset;
        return true;
    }

    public static float TryInferCellStepFromPixelRoot(Transform pixelCubeRoot)
    {
        if (TryInferCellStepFromPixelRoot(pixelCubeRoot, out float cellStep))
        {
            return cellStep;
        }

        return 1f;
    }

    public static bool TryInferCellStepFromPixelRoot(Transform pixelCubeRoot, out float cellStep)
    {
        cellStep = 1f;
        if (pixelCubeRoot == null)
        {
            return false;
        }

        PixelCubeCell[] cells = pixelCubeRoot.GetComponentsInChildren<PixelCubeCell>(true);
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            PixelCubeCell a = cells[i];
            if (a == null)
            {
                continue;
            }

            for (int j = i + 1; j < cells.Length; j++)
            {
                PixelCubeCell b = cells[j];
                if (b == null)
                {
                    continue;
                }

                Vector2Int delta = b.GridPosition - a.GridPosition;
                Vector3 aCenter = a.GetLatticeSnapReferenceWorldCenter();
                Vector3 bCenter = b.GetLatticeSnapReferenceWorldCenter();
                if (delta.x == 1 && delta.y == 0)
                {
                    float dx = Mathf.Abs(bCenter.x - aCenter.x);
                    if (dx > 1e-5f)
                    {
                        sum += dx;
                        count++;
                    }
                }

                if (delta.x == 0 && delta.y == 1)
                {
                    float dy = Mathf.Abs(bCenter.y - aCenter.y);
                    if (dy > 1e-5f)
                    {
                        sum += dy;
                        count++;
                    }
                }
            }
        }

        if (count == 0)
        {
            return false;
        }

        cellStep = Mathf.Max(0.0001f, sum / count);
        return true;
    }

}
