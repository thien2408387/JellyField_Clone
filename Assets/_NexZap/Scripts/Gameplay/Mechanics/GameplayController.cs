using System;
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

        public event Action LevelCompleted;
        public event Action<int, int> TargetChanged;

        private bool isLevelCompleted;
        private ColorBlock draggedBlock;
        private SelectionLine draggedFromLine;
        private Transform dragOriginalParent;
        private Vector3 dragOriginalLocalPosition;

        public PixelBoard PixelBoard => pixelBoard;
        public CircularPath CircularPath => circularPath;
        public BlockQueue BlockQueue => blockQueue;
        public SelectionLineManager SelectionLineManager => selectionLineManager;
        public int RemainingTarget => pixelBoard != null ? pixelBoard.RemainingTarget : 0;
        public int TotalTarget => pixelBoard != null ? pixelBoard.TotalTarget : 0;

        private void Awake()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                BeginDrag(Input.mousePosition);
            }

            if (draggedBlock != null && Input.GetMouseButton(0))
            {
                draggedBlock.transform.position = ScreenToWorld(Input.mousePosition);
            }

            if (draggedBlock != null && Input.GetMouseButtonUp(0))
            {
                EndDrag(Input.mousePosition);
            }
        }

        public void Initialize()
        {
            isLevelCompleted = false;
            draggedBlock = null;
            draggedFromLine = null;
            selectionLineManager.RefreshSelection();
            TargetChanged?.Invoke(RemainingTarget, TotalTarget);
            CheckLevelCompleted();
        }

        public void ResetState()
        {
            StopAllCoroutines();
            draggedBlock = null;
            draggedFromLine = null;

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

        private void BeginDrag(Vector2 screenPosition)
        {
            if (isLevelCompleted || gameplayCamera == null)
            {
                return;
            }

            var hit = Physics2D.OverlapPoint(ScreenToWorld(screenPosition));
            var block = hit != null ? hit.GetComponentInParent<ColorBlock>() : null;
            if (block == null || !block.IsSelectable)
            {
                return;
            }

            SelectionLine sourceLine = null;
            foreach (var line in selectionLineManager.Lines)
            {
                if (line.Contains(block))
                {
                    sourceLine = line;
                    break;
                }
            }

            if (sourceLine == null)
            {
                return;
            }

            draggedBlock = block;
            draggedFromLine = sourceLine;
            dragOriginalParent = block.transform.parent;
            dragOriginalLocalPosition = block.transform.localPosition;
            block.PlayTapFeedback();
            block.SetState(ColorBlockState.Filling);
            block.transform.SetParent(transform, true);
        }

        private void EndDrag(Vector2 screenPosition)
        {
            var block = draggedBlock;
            var sourceLine = draggedFromLine;
            draggedBlock = null;
            draggedFromLine = null;

            if (block == null || sourceLine == null)
            {
                return;
            }

            var worldPosition = ScreenToWorld(screenPosition);
            if (pixelBoard.TryResolveDrop(worldPosition, block.ColorId, out var removedCount))
            {
                sourceLine.TryRemoveBlock(block);
                pixelBoard.TryGetDropWorldPosition(worldPosition, out var snappedPosition);
                block.transform.position = snappedPosition;
                block.ConsumeCapacity(removedCount);
                block.Despawn();
                selectionLineManager.RefreshSelection();
                TargetChanged?.Invoke(RemainingTarget, TotalTarget);
                CheckLevelCompleted();
                return;
            }

            block.transform.SetParent(dragOriginalParent, false);
            block.transform.localPosition = dragOriginalLocalPosition;
            block.SetState(ColorBlockState.Idle);
            selectionLineManager.RefreshSelection();
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var zDistance = Mathf.Abs(gameplayCamera.transform.position.z);
            var point = gameplayCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, zDistance));
            point.z = 0f;
            return point;
        }

        private void CheckLevelCompleted()
        {
            if (isLevelCompleted || pixelBoard == null || !pixelBoard.IsTargetComplete)
            {
                return;
            }

            isLevelCompleted = true;
            LevelCompleted?.Invoke();
        }
    }
}
