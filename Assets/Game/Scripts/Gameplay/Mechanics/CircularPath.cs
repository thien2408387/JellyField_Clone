using System.Collections.Generic;
using DG.Tweening;
using NexZap.Gameplay.Items;
using UnityEngine;

namespace NexZap.Gameplay.Mechanics
{
    public class CircularPath : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [Tooltip("Khoảng cách từ mép ngoài cùng của pixel ra đường viền (mỗi phía). Giảm để viền sát pixel hơn.")]
        [SerializeField] private float padding = 0.25f;
        [SerializeField] private float moveDuration = 1.5f;
        [SerializeField] private int cornerSegments = 8;

        private readonly List<Vector3> waypoints = new();
        private ColorBlock activeBlock;
        private bool isBusy;

        public bool CanAcceptBlock => !isBusy;

        // Giải phóng path khi block rời sớm (bị kill tween trước khi chạy hết vòng).
        // Nếu không gọi, isBusy kẹt true và không block nào lên path được nữa.
        public void ReleaseBlock(ColorBlock block)
        {
            if (block != null && activeBlock != block)
            {
                return;
            }

            activeBlock = null;
            isBusy = false;
        }

        public void BuildAround(PixelBoard board)
        {
            // Dựng lại path => path trống, đảm bảo không kẹt trạng thái bận từ lần chơi trước.
            activeBlock = null;
            isBusy = false;

            waypoints.Clear();

            var bounds = board.WorldBounds;
            var min = bounds.min;
            var max = bounds.max;
            min -= new Vector3(padding, padding, 0f);
            max += new Vector3(padding, padding, 0f);

            AddEdge(min.x, min.y, max.x, min.y);
            AddEdge(max.x, min.y, max.x, max.y);
            AddEdge(max.x, max.y, min.x, max.y);
            AddEdge(min.x, max.y, min.x, min.y);

            DrawPath();
        }

        /// <summary>
        /// Đưa block lên vòng và chạy đúng 1 vòng. Chỉ lo di chuyển; việc fill/định tuyến
        /// sau khi xong do GameplayController xử lý. Trả về Tween để caller theo dõi tiến trình.
        /// </summary>
        public Tween SendBlock(ColorBlock block)
        {
            if (!CanAcceptBlock || waypoints.Count < 2)
            {
                return null;
            }

            isBusy = true;
            activeBlock = block;
            block.SetState(ColorBlockState.OnPath);

            var path = new Vector3[waypoints.Count];
            for (var i = 0; i < waypoints.Count; i++)
            {
                path[i] = waypoints[i];
            }

            block.transform.position = path[0];
            return block.transform
                .DOPath(path, moveDuration, PathType.Linear, PathMode.Full3D, 10)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    activeBlock = null;
                    isBusy = false;
                });
        }

        private void AddEdge(float x1, float y1, float x2, float y2)
        {
            for (var i = 0; i <= cornerSegments; i++)
            {
                var t = i / (float)cornerSegments;
                var point = new Vector3(Mathf.Lerp(x1, x2, t), Mathf.Lerp(y1, y2, t), 0f);
                if (waypoints.Count == 0 || Vector3.Distance(waypoints[^1], point) > 0.01f)
                {
                    waypoints.Add(point);
                }
            }
        }

        private void DrawPath()
        {
            if (lineRenderer == null || waypoints.Count == 0)
            {
                return;
            }

            lineRenderer.positionCount = waypoints.Count;
            lineRenderer.SetPositions(waypoints.ToArray());
        }

        private void OnDestroy()
        {
            if (activeBlock != null)
            {
                activeBlock.transform.DOKill();
            }
        }
    }
}
