using UnityEngine;
using UnityEngine.Pool;

namespace NexZap.Gameplay.Items
{
    public class ColorBlockPool : MonoBehaviour
    {
        [SerializeField] private ColorBlock prefab;
        [SerializeField] private int defaultCapacity = 20;
        [SerializeField] private int maxSize = 100;

        private ObjectPool<ColorBlock> pool;

        private void Awake()
        {
            EnsurePool();
        }

        public ColorBlock Get()
        {
            EnsurePool();
            return pool.Get();
        }

        public void Release(ColorBlock block)
        {
            if (block == null)
            {
                return;
            }

            EnsurePool();
            pool.Release(block);
        }

        private void EnsurePool()
        {
            if (pool != null)
            {
                return;
            }

            pool = new ObjectPool<ColorBlock>(
                CreateBlock,
                OnGetBlock,
                OnReleaseBlock,
                OnDestroyBlock,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        private ColorBlock CreateBlock()
        {
            var block = Instantiate(prefab, transform);
            block.SetPool(this);
            return block;
        }

        private void OnGetBlock(ColorBlock block)
        {
            block.gameObject.SetActive(true);
        }

        private void OnReleaseBlock(ColorBlock block)
        {
            block.OnReturnedToPool();
            block.transform.SetParent(transform, false);
            block.gameObject.SetActive(false);
        }

        private void OnDestroyBlock(ColorBlock block)
        {
            if (block != null)
            {
                Destroy(block.gameObject);
            }
        }
    }
}
