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

                var configs = new List<(string[] colorIds, int capacity)>();
                var lineConfig = levelData.selectionLines[i];
                if (lineConfig.blocks != null)
                {
                    foreach (var block in lineConfig.blocks)
                    {
                        var ids = block.GetColorIds();
                        if (ids != null && ids.Length > 0 && !string.IsNullOrEmpty(ids[0]))
                        {
                            configs.Add((ids, block.capacity));
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
        /// Reveal đúng một block theo thứ tự line rồi tới thứ tự block đã cấu hình
        /// trong Odin. Block tiếp theo chỉ xuất hiện sau khi block hiện tại bị xóa
        /// khỏi line bởi một lần đặt thành công.
        /// </summary>
        public void RefreshSelection()
        {
            var revealedNextBlock = false;
            foreach (var line in lines)
            {
                for (var slot = 0; slot < line.SlotCount; slot++)
                {
                    var block = line.GetSlot(slot);
                    if (block == null)
                    {
                        continue;
                    }

                    var shouldReveal = !revealedNextBlock;
                    block.SetSequenceVisible(shouldReveal);
                    revealedNextBlock = true;
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
