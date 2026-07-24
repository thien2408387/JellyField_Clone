using KingCat.Base.Assets;
using UnityEngine;

namespace KingCat.Base
{
    public sealed class MaterialColorController : MonoSingleton<MaterialColorController>
    {
        [Header("Database")]
        [SerializeField] private MaterialColorDatabaseSO database;

        public MaterialColorDatabaseSO Database => database;

        public override void Init()
        {
            base.Init();

            if (database == null)
            {
                Debug.LogWarning("[MaterialColorController] Database is null. Please assign MaterialColorDatabaseSO in Inspector.");
            }
        }

        public Material GetMaterial(ColorType colorType)
        {
            if (database == null)
            {
                Debug.LogError("[MaterialColorController] Database is null.");
                return null;
            }

            return database.GetMaterial(colorType);
        }

        public bool TryGetMaterial(ColorType colorType, out Material material)
        {
            material = null;

            if (database == null)
            {
                Debug.LogError("[MaterialColorController] Database is null.");
                return false;
            }

            return database.TryGetMaterial(colorType, out material);
        }

        public void ApplyMaterial(Renderer targetRenderer, ColorType colorType)
        {
            if (targetRenderer == null)
            {
                Debug.LogWarning("[MaterialColorController] Target Renderer is null.");
                return;
            }

            Material material = GetMaterial(colorType);
            if (material == null) return;

            targetRenderer.material = material;
        }

        public void ApplySharedMaterial(Renderer targetRenderer, ColorType colorType)
        {
            if (targetRenderer == null)
            {
                Debug.LogWarning("[MaterialColorController] Target Renderer is null.");
                return;
            }

            Material material = GetMaterial(colorType);
            if (material == null) return;

            targetRenderer.sharedMaterial = material;
        }

        public void ApplyMaterial(Renderer[] renderers, ColorType colorType)
        {
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[MaterialColorController] Renderers array is null or empty.");
                return;
            }

            Material material = GetMaterial(colorType);
            if (material == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].material = material;
            }
        }

        public void ApplySharedMaterial(Renderer[] renderers, ColorType colorType)
        {
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[MaterialColorController] Renderers array is null or empty.");
                return;
            }

            Material material = GetMaterial(colorType);
            if (material == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = material;
            }
        }
    }
}