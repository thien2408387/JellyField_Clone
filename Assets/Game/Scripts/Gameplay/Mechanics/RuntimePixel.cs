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

            var full = library != null ? library.GetTint(TargetColorId) : Color.gray;
            var dark = new Color(0.15f, 0.15f, 0.18f, 1f);

            // Visual idle: tô màu mờ
            if (idlePrefab != null)
            {
                idleVisual = Instantiate(idlePrefab, transform);
                idleVisual.transform.localPosition = Vector3.zero;
                Tint(idleVisual, Color.Lerp(dark, full, previewStrength));
            }

            // Visual fill: tô màu đậm, ẩn sẵn cho tới khi được fill
            if (fillPrefab != null)
            {
                fillVisual = Instantiate(fillPrefab, transform);
                fillVisual.transform.localPosition = Vector3.zero;
                Tint(fillVisual, full);
                fillVisual.SetActive(false);
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
            return true;
        }

        // Tô màu cho mọi SpriteRenderer bên trong visual (phòng khi prefab có nhiều lớp).
        private static void Tint(GameObject visual, Color color)
        {
            var renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.color = color;
            }
        }
    }
}

