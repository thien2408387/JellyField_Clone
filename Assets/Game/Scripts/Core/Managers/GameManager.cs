using NexZap.Gameplay.Level;
using NexZap.Gameplay.Mechanics;
using UnityEngine;

namespace NexZap.Managers
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private GameplayController gameplayController;

        public LevelManager LevelManager => levelManager;
        public GameplayController GameplayController => gameplayController;

        public void RestartLevel()
        {
            if (levelManager?.CurrentLevel != null)
            {
                levelManager.LoadLevel(levelManager.CurrentLevel);
            }
        }
    }
}
