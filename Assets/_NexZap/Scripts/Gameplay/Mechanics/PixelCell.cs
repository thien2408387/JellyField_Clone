using DG.Tweening;
using NexZap.Data;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class PixelCell : MonoBehaviour
    {
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer fillRenderer;

        [Header("Preview")]
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

        private PixelMaterialLibrary materialLibrary;
        private MaterialPropertyBlock bodyPropertyBlock;
        private MaterialPropertyBlock fillPropertyBlock;

        public Vector2Int GridPosition { get; private set; }
        public string TargetColorId { get; private set; }
        public bool IsFilled { get; private set; }

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
            IsFilled = false;
            SetSize(size, depth);
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
            var targetColor = materialLibrary != null
                ? materialLibrary.GetTint(TargetColorId)
                : Color.gray;

            if (IsFilled)
            {
                ApplyRenderer(bodyRenderer, targetColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness);

                if (fillRenderer != null)
                {
                    fillRenderer.gameObject.SetActive(true);
                    fillRenderer.transform.localScale = Vector3.one * filledPopScale;
                    fillRenderer.transform.localPosition = new Vector3(0f, 0f, filledPopZ);
                    ApplyRenderer(fillRenderer, targetColor, fillPropertyBlock ??= new MaterialPropertyBlock(), true, filledSmoothness);
                }

                return;
            }

            var shell = new Color(0.14f, 0.14f, 0.16f, 1f);
            var previewColor = Color.Lerp(shell, targetColor, previewStrength);
            previewColor = Desaturate(previewColor, previewDesaturation);

            ApplyRenderer(bodyRenderer, previewColor, bodyPropertyBlock ??= new MaterialPropertyBlock(), true, unfilledSmoothness);

            if (fillRenderer != null)
            {
                fillRenderer.gameObject.SetActive(false);
                fillRenderer.transform.localScale = Vector3.one;
                fillRenderer.transform.localPosition = Vector3.zero;
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
            float smoothness)
        {
            if (renderer == null)
            {
                return;
            }

            if (useSharedMaterial && materialLibrary != null)
            {
                var sharedMaterial = materialLibrary.GetMaterial(TargetColorId);
                if (sharedMaterial != null)
                {
                    renderer.sharedMaterial = sharedMaterial;
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
