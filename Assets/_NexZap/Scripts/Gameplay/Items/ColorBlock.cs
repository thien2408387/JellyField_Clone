using System;
using DG.Tweening;
using NexZap.Data;
using TMPro;
using UnityEngine;

namespace NexZap.Gameplay.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class ColorBlock : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Child chứa toàn bộ hình ảnh. Mọi animation scale/punch chạy trên đây để không đụng tới chuyển động của root (DOPath).")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMeshPro capacityLabel;
        [SerializeField] private SpriteRenderer highlightRenderer;
        [SerializeField] private Collider2D bodyCollider;

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

            ConfigureLabelSorting();
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
            ColorId = colorId ?? PixelColorIds.Empty;
            RemainingCapacity = capacity;
            State = ColorBlockState.Idle;
            baseColor = materialLibrary != null
                ? materialLibrary.GetTint(ColorId)
                : Color.gray;

            isSelectable = false;
            isDimmed = false;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = false;
            }

            RefreshVisual();
            PlaySpawn();
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
                    bodyCollider.enabled = selectable;
                }

                if (highlightRenderer != null)
                {
                    highlightRenderer.enabled = selectable;
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
            pulseTween?.Kill();
            pulseTween = null;
            ReturnToPool();
        }

        public void OnReturnedToPool()
        {
            transform.DOKill();
            Vis.DOKill();
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

            if (highlightRenderer != null)
            {
                highlightRenderer.enabled = false;
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
            if (spriteRenderer == null)
            {
                return;
            }

            var color = baseColor;
            color.a = alpha;
            spriteRenderer.color = color;
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
        }
    }
}
