using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TBN
{
    public static class PoolExtensions
    {
        private static readonly Dictionary<int, Stack<GameObject>> PoolsByPrefabId = new Dictionary<int, Stack<GameObject>>();
        private static PoolRunner _runner;

        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            var prefabId = prefab.GetInstanceID();
            if (!PoolsByPrefabId.TryGetValue(prefabId, out var pool))
            {
                pool = new Stack<GameObject>(16);
                PoolsByPrefabId[prefabId] = pool;
            }

            GameObject instance = null;
            while (pool.Count > 0 && instance == null)
            {
                var candidate = pool.Pop();
                if (candidate == null) continue;

                var pooledCandidate = candidate.GetComponent<PooledObject>();
                if (pooledCandidate == null)
                {
                    Object.Destroy(candidate);
                    continue;
                }

                // Skip stale duplicate entries that no longer represent an in-pool instance.
                if (!pooledCandidate.IsInPool) continue;

                instance = candidate;
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab, position, rotation, parent);
                var pooled = instance.GetComponent<PooledObject>() ?? instance.AddComponent<PooledObject>();
                pooled.PrefabId = prefabId;
                pooled.IsInPool = false;
                pooled.IsRecycleQueued = false;
            }

            var pooledInstance = instance.GetComponent<PooledObject>() ?? instance.AddComponent<PooledObject>();
            pooledInstance.PrefabId = prefabId;
            pooledInstance.IsInPool = false;
            pooledInstance.IsRecycleQueued = false;

            var tr = instance.transform;
            tr.SetParent(parent, false);
            tr.SetPositionAndRotation(position, rotation);
            if (!instance.activeSelf)
                instance.SetActive(true);

            return instance;
        }

        /// <summary>
        /// Spawn at world position while preserving prefab default rotation.
        /// </summary>
        public static GameObject Spawn(this GameObject prefab, Vector3 position, Transform parent = null)
        {
            if (prefab == null) return null;
            return Spawn(prefab, position, prefab.transform.rotation, parent);
        }

        public static GameObject Spawn(this GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            var pos = parent != null ? parent.position : Vector3.zero;
            var rot = parent != null ? parent.rotation : Quaternion.identity;
            return Spawn(prefab, pos, rot, parent);
        }

        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null) return null;
            var go = Spawn(prefab.gameObject, position, rotation, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static T Spawn<T>(this T prefab, Vector3 position, Transform parent = null) where T : Component
        {
            if (prefab == null) return null;
            var go = Spawn(prefab.gameObject, position, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static T Spawn<T>(this T prefab, Transform parent) where T : Component
        {
            if (prefab == null) return null;
            var go = Spawn(prefab.gameObject, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static void Recycle(this GameObject instance, float delay = 0f)
        {
            if (instance == null) return;
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled != null && pooled.IsInPool) return;

            if (delay <= 0f)
            {
                RecycleInternal(instance);
                return;
            }

            if (pooled != null)
            {
                if (pooled.IsRecycleQueued) return;
                pooled.IsRecycleQueued = true;
            }

            EnsureRunner().StartCoroutine(DelayedRecycle(instance, delay));
        }

        private static IEnumerator DelayedRecycle(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            RecycleInternal(instance);
        }

        private static void RecycleInternal(GameObject instance)
        {
            if (instance == null) return;

            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null)
            {
                Object.Destroy(instance);
                return;
            }
            if (pooled.IsInPool) return;

            pooled.IsRecycleQueued = false;

            if (!PoolsByPrefabId.TryGetValue(pooled.PrefabId, out var pool))
            {
                pool = new Stack<GameObject>(16);
                PoolsByPrefabId[pooled.PrefabId] = pool;
            }

            instance.SetActive(false);
            instance.transform.SetParent(EnsureRunner().transform, false);
            pool.Push(instance);
            pooled.IsInPool = true;
        }

        private static PoolRunner EnsureRunner()
        {
            if (_runner != null) return _runner;

            var go = new GameObject("[PoolRunner]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<PoolRunner>();
            return _runner;
        }

        private sealed class PoolRunner : MonoBehaviour { }

        private sealed class PooledObject : MonoBehaviour
        {
            public int PrefabId;
            public bool IsInPool;
            public bool IsRecycleQueued;
        }
    }
}
