using System.Collections.Generic;
using Sirenix.OdinInspector;
using TBN;
using UnityEngine;

public class PixelClusterRigidbody : MonoBehaviour
{
    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
    };

    [SerializeField] private float massPerCube = 1f;
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool allowFurtherSplitting = true;
    private PixelClusterRigidbody splitClusterPrefab => this;
    private bool rebuildQueued;
    private bool rebuildInProgress;
    private readonly List<PixelCubeCell> cells = new List<PixelCubeCell>(64);
    private readonly HashSet<PixelCubeCell> cellLookup = new HashSet<PixelCubeCell>();
    /// <summary>All cells per logical grid slot (multiple cubes may share one slot — overwriting a single cell breaks island detection and visuals).</summary>
    private readonly Dictionary<Vector2Int, List<PixelCubeCell>> cellsByGrid = new Dictionary<Vector2Int, List<PixelCubeCell>>(64);
    private readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();
    private readonly List<List<PixelCubeCell>> islands = new List<List<PixelCubeCell>>(8);
    private readonly Stack<List<PixelCubeCell>> islandPool = new Stack<List<PixelCubeCell>>(8);
    private readonly List<PixelCubeCell> stableCellsBuffer = new List<PixelCubeCell>(64);
    private readonly HashSet<PixelCubeCell> breakingCellsBuffer = new HashSet<PixelCubeCell>();
    private readonly HashSet<PixelCubeCell> keepOnRootBuffer = new HashSet<PixelCubeCell>();
    private readonly List<PixelCubeCell> childCellsBuffer = new List<PixelCubeCell>(128);

    private const int MaxSplitClustersPerRebuild = 16;

    public void Configure(float newMassPerCube)
    {
        massPerCube = Mathf.Max(0.01f, newMassPerCube);
    }

    public void SetAllowFurtherSplitting(bool isAllowed)
    {
        allowFurtherSplitting = isAllowed;
    }

    private void Start()
    {
        RegisterAllCurrentChildren();

        if (rebuildOnStart)
        {
            RebuildDisconnectedIslands();
        }
        else
        {
            EnsureRootBody();
            UpdateRootMass(cells.Count);
            UpdateCenterOfMass();
        }
    }

    private void LateUpdate()
    {
        if (!rebuildQueued)
        {
            return;
        }

        rebuildQueued = false;
        RebuildDisconnectedIslands();
    }

    public void RemoveCell(PixelCubeCell cubeCell)
    {
        if (cubeCell == null)
        {
            return;
        }

        UnregisterCell(cubeCell);
        cubeCell.gameObject.Recycle();
        QueueRebuild();
    }

    public void OnCellDisabled(PixelCubeCell cubeCell)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (cubeCell != null)
        {
            UnregisterCell(cubeCell);
        }

        if (!rebuildInProgress)
        {
            QueueRebuild();
        }
    }

    public void RegisterCell(PixelCubeCell cubeCell)
    {
        if (cubeCell == null || cubeCell.GetComponentInParent<PixelClusterRigidbody>() != this)
        {
            return;
        }

        if (cellLookup.Add(cubeCell))
        {
            cells.Add(cubeCell);
        }

        AddCellToGridBucket(cubeCell);
    }

    public void UnregisterCell(PixelCubeCell cubeCell)
    {
        if (cubeCell == null || !cellLookup.Remove(cubeCell))
        {
            return;
        }

        cells.Remove(cubeCell);
        RemoveCellFromGridBucket(cubeCell);
    }

    public void RemoveCellAt(Vector2Int gridPosition)
    {
        if (cellsByGrid.TryGetValue(gridPosition, out List<PixelCubeCell> bucket)
            && bucket != null
            && bucket.Count > 0
            && bucket[0] != null)
        {
            RemoveCell(bucket[0]);
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].GridPosition == gridPosition)
            {
                RemoveCell(cells[i]);
                return;
            }
        }
    }

    [ContextMenu("Rebuild Disconnected Islands")]
    [Button]
    public void RebuildDisconnectedIslands()
    {
        if (rebuildInProgress)
        {
            return;
        }

        rebuildInProgress = true;
        try
        {
            RebuildDisconnectedIslandsCore();
        }
        finally
        {
            rebuildInProgress = false;
        }
    }

    private void RebuildDisconnectedIslandsCore()
    {
        // Re-scan children before every rebuild so split logic never relies on stale registration state.
        RegisterAllCurrentChildren();
        CompactRegistry();
        WakeBody();
        if (cells.Count == 0)
        {
            gameObject.Recycle();
            return;
        }

        // Rebuild/split should only consider stable cells. Cells already breaking are preserved
        // on current cluster and will be removed naturally by their break finalize flow.
        stableCellsBuffer.Clear();
        breakingCellsBuffer.Clear();
        cellsByGrid.Clear();
        for (int i = cells.Count - 1; i >= 0; i--)
        {
            PixelCubeCell cell = cells[i];
            if (cell == null)
            {
                if (cell != null)
                {
                    cellLookup.Remove(cell);
                }
                cells.RemoveAt(i);
                continue;
            }

            if (cell.IsBreaking)
            {
                breakingCellsBuffer.Add(cell);
                continue;
            }

            stableCellsBuffer.Add(cell);
            AddCellToGridBucket(cell);
        }

        if (stableCellsBuffer.Count == 0)
        {
            EnsureRootBody();
            UpdateRootMass(cells.Count);
            UpdateCenterOfMass();
            RefreshOuterCellColliders();
            WakeBody();
            ReleaseIslandLists();
            if (PixelCubeManager.Instance != null)
            {
                PixelCubeManager.Instance.RefreshAllCells();
            }
            return;
        }

        BuildIslandsFromRegistry();
        if (islands.Count <= 1 || !allowFurtherSplitting)
        {
            RecenterTransformToChildren(transform, stableCellsBuffer);
            EnsureRootBody();
            UpdateRootMass(stableCellsBuffer.Count);
            UpdateCenterOfMass();
            RefreshOuterCellColliders();
            WakeBody();
            ReleaseIslandLists();
            if (PixelCubeManager.Instance != null)
            {
                PixelCubeManager.Instance.RefreshAllCells();
            }
            return;
        }

        islands.Sort((a, b) => b.Count.CompareTo(a.Count));

        keepOnRootBuffer.Clear();
        List<PixelCubeCell> rootIsland = islands[0];
        for (int i = 0; i < rootIsland.Count; i++)
        {
            PixelCubeCell rootCell = rootIsland[i];
            if (rootCell != null)
            {
                keepOnRootBuffer.Add(rootCell);
            }
        }

        foreach (PixelCubeCell breakingCell in breakingCellsBuffer)
        {
            if (breakingCell != null)
            {
                keepOnRootBuffer.Add(breakingCell);
            }
        }
        int splitCreatedCount = 0;

        for (int i = 1; i < islands.Count; i++)
        {
            List<PixelCubeCell> island = islands[i];
            if (island == null || island.Count == 0)
            {
                continue;
            }

            bool splitLimitReached = splitCreatedCount >= MaxSplitClustersPerRebuild;
            if (splitLimitReached || splitClusterPrefab == null)
            {
                for (int j = 0; j < island.Count; j++)
                {
                    if (island[j] != null)
                    {
                        keepOnRootBuffer.Add(island[j]);
                    }
                }
                continue;
            }

            CreateSplitCluster(island);
            splitCreatedCount++;
        }

        for (int i = cells.Count - 1; i >= 0; i--)
        {
            PixelCubeCell cell = cells[i];
            if (cell == null || cell.GetComponentInParent<PixelClusterRigidbody>() != this)
            {
                if (cell != null)
                {
                    cellLookup.Remove(cell);
                }
                cells.RemoveAt(i);
                continue;
            }

            if (cell.IsBreaking)
            {
                continue;
            }

            if (!keepOnRootBuffer.Contains(cell))
            {
                UnregisterCell(cell);
            }
        }

        EnsureRootBody();
        UpdateRootMass(stableCellsBuffer.Count);
        UpdateCenterOfMass();
        RecenterTransformToChildren(transform, stableCellsBuffer);
        RefreshOuterCellColliders();
        WakeBody();
        ReleaseIslandLists();

        // Reparent/split may temporarily desync manager color buckets; resync once per rebuild.
        if (PixelCubeManager.Instance != null)
        {
            PixelCubeManager.Instance.RefreshAllCells();
        }
    }

    private void AddCellToGridBucket(PixelCubeCell cubeCell)
    {
        Vector2Int gp = cubeCell.GridPosition;
        if (!cellsByGrid.TryGetValue(gp, out List<PixelCubeCell> bucket))
        {
            bucket = new List<PixelCubeCell>(2);
            cellsByGrid[gp] = bucket;
        }

        if (!bucket.Contains(cubeCell))
        {
            bucket.Add(cubeCell);
        }
    }

    private void RemoveCellFromGridBucket(PixelCubeCell cubeCell)
    {
        Vector2Int gp = cubeCell.GridPosition;
        if (!cellsByGrid.TryGetValue(gp, out List<PixelCubeCell> bucket))
        {
            return;
        }

        bucket.Remove(cubeCell);
        if (bucket.Count == 0)
        {
            cellsByGrid.Remove(gp);
        }
    }

    private void QueueRebuild()
    {
        rebuildQueued = true;
    }

    private void CreateSplitCluster(List<PixelCubeCell> island)
    {
        if (splitClusterPrefab == null)
        {
            return;
        }

        // Build an empty split root to avoid cloning + clearing all child cubes (large spikes from Instantiate/Deactivate/Activate).
        GameObject clusterObject = new GameObject($"{gameObject.name}_Split");
        Transform clusterTransform = clusterObject.transform;
        clusterTransform.SetParent(transform.parent, true);
        clusterTransform.SetPositionAndRotation(transform.position, transform.rotation);
        clusterTransform.localScale = transform.localScale;

        PixelClusterRigidbody cluster = clusterObject.AddComponent<PixelClusterRigidbody>();
        if (cluster == null)
        {
            Object.Destroy(clusterObject);
            return;
        }

        // Island is already one BFS component — no split pass needed; avoids nested Rebuild during parent rebuild.
        cluster.rebuildOnStart = false;
        cluster.Configure(massPerCube);

        for (int i = 0; i < island.Count; i++)
        {
            if (island[i] != null)
            {
                island[i].transform.SetParent(clusterTransform, true);
            }
        }

        RecenterTransformToChildren(clusterTransform, island);
    }

    private void RegisterAllCurrentChildren()
    {
        cells.Clear();
        cellLookup.Clear();
        cellsByGrid.Clear();

        childCellsBuffer.Clear();
        GetComponentsInChildren(true, childCellsBuffer);
        for (int i = 0; i < childCellsBuffer.Count; i++)
        {
            PixelCubeCell cell = childCellsBuffer[i];
            if (cell == null || cell.GetComponentInParent<PixelClusterRigidbody>() != this)
            {
                continue;
            }

            RegisterCell(cell);
        }

        RefreshOuterCellColliders();
    }

    private void CompactRegistry()
    {
        cellsByGrid.Clear();

        for (int i = cells.Count - 1; i >= 0; i--)
        {
            PixelCubeCell cell = cells[i];
            if (cell == null || cell.GetComponentInParent<PixelClusterRigidbody>() != this || !cell.isActiveAndEnabled)
            {
                if (cell != null)
                {
                    cellLookup.Remove(cell);
                }
                cells.RemoveAt(i);
                continue;
            }

            AddCellToGridBucket(cell);
        }
    }

    private void UpdateCenterOfMass()
    {
    }

    private void WakeBody()
    {
    }

    private static void RecenterTransformToChildren(Transform clusterTransform, List<PixelCubeCell> clusterCells)
    {
        if (clusterTransform == null || clusterCells == null || clusterCells.Count == 0)
        {
            return;
        }

        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < clusterCells.Count; i++)
        {
            PixelCubeCell cell = clusterCells[i];
            if (cell == null)
            {
                continue;
            }

            center += cell.transform.position;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        center /= count;
        if ((center - clusterTransform.position).sqrMagnitude < 0.000001f)
        {
            return;
        }

        int childCount = clusterTransform.childCount;
        Transform[] children = new Transform[childCount];
        Vector3[] childWorldPositions = new Vector3[childCount];
        Quaternion[] childWorldRotations = new Quaternion[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform child = clusterTransform.GetChild(i);
            children[i] = child;
            childWorldPositions[i] = child.position;
            childWorldRotations[i] = child.rotation;
        }

        clusterTransform.position = center;

        for (int i = 0; i < childCount; i++)
        {
            if (children[i] == null)
            {
                continue;
            }

            children[i].SetPositionAndRotation(childWorldPositions[i], childWorldRotations[i]);
        }
    }

    /// <summary>Physics-driven clusters disabled — strip legacy <see cref="Rigidbody"/> from prefabs/scenes.</summary>
    private void EnsureRootBody()
    {
        Rigidbody legacy = GetComponent<Rigidbody>();
        if (legacy != null)
        {
            Destroy(legacy);
        }
    }

    private void UpdateRootMass()
    {
        CompactRegistry();
        UpdateRootMass(cells.Count);
    }

    private void UpdateRootMass(int _)
    {
    }

    private void BuildIslandsFromRegistry()
    {
        ReleaseIslandLists();
        visited.Clear();
        queue.Clear();

        foreach (KeyValuePair<Vector2Int, List<PixelCubeCell>> pair in cellsByGrid)
        {
            if (pair.Value == null || pair.Value.Count == 0 || visited.Contains(pair.Key))
            {
                continue;
            }

            List<PixelCubeCell> island = GetIslandList();
            queue.Enqueue(pair.Key);
            visited.Add(pair.Key);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (!cellsByGrid.TryGetValue(current, out List<PixelCubeCell> bucket) || bucket == null || bucket.Count == 0)
                {
                    continue;
                }

                for (int b = 0; b < bucket.Count; b++)
                {
                    PixelCubeCell c = bucket[b];
                    if (c != null)
                    {
                        island.Add(c);
                    }
                }

                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    Vector2Int next = current + NeighborOffsets[i];
                    if (visited.Contains(next) || !cellsByGrid.ContainsKey(next))
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            if (island.Count > 0)
            {
                islands.Add(island);
            }
            else
            {
                islandPool.Push(island);
            }
        }
    }

    private List<PixelCubeCell> GetIslandList()
    {
        if (islandPool.Count > 0)
        {
            List<PixelCubeCell> pooled = islandPool.Pop();
            pooled.Clear();
            return pooled;
        }

        return new List<PixelCubeCell>(16);
    }

    private void ReleaseIslandLists()
    {
        for (int i = 0; i < islands.Count; i++)
        {
            islands[i].Clear();
            islandPool.Push(islands[i]);
        }

        islands.Clear();
    }

    private void RefreshOuterCellColliders()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            PixelCubeCell cell = cells[i];
            if (cell == null || !cell.isActiveAndEnabled || cell.IsBreaking)
            {
                continue;
            }

            bool isOuterCell = IsOuterCell(cell.GridPosition);
            cell.SetTargetColliderEnabled(isOuterCell);
        }
    }

    private bool IsOuterCell(Vector2Int gridPosition)
    {
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector2Int neighbor = gridPosition + NeighborOffsets[i];
            if (!cellsByGrid.TryGetValue(neighbor, out List<PixelCubeCell> neighborBucket)
                || neighborBucket == null
                || neighborBucket.Count == 0)
            {
                return true;
            }
        }

        return false;
    }
}
