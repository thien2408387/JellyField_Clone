using NexZap.Data;
using NexZap.Gameplay.Mechanics;
using UnityEngine;

namespace NexZap.Gameplay.Level
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private BaseLevel currentLevel;
        [SerializeField] private GameplayController gameplayController;

        [Header("Testing")]
        [Tooltip("Phím bấm để chơi lại level từ đầu (hỗ trợ kiểm thử). None = tắt.")]
        [SerializeField] private KeyCode reloadKey = KeyCode.R;

        private BaseLevel subscribedLevel;

        public BaseLevel CurrentLevel => currentLevel;

        private void Start()
        {
            if (currentLevel != null)
            {
                LoadLevel(currentLevel);
            }
        }

        private void Update()
        {
            if (reloadKey != KeyCode.None && Input.GetKeyDown(reloadKey))
            {
                ReloadLevel();
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

            if (gameplayController != null)
            {
                gameplayController.ResetState();
            }

            LoadLevel(currentLevel);
        }

        public void LoadLevel(BaseLevel levelData)
        {
            currentLevel = levelData;
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
        }
    }
}
