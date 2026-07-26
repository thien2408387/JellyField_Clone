using UnityEngine;

namespace KingCat.Base
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<T>();
                }

                return instance;
            }
        }

        public virtual void Init()
        {
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                Init();
                return;
            }

            if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static bool TryGetInstance(out T value)
        {
            value = Instance;
            return value != null;
        }
    }
}
