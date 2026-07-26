using DG.Tweening;
using NexZap.Data;
using NexZap.Gameplay.Visuals;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class PixelCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer fillRenderer;

        [Header("Preview")]
        [SerializeField] private Color emptyCellColor = new Color(0.04f, 0.04f, 0.055f, 1f);
        [SerializeField, Range(0f, 1f)] private float previewStrength = 0.28f;
        [SerializeField, Range(0f, 1f)] private float previewDesaturation = 0.45f;
        [SerializeField, Range(0f, 1f)] private float unfilledSmoothness = 0.05f;
        [SerializeField, Range(0f, 1f)] private float filledSmoothness = 0.55f;
        [SerializeField, Min(1f)] private float filledPopScale = 1.08f;
        [SerializeField] private float filledPopZ = 0.015f;

        [Header("Fill")]
        [Tooltip("PixelBoard cập nhật cờ này: bật = được phép fill, tắt = bị chặn.")]
        [SerializeField] private bool isFillable;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        // Ô 2 màu chia đôi theo chiều dọc: nửa trên = màu chính, nửa dưới = màu phụ (khớp preview trong Level Editor).
        private const float HalfHeightScale = 0.5f;
        private const float HalfCenterOffset = 0.25f;

        private PixelMaterialLibrary materialLibrary;
        private Material emptyCellMaterial;
        private MaterialPropertyBlock propertyBlock;
        private MeshRenderer secondaryBodyRenderer;
        private MeshRenderer secondaryFillRenderer;
        private bool isExistingBoardColor;

        public Vector2Int GridPosition { get; private set; }
        public string TargetColorId { get; private set; }
        public bool IsFilled { get; private set; }
        public bool IsEmpty => string.IsNullOrEmpty(TargetColorId);
        public bool CountsTowardTarget { get; private set; }

        public bool IsFillableFlag => isFillable;
        public void SetFillableFlag(bool value) => isFillable = value;

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
            TargetColorId = colorId ?? PixelColorIds.Empty;
            IsFilled = !string.IsNullOrEmpty(TargetColorId);
            isExistingBoardColor = true;
            isFillable = false;
            CountsTowardTarget = false;
            RefreshVisual();
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
            PlayFillFeedback();
            return true;
        }

        private void RefreshVisual()
        {
            var isDual = PixelColorIds.IsDual(TargetColorId);
            if (isDual)
            {
                EnsureSecondaryRenderers();
            }

            if (IsEmpty)
            {
                if (bodyRenderer != null && emptyCellMaterial != null)
                {
                    bodyRenderer.sharedMaterial = emptyCellMaterial;
                }

                LayoutHalf(bodyRenderer, false, true, 1f, 0f);
                ApplyRenderer(bodyRenderer, PixelColorIds.Empty, emptyCellColor, false, unfilledSmoothness);
                HideRenderer(secondaryBodyRenderer);
                HideRenderer(fillRenderer);
                HideRenderer(secondaryFillRenderer);
                return;
            }

            RefreshHalf(bodyRenderer, fillRenderer, PixelColorIds.GetPrimary(TargetColorId), isDual, true);

            if (isDual)
            {
                RefreshHalf(
                    secondaryBodyRenderer, secondaryFillRenderer, PixelColorIds.GetSecondary(TargetColorId), true, false);
                return;
            }

            HideRenderer(secondaryBodyRenderer);
            HideRenderer(secondaryFillRenderer);
        }

        private void RefreshHalf(MeshRenderer body, MeshRenderer fill, string colorId, bool isDual, bool isTopHalf)
        {
            if (body != null)
            {
                body.gameObject.SetActive(true);
                LayoutHalf(body, isDual, isTopHalf, 1f, 0f);
            }

            var targetColor = materialLibrary != null ? materialLibrary.GetTint(colorId) : Color.gray;

            if (IsFilled)
            {
                ApplyRenderer(body, colorId, targetColor, true, filledSmoothness);

                if (fill != null)
                {
                    fill.gameObject.SetActive(!isExistingBoardColor);
                    if (!isExistingBoardColor)
                    {
                        LayoutHalf(fill, isDual, isTopHalf, filledPopScale, filledPopZ);
                        ApplyRenderer(fill, colorId, targetColor, true, filledSmoothness);
                    }
                }

                return;
            }

            var shell = new Color(0.14f, 0.14f, 0.16f, 1f);
            var previewColor = Color.Lerp(shell, targetColor, previewStrength);
            previewColor = Desaturate(previewColor, previewDesaturation);

            ApplyRenderer(body, colorId, previewColor, true, unfilledSmoothness);
            HideRenderer(fill);
        }

        // Nhân bản Body/Fill để có renderer riêng cho màu phụ, tránh phải sửa prefab.
        private void EnsureSecondaryRenderers()
        {
            if (secondaryBodyRenderer == null && bodyRenderer != null)
            {
                secondaryBodyRenderer = CloneRenderer(bodyRenderer, "BodySecondary");
            }

            if (secondaryFillRenderer == null && fillRenderer != null)
            {
                secondaryFillRenderer = CloneRenderer(fillRenderer, "FillSecondary");
            }
        }

        private static MeshRenderer CloneRenderer(MeshRenderer source, string objectName)
        {
            var clone = Instantiate(source.gameObject, source.transform.parent);
            clone.name = objectName;
            clone.transform.localRotation = source.transform.localRotation;
            return clone.GetComponent<MeshRenderer>();
        }

        private static void LayoutHalf(
            MeshRenderer renderer, bool isDual, bool isTopHalf, float scale, float localZ)
        {
            if (renderer == null)
            {
                return;
            }

            var target = renderer.transform;
            target.localScale = isDual
                ? new Vector3(scale, scale * HalfHeightScale, scale)
                : new Vector3(scale, scale, scale);
            var localY = isDual ? (isTopHalf ? 1f : -1f) * HalfCenterOffset * scale : 0f;
            target.localPosition = new Vector3(0f, localY, localZ);
        }

        private static void HideRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.gameObject.SetActive(false);
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localPosition = Vector3.zero;
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
            string colorId,
            Color color,
            bool useSharedMaterial,
            float smoothness)
        {
            if (renderer == null)
            {
                return;
            }

            if (useSharedMaterial && materialLibrary != null)
            {
                var sharedMaterial = materialLibrary.GetMaterial(colorId);
                if (sharedMaterial != null)
                {
                    renderer.sharedMaterial = JellyMaterialUtility.GetOrCreate(sharedMaterial, filledSmoothness);
                }
            }

            propertyBlock ??= new MaterialPropertyBlock();
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
            fillRenderer = transform.Find("Fill")?.GetComponent<MeshRenderer>();
        }
    }
}
