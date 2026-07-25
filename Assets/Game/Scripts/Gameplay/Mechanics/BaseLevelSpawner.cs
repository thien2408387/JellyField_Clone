using System.Collections.Generic;
using NexZap.Data;
using UnityEngine;

namespace NexZap.Gameplay
{
    public class BaseLevelSpawner : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [SerializeField] private BaseLevel level;

        [Header("Tuỳ chọn")]
        [Tooltip("Nơi chứa pixel sinh ra. Để trống thì dùng chính object này.")]
        [SerializeField] private Transform pixelsRoot;

        [Tooltip("Độ mờ màu gợi ý của ô chưa fill (0 = tối, 1 = đậm như khi fill).")]
        [SerializeField, Range(0f, 1f)] private float previewStrength = 0.3f;

        [SerializeField] private bool spawnOnStart = true;

        // Tra cứu nhanh pixel theo toạ độ lưới (để sau này fill).
        private readonly Dictionary<Vector2Int, RuntimePixel> pixels = new();
        public IReadOnlyDictionary<Vector2Int, RuntimePixel> Pixels => pixels;

        private BaseLevel subscribedLevel;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
            }
        }

        // Realtime Update
        private void Subscribe()
        {
            if (level == subscribedLevel)
            {
                return;
            }

            Unsubscribe();
            if (level != null)
            {
                level.Changed += HandleLevelChanged;
                subscribedLevel = level;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedLevel != null)
            {
                subscribedLevel.Changed -= HandleLevelChanged;
                subscribedLevel = null;
            }
        }

        private void HandleLevelChanged()
        {
            // Chỉ dựng lại khi đang chạy game; lúc edit thường không có pixel để cập nhật.
            if (Application.isPlaying)
            {
                Spawn();
            }
        }

        public void Spawn()
        {
            Clear();
            if (level == null)
            {
                Debug.LogWarning("[BaseLevelSpawner] Chưa gán BaseLevel.");
                return;
            }

            // Đảm bảo đang nghe đúng level 
            Subscribe();
            var root = pixelsRoot != null ? pixelsRoot : transform;
            var spacing = level.spacing;
            // Tính offset để hình nằm giữa
            var offsetX = -(level.width - 1) * spacing * 0.5f;
            var offsetY = -(level.height - 1) * spacing * 0.5f;
            var library = level.ResolvePixelMaterialLibrary();
            for (var x = 0; x < level.width; x++)
            {
                for (var y = 0; y < level.height; y++)
                {
                    var colorId = level.GetCellColorId(x, y);
                    if (string.IsNullOrEmpty(colorId))
                    {
                        continue;
                    }
                    var gridPos = new Vector2Int(x, y);
                    var cell = new GameObject($"Pixel_{x}_{y}");
                    cell.transform.SetParent(root, false);
                    cell.transform.localPosition = new Vector3(
                        offsetX + x * spacing,
                        offsetY + y * spacing,
                        0f);
                    cell.transform.localScale = new Vector3(level.pixelScale.x, level.pixelScale.y, 1f);
                    var pixel = cell.AddComponent<RuntimePixel>();
                    pixel.Setup(gridPos, colorId, library, level.idlePixelPrefab, level.fillPixelPrefab, previewStrength);
                    pixels[gridPos] = pixel;
                }
            }
        }

        public bool TryFill(Vector2Int gridPos, string colorId)
        {
            return pixels.TryGetValue(gridPos, out var pixel) && pixel.Fill(colorId);
        }
        public void Clear()
        {
            foreach (var pixel in pixels.Values)
            {
                if (pixel != null)
                {
                    Destroy(pixel.gameObject);
                }
            }
            pixels.Clear();
        }
    }
}