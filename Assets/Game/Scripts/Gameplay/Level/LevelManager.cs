using System.Collections.Generic;
using NexZap.Data;
using NexZap.Gameplay.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace NexZap.Gameplay.Level
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private BaseLevel currentLevel;
        [SerializeField] private List<BaseLevel> levels = new();
        [SerializeField] private GameplayController gameplayController;

        [Header("Testing")]
        [Tooltip("Phím bấm để chơi lại level từ đầu (hỗ trợ kiểm thử). None = tắt.")]
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [Tooltip("Phím chuyển sang level tiếp theo. None = tắt.")]
        [SerializeField] private KeyCode nextLevelKey = KeyCode.N;

        [Header("Runtime Controls")]
        [SerializeField] private bool showRuntimeControls = true;
        [SerializeField] private bool loopLevels = true;

        private BaseLevel subscribedLevel;
        private Canvas controlsCanvas;
        private int lastHotkeyFrame = -1;

        public BaseLevel CurrentLevel => currentLevel;
        public IReadOnlyList<BaseLevel> Levels => levels;

        private void Start()
        {
            EnsureCurrentLevelIsRegistered();

            if (currentLevel != null)
            {
                LoadLevel(currentLevel);
            }

            if (showRuntimeControls)
            {
                CreateRuntimeControls();
            }
        }

        private void Update()
        {
            if (reloadKey != KeyCode.None && Input.GetKeyDown(reloadKey))
            {
                ExecuteHotkey(reloadKey);
            }

            if (nextLevelKey != KeyCode.None && Input.GetKeyDown(nextLevelKey))
            {
                ExecuteHotkey(nextLevelKey);
            }
        }

        // OnGUI is a reliable fallback for legacy keyboard events in the Editor.
        // Frame debouncing prevents the same key from executing in both callbacks.
        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == reloadKey || currentEvent.keyCode == nextLevelKey)
            {
                ExecuteHotkey(currentEvent.keyCode);
                currentEvent.Use();
            }
        }

        private void ExecuteHotkey(KeyCode keyCode)
        {
            if (lastHotkeyFrame == Time.frameCount)
            {
                return;
            }

            lastHotkeyFrame = Time.frameCount;
            if (keyCode == reloadKey)
            {
                ReloadLevel();
            }
            else if (keyCode == nextLevelKey)
            {
                LoadNextLevel();
            }
        }

        // Reset level hiện tại về trạng thái ban đầu: dọn block đang chạy + dựng lại bàn pixel/line/queue.
        [ContextMenu("Reload Level (Reset)")]
        public void ReloadLevel()
        {
            if (currentLevel == null)
            {
                return;
            }

            LoadLevel(currentLevel);
        }

        public void LoadNextLevel()
        {
            RemoveNullLevels();
            if (levels.Count == 0)
            {
                return;
            }

            var currentIndex = levels.IndexOf(currentLevel);
            var nextIndex = currentIndex < 0 ? 0 : currentIndex + 1;
            if (nextIndex >= levels.Count)
            {
                if (!loopLevels)
                {
                    return;
                }

                nextIndex = 0;
            }

            LoadLevel(levels[nextIndex]);
        }

        public void LoadLevel(BaseLevel levelData)
        {
            if (levelData == null || gameplayController == null)
            {
                return;
            }

            gameplayController.ResetState();
            currentLevel = levelData;
            EnsureCurrentLevelIsRegistered();
            SubscribeToLevel(levelData);

            gameplayController.PixelBoard.Build(levelData);
            gameplayController.SelectionLineManager.Build(levelData);
            gameplayController.BlockQueue.Clear();
            gameplayController.Initialize();

            gameplayController.LevelCompleted -= HandleLevelCompleted;
            gameplayController.LevelCompleted += HandleLevelCompleted;
        }

        // Nghe thay đổi của level và dựng lại toàn bộ board để màu/vị trí luôn khớp editor.
        private void SubscribeToLevel(BaseLevel levelData)
        {
            if (subscribedLevel == levelData)
            {
                return;
            }

            if (subscribedLevel != null)
            {
                subscribedLevel.Changed -= HandleLevelChanged;
            }

            subscribedLevel = levelData;
            if (subscribedLevel != null)
            {
                subscribedLevel.Changed += HandleLevelChanged;
            }
        }

        private void HandleLevelChanged()
        {
            // Chỉ cập nhật khi đang chạy game (lúc đó mới có bàn pixel + path để chỉnh).
            if (!Application.isPlaying || gameplayController == null || currentLevel == null)
            {
                return;
            }

            ReloadLevel();
        }

        private void HandleLevelCompleted()
        {
            Debug.Log($"Level complete: {currentLevel.name}");
        }

        private void EnsureCurrentLevelIsRegistered()
        {
            RemoveNullLevels();
            if (currentLevel != null && !levels.Contains(currentLevel))
            {
                levels.Add(currentLevel);
            }
        }

        private void RemoveNullLevels()
        {
            levels.RemoveAll(level => level == null);
        }

        private void CreateRuntimeControls()
        {
            if (controlsCanvas != null)
            {
                return;
            }

            var canvasObject = new GameObject(
                "GameplayControlsCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            controlsCanvas = canvasObject.GetComponent<Canvas>();
            controlsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            controlsCanvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject(
                "LevelControls",
                typeof(RectTransform),
                typeof(Image),
                typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(canvasObject.transform, false);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = new Vector2(-24f, -24f);
            panelRect.sizeDelta = new Vector2(372f, 76f);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.04f, 0.04f, 0.06f, 0.72f);
            panelImage.raycastTarget = false;

            var layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateButton(panel.transform, "ResetButton", "RESET", ReloadLevel,
                new Color(0.92f, 0.29f, 0.35f, 1f));
            CreateButton(panel.transform, "NextLevelButton", "NEXT", LoadNextLevel,
                new Color(0.25f, 0.68f, 1f, 1f));
        }

        private static void CreateButton(
            Transform parent,
            string objectName,
            string label,
            UnityEngine.Events.UnityAction action,
            Color color)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minWidth = 170f;
            layoutElement.minHeight = 60f;

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 25;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (gameplayController != null)
            {
                gameplayController.LevelCompleted -= HandleLevelCompleted;
            }

            if (subscribedLevel != null)
            {
                subscribedLevel.Changed -= HandleLevelChanged;
                subscribedLevel = null;
            }

            if (controlsCanvas != null)
            {
                Destroy(controlsCanvas.gameObject);
                controlsCanvas = null;
            }
        }
    }
}
