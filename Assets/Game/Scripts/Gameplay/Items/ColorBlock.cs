using System;
using System.Collections.Generic;
using DG.Tweening;
using NexZap.Data;
using NexZap.Gameplay.Mechanics;
using TMPro;
using UnityEngine;

namespace NexZap.Gameplay.Items
{
    [RequireComponent(typeof(BoxCollider))]
    public class ColorBlock : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessPropertyId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");
        private static readonly int CoatMaskPropertyId = Shader.PropertyToID("_ClearCoatMask");
        private static readonly int CoatSmoothnessPropertyId = Shader.PropertyToID("_ClearCoatSmoothness");
        private static readonly Dictionary<Material, Material> JellyMaterials = new();

        [Header("Visual")]
        [Tooltip("Child chứa toàn bộ hình ảnh. Mọi animation scale/punch chạy trên đây để không đụng tới chuyển động của root (DOPath).")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMeshPro capacityLabel;
        [SerializeField] private SpriteRenderer highlightRenderer;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private JellyController jellyController;

        [Header("3D Visual")]
        [SerializeField] private Vector3 cubeSize = new Vector3(0.2f, 0.2f, 0.12f);
        [SerializeField] private float highlightScale = 1.16f;

        [Header("Jelly Material")]
        [SerializeField, Range(0f, 1f)] private float jellySmoothness = 0.92f;
        [SerializeField, Range(0f, 1f)] private float jellyClearCoat = 0.65f;

        private MeshRenderer cubeRenderer;
        private MeshRenderer cubeHighlightRenderer;
        [SerializeField] private BoxCollider bodyCollider3D;
        private MaterialPropertyBlock cubePropertyBlock;
        private MaterialPropertyBlock highlightPropertyBlock;

        [Header("Animation")]
        [SerializeField] private float spawnDuration = 0.25f;
        [SerializeField] private float selectablePulseScale = 1.12f;
        [SerializeField] private float selectablePulseDuration = 0.55f;
        [SerializeField] private float tapPunchScale = 0.25f;
        [SerializeField] private float consumePunchScale = 0.18f;
        [SerializeField] private float depleteDuration = 0.28f;
        [SerializeField] private float dimmedAlpha = 0.45f;
        [Tooltip("Text sức chứa luôn render trên body theo offset này để tránh bị đè/nhấp nháy.")]
        [SerializeField] private int labelSortingOffset = 1;

        private Tween pulseTween;
        private static Material sharedSpriteMaterial;
        private Color baseColor;
        private bool isSelectable;
        private bool isDimmed;
        private ColorBlockPool pool;

        public string ColorId { get; private set; }
        public int RemainingCapacity { get; private set; }
        public ColorBlockState State { get; private set; } = ColorBlockState.Idle;

        public bool HasCapacity => RemainingCapacity > 0;
        public bool IsSelectable => isSelectable;

        private Transform Vis => visualRoot != null ? visualRoot : transform;

        private void Awake()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (jellyController == null)
            {
                jellyController = GetComponent<JellyController>();
            }

            Build3DVisual();
            EnsureValidSpriteMaterials();
            ConfigureLabelSorting();
        }

