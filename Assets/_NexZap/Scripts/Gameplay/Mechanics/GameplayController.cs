using System;
using System.Collections;
using DG.Tweening;
using NexZap.Gameplay.Items;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class GameplayController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PixelBoard pixelBoard;
        [SerializeField] private CircularPath circularPath;
        [SerializeField] private BlockQueue blockQueue;
        [SerializeField] private SelectionLineManager selectionLineManager;
        [SerializeField] private ColorBlockPool blockPool;
        [SerializeField] private Camera gameplayCamera;

        [Header("Config")]
        [Tooltip("Cứ mỗi quãng đường này block đi được thì fill 1 pixel -> fill bám theo block, không bỏ sót dù block đi nhanh.")]
        [SerializeField] private float fillStepDistance = 0.5f;

        public event Action LevelCompleted;

        private bool isLevelCompleted;

        public PixelBoard PixelBoard => pixelBoard;
        public CircularPath CircularPath => circularPath;
        public BlockQueue BlockQueue => blockQueue;
        public SelectionLineManager SelectionLineManager => selectionLineManager;

        private void Awake()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            TryHandleTap(Input.mousePosition);
        }

        public void Initialize()
        {
            isLevelCompleted = false;
            circularPath.BuildAround(pixelBoard);
            selectionLineManager.RefreshSelection();
        }

        // Dọn trạng thái đang chạy để chuẩn bị nạp lại level (phục vụ kiểm thử/reset).
        // PixelBoard/SelectionLine/Queue tự dọn block của mình khi build lại; ở đây chỉ cần
        // dừng coroutine và thu hồi các block đang chạy trên đường (đang là con của controller).
        public void ResetState()
        {
            StopAllCoroutines();

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var block = transform.GetChild(i).GetComponent<ColorBlock>();
                if (block != null)
                {
                    block.ReturnToPoolImmediate();
                }
            }

            isLevelCompleted = false;
        }

        public bool TrySelectLineBlock(SelectionLine line, ColorBlock block)
        {
            if (line == null || block == null || !block.IsSelectable)
            {
                return false;
            }

            if (!circularPath.CanAcceptBlock || blockQueue.IsFull)
            {
                return false;
            }

            if (!line.TryRemoveBlock(block))
            {
                return false;
            }

            block.PlayTapFeedback();
            block.transform.SetParent(transform, true);
            selectionLineManager.RefreshSelection();

            StartCoroutine(TravelAndResolve(block));
            return true;
        }

        private void TryHandleTap(Vector2 screenPosition)
        {
            if (gameplayCamera == null)
            {
                return;
            }

            var zDistance = Mathf.Abs(gameplayCamera.transform.position.z);
            var worldPoint = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
            worldPoint.z = 0f;
            var hit = Physics2D.OverlapPoint(worldPoint);

            if (hit == null)
            {
                return;
            }

            var block = hit.GetComponentInParent<ColorBlock>();
            if (block == null || !block.IsSelectable)
            {
                return;
            }

            // Tap block đang trong hàng đợi -> chạy lại vòng (loop tới khi hết màu).
            if (blockQueue.Contains(block))
            {
                TrySelectQueueBlock(block);
                return;
            }

            for (var i = 0; i < selectionLineManager.Lines.Count; i++)
            {
                var line = selectionLineManager.Lines[i];
                if (line.Contains(block))
                {
                    TrySelectLineBlock(line, block);
                    break;
                }
            }
        }

        public bool TrySelectQueueBlock(ColorBlock block)
        {
            if (block == null || !circularPath.CanAcceptBlock)
            {
                return false;
            }

            if (!blockQueue.TryRemove(block))
            {
                return false;
            }

            block.PlayTapFeedback();
            block.transform.SetParent(transform, true);

            StartCoroutine(TravelAndResolve(block));
            return true;
        }

        // Block chạy 1 vòng; mỗi lần chạy fill trong phạm vi lớp peel (PixelBoard.fillPeelLayersPerWave), không lan sâu hơn.
        private IEnumerator TravelAndResolve(ColorBlock block)
        {
            var tween = circularPath.SendBlock(block);
            if (tween == null)
            {
                ResolveBlockAfterTravel(block);
                yield break;
            }

            pixelBoard.BeginFillWave(block.ColorId);

            var lastPosition = block.transform.position;
            var movedDistance = 0f;
            try
            {
                while (block != null && block.HasCapacity && tween.IsActive() && !tween.IsComplete())
                {
                    var currentPosition = block.transform.position;
                    movedDistance += Vector3.Distance(currentPosition, lastPosition);
                    lastPosition = currentPosition;

                    while (movedDistance >= fillStepDistance && block.HasCapacity)
                    {
                        if (!TryFillOnePixel(block))
                        {
                            movedDistance = Mathf.Min(movedDistance, fillStepDistance);
                            break;
                        }

                        movedDistance -= fillStepDistance;
                    }

                    yield return null;
                }
            }
            finally
            {
                pixelBoard.EndFillWave();
            }

            if (block != null && !block.HasCapacity && tween.IsActive())
            {
                tween.Kill();
                circularPath.ReleaseBlock(block);
            }

            ResolveBlockAfterTravel(block);
        }

        private bool TryFillOnePixel(ColorBlock block)
        {
            if (block == null || !block.HasCapacity)
            {
                return false;
            }

            // Fill pixel gần vị trí hiện tại của block nhất -> block đi tới đâu fill tới đó.
            if (!pixelBoard.TryFillNearest(block.ColorId, block.transform.position))
            {
                return false;
            }

            block.ConsumeCapacity(1);
            CheckLevelCompleted();
            return true;
        }

        private void ResolveBlockAfterTravel(ColorBlock block)
        {
            if (block == null)
            {
                return;
            }

            // Hết màu -> trả về pool (tắt object, không Destroy).
            if (!block.HasCapacity)
            {
                block.Despawn();
                CheckLevelCompleted();
                return;
            }

            // Còn màu -> vào hàng đợi, chờ người chơi tap để chạy lại vòng.
            if (!blockQueue.TryEnqueue(block))
            {
                Debug.LogWarning("Queue is full. Block could not be enqueued.");
            }

            CheckLevelCompleted();
        }

        private void CheckLevelCompleted()
        {
            if (isLevelCompleted || !pixelBoard.IsComplete)
            {
                return;
            }

            isLevelCompleted = true;
            LevelCompleted?.Invoke();
        }
    }
}
