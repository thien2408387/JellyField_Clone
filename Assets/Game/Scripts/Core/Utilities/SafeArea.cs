using UnityEngine;

namespace NexZap.Utilities
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        [Tooltip("RectTransform chứa UI cần được điều chỉnh theo Safe Area.")]
        [SerializeField] private RectTransform targetRect;

        private Rect lastSafeArea = Rect.zero;
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = targetRect != null ? targetRect : GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (SafeAreaChanged())
            {
                ApplySafeArea();
            }
        }

        private bool SafeAreaChanged()
        {
            Rect safeArea = Screen.safeArea;
            return safeArea != lastSafeArea || Screen.width != rectTransform.rect.width || Screen.height != rectTransform.rect.height;
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea == lastSafeArea)
                return;

            lastSafeArea = safeArea;

            if (rectTransform == null)
                rectTransform = targetRect != null ? targetRect : GetComponent<RectTransform>();

            if (rectTransform == null)
                return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}