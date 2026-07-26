using DG.Tweening;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class JellyController : MonoBehaviour
    {
        [Header("Jelly Visuals")]
        [SerializeField] private Transform jellyMesh;

        [Header("Drag Jiggle")]
        [SerializeField, Range(0f, 0.8f)] private float maxStretch = 0.5f;
        [SerializeField, Range(0f, 35f)] private float maxRotation = 5f;
        [SerializeField, Range(0f, 0.2f)] private float positionLag = 0.075f;
        [SerializeField, Min(0.01f)] private float velocityForMaxEffect = 1.5f;
        [SerializeField, Min(0.01f)] private float followSmoothness = 11f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.2f;

        [Header("Elasticity")]
        [SerializeField, Min(1f)] private float springStrength = 72f;
        [SerializeField, Min(0.1f)] private float springDamping = 5.5f;
        [SerializeField, Range(0f, 0.25f)] private float directionWobble = 0.16f;

        private bool isDragging;
        private Vector3 previousWorldPosition;
        private Vector2 smoothedVelocity;
        private Vector2 previousSmoothedVelocity;
        private Vector3 scaleSpringVelocity;
        private Vector3 positionSpringVelocity;
        private float rotationSpringVelocity;

        public Transform JellyMesh => jellyMesh;

        public void BeginDragJiggle(Vector3 worldPosition)
        {
            if (jellyMesh == null)
            {
                return;
            }

            jellyMesh.DOKill();
            jellyMesh.localScale = Vector3.one;
            jellyMesh.localRotation = Quaternion.identity;
            jellyMesh.localPosition = Vector3.zero;
            previousWorldPosition = worldPosition;
            smoothedVelocity = Vector2.zero;
            previousSmoothedVelocity = Vector2.zero;
            scaleSpringVelocity = Vector3.zero;
            positionSpringVelocity = Vector3.zero;
            rotationSpringVelocity = 0f;
            isDragging = true;
        }

        public void UpdateDragJiggle(Vector3 worldPosition, float deltaTime)
        {
            if (!isDragging || jellyMesh == null || deltaTime <= 0f)
            {
                return;
            }

            var rawVelocity = (Vector2)(worldPosition - previousWorldPosition) / deltaTime;
            previousWorldPosition = worldPosition;

            var simulationDelta = Mathf.Min(deltaTime, 1f / 30f);
            var smoothing = 1f - Mathf.Exp(-followSmoothness * simulationDelta);
            previousSmoothedVelocity = smoothedVelocity;
            smoothedVelocity = Vector2.Lerp(smoothedVelocity, rawVelocity, smoothing);

            var speedFactor = Mathf.Clamp01(smoothedVelocity.magnitude / velocityForMaxEffect);
            var direction = smoothedVelocity.sqrMagnitude > 0.0001f
                ? smoothedVelocity.normalized
                : Vector2.zero;
            var stretch = maxStretch * speedFactor;

            var targetScale = new Vector3(
                1f + stretch * Mathf.Abs(direction.x) - stretch * 0.62f * Mathf.Abs(direction.y),
                1f + stretch * Mathf.Abs(direction.y) - stretch * 0.62f * Mathf.Abs(direction.x),
                1f + stretch * 0.18f);
            var velocityChange = smoothedVelocity - previousSmoothedVelocity;
            var wobble = Mathf.Clamp(
                velocityChange.x / Mathf.Max(0.01f, velocityForMaxEffect), -1f, 1f);
            var targetRotationZ = -direction.x * maxRotation * speedFactor
                - wobble * maxRotation * directionWobble;
            var targetPosition = new Vector3(
                -direction.x * positionLag * speedFactor,
                -direction.y * positionLag * speedFactor,
                0f);

            var damping = Mathf.Exp(-springDamping * simulationDelta);
            scaleSpringVelocity += (targetScale - jellyMesh.localScale)
                * (springStrength * simulationDelta);
            scaleSpringVelocity *= damping;
            var nextScale = jellyMesh.localScale + scaleSpringVelocity * simulationDelta;
            nextScale.x = Mathf.Clamp(nextScale.x, 0.55f, 1.55f);
            nextScale.y = Mathf.Clamp(nextScale.y, 0.55f, 1.55f);
            nextScale.z = Mathf.Clamp(nextScale.z, 0.55f, 1.4f);
            jellyMesh.localScale = nextScale;

            positionSpringVelocity += (targetPosition - jellyMesh.localPosition)
                * (springStrength * simulationDelta);
            positionSpringVelocity *= damping;
            jellyMesh.localPosition += positionSpringVelocity * simulationDelta;

            var currentRotationZ = NormalizeAngle(jellyMesh.localEulerAngles.z);
            rotationSpringVelocity += Mathf.DeltaAngle(currentRotationZ, targetRotationZ)
                * (springStrength * simulationDelta);
            rotationSpringVelocity *= damping;
            currentRotationZ += rotationSpringVelocity * simulationDelta;
            jellyMesh.localRotation = Quaternion.Euler(0f, 0f, currentRotationZ);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        public void EndDragJiggle(bool successfulDrop)
        {
            if (jellyMesh == null)
            {
                return;
            }

            isDragging = false;
            smoothedVelocity = Vector2.zero;
            previousSmoothedVelocity = Vector2.zero;
            scaleSpringVelocity = Vector3.zero;
            positionSpringVelocity = Vector3.zero;
            rotationSpringVelocity = 0f;
            jellyMesh.DOKill();

            if (successfulDrop)
            {
                jellyMesh.localRotation = Quaternion.identity;
                jellyMesh.localPosition = Vector3.zero;
                PlaySquashImpact();
                return;
            }

            var sequence = DOTween.Sequence();
            sequence.Join(jellyMesh.DOScale(Vector3.one, settleDuration).SetEase(Ease.OutBack));
            sequence.Join(jellyMesh.DOLocalRotate(Vector3.zero, settleDuration).SetEase(Ease.OutCubic));
            sequence.Join(jellyMesh.DOLocalMove(Vector3.zero, settleDuration).SetEase(Ease.OutCubic));
        }

    // Gọi hàm này khi khối thạch RƠI XUỐNG ĐẤT hoặc được THẢ RA (OnDrop)
        public void PlaySquashImpact()
        {
            if (jellyMesh == null)
            {
                return;
            }

            jellyMesh.DOKill();
            jellyMesh.localScale = Vector3.one;
            jellyMesh.localRotation = Quaternion.identity;
            jellyMesh.localPosition = Vector3.zero;

            var sequence = DOTween.Sequence();
            sequence.Append(jellyMesh
                .DOScale(new Vector3(1.28f, 0.68f, 1.22f), 0.09f)
                .SetEase(Ease.OutQuad));
            sequence.Append(jellyMesh
                .DOScale(new Vector3(0.88f, 1.2f, 0.9f), 0.12f)
                .SetEase(Ease.OutCubic));
            sequence.Append(jellyMesh
                .DOScale(Vector3.one, 0.28f)
                .SetEase(Ease.OutElastic, 1.15f, 0.35f));
        }

    // Gọi hàm này khi NHẤC khối thạch lên (OnPickUp/Drag)
        public void PlayStretchEffect()
        {
            if (jellyMesh == null)
            {
                return;
            }

            jellyMesh.DOKill();
            jellyMesh.localScale = Vector3.one;
            jellyMesh.DOPunchScale(new Vector3(-0.2f, 0.4f, -0.2f), 0.4f, 3, 1f);
        }

        public void ResetVisual()
        {
            if (jellyMesh == null)
            {
                return;
            }

            jellyMesh.DOKill();
            jellyMesh.localScale = Vector3.one;
            jellyMesh.localRotation = Quaternion.identity;
            jellyMesh.localPosition = Vector3.zero;
            isDragging = false;
            smoothedVelocity = Vector2.zero;
            previousSmoothedVelocity = Vector2.zero;
            scaleSpringVelocity = Vector3.zero;
            positionSpringVelocity = Vector3.zero;
            rotationSpringVelocity = 0f;
        }

        private void OnDisable()
        {
            ResetVisual();
        }

        private void Reset()
        {
            jellyMesh = transform.Find("Visual/JellyVisual");
        }
    }
}
