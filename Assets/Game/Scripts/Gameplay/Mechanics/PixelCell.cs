using DG.Tweening;
using System;
using System.Linq;
using NexZap.Data;
using NexZap.Gameplay.Visuals;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class PixelCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer bodyRenderer2;
        [SerializeField] private MeshRenderer fillRenderer;
        [SerializeField] private MeshRenderer fillRenderer2;

        [Header("Preview")]
        [SerializeField] private Color emptyCellColor = new Color(0.04f, 0.04f, 0.055f, 1f);
        [SerializeField, Range(0f, 1f)] private float previewStrength = 0.28f;
        [SerializeField, Range(0f, 1f)] private float previewDesaturation = 0.45f;
        [SerializeField, Range(0f, 1f)] private float unfilledSmoothness = 0.05f;
        [SerializeField, Range(0f, 1f)] private float filledSmoothness = 0.55f;
        [SerializeField, Min(1f)] private float filledPopScale = 1.08f;
        [SerializeField] private float filledPopZ = 0.015f;

        [Header("Fill")]
        [Tooltip("PixelBoard  cập nhật cờ này: bật = được phép fill, tắt = bị chặn.")]
        [SerializeField] private bool isFillable;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        private PixelMaterialLibrary materialLibrary;
        private Material emptyCellMaterial;
        private MaterialPropertyBlock bodyPropertyBlock;
        private MaterialPropertyBlock bodyPropertyBlock2;
        private MaterialPropertyBlock fillPropertyBlock;
        private MaterialPropertyBlock fillPropertyBlock2;
        private bool isExistingBoardColor;
        private Vector2 baseCellSize;
        private float baseCellDepth;

        public Vector2Int GridPosition { get; private set; }
        public string TargetColorId { get; private set; }
        public bool IsFilled { get; private set; }
        public bool IsEmpty => string.IsNullOrEmpty(TargetColorId);
        public bool CountsTowardTarget { get; private set; }

        public bool IsFillableFlag => isFillable;
        public void SetFillableFlag(bool value) => isFillable = value;

        public string[] GetColorIds()
        {
            return ExtractColorIds(TargetColorId);
        }

        public bool ContainsColor(string colorId)
        {
            return !string.IsNullOrEmpty(colorId) &&
                   GetColorIds().Any(id => id == colorId);
        }

        public void Initialize(
            Vector2Int gridPosition,
            string targetColorId,
            Vector2 size,
            float depth,
            PixelMaterialLibrary library)
        {
            GridPosition = gridPosition;
            TargetColorId = targetColorId ?? PixelColorIds.Empty;
            materialLibrary = library;
            emptyCellMaterial = FindEmptyCellMaterial(library);
            if (emptyCellMaterial == null && bodyRenderer != null)
            {
                emptyCellMaterial = bodyRenderer.sharedMaterial;
            }
            IsFilled = false;
            CountsTowardTarget = false;
            isExistingBoardColor = false;
            SetSize(size, depth);
            RefreshVisual();
        }

        private static Material FindEmptyCellMaterial(PixelMaterialLibrary library)
        {
            if (library == null || library.colors == null)
            {
                return null;
            }

            foreach (var definition in library.colors)
            {
                if (definition != null && definition.material != null)
                {
                    return definition.material;
                }
            }

            return null;
        }

        public void ClearColor()
        {
            TargetColorId = PixelColorIds.Empty;
            IsFilled = false;
            isExistingBoardColor = false;
            isFillable = false;
            CountsTowardTarget = false;
            RefreshVisual();
        }

        public void SetPlacedColor(string colorId)
        {
            SetPlacedColors(new[] { colorId });
        }

        public void SetPlacedColors(string[] colorIds)
        {
            TargetColorId = JoinColorIds(colorIds);
            IsFilled = !string.IsNullOrEmpty(TargetColorId);
            isExistingBoardColor = true;
            isFillable = false;
            CountsTowardTarget = false;
            RefreshVisual();
        }

        /// <summary>
        /// Removes one colour layer. Initial map colours continue counting toward
        /// the target until their own layer is removed; player-placed colours do not.
        /// </summary>
        public bool RemoveColor(string colorId, out bool removedTargetColor)
        {
            removedTargetColor = false;
            var colors = GetColorIds().ToList();
            var index = colors.FindIndex(id => id == colorId);
            if (index < 0)
            {
                return false;
            }

            removedTargetColor = CountsTowardTarget;
            colors.RemoveAt(index);
            TargetColorId = JoinColorIds(colors.ToArray());
            IsFilled = colors.Count > 0;
            isExistingBoardColor = IsFilled;
            isFillable = false;
            if (!IsFilled)
            {
                CountsTowardTarget = false;
            }

            RefreshVisual();
            return true;
        }

        public void ShowAsExistingColor()
        {
            if (IsEmpty)
            {
                return;
            }

            IsFilled = true;
            isExistingBoardColor = true;
            isFillable = false;
            CountsTowardTarget = true;
            RefreshVisual();
        }

        public void SetSize(Vector2 size, float depth)
        {
            baseCellSize = size;
            baseCellDepth = depth;
            transform.localScale = new Vector3(size.x, size.y, depth);
        }

        public bool TryFill(string colorId)
        {
            if (IsFilled || string.IsNullOrEmpty(colorId) || colorId != TargetColorId || !isFillable)
            {
                return false;
            }

            IsFilled = true;
            isFillable = false;
            RefreshVisual();
            //PlayFillFeedback();
            return true;
        }

        private void RefreshVisual()
        {
            if (IsEmpty)
            {
                if (bodyRenderer != null && emptyCellMaterial != null)
                {
                    bodyRenderer.sharedMaterial = emptyCellMaterial;
                }

                ApplyRenderer(bodyRenderer, emptyCellColor,
                    bodyPropertyBlock ??= new MaterialPropertyBlock(), false, unfilledSmoothness);

                if (bodyRenderer != null)
                {
                    bodyRenderer.transform.localScale = Vector3.one;
                    bodyRenderer.transform.localPosition = Vector3.zero;
                }

                if (bodyRenderer2 != null)
                {
                    bodyRenderer2.gameObject.SetActive(false);
                }

                if (fillRenderer != null)
                {
                    fillRenderer.gameObject.SetActive(false);
                    fillRenderer.transform.localScale = Vector3.one;
                    fillRenderer.transform.localPosition = Vector3.zero;
                }

                if (fillRenderer2 != null)
                {
                    fillRenderer2.gameObject.SetActive(false);
                }

                return;
            }

            var colorIds = ExtractColorIds(TargetColorId);
            var primaryColorId = colorIds[0];
            var secondaryColorId = colorIds.Length > 1 ? colorIds[1] : primaryColorId;

            if (colorIds.Length > 1)
            {
                RenderDualColor(primaryColorId, secondaryColorId);
            }
            else
            {
                RenderSingleColor(primaryColorId);
            }
        }

        private static string[] ExtractColorIds(string colorId)
        {
            if (string.IsNullOrEmpty(colorId))
            {
                return Array.Empty<string>();
            }

            return colorId
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .Take(2)
                .ToArray();
        }

        private static string JoinColorIds(string[] colorIds)
        {
            return string.Join("/", (colorIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct()
                .Take(2));
        }

        private void RenderSingleColor(string colorId)
        {
            var targetColor = materialLibrary != null
                ? materialLibrary.GetTint(colorId)
                : Color.gray;

            if (IsFilled)
            {
                ApplyRenderer(bodyRenderer, targetColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness, colorId);

                if (bodyRenderer != null)
                {
                    bodyRenderer.transform.localScale = Vector3.one;
                    bodyRenderer.transform.localPosition = Vector3.zero;
                }

                if (bodyRenderer2 != null)
                {
                    bodyRenderer2.gameObject.SetActive(false);
                }

                if (fillRenderer != null)
                {
                    fillRenderer.gameObject.SetActive(!isExistingBoardColor);
                    if (!isExistingBoardColor)
                    {
                        fillRenderer.transform.localScale = Vector3.one * filledPopScale;
                        fillRenderer.transform.localPosition = new Vector3(0f, 0f, filledPopZ);
                        ApplyRenderer(fillRenderer, targetColor, fillPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness, colorId);
                    }
                }

                if (fillRenderer2 != null)
                {
                    fillRenderer2.gameObject.SetActive(false);
                }

                return;
            }

            var shell = new Color(0.14f, 0.14f, 0.16f, 1f);
            var previewColor = Color.Lerp(shell, targetColor, previewStrength);
            previewColor = Desaturate(previewColor, previewDesaturation);

            ApplyRenderer(bodyRenderer, previewColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, unfilledSmoothness, colorId);

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = Vector3.one;
                bodyRenderer.transform.localPosition = Vector3.zero;
            }

            if (bodyRenderer2 != null)
            {
                bodyRenderer2.gameObject.SetActive(false);
            }

            if (fillRenderer != null)
            {
                fillRenderer.gameObject.SetActive(false);
                fillRenderer.transform.localScale = Vector3.one;
                fillRenderer.transform.localPosition = Vector3.zero;
            }

            if (fillRenderer2 != null)
            {
                fillRenderer2.gameObject.SetActive(false);
            }
        }

        private void RenderDualColor(string primaryColorId, string secondaryColorId)
        {
            var primaryColor = materialLibrary != null ? materialLibrary.GetTint(primaryColorId) : Color.gray;
            var secondaryColor = materialLibrary != null ? materialLibrary.GetTint(secondaryColorId) : Color.gray;

            if (IsFilled)
            {
                ApplyRenderer(bodyRenderer, primaryColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness, primaryColorId);
                if (bodyRenderer != null)
                {
                    bodyRenderer.transform.localScale = new Vector3(1f, 0.5f, 1f);
                    bodyRenderer.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                }

                ApplyRenderer(bodyRenderer2, secondaryColor, bodyPropertyBlock2 ??= new MaterialPropertyBlock(), true, filledSmoothness, secondaryColorId);
                if (bodyRenderer2 != null)
                {
                    bodyRenderer2.transform.localScale = new Vector3(1f, 0.5f, 1f);
                    bodyRenderer2.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                    bodyRenderer2.gameObject.SetActive(true);
                }

                if (fillRenderer != null)
                {
                    fillRenderer.gameObject.SetActive(!isExistingBoardColor);
                    if (!isExistingBoardColor)
                    {
                        fillRenderer.transform.localScale = new Vector3(1f, 0.5f, 1f) * filledPopScale;
                        fillRenderer.transform.localPosition = new Vector3(0f, -0.25f, filledPopZ);
                        ApplyRenderer(fillRenderer, primaryColor, fillPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness, primaryColorId);
                    }
                }

                if (fillRenderer2 != null)
                {
                    fillRenderer2.gameObject.SetActive(!isExistingBoardColor);
                    if (!isExistingBoardColor)
                    {
                        fillRenderer2.transform.localScale = new Vector3(1f, 0.5f, 1f) * filledPopScale;
                        fillRenderer2.transform.localPosition = new Vector3(0f, 0.25f, filledPopZ);
                        ApplyRenderer(fillRenderer2, secondaryColor, fillPropertyBlock2 ??= new MaterialPropertyBlock(), true, filledSmoothness, secondaryColorId);
                    }
                }

                return;
            }

            var shell = new Color(0.14f, 0.14f, 0.16f, 1f);
            var previewPrimaryColor = Color.Lerp(shell, primaryColor, previewStrength);
            previewPrimaryColor = Desaturate(previewPrimaryColor, previewDesaturation);
            var previewSecondaryColor = Color.Lerp(shell, secondaryColor, previewStrength);
            previewSecondaryColor = Desaturate(previewSecondaryColor, previewDesaturation);

            ApplyRenderer(bodyRenderer, previewPrimaryColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, unfilledSmoothness, primaryColorId);
            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = new Vector3(1f, 0.5f, 1f);
                bodyRenderer.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            }

            ApplyRenderer(bodyRenderer2, previewSecondaryColor, bodyPropertyBlock2 ??= new MaterialPropertyBlock(), true, unfilledSmoothness, secondaryColorId);
            if (bodyRenderer2 != null)
            {
                bodyRenderer2.transform.localScale = new Vector3(1f, 0.5f, 1f);
                bodyRenderer2.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                bodyRenderer2.gameObject.SetActive(true);
            }

            if (fillRenderer != null)
            {
                fillRenderer.gameObject.SetActive(false);
                fillRenderer.transform.localScale = Vector3.one;
                fillRenderer.transform.localPosition = Vector3.zero;
            }

            if (fillRenderer2 != null)
            {
                fillRenderer2.gameObject.SetActive(false);
            }
        }

        private static Color Desaturate(Color color, float amount)
        {
            var gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            var t = Mathf.Clamp01(amount);
            return new Color(
                Mathf.Lerp(color.r, gray, t),
                Mathf.Lerp(color.g, gray, t),
                Mathf.Lerp(color.b, gray, t),
                color.a);
        }

        private void ApplyRenderer(
            MeshRenderer renderer,
            Color color,
            MaterialPropertyBlock propertyBlock,
            bool useSharedMaterial,
            float smoothness,
            string colorId = null)
        {
            if (renderer == null)
            {
                return;
            }

            if (useSharedMaterial && materialLibrary != null)
            {
                var targetColorId = colorId ?? TargetColorId;
                var sharedMaterial = materialLibrary.GetMaterial(targetColorId);
                if (sharedMaterial != null)
                {
                    renderer.sharedMaterial = JellyMaterialUtility.GetOrCreate(sharedMaterial, filledSmoothness);
                }
            }

            propertyBlock.Clear();
            if (renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, color);
                }
                else if (renderer.sharedMaterial.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, color);
                }

                if (renderer.sharedMaterial.HasProperty(SmoothnessId))
                {
                    propertyBlock.SetFloat(SmoothnessId, smoothness);
                }

                if (renderer.sharedMaterial.HasProperty(GlossinessId))
                {
                    propertyBlock.SetFloat(GlossinessId, smoothness);
                }
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        private void PlayFillFeedback()
        {
            transform.DOKill(true);
            var punch = new Vector3(
                transform.localScale.x * 0.2f,
                transform.localScale.y * 0.2f,
                transform.localScale.z * 0.15f);
            transform.DOPunchScale(punch, 0.2f, 6, 0.7f);
        }

        private void Reset()
        {
            bodyRenderer = transform.Find("Body")?.GetComponent<MeshRenderer>();
            bodyRenderer2 = transform.Find("Body2")?.GetComponent<MeshRenderer>();
            fillRenderer = transform.Find("Fill")?.GetComponent<MeshRenderer>();
            fillRenderer2 = transform.Find("Fill2")?.GetComponent<MeshRenderer>();
        }
    }
}
