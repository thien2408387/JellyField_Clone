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

        // Các slot giữ thứ tự đã khai báo trong Odin. Manager chỉ reveal slot đầu
        // tiên còn lại, vì vậy toàn bộ sequence có thể được tạo trước nhưng chỉ có
        // một block xuất hiện tại mỗi thời điểm.
        private ColorBlock[] slots = Array.Empty<ColorBlock>();
        private ColorBlockPool blockPool;

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

        public void SetArrangeColumns(int columns)
        {
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
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    // Every queued block shares the same presentation slot. Hidden
                    // blocks therefore appear exactly where the previous one was.
                    slots[i].transform.localPosition = Vector3.zero;
                }
            }
        }

        private void Reset()
        {
            blocksRoot = transform;
        }
    }
}
