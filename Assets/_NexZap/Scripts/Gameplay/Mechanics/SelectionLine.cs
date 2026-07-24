using System;
using System.Collections.Generic;
using NexZap.Data;
using NexZap.Gameplay.Items;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class SelectionLine : MonoBehaviour
    {
        [SerializeField] private Transform blocksRoot;
        [SerializeField] private float blockSpacing = 0.65f;
        [SerializeField] private float lineYOffset;

        // Mỗi block giữ nguyên cột (slot) của nó. Khi lấy block ra, slot để trống (null)
        // chứ KHÔNG dồn các block còn lại -> cột thẳng hàng dọc giữa các line.
        private ColorBlock[] slots = Array.Empty<ColorBlock>();
        private ColorBlockPool blockPool;
        private int arrangeColumns;

        public int Index { get; private set; }
        public int SlotCount => slots.Length;

        public bool HasBlocks
        {
            get
            {
                foreach (var block in slots)
                {
                    if (block != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Initialize(int index, float yOffset)
        {
            Index = index;
            lineYOffset = yOffset;
            transform.localPosition = new Vector3(0f, lineYOffset, 0f);
        }

        public void Populate(
            IReadOnlyList<(string colorId, int capacity)> blockConfigs,
            ColorBlockPool pool,
            PixelMaterialLibrary materialLibrary)
        {
            blockPool = pool;
            Clear();

            slots = new ColorBlock[blockConfigs.Count];
            for (var i = 0; i < blockConfigs.Count; i++)
            {
                var config = blockConfigs[i];
                var block = pool.Get();
                block.transform.SetParent(blocksRoot, false);
                block.Initialize(config.colorId, config.capacity, materialLibrary);
                slots[i] = block;
            }

            arrangeColumns = slots.Length;
            ArrangeBlocks();
        }

        public ColorBlock GetSlot(int column)
        {
            return column >= 0 && column < slots.Length ? slots[column] : null;
        }

        public bool Contains(ColorBlock block)
        {
            if (block == null)
            {
                return false;
            }

            foreach (var slot in slots)
            {
                if (slot == block)
                {
                    return true;
                }
            }

            return false;
        }

        // Lấy đúng block người chơi tap ra khỏi line, để trống slot (giữ nguyên cột các block khác).
        public bool TryRemoveBlock(ColorBlock block)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == block)
                {
                    slots[i] = null;
                    return true;
                }
            }

            return false;
        }

        // Canh các line theo cùng số cột để các cột thẳng hàng dọc giữa các line.
        public void SetArrangeColumns(int columns)
        {
            arrangeColumns = Mathf.Max(columns, slots.Length);
            ArrangeBlocks();
        }

        public void Clear()
        {
            foreach (var block in slots)
            {
                if (block == null)
                {
                    continue;
                }

                if (blockPool != null)
                {
                    blockPool.Release(block);
                }
                else
                {
                    Destroy(block.gameObject);
                }
            }

            slots = Array.Empty<ColorBlock>();
        }

        private void ArrangeBlocks()
        {
            var columns = Mathf.Max(arrangeColumns, 1);
            var startX = -(columns - 1) * blockSpacing * 0.5f;

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].transform.localPosition = new Vector3(startX + i * blockSpacing, 0f, 0f);
                }
            }
        }

        private void Reset()
        {
            blocksRoot = transform;
        }
    }
}
