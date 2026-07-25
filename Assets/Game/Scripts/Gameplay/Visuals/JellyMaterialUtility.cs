using System.Collections.Generic;
using UnityEngine;

namespace NexZap.Gameplay.Visuals
{
    /// <summary>Creates and caches the shared runtime jelly look used by blocks and map cells.</summary>
    public static class JellyMaterialUtility
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly Dictionary<Material, Material> Cache = new();

        public static Material GetOrCreate(Material source, float smoothness = 0.92f)
        {
            if (source == null)
            {
                return null;
            }

            if (Cache.TryGetValue(source, out var cached) && cached != null)
            {
                return cached;
            }

            var shader = Shader.Find("NexZap/JellyBlock");
            var material = shader != null ? new Material(shader) : new Material(source);
            material.name = $"{source.name} (Jelly Shared)";
            material.hideFlags = HideFlags.HideAndDontSave;

            var color = source.HasProperty(BaseColorId)
                ? source.GetColor(BaseColorId)
                : source.HasProperty(ColorId) ? source.GetColor(ColorId) : Color.white;

            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
            if (material.HasProperty(GlossinessId)) material.SetFloat(GlossinessId, smoothness);
            if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, smoothness);
            if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, 0f);

            Cache[source] = material;
            return material;
        }
    }
}
