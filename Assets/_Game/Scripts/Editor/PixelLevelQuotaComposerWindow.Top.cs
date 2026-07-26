using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class PixelLevelQuotaComposerWindow
{
    private struct PaintedCellData
    {
        public Vector2Int Position;
        public PixelCubeColor Color;
        public int TimingSeconds;
    }

    private enum TopEditTool
    {
        Paint,
        Erase,
        Key,
    }

    private const int MinTopGridSize = 4;
    private const int FixedTopGridWidth = 30;
    private const int MaxTopGridHeight = 100;
    private const int MaxTopPaintedSpanForFrameFit = 20;

    private TopEditTool _topEditTool = TopEditTool.Paint;
    private int _topGridWidth = FixedTopGridWidth;
    private int _topGridHeight = 12;
    private float _cubeSize = 1f;
    private float _cubeSpacing = 0.02f;
    private bool _centerPivot = true;
    private PixelCubeColor _selectedTopBrushColor = PixelCubeColor.Red;
    private int _selectedTopTimingSeconds;
    private PixelCubeColor[,] _topCellColors;
    private int[,] _topCellTimingSeconds;
    private bool[,] _topCellIsKey;
    private GameObject _topCellPrefab;
    private KeyPickup _keyPrefab;

    private void TryPaintTopCell(int x, int y, PixelCubeColor targetColor)
    {
        PixelCubeColor currentColor = _topCellColors[x, y];
        if (currentColor == targetColor)
        {
            if (targetColor != PixelCubeColor.None &&
                _topCellTimingSeconds != null &&
                _topCellTimingSeconds[x, y] != Mathf.Max(0, _selectedTopTimingSeconds))
            {
                _topCellTimingSeconds[x, y] = Mathf.Max(0, _selectedTopTimingSeconds);
                Repaint();
            }

            return;
        }

        if (targetColor != PixelCubeColor.None)
        {
            int usedExcludingThisCell = CountTopCellsForColor(targetColor) - (currentColor == targetColor ? 1 : 0);
            if (usedExcludingThisCell >= GetTargetCount(targetColor))
            {
                ShowNotification(new GUIContent($"No top quota left for {targetColor}."));
                return;
            }
        }

        _topCellColors[x, y] = targetColor;
        _topCellTimingSeconds[x, y] = targetColor == PixelCubeColor.None ? 0 : Mathf.Max(0, _selectedTopTimingSeconds);
        if (targetColor != PixelCubeColor.None && _topCellIsKey != null)
        {
            if (TryFindKeyAnchorCovering(x, y, out int ax, out int ay))
            {
                _topCellIsKey[ax, ay] = false;
            }
        }
        Repaint();
    }

    private void TryToggleTopCellKey(int x, int y)
    {
        if (_topCellIsKey == null)
        {
            return;
        }

        if (TryFindKeyAnchorCovering(x, y, out int existingAx, out int existingAy))
        {
            _topCellIsKey[existingAx, existingAy] = false;
            Repaint();
            return;
        }

        int w = _topCellIsKey.GetLength(0);
        int h = _topCellIsKey.GetLength(1);
        if (x + 1 >= w || y + 1 >= h)
        {
            ShowNotification(new GUIContent("Key needs 2x2 space — move away from the top/right edge."));
            return;
        }

        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                if (_topCellColors[x + dx, y + dy] != PixelCubeColor.None)
                {
                    ShowNotification(new GUIContent("Key footprint must cover empty cells — all four 2x2 cells must be unpainted."));
                    return;
                }

                if (TryFindKeyAnchorCovering(x + dx, y + dy, out int _, out int _))
                {
                    ShowNotification(new GUIContent("Key footprint overlaps another key."));
                    return;
                }
            }
        }

        _topCellIsKey[x, y] = true;
        Repaint();
    }

    private bool TryFindKeyAnchorCovering(int x, int y, out int anchorX, out int anchorY)
    {
        anchorX = 0;
        anchorY = 0;
        if (_topCellIsKey == null)
        {
            return false;
        }

        int w = _topCellIsKey.GetLength(0);
        int h = _topCellIsKey.GetLength(1);
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                int cx = x - dx;
                int cy = y - dy;
                if (cx < 0 || cy < 0 || cx >= w || cy >= h)
                {
                    continue;
                }

                if (_topCellIsKey[cx, cy])
                {
                    anchorX = cx;
                    anchorY = cy;
                    return true;
                }
            }
        }

        return false;
    }

    private int CountKeyAnchors()
    {
        if (_topCellIsKey == null)
        {
            return 0;
        }

        int count = 0;
        int w = _topCellIsKey.GetLength(0);
        int h = _topCellIsKey.GetLength(1);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (_topCellIsKey[x, y])
                {
                    count++;
                }
            }
        }
        return count;
    }

    private void FillTopEmptyCellsWithSelectedColor()
    {
        int remaining = GetTopRemaining(_selectedTopBrushColor);
        if (remaining <= 0)
        {
            ShowNotification(new GUIContent($"No top quota left for {_selectedTopBrushColor}."));
            return;
        }

        for (int y = 0; y < _topGridHeight; y++)
        {
            for (int x = 0; x < _topGridWidth; x++)
            {
                if (remaining <= 0)
                {
                    Repaint();
                    return;
                }

                if (_topCellColors[x, y] != PixelCubeColor.None)
                {
                    continue;
                }

                _topCellColors[x, y] = _selectedTopBrushColor;
                _topCellTimingSeconds[x, y] = Mathf.Max(0, _selectedTopTimingSeconds);
                remaining--;
            }
        }

        Repaint();
    }

    private void ClearTopGrid()
    {
        for (int x = 0; x < _topGridWidth; x++)
        {
            for (int y = 0; y < _topGridHeight; y++)
            {
                _topCellColors[x, y] = PixelCubeColor.None;
                _topCellTimingSeconds[x, y] = 0;
                if (_topCellIsKey != null)
                {
                    _topCellIsKey[x, y] = false;
                }
            }
        }

        Repaint();
    }

    private void InitializeTopGridIfNeeded()
    {
        if (_topCellColors == null || _topCellColors.GetLength(0) != _topGridWidth || _topCellColors.GetLength(1) != _topGridHeight)
        {
            _topCellColors = new PixelCubeColor[_topGridWidth, _topGridHeight];
        }

        if (_topCellTimingSeconds == null ||
            _topCellTimingSeconds.GetLength(0) != _topGridWidth ||
            _topCellTimingSeconds.GetLength(1) != _topGridHeight)
        {
            _topCellTimingSeconds = new int[_topGridWidth, _topGridHeight];
        }

        if (_topCellIsKey == null || _topCellIsKey.GetLength(0) != _topGridWidth || _topCellIsKey.GetLength(1) != _topGridHeight)
        {
            _topCellIsKey = new bool[_topGridWidth, _topGridHeight];
        }
    }

    private void ResizeTopGrid(int newWidth, int newHeight)
    {
        PixelCubeColor[,] newGrid = new PixelCubeColor[newWidth, newHeight];
        int[,] newTimingGrid = new int[newWidth, newHeight];
        bool[,] newKeyGrid = new bool[newWidth, newHeight];
        if (_topCellColors != null)
        {
            int copyWidth = Mathf.Min(_topCellColors.GetLength(0), newWidth);
            int copyHeight = Mathf.Min(_topCellColors.GetLength(1), newHeight);
            for (int x = 0; x < copyWidth; x++)
            {
                for (int y = 0; y < copyHeight; y++)
                {
                    newGrid[x, y] = _topCellColors[x, y];
                    if (_topCellTimingSeconds != null &&
                        x < _topCellTimingSeconds.GetLength(0) &&
                        y < _topCellTimingSeconds.GetLength(1))
                    {
                        newTimingGrid[x, y] = Mathf.Max(0, _topCellTimingSeconds[x, y]);
                    }

                    if (_topCellIsKey != null &&
                        x < _topCellIsKey.GetLength(0) &&
                        y < _topCellIsKey.GetLength(1))
                    {
                        newKeyGrid[x, y] = _topCellIsKey[x, y];
                    }
                }
            }
        }

        _topCellColors = newGrid;
        _topCellTimingSeconds = newTimingGrid;
        _topCellIsKey = newKeyGrid;
        _topGridWidth = newWidth;
        _topGridHeight = newHeight;
    }

    private int CountTopCellsForColor(PixelCubeColor color)
    {
        int count = 0;
        if (_topCellColors == null)
        {
            return 0;
        }

        for (int x = 0; x < _topCellColors.GetLength(0); x++)
        {
            for (int y = 0; y < _topCellColors.GetLength(1); y++)
            {
                if (_topCellColors[x, y] == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int GetTopRemaining(PixelCubeColor color)
    {
        return GetTargetCount(color) - CountTopCellsForColor(color);
    }

    private List<PaintedCellData> CollectTopPaintedCells()
    {
        List<PaintedCellData> filled = new List<PaintedCellData>();
        if (_topCellColors == null)
        {
            return filled;
        }

        for (int x = 0; x < _topCellColors.GetLength(0); x++)
        {
            for (int y = 0; y < _topCellColors.GetLength(1); y++)
            {
                PixelCubeColor color = _topCellColors[x, y];
                if (color == PixelCubeColor.None)
                {
                    continue;
                }

                filled.Add(new PaintedCellData
                {
                    Position = new Vector2Int(x, y),
                    Color = color,
                    TimingSeconds = _topCellTimingSeconds != null ? Mathf.Max(0, _topCellTimingSeconds[x, y]) : 0,
                });
            }
        }

        return filled;
    }

    private static void DisableSplitClusterGeneration(string prefabPath)
    {
        PixelClusterRigidbody clusterPrefab = AssetDatabase.LoadAssetAtPath<PixelClusterRigidbody>(prefabPath);
        if (clusterPrefab == null)
        {
            return;
        }

        clusterPrefab.SetAllowFurtherSplitting(false);
        EditorUtility.SetDirty(clusterPrefab);
    }

    private GameObject BuildGeneratedBlockRoot(List<PaintedCellData> filled, string rootName)
    {
        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Pixel Level Root");

        root.AddComponent<PixelClusterRigidbody>();

        float step = _cubeSize + _cubeSpacing;
        Vector3 pivotOffset = Vector3.zero;
        if (_centerPivot)
        {
            GetPaintedBounds(filled, out int minX, out int maxX, out int minY, out int maxY);
            float xOffset = (minX + maxX) * step * 0.5f;
            float yOffset = (minY + maxY) * step * 0.5f;
            pivotOffset = new Vector3(xOffset, yOffset, 0f);
        }

        Dictionary<PixelCubeColor, Material> materialCache = new Dictionary<PixelCubeColor, Material>();
        for (int i = 0; i < filled.Count; i++)
        {
            PaintedCellData cell = filled[i];

            if (_topCellPrefab != null)
            {
                CreateCellFromPrefab(root.transform, cell, pivotOffset, step);
            }
            else
            {
                CreateCellFromPrimitiveCube(root.transform, cell, pivotOffset, step, materialCache);
            }
        }

        BakeKeysIntoRoot(root.transform, step, pivotOffset);

        float fitScale = GetTopFrameFitScale();
        if (fitScale < 0.9999f)
        {
            root.transform.localScale = Vector3.one * fitScale;
        }

        return root;
    }

    private void BakeKeysIntoRoot(Transform root, float step, Vector3 pivotOffset)
    {
        if (_topCellIsKey == null)
        {
            return;
        }

        if (_keyPrefab == null)
        {
            TryAutoAssignKeyPrefab();
        }

        int anchorCount = CountKeyAnchors();
        if (anchorCount == 0)
        {
            return;
        }

        if (_keyPrefab == null)
        {
            Debug.LogWarning($"[PixelLevelQuotaComposer] {anchorCount} key anchor(s) found but Key Prefab is not assigned (expected at '{DefaultKeyPrefabPath}'). No keys were baked into the block prefab.");
            return;
        }

        GameObject keyAsset = _keyPrefab.gameObject;
        int tw = _topGridWidth;
        int th = _topGridHeight;

        for (int ay = 0; ay < th; ay++)
        {
            for (int ax = 0; ax < tw; ax++)
            {
                if (ax >= _topCellIsKey.GetLength(0) || ay >= _topCellIsKey.GetLength(1))
                {
                    continue;
                }
                if (!_topCellIsKey[ax, ay])
                {
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(keyAsset);
                if (instance == null)
                {
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Bake Key");
                instance.transform.SetParent(root, false);
                float lx = (ax + 0.5f) * step - pivotOffset.x;
                float ly = (ay + 0.5f) * step - pivotOffset.y;
                instance.transform.localPosition = new Vector3(lx, ly, 0f);
                instance.transform.localRotation = Quaternion.identity;
                instance.name = $"Key_{ax}_{ay}";

                KeyPickup pickup = instance.GetComponent<KeyPickup>();
                if (pickup != null)
                {
                    pickup.SetAnchorGridPosition(new Vector2Int(ax, ay));
                }
            }
        }
    }

    private static void GetPaintedBounds(List<PaintedCellData> filled, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = 0;
        maxX = 0;
        minY = 0;
        maxY = 0;
        if (filled == null || filled.Count == 0)
        {
            return;
        }

        minX = int.MaxValue;
        maxX = int.MinValue;
        minY = int.MaxValue;
        maxY = int.MinValue;

        for (int i = 0; i < filled.Count; i++)
        {
            Vector2Int position = filled[i].Position;
            if (position.x < minX) minX = position.x;
            if (position.x > maxX) maxX = position.x;
            if (position.y < minY) minY = position.y;
            if (position.y > maxY) maxY = position.y;
        }
    }

    private float GetTopFrameFitScale()
    {
        int gridSpanX = Mathf.Max(1, _topGridWidth);
        int gridSpanY = Mathf.Max(1, _topGridHeight);
        int maxSpan = Mathf.Max(gridSpanX, gridSpanY);
        if (maxSpan <= 1)
        {
            return 1f;
        }

        // Fit by real world span, not just cell count:
        // worldSpan = (cellCount - 1) * (_cubeSize + _cubeSpacing) + _cubeSize
        float step = Mathf.Max(0.0001f, _cubeSize + _cubeSpacing);
        float worldSpan = ((maxSpan - 1) * step) + Mathf.Max(0.0001f, _cubeSize);
        float fitScale = MaxTopPaintedSpanForFrameFit / Mathf.Max(0.0001f, worldSpan);
        return Mathf.Min(1f, fitScale);
    }

    private void CreateCellFromPrefab(Transform parent, PaintedCellData cell, Vector3 pivotOffset, float step)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(_topCellPrefab) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(_topCellPrefab);
        }

        instance.name = $"{_topCellPrefab.name}_{cell.Position.x}_{cell.Position.y}";
        instance.transform.SetParent(parent, false);
        FitTopCellPrefabInstanceToCubeSize(instance);
        Vector3 localPosition = GetTopCellLocalPosition(cell, pivotOffset, step);
        instance.transform.localPosition = localPosition;
        SnapTopCellPrefabVisualCenter(instance, localPosition);

        PixelCubeCell cubeCell = instance.GetComponent<PixelCubeCell>();
        if (cubeCell == null)
        {
            cubeCell = instance.AddComponent<PixelCubeCell>();
        }

        cubeCell.Initialize(cell.Position, cell.Color);
    }

    private void FitTopCellPrefabInstanceToCubeSize(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        float targetSize = Mathf.Max(0.0001f, _cubeSize);
        if (!TryGetRendererBounds(instance, out Bounds bounds))
        {
            instance.transform.localScale = Vector3.one * targetSize;
            return;
        }

        float currentSize = Mathf.Max(bounds.size.x, bounds.size.y);
        if (currentSize <= 0.0001f)
        {
            instance.transform.localScale = Vector3.one * targetSize;
            return;
        }

        float scaleMultiplier = targetSize / currentSize;
        instance.transform.localScale *= scaleMultiplier;
    }

    private static void SnapTopCellPrefabVisualCenter(GameObject instance, Vector3 targetLocalCenter)
    {
        if (instance == null || instance.transform.parent == null)
        {
            return;
        }

        if (!TryGetRendererBounds(instance, out Bounds bounds))
        {
            return;
        }

        Vector3 currentLocalCenter = instance.transform.parent.InverseTransformPoint(bounds.center);
        Vector3 localDelta = targetLocalCenter - currentLocalCenter;
        instance.transform.localPosition += localDelta;
    }

    private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        if (instance == null)
        {
            return false;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private void CreateCellFromPrimitiveCube(
        Transform parent,
        PaintedCellData cell,
        Vector3 pivotOffset,
        float step,
        Dictionary<PixelCubeColor, Material> materialCache)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Cube_{cell.Position.x}_{cell.Position.y}";
        cube.transform.SetParent(parent, false);
        cube.transform.localScale = Vector3.one * _cubeSize;
        cube.transform.localPosition = GetTopCellLocalPosition(cell, pivotOffset, step);

        PixelCubeCell cubeCell = cube.AddComponent<PixelCubeCell>();
        cubeCell.Initialize(cell.Position, cell.Color);

        if (!materialCache.TryGetValue(cell.Color, out Material generatedMaterial))
        {
            generatedMaterial = GetMaterialForColor(cell.Color);
            materialCache[cell.Color] = generatedMaterial;
        }

        if (generatedMaterial != null)
        {
            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = generatedMaterial;
        }
    }

    private Vector3 GetTopCellLocalPosition(PaintedCellData cell, Vector3 pivotOffset, float step)
    {
        return new Vector3(cell.Position.x * step, cell.Position.y * step, 0f) - pivotOffset;
    }
}
