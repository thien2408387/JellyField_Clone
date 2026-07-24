using System.Collections.Generic;
using NexZap.Data;
using NexZap.Gameplay.Items;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class SelectionLineManager : MonoBehaviour
    {
        [SerializeField] private SelectionLine linePrefab;
        [SerializeField] private ColorBlockPool blockPool;
        [SerializeField] private Transform linesRoot;
        [SerializeField] private float lineSpacing = 1.1f;

        // Index 0 là line trên cùng (ưu tiên chọn trước).
        private readonly List<SelectionLine> lines = new();

        public IReadOnlyList<SelectionLine> Lines => lines;

        public void Build(BaseLevel levelData)
        {
            Clear();

            if (levelData.selectionLines == null)
            {
                return;
            }

            // List -> dùng .Count thay vì .Length
            var materialLibrary = levelData.ResolvePixelMaterialLibrary();
            var lineCount = Mathf.Min(levelData.selectionLines.Count, GameplayConstants.MaxSelectionLines);
            var topY = (lineCount - 1) * lineSpacing * 0.5f;

            for (var i = 0; i < lineCount; i++)
            {
                var line = Instantiate(linePrefab, linesRoot);
                line.Initialize(i, topY - i * lineSpacing);

                var configs = new List<(string colorId, int capacity)>();
                var lineConfig = levelData.selectionLines[i];
                if (lineConfig.blocks != null)
                {
                    foreach (var block in lineConfig.blocks)
                    {
                        if (!string.IsNullOrEmpty(block.colorId))
                        {
                            configs.Add((block.colorId, block.capacity));
                        }
                    }
                }

                line.Populate(configs, blockPool, materialLibrary);
                lines.Add(line);
            }

            AlignColumns();
            RefreshSelection();
        }

        /// <summary>
        /// Thứ tự chọn theo cột (giống pixel): trong mỗi cột, chỉ block trên cùng còn lại mới
        /// được phép chọn. Lấy block đó đi thì block ngay dưới nó (cùng cột) mở khóa, không cần
        /// phải hết cả hàng đầu mới chọn được hàng dưới.
        /// </summary>
        public void RefreshSelection()
        {
            var maxColumns = 0;
            foreach (var line in lines)
            {
                maxColumns = Mathf.Max(maxColumns, line.SlotCount);
            }

            for (var column = 0; column < maxColumns; column++)
            {
                var topFound = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    var block = lines[i].GetSlot(column);
                    if (block == null)
                    {
                        continue;
                    }

                    var selectable = !topFound;
                    block.SetSelectable(selectable);
                    block.SetDimmed(!selectable);
                    topFound = true;
                }
            }
        }

        // Canh tất cả line theo cùng số cột để các cột thẳng hàng dọc với nhau.
        private void AlignColumns()
        {
            var maxColumns = 0;
            foreach (var line in lines)
            {
                maxColumns = Mathf.Max(maxColumns, line.SlotCount);
            }

            foreach (var line in lines)
            {
                line.SetArrangeColumns(maxColumns);
            }
        }

        public SelectionLine GetLineAt(int index)
        {
            return index >= 0 && index < lines.Count ? lines[index] : null;
        }

        public void Clear()
        {
            foreach (var line in lines)
            {
                if (line != null)
                {
                    line.Clear();
                    Destroy(line.gameObject);
                }
            }

            lines.Clear();
        }

        private void Reset()
        {
            linesRoot = transform;
        }
    }
}
