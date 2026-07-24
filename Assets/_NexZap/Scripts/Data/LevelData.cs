using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NexZap.Data
{
    [Serializable]
    public struct SelectionLineConfig
    {
        [EnumToggleButtons]
        public BlockColor[] blocks;
    }

    [CreateAssetMenu(fileName = "LevelData", menuName = "NexZap/Level Data")]
    public class LevelData : SerializedScriptableObject
    {
        [BoxGroup("Kích thước"), MinValue(1), OnValueChanged(nameof(EnsureGridSize))]
        public int width = 8;

        [BoxGroup("Kích thước"), MinValue(1), OnValueChanged(nameof(EnsureGridSize))]
        public int height = 8;

        [Title("Hình mẫu Pixel")]
        [EnumToggleButtons, OnValueChanged(nameof(SyncPaint))]
        [Tooltip("Màu cọ. Click trái vào ô = tô màu này, click phải = xoá ô (None).")]
        public BlockColor paintColor = BlockColor.Red;

        [TableMatrix(SquareCells = true, DrawElementMethod = "DrawCell", HideColumnIndices = true, HideRowIndices = true)]
        [SerializeField]
        private BlockColor[,] grid;

        [Title("Line chọn của người chơi")]
        public SelectionLineConfig[] selectionLines;

        private static BlockColor paintBrush = BlockColor.Red;

        public BlockColor GetCell(int x, int y)
        {
            if (grid == null || x < 0 || y < 0 || x >= width || y >= height)
            {
                return BlockColor.None;
            }

            // Odin vẽ [0,0] ở góc trên-trái; game dùng y=0 ở dưới nên lật trục dọc cho khớp WYSIWYG.
            return grid[x, height - 1 - y];
        }

        public void SetCell(int x, int y, BlockColor color)
        {
            EnsureGridSize();
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            grid[x, height - 1 - y] = color;
        }

        public void EnsureGridSize()
        {
            if (grid != null && grid.GetLength(0) == width && grid.GetLength(1) == height)
            {
                return;
            }

            var newGrid = new BlockColor[Mathf.Max(1, width), Mathf.Max(1, height)];
            if (grid != null)
            {
                var copyW = Mathf.Min(newGrid.GetLength(0), grid.GetLength(0));
                var copyH = Mathf.Min(newGrid.GetLength(1), grid.GetLength(1));
                for (var x = 0; x < copyW; x++)
                {
                    for (var y = 0; y < copyH; y++)
                    {
                        newGrid[x, y] = grid[x, y];
                    }
                }
            }

            grid = newGrid;
        }

        private void SyncPaint()
        {
            paintBrush = paintColor;
        }

#if UNITY_EDITOR
        [BoxGroup("Kích thước")]
        [Button("Xoá toàn bộ lưới")]
        private void ClearGrid()
        {
            grid = new BlockColor[Mathf.Max(1, width), Mathf.Max(1, height)];
        }

        // Vẽ 1 ô trong TableMatrix: hiển thị màu + cho click để tô/xoá.
        private static BlockColor DrawCell(Rect rect, BlockColor value)
        {
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                value = Event.current.button == 1 ? BlockColor.None : paintBrush;
                GUI.changed = true;
                Event.current.Use();
            }

            var inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            var color = value == BlockColor.None
                ? new Color(0.18f, 0.18f, 0.2f)
                : ColorPalette.GetUnityColor(value);
            UnityEditor.EditorGUI.DrawRect(inner, color);
            return value;
        }
#endif
    }
}
