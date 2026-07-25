using System.Collections.Generic;
using NexZap.Data;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class PixelBoard : MonoBehaviour
    {
        [SerializeField] private PixelCell cellPrefab;
        [SerializeField] private PixelMaterialLibrary materialLibrary;
        [SerializeField] private float cellSize = 0.5f;
        [SerializeField, Min(0.01f)] private float pixelDepth = 0.25f;
        [SerializeField] private Transform cellsRoot;

        // Kích thước hiển thị (X/Y) của mỗi pixel, lấy từ BaseLevel.pixelScale. Dùng cho cả size ô lẫn bounds (để path bám theo).
        private Vector2 pixelExtent = Vector2.one;

        [Tooltip("Chỉ fill pixel nằm trong khoảng góc này (độ) so với hướng block -> tâm board (tính từ vị trí block).")]
        [SerializeField] private float fillAngleTolerance;

        [Tooltip("Mỗi lần block chạy 1 vòng, cho phép fill tối đa bao nhiêu lớp peel (BFS) cùng màu. 1 = từng lớp ngoài, 2+ = lan sâu hơn mỗi vòng.")]
        [SerializeField] private int fillPeelLayersPerWave = 1;

        private readonly List<PixelCell> cells = new();
        private readonly List<PixelCell> unfilledCells = new();
        private readonly Dictionary<Vector2Int, PixelCell> cellLookup = new();
        private readonly Dictionary<string, int> remainingTargetsByColor = new();

        // Snapshot lớp peel ngoài cùng cho 1 lần chạy block; visibility qua line-of-sight lúc TryFill.
        private HashSet<PixelCell> fillWaveSnapshot;

        private static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsComplete => unfilledCells.Count == 0;
        public int RemainingTarget { get; private set; }
        public int TotalTarget { get; private set; }
        public bool IsTargetComplete => RemainingTarget <= 0;
        public IReadOnlyDictionary<string, int> RemainingTargetsByColor => remainingTargetsByColor;
        public Bounds WorldBounds { get; private set; }

        public void Build(BaseLevel levelData)
        {
            Clear();

            cellSize = levelData.spacing;
            pixelExtent = GetNonOverlappingPixelExtent(levelData.pixelScale, cellSize);

            Width = levelData.width;
            Height = levelData.height;

            if (levelData.pixelMaterialLibrary != null)
            {
                materialLibrary = levelData.pixelMaterialLibrary;
            }

            var offsetX = -(Width - 1) * cellSize * 0.5f;
            var offsetY = -(Height - 1) * cellSize * 0.5f;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var targetColorId = levelData.GetCellColorId(x, y);
                    var cell = Instantiate(cellPrefab, cellsRoot);
                    cell.transform.localPosition = new Vector3(offsetX + x * cellSize, offsetY + y * cellSize, 0f);
                    var gridPos = new Vector2Int(x, y);
                    cell.Initialize(gridPos, targetColorId, pixelExtent, pixelDepth, materialLibrary);
                    if (!string.IsNullOrEmpty(targetColorId))
                    {
                        cell.ShowAsExistingColor();
                        RemainingTarget++;
                        remainingTargetsByColor.TryGetValue(targetColorId, out var colorCount);
                        remainingTargetsByColor[targetColorId] = colorCount + 1;
                    }
                    cells.Add(cell);
                    cellLookup[gridPos] = cell;
                }
            }

            TotalTarget = RemainingTarget;

            RecalculateBounds();
            RefreshFillableFlags();
        }

        public bool TryResolveDrop(Vector3 worldPosition, string[] colorIds, out int removedCount)
        {
            removedCount = 0;
            if (colorIds == null || colorIds.Length == 0 || string.IsNullOrEmpty(colorIds[0]))
            {
                return false;
            }

            var dropPosition = WorldToGrid(worldPosition);
            if (!cellLookup.TryGetValue(dropPosition, out var dropCell) || !dropCell.IsEmpty)
            {
                return false;
            }

            var matchedCells = new HashSet<PixelCell>();
            var matchedColors = new HashSet<string>();

            // Collect matches for all colors in the block
            foreach (var colorId in colorIds)
            {
                var matchedForColor = new HashSet<PixelCell>();
                var queue = new Queue<PixelCell>();
                
                foreach (var offset in Neighbors)
                {
                    if (cellLookup.TryGetValue(dropPosition + offset, out var neighbor) &&
                        neighbor.TargetColorId == colorId)
                    {
                        matchedForColor.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                if (queue.Count > 0)
                {
                    matchedColors.Add(colorId);
                    
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var offset in Neighbors)
                        {
                            if (!cellLookup.TryGetValue(current.GridPosition + offset, out var neighbor) ||
                                neighbor.TargetColorId != colorId || !matchedForColor.Add(neighbor))
                            {
                                continue;
                            }

                            queue.Enqueue(neighbor);
                        }
                    }
                    
                    foreach (var m in matchedForColor) matchedCells.Add(m);
                }
            }

            // Logic for dropping
            if (matchedColors.Count == 0)
            {
                // No matches
                if (colorIds.Length == 1)
                {
                    // Single color: just place it
                    dropCell.SetPlacedColor(colorIds[0]);
                    return true;
                }
                else
                {
                    // Dual color: if neither matches, it bounces back (cannot place 2 colors in 1 cell)
                    return false;
                }
            }

            // Apply removals
            var removedTargetCount = 0;
            foreach (var cell in matchedCells)
            {
                if (cell.CountsTowardTarget)
                {
                    removedTargetCount++;
                }
                
                var cellColor = cell.TargetColorId;
                cell.ClearColor();

                if (!string.IsNullOrEmpty(cellColor))
                {
                    remainingTargetsByColor.TryGetValue(cellColor, out var remainingForColor);
                    remainingTargetsByColor[cellColor] = Mathf.Max(0, remainingForColor - 1);
                }
            }

            removedCount = matchedCells.Count;
            RemainingTarget = Mathf.Max(0, RemainingTarget - removedTargetCount);

            // Handle remainder for dual color block
            if (colorIds.Length > 1 && matchedColors.Count == 1)
            {
                var remainingColor = matchedColors.Contains(colorIds[0]) ? colorIds[1] : colorIds[0];
                dropCell.SetPlacedColor(remainingColor);
            }

            return true;
        }

        public bool TryGetDropWorldPosition(Vector3 worldPosition, out Vector3 snappedPosition)
        {
            var gridPosition = WorldToGrid(worldPosition);
            if (cellLookup.TryGetValue(gridPosition, out var cell) && cell.IsEmpty)
            {
                snappedPosition = cell.transform.position;
                return true;
            }

            snappedPosition = worldPosition;
            return false;
        }

        public void ApplyLayout(BaseLevel levelData)
        {
            cellSize = levelData.spacing;
            pixelExtent = GetNonOverlappingPixelExtent(levelData.pixelScale, cellSize);

            var offsetX = -(Width - 1) * cellSize * 0.5f;
            var offsetY = -(Height - 1) * cellSize * 0.5f;

            foreach (var cell in cells)
            {
                var gridPos = cell.GridPosition;
                cell.transform.localPosition = new Vector3(offsetX + gridPos.x * cellSize, offsetY + gridPos.y * cellSize, 0f);
                cell.SetSize(pixelExtent, pixelDepth);
            }

            RecalculateBounds();
        }

        private static Vector2 GetNonOverlappingPixelExtent(Vector2 requestedSize, float spacing)
        {
            var maxSize = Mathf.Max(0.01f, spacing);
            return new Vector2(
                Mathf.Clamp(requestedSize.x, 0.01f, maxSize),
                Mathf.Clamp(requestedSize.y, 0.01f, maxSize));
        }

        private void RefreshFillableFlags()
        {
            foreach (var cell in cells)
            {
                cell.SetFillableFlag(!cell.IsFilled);
            }
        }

        // Chụp các lớp peel ngoài (minLayer .. minLayer + fillPeelLayersPerWave - 1).
        public void BeginFillWave(string colorId)
        {
            var layers = ComputeUnfilledColorLayers(colorId);
            var minLayer = int.MaxValue;

            foreach (var cell in unfilledCells)
            {
                if (cell.TargetColorId != colorId)
                {
                    continue;
                }

                var layer = layers.TryGetValue(cell, out var assignedLayer) ? assignedLayer : 0;
                if (layer < minLayer)
                {
                    minLayer = layer;
                }
            }

            var maxLayer = minLayer + fillPeelLayersPerWave - 1;
            fillWaveSnapshot = new HashSet<PixelCell>();
            foreach (var cell in unfilledCells)
            {
                if (cell.TargetColorId != colorId)
                {
                    continue;
                }

                var layer = layers.TryGetValue(cell, out var assignedLayer) ? assignedLayer : 0;
                if (layer < minLayer || layer > maxLayer)
                {
                    continue;
                }

                fillWaveSnapshot.Add(cell);
                cell.SetFillableFlag(true);
            }
        }

        // BFS trong pixel cùng màu chưa fill. Lớp 0 = kề None hoặc mép lưới (không seed bằng kề màu khác).
        private Dictionary<PixelCell, int> ComputeUnfilledColorLayers(string colorId)
        {
            var layers = new Dictionary<PixelCell, int>();
            var queue = new Queue<PixelCell>();

            foreach (var cell in unfilledCells)
            {
                if (cell.TargetColorId != colorId || !IsUnfilledColorRegionFrontier(cell))
                {
                    continue;
                }

                layers[cell] = 0;
                queue.Enqueue(cell);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var nextLayer = layers[current] + 1;

                foreach (var offset in Neighbors)
                {
                    var neighborPos = current.GridPosition + offset;
                    if (!cellLookup.TryGetValue(neighborPos, out var neighbor))
                    {
                        continue;
                    }

                    if (neighbor.IsFilled || neighbor.TargetColorId != colorId || layers.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    layers[neighbor] = nextLayer;
                    queue.Enqueue(neighbor);
                }
            }

            return layers;
        }

        private bool IsUnfilledColorRegionFrontier(PixelCell cell)
        {
            if (cell.IsFilled)
            {
                return false;
            }

            return IsOnGridBoundary(cell) || HasNoneNeighbor(cell);
        }

        private bool HasNoneNeighbor(PixelCell cell)
        {
            foreach (var offset in Neighbors)
            {
                var neighborPos = cell.GridPosition + offset;
                if (neighborPos.x < 0 || neighborPos.y < 0 || neighborPos.x >= Width || neighborPos.y >= Height)
                {
                    continue;
                }

                if (!cellLookup.ContainsKey(neighborPos))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOnGridBoundary(PixelCell cell)
        {
            var pos = cell.GridPosition;
            return pos.x == 0 || pos.y == 0 || pos.x == Width - 1 || pos.y == Height - 1;
        }

        public void EndFillWave()
        {
            fillWaveSnapshot = null;
        }

        // Fill pixel cùng màu gần block nhất: hướng về board + không bị pixel khác màu chặn giữa block và đích.
        public bool TryFillNearest(string colorId, Vector3 worldPosition)
        {
            var bestIndex = -1;
            var bestDistance = float.MaxValue;

            var center = WorldBounds.center;
            var towardCenterAngle = Mathf.Atan2(center.y - worldPosition.y, center.x - worldPosition.x) * Mathf.Rad2Deg;

            for (var i = 0; i < unfilledCells.Count; i++)
            {
                var cell = unfilledCells[i];
                if (cell.TargetColorId != colorId)
                {
                    continue;
                }

                if (fillWaveSnapshot != null && !fillWaveSnapshot.Contains(cell))
                {
                    continue;
                }

                if (IsBlockedFromBlock(cell, worldPosition, colorId))
                {
                    continue;
                }

                var cellPosition = cell.transform.position;
                var towardCellAngle = Mathf.Atan2(cellPosition.y - worldPosition.y, cellPosition.x - worldPosition.x) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(towardCellAngle, towardCenterAngle)) > fillAngleTolerance)
                {
                    continue;
                }

                var distance = (cellPosition - worldPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            var chosen = unfilledCells[bestIndex];
            chosen.SetFillableFlag(true);
            if (!chosen.TryFill(colorId))
            {
                return false;
            }

            unfilledCells.RemoveAt(bestIndex);
            fillWaveSnapshot?.Remove(chosen);
            RefreshFillableFlags();
            return true;
        }

        // Chặn nếu có pixel khác màu trên đường cardinal (kể cả trục phụ), Bresenham, hoặc tia world.
        private bool IsBlockedFromBlock(PixelCell target, Vector3 blockWorldPos, string colorId)
        {
            var targetGrid = target.GridPosition;
            var targetWorld = target.transform.position;

            if (IsBlockedOnCardinalScan(target, blockWorldPos, colorId, useSecondaryAxis: false))
            {
                return true;
            }

            if (IsBlockedOnCardinalScan(target, blockWorldPos, colorId, useSecondaryAxis: true))
            {
                return true;
            }

            if (IsBlockedOnGridLine(targetGrid, WorldToGrid(blockWorldPos), colorId))
            {
                return true;
            }

            if (IsBlockedByNeighborTowardBlock(targetGrid, blockWorldPos, colorId))
            {
                return true;
            }

            return IsBlockedOnWorldRay(blockWorldPos, targetWorld, targetGrid, colorId);
        }

        // Pixel kề ngay hướng block là màu khác chưa fill (vd. cánh sát bên cùng hàng).
        private bool IsBlockedByNeighborTowardBlock(Vector2Int targetGrid, Vector3 blockWorldPos, string colorId)
        {
            var blockGrid = WorldToGrid(blockWorldPos);
            var targetWorld = GridToWorldCenter(targetGrid);

            if (blockGrid.x != targetGrid.x)
            {
                var stepX = blockGrid.x > targetGrid.x ? Vector2Int.right : Vector2Int.left;
                if (IsBlockingCell(targetGrid + stepX, colorId))
                {
                    return true;
                }
            }

            if (blockGrid.y != targetGrid.y)
            {
                var stepY = blockGrid.y > targetGrid.y ? Vector2Int.up : Vector2Int.down;
                if (IsBlockingCell(targetGrid + stepY, colorId))
                {
                    return true;
                }
            }

            // Block ở dước: cánh cùng hàng thường nằm trái/phải, không trên cột quét dọc.
            if (blockWorldPos.y < targetWorld.y - cellSize * 0.15f)
            {
                if (IsBlockingCell(targetGrid + Vector2Int.left, colorId)
                    || IsBlockingCell(targetGrid + Vector2Int.right, colorId))
                {
                    return true;
                }

                if (IsBlockingCell(targetGrid + Vector2Int.down, colorId)
                    || IsBlockingCell(targetGrid + Vector2Int.down + Vector2Int.left, colorId)
                    || IsBlockingCell(targetGrid + Vector2Int.down + Vector2Int.right, colorId))
                {
                    return true;
                }
            }

            return false;
        }

        // Quét 1 hướng chính (hoặc trục phụ vuông góc) từ pixel về phía block.
        private bool IsBlockedOnCardinalScan(PixelCell target, Vector3 blockWorldPos, string colorId, bool useSecondaryAxis)
        {
            var targetWorld = target.transform.position;
            var dx = blockWorldPos.x - targetWorld.x;
            var dy = blockWorldPos.y - targetWorld.y;
            if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dy) < 0.0001f)
            {
                return false;
            }

            var minAxisOffset = cellSize * 0.2f;
            Vector2Int gridStep;
            if (useSecondaryAxis)
            {
                if (Mathf.Abs(dx) < minAxisOffset || Mathf.Abs(dy) < minAxisOffset)
                {
                    return false;
                }

                gridStep = Mathf.Abs(dy) >= Mathf.Abs(dx)
                    ? dx > 0 ? Vector2Int.right : Vector2Int.left
                    : dy > 0 ? Vector2Int.up : Vector2Int.down;
            }
            else
            {
                gridStep = Mathf.Abs(dy) >= Mathf.Abs(dx)
                    ? dy > 0 ? Vector2Int.up : Vector2Int.down
                    : dx > 0 ? Vector2Int.right : Vector2Int.left;
            }

            var grid = target.GridPosition;
            while (true)
            {
                grid += gridStep;

                if (grid.x < 0 || grid.y < 0 || grid.x >= Width || grid.y >= Height)
                {
                    break;
                }

                if (HasPassedBlockOnAxis(GridToWorldCenter(grid), blockWorldPos, gridStep))
                {
                    break;
                }

                if (IsBlockingCell(grid, colorId))
                {
                    return true;
                }
            }

            return false;
        }

        // Bresenham trên lưới pixel -> block (bắt cánh lệch cột khi block ở góc path).
        private bool IsBlockedOnGridLine(Vector2Int from, Vector2Int to, string colorId)
        {
            if (from == to)
            {
                return false;
            }

            var x = from.x;
            var y = from.y;
            var dx = Mathf.Abs(to.x - from.x);
            var dy = Mathf.Abs(to.y - from.y);
            var sx = from.x < to.x ? 1 : -1;
            var sy = from.y < to.y ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                if (x != from.x || y != from.y)
                {
                    if (x == to.x && y == to.y)
                    {
                        break;
                    }

                    if (IsBlockingCell(new Vector2Int(x, y), colorId))
                    {
                        return true;
                    }
                }

                if (x == to.x && y == to.y)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return false;
        }

        // Tia world block -> pixel; bước nhỏ để không bỏ sót pixel chặn mỏng (cánh).
        private bool IsBlockedOnWorldRay(Vector3 blockWorldPos, Vector3 targetWorldPos, Vector2Int targetGrid, string colorId)
        {
            var delta = targetWorldPos - blockWorldPos;
            var distance = delta.magnitude;
            if (distance <= cellSize * 0.15f)
            {
                return false;
            }

            var direction = delta / distance;
            var step = cellSize * 0.25f;
            var lastGrid = new Vector2Int(int.MinValue, int.MinValue);

            for (var traveled = step; traveled < distance - step * 0.15f; traveled += step)
            {
                var grid = WorldToGrid(blockWorldPos + direction * traveled);
                if (grid == targetGrid || grid == lastGrid)
                {
                    continue;
                }

                lastGrid = grid;
                if (IsBlockingCell(grid, colorId))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var local = cellsRoot.InverseTransformPoint(worldPosition);
            var offsetX = -(Width - 1) * cellSize * 0.5f;
            var offsetY = -(Height - 1) * cellSize * 0.5f;
            var x = Mathf.RoundToInt((local.x - offsetX) / cellSize);
            var y = Mathf.RoundToInt((local.y - offsetY) / cellSize);
            return new Vector2Int(x, y);
        }

        private static bool HasPassedBlockOnAxis(Vector3 cellWorld, Vector3 blockWorld, Vector2Int gridStep)
        {
            if (gridStep.y > 0)
            {
                return cellWorld.y > blockWorld.y;
            }

            if (gridStep.y < 0)
            {
                return cellWorld.y < blockWorld.y;
            }

            if (gridStep.x > 0)
            {
                return cellWorld.x > blockWorld.x;
            }

            return cellWorld.x < blockWorld.x;
        }

        private Vector3 GridToWorldCenter(Vector2Int grid)
        {
            var offsetX = -(Width - 1) * cellSize * 0.5f;
            var offsetY = -(Height - 1) * cellSize * 0.5f;
            var local = new Vector3(offsetX + grid.x * cellSize, offsetY + grid.y * cellSize, 0f);
            return cellsRoot.TransformPoint(local);
        }

        // Pixel khác màu chưa fill trên đường tới block = chặn. Ô None / cùng màu / đã fill = không chặn.
        private bool IsBlockingCell(Vector2Int pos, string colorId)
        {
            if (pos.x < 0 || pos.y < 0 || pos.x >= Width || pos.y >= Height)
            {
                return false;
            }

            if (!cellLookup.TryGetValue(pos, out var cell))
            {
                return false;
            }

            return !cell.IsFilled && cell.TargetColorId != colorId;
        }

        private void RecalculateBounds()
        {
            var totalWidth = Mathf.Max(1, Width) * cellSize;
            var totalHeight = Mathf.Max(1, Height) * cellSize;
            WorldBounds = new Bounds(cellsRoot.position, new Vector3(totalWidth, totalHeight, cellSize));
        }

        private void Clear()
        {
            foreach (var cell in cells)
            {
                if (cell != null)
                {
                    Destroy(cell.gameObject);
                }
            }

            cells.Clear();
            unfilledCells.Clear();
            cellLookup.Clear();
            remainingTargetsByColor.Clear();
            fillWaveSnapshot = null;
            RemainingTarget = 0;
            TotalTarget = 0;
        }

        private void Reset()
        {
            cellsRoot = transform;
        }
    }
}
