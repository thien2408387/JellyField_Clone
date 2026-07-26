using System;
using System.Linq;
using NexZap.Data;
using UnityEngine;


namespace NexZap.Gameplay
{
    // Mỗi ô pixel = 1 GameObject "vỏ", bên trong chứa 2 visual: idle (chưa fill) và fill (đã fill).
    // Lúc fill chỉ cần bật/tắt qua lại, không Instantiate thêm -> mượt và đơn giản.
    public class RuntimePixel : MonoBehaviour
    {
        public Vector2Int GridPosition { get; private set; }
        public string TargetColorId { get; private set; }
        public bool IsFilled { get; private set; }

        private GameObject idleVisual;
        private GameObject fillVisual;
        private GameObject idleVisualTop;
        private GameObject idleVisualBottom;
        private GameObject fillVisualTop;
        private GameObject fillVisualBottom;

        public void Setup(
            Vector2Int gridPos,
            string colorId,
            PixelMaterialLibrary library,
            GameObject idlePrefab,
            GameObject fillPrefab,
            float previewStrength)
        {
            GridPosition = gridPos;
            TargetColorId = colorId ?? PixelColorIds.Empty;
            IsFilled = false;

            var ids = SplitColorIds(TargetColorId);
            var isDual = ids.Length == 2;
            var firstColor = library != null ? library.GetTint(ids.Length > 0 ? ids[0] : PixelColorIds.Empty) : Color.gray;
            var secondColor = library != null && ids.Length > 1 ? library.GetTint(ids[1]) : firstColor;
            var dark = new Color(0.15f, 0.15f, 0.18f, 1f);

            if (isDual)
            {
                var topPreview = Color.Lerp(dark, firstColor, previewStrength);
                var bottomPreview = Color.Lerp(dark, secondColor, previewStrength);

                if (idlePrefab != null)
                {
                    CreateSplitVisual(idlePrefab, transform, topPreview, bottomPreview, out idleVisualTop, out idleVisualBottom);
                }

                if (fillPrefab != null)
                {
                    CreateSplitVisual(fillPrefab, transform, firstColor, secondColor, out fillVisualTop, out fillVisualBottom);
                    if (fillVisualTop != null) fillVisualTop.SetActive(false);
                    if (fillVisualBottom != null) fillVisualBottom.SetActive(false);
                }
            }
            else
            {
                var full = library != null ? library.GetTint(TargetColorId) : Color.gray;
                if (idlePrefab != null)
                {
                    idleVisual = Instantiate(idlePrefab, transform);
                    idleVisual.transform.localPosition = Vector3.zero;
                    Tint(idleVisual, Color.Lerp(dark, full, previewStrength));
                }

                if (fillPrefab != null)
                {
                    fillVisual = Instantiate(fillPrefab, transform);
                    fillVisual.transform.localPosition = Vector3.zero;
                    Tint(fillVisual, full);
                    fillVisual.SetActive(false);
                }
            }
        }

        public bool Fill(string colorId)
        {
            if (IsFilled || colorId != TargetColorId)
            {
                return false;
            }
            IsFilled = true;
            if (idleVisual != null)
            {
                idleVisual.SetActive(false);
            }
            if (fillVisual != null)
            {
                fillVisual.SetActive(true);
            }

            if (idleVisualTop != null) idleVisualTop.SetActive(false);
            if (idleVisualBottom != null) idleVisualBottom.SetActive(false);
            if (fillVisualTop != null) fillVisualTop.SetActive(true);
            if (fillVisualBottom != null) fillVisualBottom.SetActive(true);
            return true;
        }

        private static void CreateSplitVisual(
            GameObject prefab,
            Transform parent,
            Color topColor,
            Color bottomColor,
            out GameObject topVisual,
            out GameObject bottomVisual)
        {
            topVisual = Instantiate(prefab, parent, false);
            bottomVisual = Instantiate(prefab, parent, false);
            topVisual.name = prefab.name + "_Top";
            bottomVisual.name = prefab.name + "_Bottom";

            CopyRenderMaterials(prefab, topVisual);
            CopyRenderMaterials(prefab, bottomVisual);

            topVisual.transform.localScale = new Vector3(1f, 0.5f, 1f);
            bottomVisual.transform.localScale = new Vector3(1f, 0.5f, 1f);
            topVisual.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            bottomVisual.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            Tint(topVisual, topColor);
            Tint(bottomVisual, bottomColor);
        }

        private static void CopyRenderMaterials(GameObject source, GameObject target)
        {
            var sourceRenderers = source.GetComponentsInChildren<MeshRenderer>(true);
            var targetRenderers = target.GetComponentsInChildren<MeshRenderer>(true);
            var count = Mathf.Min(sourceRenderers.Length, targetRenderers.Length);
            for (var i = 0; i < count; i++)
            {
                targetRenderers[i].sharedMaterials = sourceRenderers[i].sharedMaterials;
            }
        }

        private static string[] SplitColorIds(string colorId)
        {
            if (string.IsNullOrEmpty(colorId))
            {
                return Array.Empty<string>();
            }

            return colorId.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();
        }

        // Tô màu cho mọi renderer trong visual (SpriteRenderer/ MeshRenderer).
        private static void Tint(GameObject visual, Color color)
        {
            var spriteRenderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in spriteRenderers)
            {
                renderer.color = color;
            }

            var meshRenderers = visual.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in meshRenderers)
            {
                var material = renderer.material;
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
                else
                {
                    material.color = color;
                }
            }
        }
    }
}

