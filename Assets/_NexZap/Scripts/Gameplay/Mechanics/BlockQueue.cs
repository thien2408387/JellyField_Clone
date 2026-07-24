using System.Collections.Generic;
using NexZap.Data;
using NexZap.Gameplay.Items;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class BlockQueue : MonoBehaviour
    {
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private float slotSpacing = 0.9f;

        private readonly List<ColorBlock> slots = new();

        public int Count => slots.Count;
        public bool IsFull => slots.Count >= GameplayConstants.MaxQueueCapacity;
        public IReadOnlyList<ColorBlock> Blocks => slots;

        public bool TryEnqueue(ColorBlock block)
        {
            if (IsFull || block == null)
            {
                return false;
            }

            block.SetState(ColorBlockState.InQueue);
            slots.Add(block);
            block.transform.SetParent(slotsRoot, false);
            ArrangeSlots();
            RefreshSelectable();
            return true;
        }

        public bool Contains(ColorBlock block)
        {
            return block != null && slots.Contains(block);
        }

        // Lấy 1 block cụ thể ra khỏi queue (khi người chơi tap để chạy lại vòng).
        public bool TryRemove(ColorBlock block)
        {
            if (!slots.Remove(block))
            {
                return false;
            }

            ArrangeSlots();
            RefreshSelectable();
            return true;
        }

        public void Clear()
        {
            foreach (var block in slots)
            {
                if (block != null)
                {
                    block.ReturnToPoolImmediate();
                }
            }

            slots.Clear();
        }

        // Mọi block trong queue đều có thể tap để chạy lại vòng.
        private void RefreshSelectable()
        {
            foreach (var block in slots)
            {
                block.SetSelectable(true);
                block.SetDimmed(false);
            }
        }

        private void ArrangeSlots()
        {
            var startX = -(slots.Count - 1) * slotSpacing * 0.5f;
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i].transform.localPosition = new Vector3(startX + i * slotSpacing, 0f, 0f);
            }
        }

        private void Reset()
        {
            slotsRoot = transform;
        }
    }
}