        private void Build3DVisual()
        {
            var jellyRoot = jellyController != null && jellyController.JellyMesh != null
                ? jellyController.JellyMesh
                : Vis;

            var existingBody = jellyRoot.Find("CubeBody");
            cubeRenderer = existingBody != null ? existingBody.GetComponent<MeshRenderer>() : null;
            if (cubeRenderer == null)
            {
                cubeRenderer = CreateCubeRenderer("CubeBody", jellyRoot, cubeSize, Vector3.zero);
            }

            var existingHighlight = jellyRoot.Find("CubeHighlight");
            cubeHighlightRenderer = existingHighlight != null
                ? existingHighlight.GetComponent<MeshRenderer>()
                : null;
            if (cubeHighlightRenderer == null)
            {
                var highlightSize = new Vector3(
                    cubeSize.x * highlightScale,
                    cubeSize.y * highlightScale,
                    cubeSize.z * 0.9f);
                cubeHighlightRenderer = CreateCubeRenderer(
                    "CubeHighlight", jellyRoot, highlightSize, new Vector3(0f, 0f, cubeSize.z * 0.12f));
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = false;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            bodyCollider3D = GetComponent<BoxCollider>();
            if (bodyCollider3D == null)
            {
                bodyCollider3D = gameObject.AddComponent<BoxCollider>();
            }

            bodyCollider3D.center = Vector3.zero;
            bodyCollider3D.size = cubeSize;
            bodyCollider3D.enabled = false;
            cubeHighlightRenderer.enabled = false;
        }

        private static MeshRenderer CreateCubeRenderer(
            string objectName, Transform parent, Vector3 size, Vector3 localPosition)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = size;
            cube.layer = parent.gameObject.layer;

            var generatedCollider = cube.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                generatedCollider.enabled = false;
                UnityEngine.Object.Destroy(generatedCollider);
            }

            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void EnsureValidSpriteMaterials()
        {
            if (sharedSpriteMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    sharedSpriteMaterial = new Material(shader)
                    {
                        name = "ColorBlock Sprite Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            if (sharedSpriteMaterial == null)
            {
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = sharedSpriteMaterial;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.sharedMaterial = sharedSpriteMaterial;
            }
        }

        // Ép text render trên body (cùng sorting layer, order cao hơn) để không bị đè/nhấp nháy.
        private void ConfigureLabelSorting()
        {
            if (capacityLabel == null || spriteRenderer == null)
            {
                return;
            }

            var labelRenderer = capacityLabel.GetComponent<Renderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                labelRenderer.sortingOrder = spriteRenderer.sortingOrder + labelSortingOffset;
            }
        }

        public void SetPool(ColorBlockPool owner)
        {
            pool = owner;
        }

        public void Initialize(string colorId, int capacity, PixelMaterialLibrary materialLibrary)
        {
            EnsureValidSpriteMaterials();
            jellyController?.ResetVisual();
            ColorId = colorId ?? PixelColorIds.Empty;
            RemainingCapacity = capacity;
            State = ColorBlockState.Idle;
            baseColor = materialLibrary != null
                ? materialLibrary.GetTint(ColorId)
                : Color.gray;

            var colorMaterial = materialLibrary != null ? materialLibrary.GetMaterial(ColorId) : null;
            var jellyMaterial = GetOrCreateJellyMaterial(colorMaterial);
            if (cubeRenderer != null && jellyMaterial != null)
            {
                cubeRenderer.sharedMaterial = jellyMaterial;
            }

            if (cubeHighlightRenderer != null && jellyMaterial != null)
            {
                cubeHighlightRenderer.sharedMaterial = jellyMaterial;
            }

            isSelectable = false;
            isDimmed = false;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (bodyCollider3D != null)
            {
                bodyCollider3D.enabled = false;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = false;
            }

            if (cubeHighlightRenderer != null)
            {
                cubeHighlightRenderer.enabled = false;
            }

            RefreshVisual();
            PlaySpawn();
        }

        private Material GetOrCreateJellyMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (JellyMaterials.TryGetValue(source, out var cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(source)
            {
                name = $"{source.name} (Jelly)",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty(SmoothnessPropertyId))
            {
                material.SetFloat(SmoothnessPropertyId, jellySmoothness);
            }

            if (material.HasProperty(MetallicPropertyId))
            {
                material.SetFloat(MetallicPropertyId, 0f);
            }

            if (material.HasProperty(CoatMaskPropertyId))
            {
                material.SetFloat(CoatMaskPropertyId, jellyClearCoat);
                material.EnableKeyword("_CLEARCOAT");
            }

            if (material.HasProperty(CoatSmoothnessPropertyId))
            {
                material.SetFloat(CoatSmoothnessPropertyId, 1f);
            }

            JellyMaterials[source] = material;
            return material;
        }

        public void SetState(ColorBlockState state)
        {
            State = state;

            // Khi block rời line (lên path, vào queue, fill...) thì luôn sáng và không tap được.
            if (state != ColorBlockState.Idle)
            {
                SetSelectable(false);
                SetDimmed(false);
            }
        }

        public int ConsumeCapacity(int amount)
        {
            var consumed = Mathf.Min(amount, RemainingCapacity);
            RemainingCapacity -= consumed;
            RefreshVisual();

            if (consumed > 0)
            {
                PlayConsumeFeedback();
            }

            return consumed;
        }

        public void SetSelectable(bool selectable)
        {
            if (isSelectable != selectable)
            {
                isSelectable = selectable;

                if (bodyCollider != null)
                {
                    bodyCollider.enabled = false;
                }

                if (bodyCollider3D != null)
                {
                    bodyCollider3D.enabled = selectable;
                }

                if (highlightRenderer != null)
                {
                    highlightRenderer.enabled = false;
                }

                if (cubeHighlightRenderer != null)
                {
                    cubeHighlightRenderer.enabled = selectable;
                }
            }

            if (selectable)
            {
                StartPulse();
            }
            else
            {
                StopPulse();
            }
        }

        public void SetDimmed(bool dimmed)
        {
            isDimmed = dimmed;
            ApplyAlpha(dimmed ? dimmedAlpha : 1f);
        }

        public void PlayTapFeedback()
        {
            StopPulse();
            Vis.DOKill(true);
            Vis.localScale = Vector3.one;
            Vis.DOPunchScale(Vector3.one * tapPunchScale, 0.22f, 8, 0.6f);
        }

        public void PlayPickupJelly()
        {
            jellyController?.PlayStretchEffect();
        }

        public void BeginDragJiggle(Vector3 worldPosition)
        {
            jellyController?.BeginDragJiggle(worldPosition);
        }

        public void UpdateDragJiggle(Vector3 worldPosition, float deltaTime)
        {
            jellyController?.UpdateDragJiggle(worldPosition, deltaTime);
        }

        public void EndDragJiggle(bool successfulDrop)
        {
            jellyController?.EndDragJiggle(successfulDrop);
        }

        public void PlayDropJelly()
        {
            jellyController?.PlaySquashImpact();
        }

        public void ResetJellyVisual()
        {
            jellyController?.ResetVisual();
        }

        /// <summary>
        /// Animation co lại + mờ dần rồi trả về pool (KHÔNG Destroy).
        /// </summary>
        public void Despawn()
        {
            SetState(ColorBlockState.Depleting);
            StopPulse();
            Vis.DOKill();

            if (capacityLabel != null)
            {
                capacityLabel.gameObject.SetActive(false);
            }

            var sequence = DOTween.Sequence();
            sequence.Join(Vis.DOScale(Vector3.zero, depleteDuration).SetEase(Ease.InBack));

            if (spriteRenderer != null)
            {
                sequence.Join(spriteRenderer.DOFade(0f, depleteDuration));
            }

            sequence.OnComplete(ReturnToPool);
        }

        /// <summary>
        /// Trả ngay về pool, bỏ qua animation (dùng khi reload level / dọn dẹp).
        /// </summary>
        public void ReturnToPoolImmediate()
        {
            transform.DOKill();
            Vis.DOKill();
            ResetJellyVisual();
            pulseTween?.Kill();
            pulseTween = null;
            ReturnToPool();
        }

        public void OnReturnedToPool()
        {
            transform.DOKill();
            Vis.DOKill();
            ResetJellyVisual();
            pulseTween?.Kill();
            pulseTween = null;

            isSelectable = false;
            isDimmed = false;
            transform.localScale = Vector3.one;
            Vis.localScale = Vector3.one;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (bodyCollider3D != null)
            {
                bodyCollider3D.enabled = false;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = false;
            }

            if (cubeHighlightRenderer != null)
            {
                cubeHighlightRenderer.enabled = false;
            }

            if (spriteRenderer != null)
            {
                var color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
            }
        }

        public void RefreshVisual()
        {
            ApplyAlpha(isDimmed ? dimmedAlpha : 1f);

            if (capacityLabel != null)
            {
                capacityLabel.text = RemainingCapacity.ToString();
                capacityLabel.gameObject.SetActive(RemainingCapacity > 0);
            }

            if (highlightRenderer != null)
            {
                var highlightColor = baseColor;
                highlightColor.a = 0.5f;
                highlightRenderer.color = highlightColor;
            }

            var cubeHighlightColor = Color.Lerp(baseColor, Color.white, 0.45f);
            ApplyCubeColor(
                cubeHighlightRenderer,
                cubeHighlightColor,
                highlightPropertyBlock ??= new MaterialPropertyBlock());
        }

        private static void ApplyCubeColor(
            MeshRenderer renderer, Color color, MaterialPropertyBlock propertyBlock)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            propertyBlock.Clear();
            if (renderer.sharedMaterial.HasProperty(BaseColorPropertyId))
            {
                propertyBlock.SetColor(BaseColorPropertyId, color);
            }
            else if (renderer.sharedMaterial.HasProperty(ColorPropertyId))
            {
                propertyBlock.SetColor(ColorPropertyId, color);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ReturnToPool()
        {
            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void PlaySpawn()
        {
            Vis.DOKill();
            Vis.localScale = Vector3.zero;
            Vis.DOScale(Vector3.one, spawnDuration).SetEase(Ease.OutBack);
        }

        private void PlayConsumeFeedback()
        {
            Vis.DOKill(true);
            Vis.localScale = Vector3.one;
            Vis.DOPunchScale(Vector3.one * consumePunchScale, 0.2f, 6, 0.7f);

            if (capacityLabel != null)
            {
                capacityLabel.transform.DOKill(true);
                capacityLabel.transform.localScale = Vector3.one;
                capacityLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.25f, 6, 0.7f);
            }
        }

        private void StartPulse()
        {
            StopPulse();
            Vis.localScale = Vector3.one;
            pulseTween = Vis
                .DOScale(Vector3.one * selectablePulseScale, selectablePulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopPulse()
        {
            if (pulseTween != null)
            {
                pulseTween.Kill();
                pulseTween = null;
                Vis.localScale = Vector3.one;
            }
        }

        private void ApplyAlpha(float alpha)
        {
            var color = baseColor;
            color.a = alpha;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }

            var cubeColor = Color.Lerp(baseColor, Color.black, (1f - alpha) * 0.55f);
            cubeColor.a = 1f;
            ApplyCubeColor(cubeRenderer, cubeColor, cubePropertyBlock ??= new MaterialPropertyBlock());
        }

        private void OnDestroy()
        {
            transform.DOKill();
            Vis.DOKill();
            pulseTween?.Kill();
        }

        private void Reset()
        {
            visualRoot = transform.Find("Visual");
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            capacityLabel = GetComponentInChildren<TextMeshPro>();
            bodyCollider = GetComponent<Collider2D>();
            bodyCollider3D = GetComponent<BoxCollider>();
            jellyController = GetComponent<JellyController>();
        }
    }
}
