using System;
using System.Collections.Generic;
using TBN;
using UnityEngine;

public class PixelCubeManager : MonoBehaviour
{
    private static readonly RaycastHit[] RaycastHitsBuffer = new RaycastHit[256];
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static PixelCubeManager Instance { get; private set; }

    [SerializeField] private bool autoCollectOnStart = true;
    [Header("Level / Pixel cluster")]
    [Tooltip("Khi không có runtime level từ GameManager: chỉ gom PixelCubeCell dưới transform này. Có runtime instance → instance được ưu tiên.")]
    [SerializeField] private Transform _pixelLevelRoot;
    private Transform _runtimeLevelInstanceRoot;
    [Header("VFX")]
    [SerializeField] private GameObject cellDestroyVfxPrefab;
    [SerializeField, Min(0f)] private float cellDestroyVfxRecycleDelay = 2f;
    [Header("Targeting Range")]
    [SerializeField] private Transform shooterTargetMinHeightLine;
    [SerializeField] private bool enforceTargetHeightLine;
    [SerializeField] private bool requireLineOfSightForTargeting = true;
    [Header("Saw spawn (tray / block below)")]
    [SerializeField] private Transform sawSpawnPoint;
    [SerializeField] private Vector3 sawSpawnSize = Vector3.zero;
    [Header("Saw target scan")]
    [SerializeField] private Collider sawTargetScanArea;
    [Header("Saw movement bounds")]
    [SerializeField] private Collider sawMovementBounds;
    private const float targetHeightTolerance = 0.01f;
    private readonly List<PixelCubeCell> allCells = new List<PixelCubeCell>(256);
    private readonly Dictionary<PixelCubeColor, List<PixelCubeCell>> byColor = new Dictionary<PixelCubeColor, List<PixelCubeCell>>();
    private readonly HashSet<PixelCubeCell> reservedTargetCells = new HashSet<PixelCubeCell>();
    private readonly HashSet<PixelCubeCell> extractedForCollectionCells = new HashSet<PixelCubeCell>();
    private readonly HashSet<PixelCubeCell> _cellCountDedupe = new HashSet<PixelCubeCell>();
    private readonly List<PixelCubeCell> _refreshCellsScratch = new List<PixelCubeCell>(256);
    private readonly List<PixelCubeCell> _nearestCapsuleReserveScratch = new List<PixelCubeCell>(128);
    private static readonly Dictionary<GameObject, VfxInstanceCache> cellDestroyVfxCaches = new Dictionary<GameObject, VfxInstanceCache>(64);
    private int initialActiveCellCountSnapshot = -1;

    private readonly Dictionary<PixelCubeCell, PixelBullet> inboundBulletByCell = new Dictionary<PixelCubeCell, PixelBullet>();

    private sealed class VfxInstanceCache
    {
        public ParticleSystem[] Systems;
        public ParticleSystemRenderer[] RenderersBySystemIndex;
        public MaterialPropertyBlock PropertyBlock;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Transform PixelLevelRoot => _pixelLevelRoot;
    public Transform EffectivePixelClusterRoot => ResolveEffectivePixelClusterRoot();

    public bool LogBulletCellDiagnosticsEnabled => false;
    public Collider SawTargetScanArea => sawTargetScanArea;

    public Collider SawMovementBounds => sawMovementBounds;

    public bool TryGetSawMovementBoundsCollider(out Collider collider)
    {
        collider = sawMovementBounds;
        return collider != null;
    }

    public bool HasValidSawSpawnPoint => TryGetSawSpawnBounds(out _);

    private bool TryGetSawSpawnBounds(out Bounds bounds)
    {
        if (sawSpawnPoint != null)
        {
            Vector3 size = new Vector3(
                Mathf.Max(0f, sawSpawnSize.x),
                Mathf.Max(0f, sawSpawnSize.y),
                Mathf.Max(0f, sawSpawnSize.z));
            bounds = new Bounds(sawSpawnPoint.position, size);
            return true;
        }

        bounds = default;
        return false;
    }

    public bool TryGetSawSpawnWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetSawSpawnBounds(out Bounds bounds))
        {
            return false;
        }

        worldPosition = bounds.center;
        return true;
    }

    public bool TryGetSawSpawnTopWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetSawSpawnBounds(out Bounds bounds))
        {
            return false;
        }

        worldPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (autoCollectOnStart)
        {
            RefreshAllCells();
        }
    }

    public void RegisterCell(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return;
        }

        if (cell.IsTargetLocked || cell.IsBreaking)
        {
            return;
        }

        if (reservedTargetCells.Contains(cell))
        {
            return;
        }

        if (!allCells.Contains(cell))
        {
            allCells.Add(cell);
        }

        PixelCubeColor color = cell.CubeColor;
        if (!byColor.TryGetValue(color, out List<PixelCubeCell> list))
        {
            list = new List<PixelCubeCell>();
            byColor[color] = list;
        }

        if (!list.Contains(cell))
        {
            list.Add(cell);
        }
    }

    public void UnregisterCell(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return;
        }

        allCells.RemoveAll(c => c == cell);

        PixelCubeColor color = cell.CubeColor;
        if (byColor.TryGetValue(color, out List<PixelCubeCell> list))
        {
            list.RemoveAll(c => c == cell);
        }

        reservedTargetCells.Remove(cell);
        extractedForCollectionCells.Remove(cell);
    }

    /// <summary>
    /// Gắn root prefab level đang chơi; <see cref="RefreshAllCells"/> chỉ đăng ký cell trong hierarchy này (ưu tiên hơn PixelLevelRoot trên Inspector).
    /// Truyền null khi hủy level (trước khi Destroy instance).
    /// </summary>
    public void SetLevelInstanceRoot(Transform levelInstanceRoot)
    {
        _runtimeLevelInstanceRoot = levelInstanceRoot;
    }

    /// <summary>Xóa registry không quét lại — dùng khi unload level để tránh giữ reference tới instance sắp Destroy.</summary>
    public void ClearCellRegistry()
    {
        allCells.Clear();
        byColor.Clear();
        reservedTargetCells.Clear();
        extractedForCollectionCells.Clear();
        inboundBulletByCell.Clear();
    }

    public void ClearCollectionRuntimeState()
    {
        _refreshCellsScratch.Clear();
        foreach (PixelCubeCell cell in extractedForCollectionCells)
        {
            if (cell != null)
            {
                _refreshCellsScratch.Add(cell);
            }
        }

        for (int i = 0; i < _refreshCellsScratch.Count; i++)
        {
            PixelCubeCell cell = _refreshCellsScratch[i];
            if (cell != null && cell.IsExtractedForCollection)
            {
                cell.CompleteCollectedDelivery();
            }
        }

        _refreshCellsScratch.Clear();
        foreach (PixelCubeCell cell in reservedTargetCells)
        {
            if (cell != null)
            {
                _refreshCellsScratch.Add(cell);
            }
        }

        for (int i = 0; i < _refreshCellsScratch.Count; i++)
        {
            ResolveReservedCell(_refreshCellsScratch[i]);
        }

        _refreshCellsScratch.Clear();
        extractedForCollectionCells.Clear();
        reservedTargetCells.Clear();
        inboundBulletByCell.Clear();
    }

    private Transform ResolveEffectivePixelClusterRoot()
    {
        if (_runtimeLevelInstanceRoot != null)
        {
            return _runtimeLevelInstanceRoot;
        }

        return _pixelLevelRoot;
    }

    public void AppendCellsForPathfindingUnderAncestor(Transform ancestor, List<PixelCubeCell> buffer)
    {
        if (ancestor == null || buffer == null)
        {
            return;
        }

        if (AStarGridProvider.TryResolve(out AStarGrid activeGrid) &&
            activeGrid != null &&
            activeGrid.TryAppendMappedPixelCubeCellsUnderAncestor(ancestor, buffer))
        {
            AppendReservedCellsForPathfindingUnderAncestor(ancestor, buffer);
            return;
        }

        for (int i = 0; i < allCells.Count; i++)
        {
            PixelCubeCell cell = allCells[i];
            if (cell == null ||
                !cell.gameObject.activeInHierarchy ||
                cell.IsBreaking)
            {
                continue;
            }

            if (!cell.transform.IsChildOf(ancestor))
            {
                continue;
            }

            buffer.Add(cell);
        }

        AppendReservedCellsForPathfindingUnderAncestor(ancestor, buffer);
    }

    public void CollectPathBlockingCellsUnderAncestor(Transform ancestor, List<PixelCubeCell> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Clear();
        AppendCellsForPathfindingUnderAncestor(ancestor, buffer);
    }

    private void AppendReservedCellsForPathfindingUnderAncestor(Transform ancestor, List<PixelCubeCell> buffer)
    {
        foreach (PixelCubeCell cell in reservedTargetCells)
        {
            if (cell == null ||
                !cell.gameObject.activeInHierarchy ||
                !cell.isActiveAndEnabled ||
                cell.IsBreaking)
            {
                continue;
            }

            if (!cell.transform.IsChildOf(ancestor))
            {
                continue;
            }

            if (buffer.Contains(cell))
            {
                continue;
            }

            buffer.Add(cell);
        }
    }

    public void RefreshAllCells()
    {
        ClearCellRegistry();

        Transform clusterRoot = ResolveEffectivePixelClusterRoot();
        if (clusterRoot != null)
        {
            _refreshCellsScratch.Clear();
            clusterRoot.GetComponentsInChildren(true, _refreshCellsScratch);
            for (int i = 0; i < _refreshCellsScratch.Count; i++)
            {
                PixelCubeCell cell = _refreshCellsScratch[i];
                if (cell != null && cell.isActiveAndEnabled)
                {
                    RegisterCell(cell);
                }
            }

            return;
        }

        PixelCubeCell[] cells = FindObjectsByType<PixelCubeCell>(FindObjectsSortMode.None);
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null && cells[i].isActiveAndEnabled)
            {
                RegisterCell(cells[i]);
            }
        }
    }

    public void CaptureInitialActiveCellCountSnapshot()
    {
        initialActiveCellCountSnapshot = Mathf.Max(0, GetActiveCellCount());
    }

    public bool TryReserveCellAsBulletTarget(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (!cell.gameObject.activeInHierarchy || !cell.isActiveAndEnabled || cell.IsBreaking)
        {
            return false;
        }

        Collider targetCollider = cell.TargetCollider;
        if (targetCollider != null && !targetCollider.enabled)
        {
            return false;
        }

        if (reservedTargetCells.Contains(cell))
        {
            return false;
        }

        if (inboundBulletByCell.TryGetValue(cell, out PixelBullet inbound) && inbound != null)
        {
            return false;
        }

        if (!cell.TryLockAsBulletTarget())
        {
            return false;
        }

        reservedTargetCells.Add(cell);
        UnregisterCellInternal(cell, keepReserved: true);
        return true;
    }

    public void ResolveReservedCell(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return;
        }

        if (inboundBulletByCell.TryGetValue(cell, out PixelBullet inbound) && inbound != null)
        {
            return;
        }

        reservedTargetCells.Remove(cell);
        if (!cell.IsBreaking)
        {
            cell.ReleaseBulletTargetLock();
        }
        if (cell.isActiveAndEnabled
            && !cell.IsBreaking
            && !cell.IsTargetLocked
            && (cell.TargetCollider == null || cell.TargetCollider.enabled))
        {
            RegisterCell(cell);
        }
    }

    public void SpawnCellDestroyVfx(Vector3 position, Quaternion rotation, Color particleColor)
    {
        if (cellDestroyVfxPrefab == null)
        {
            return;
        }

        GameObject spawned = cellDestroyVfxPrefab.Spawn(position, rotation);
        if (spawned != null)
        {
            spawned.SetActive(true);
            spawned.transform.SetPositionAndRotation(position, rotation);
            ApplyColorAndReplayCellDestroyVfx(spawned, particleColor);
        }

        if (spawned != null && cellDestroyVfxRecycleDelay > 0f)
        {
            spawned.Recycle(cellDestroyVfxRecycleDelay);
        }
    }

    private static void ApplyColorAndReplayCellDestroyVfx(GameObject spawned, Color particleColor)
    {
        if (spawned == null)
        {
            return;
        }

        VfxInstanceCache cache = GetOrBuildCellDestroyVfxCache(spawned);
        ParticleSystem[] systems = cache.Systems;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(particleColor);
            ParticleSystemRenderer renderer = cache.RenderersBySystemIndex[i];
            if (renderer != null)
            {
                cache.PropertyBlock.Clear();
                renderer.GetPropertyBlock(cache.PropertyBlock);
                cache.PropertyBlock.SetColor(BaseColorId, particleColor);
                cache.PropertyBlock.SetColor(ColorId, particleColor);
                cache.PropertyBlock.SetColor(TintColorId, particleColor);
                cache.PropertyBlock.SetColor(EmissionColorId, particleColor);
                renderer.SetPropertyBlock(cache.PropertyBlock);
            }

            system.Play(true);
        }
    }

    private static VfxInstanceCache GetOrBuildCellDestroyVfxCache(GameObject spawned)
    {
        if (cellDestroyVfxCaches.TryGetValue(spawned, out VfxInstanceCache cache) && cache != null)
        {
            return cache;
        }

        ParticleSystem[] systems = spawned.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
        {
            systems = Array.Empty<ParticleSystem>();
        }

        ParticleSystemRenderer[] renderers = new ParticleSystemRenderer[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system != null)
            {
                renderers[i] = system.GetComponent<ParticleSystemRenderer>();
            }
        }

        cache = new VfxInstanceCache
        {
            Systems = systems,
            RenderersBySystemIndex = renderers,
            PropertyBlock = new MaterialPropertyBlock()
        };

        cellDestroyVfxCaches[spawned] = cache;
        return cache;
    }

    public int GetActiveCellCount()
    {
        int count = 0;
        _cellCountDedupe.Clear();
        for (int i = 0; i < allCells.Count; i++)
        {
            PixelCubeCell cell = allCells[i];
            if (TryCountPendingPixelTargetWork(cell))
            {
                count++;
            }
        }

        foreach (PixelCubeCell cell in reservedTargetCells)
        {
            if (TryCountPendingPixelTargetWork(cell))
            {
                count++;
            }
        }

        foreach (PixelCubeCell cell in extractedForCollectionCells)
        {
            if (TryCountPendingPixelTargetWork(cell))
            {
                count++;
            }
        }

        _cellCountDedupe.Clear();
        return count;
    }

    public void RegisterExtractedCellForCollection(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return;
        }

        extractedForCollectionCells.Add(cell);
    }

    public void ResolveExtractedCellForCollection(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return;
        }

        extractedForCollectionCells.Remove(cell);
    }

    private static bool IsCellPendingPixelTargetWork(PixelCubeCell cell)
    {
        return cell != null &&
               cell.isActiveAndEnabled &&
               cell.gameObject.activeInHierarchy &&
               !cell.IsBreaking;
    }

    private bool TryCountPendingPixelTargetWork(PixelCubeCell cell)
    {
        return IsCellPendingPixelTargetWork(cell) && _cellCountDedupe.Add(cell);
    }

    public float GetRemainingActiveCellRatioFromSnapshot()
    {
        if (initialActiveCellCountSnapshot <= 0)
        {
            return -1f;
        }

        int remainingActiveCount = Mathf.Max(0, GetActiveCellCount());
        return remainingActiveCount / (float)initialActiveCellCountSnapshot;
    }

    public int GetActiveColorCount()
    {
        int colorCount = 0;
        foreach (KeyValuePair<PixelCubeColor, List<PixelCubeCell>> pair in byColor)
        {
            if (pair.Key == PixelCubeColor.None)
            {
                continue;
            }

            List<PixelCubeCell> list = pair.Value;
            if (list == null || list.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < list.Count; i++)
            {
                PixelCubeCell cell = list[i];
                if (cell != null && cell.gameObject.activeSelf && !cell.IsBreaking)
                {
                    colorCount++;
                    break;
                }
            }
        }

        return colorCount;
    }

    public bool RegisterInboundBullet(PixelCubeCell cell, PixelBullet bullet)
    {
        if (cell == null || bullet == null)
        {
            return false;
        }

        if (!cell.gameObject.activeInHierarchy || !cell.isActiveAndEnabled || cell.IsBreaking)
        {
            return false;
        }

        if (inboundBulletByCell.TryGetValue(cell, out PixelBullet registered) && registered != null && registered != bullet)
        {
            // Hard guard: never allow two live bullets toward the same cell.
            // Keep the first registered bullet and abort the later one.
            bullet.AbortFlightForLostTarget();
            return false;
        }

        if (cell.IsBreaking || !cell.IsTargetLocked)
        {
            return false;
        }

        inboundBulletByCell[cell] = bullet;
        return true;
    }

    public bool TryReserveCellForCapsuleCollection(PixelCubeCell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (!cell.gameObject.activeInHierarchy || !cell.isActiveAndEnabled || cell.IsBreaking)
        {
            return false;
        }

        if (reservedTargetCells.Contains(cell))
        {
            return false;
        }

        CancelInboundBulletsExcept(cell, null);

        Collider targetCollider = cell.TargetCollider;
        bool toggledColliderOn = false;
        if (targetCollider != null && !targetCollider.enabled)
        {
            targetCollider.enabled = true;
            toggledColliderOn = true;
        }

        if (!cell.TryLockAsBulletTarget())
        {
            if (toggledColliderOn && targetCollider != null)
            {
                targetCollider.enabled = false;
            }

            return false;
        }

        reservedTargetCells.Add(cell);
        UnregisterCellInternal(cell, keepReserved: true);
        return true;
    }

    /// <summary>
    /// Reserve một <see cref="PixelCubeCell"/> cùng màu gần <paramref name="originWorld"/> nhất (mặt phẳng XY) mà reserve được.
    /// </summary>
    public bool TryReserveNearestCellForCapsuleCollection(PixelCubeColor color, Vector3 originWorld, out PixelCubeCell reservedCell)
    {
        reservedCell = null;
        if (color == PixelCubeColor.None)
        {
            return false;
        }

        _nearestCapsuleReserveScratch.Clear();
        CollectActiveCellsByColor(color, _nearestCapsuleReserveScratch);
        if (_nearestCapsuleReserveScratch.Count == 0)
        {
            return false;
        }

        static float PlanarSqrToOrigin(PixelCubeCell cell, Vector3 o)
        {
            Vector3 p = cell.GetLatticeSnapReferenceWorldCenter();
            float dx = p.x - o.x;
            float dy = p.y - o.y;
            return dx * dx + dy * dy;
        }

        _nearestCapsuleReserveScratch.Sort((a, b) =>
            PlanarSqrToOrigin(a, originWorld).CompareTo(PlanarSqrToOrigin(b, originWorld)));

        for (int i = 0; i < _nearestCapsuleReserveScratch.Count; i++)
        {
            PixelCubeCell cell = _nearestCapsuleReserveScratch[i];
            if (TryReserveCellForCapsuleCollection(cell))
            {
                reservedCell = cell;
                return true;
            }
        }

        return false;
    }

    public void UnregisterInboundBullet(PixelCubeCell cell, PixelBullet bullet)
    {
        if (cell == null || bullet == null)
        {
            return;
        }

        if (inboundBulletByCell.TryGetValue(cell, out PixelBullet registered) && registered == bullet)
        {
            inboundBulletByCell.Remove(cell);
        }
    }

    public void CancelInboundBulletsExcept(PixelCubeCell cell, PixelBullet except)
    {
        if (cell == null || !inboundBulletByCell.TryGetValue(cell, out PixelBullet b) || b == null)
        {
            return;
        }

        if (b == except)
        {
            return;
        }

        b.AbortFlightForLostTarget();
        inboundBulletByCell.Remove(cell);
    }

    public bool HasAnyActiveCell()
    {
        return GetActiveCellCount() > 0;
    }

    public bool HasAnyInboundBullet()
    {
        if (inboundBulletByCell.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<PixelCubeCell, PixelBullet> pair in inboundBulletByCell)
        {
            if (pair.Value != null)
            {
                return true;
            }
        }

        return false;
    }

    public int BreakAllActiveCells()
    {
        int broken = 0;
        PixelCubeCell[] liveCells = FindObjectsByType<PixelCubeCell>(FindObjectsSortMode.None);
        for (int i = 0; i < liveCells.Length; i++)
        {
            PixelCubeCell cell = liveCells[i];
            if (cell == null || !cell.gameObject.activeSelf || cell.IsBreaking)
            {
                continue;
            }

            if (cell.BreakCell())
            {
                broken++;
            }
        }

        return broken;
    }

    public int BreakAllActiveCellsByColor(PixelCubeColor color)
    {
        if (color == PixelCubeColor.None)
        {
            return 0;
        }

        if (!byColor.TryGetValue(color, out List<PixelCubeCell> colorCells) || colorCells == null || colorCells.Count == 0)
        {
            return 0;
        }

        List<PixelCubeCell> snapshot = new List<PixelCubeCell>(colorCells);
        int brokenCount = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            PixelCubeCell cell = snapshot[i];
            if (cell == null || !cell.gameObject.activeSelf || cell.IsBreaking)
            {
                continue;
            }

            cell.BreakCell();
            brokenCount++;
        }

        return brokenCount;
    }

    public void CollectActiveCellsByColor(PixelCubeColor color, List<PixelCubeCell> buffer)
    {
        if (buffer == null || color == PixelCubeColor.None)
        {
            return;
        }

        if (!byColor.TryGetValue(color, out List<PixelCubeCell> colorCells) || colorCells == null || colorCells.Count == 0)
        {
            return;
        }

        for (int i = 0; i < colorCells.Count; i++)
        {
            PixelCubeCell cell = colorCells[i];
            if (cell != null && cell.gameObject.activeSelf && !cell.IsBreaking)
            {
                buffer.Add(cell);
            }
        }
    }

    public void CollectActiveCells(List<PixelCubeCell> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        for (int i = 0; i < allCells.Count; i++)
        {
            PixelCubeCell cell = allCells[i];
            if (cell != null && cell.gameObject.activeSelf && !cell.IsBreaking)
            {
                buffer.Add(cell);
            }
        }
    }

    public bool TryAcquireNearestVisibleCellAsBulletTarget(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        out PixelCubeCell acquired)
    {
        acquired = null;
        const int maxReserveRetries = 8;
        for (int attempt = 0; attempt < maxReserveRetries; attempt++)
        {
            PixelCubeCell candidate;
            bool found = FindNearestVisibleCell(color, origin, maxDistance, raycastMask, out candidate);

            if (!found)
            {
                return false;
            }

            // Reserve immediately after selecting candidate so it is removed from filtering lists.
            if (TryReserveCellAsBulletTarget(candidate))
            {
                acquired = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetNearestVisibleCell(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        out PixelCubeCell nearest)
    {
        return FindNearestVisibleCell(color, origin, maxDistance, raycastMask, out nearest);
    }

    public bool TryGetNearestVisibleCellInDirectionCone(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        Vector3 forwardDirection,
        float coneHalfAngleDegrees,
        out PixelCubeCell nearest)
    {
        return TryGetNearestVisibleCellInDirectionCone(
            color,
            origin,
            maxDistance,
            raycastMask,
            forwardDirection,
            coneHalfAngleDegrees,
            null,
            out nearest);
    }

    public bool TryGetNearestVisibleCellInDirectionCone(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        Vector3 forwardDirection,
        float coneHalfAngleDegrees,
        Collider targetAreaCollider,
        out PixelCubeCell nearest)
    {
        Vector3 forward = new Vector3(forwardDirection.x, forwardDirection.y, 0f);
        if (forward.sqrMagnitude < 0.0001f)
        {
            return FindNearestVisibleCell(color, origin, maxDistance, raycastMask, null, -2f, targetAreaCollider, out nearest);
        }

        forward.Normalize();
        float clampedHalfAngle = Mathf.Clamp(coneHalfAngleDegrees, 0f, 180f);
        float minDot = Mathf.Cos(clampedHalfAngle * Mathf.Deg2Rad);
        return FindNearestVisibleCell(color, origin, maxDistance, raycastMask, forward, minDot, targetAreaCollider, out nearest);
    }

    private bool FindNearestVisibleCell(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        out PixelCubeCell nearest)
    {
        return FindNearestVisibleCell(color, origin, maxDistance, raycastMask, null, -2f, null, out nearest);
    }

    private bool FindNearestVisibleCell(
        PixelCubeColor color,
        Vector3 origin,
        float maxDistance,
        LayerMask raycastMask,
        Vector3? forwardDirection,
        float minForwardDot,
        Collider targetAreaCollider,
        out PixelCubeCell nearest)
    {
        nearest = null;
        if (color == PixelCubeColor.None)
        {
            return false;
        }

        if (!byColor.TryGetValue(color, out List<PixelCubeCell> colorCells) || colorCells == null || colorCells.Count == 0)
        {
            return false;
        }

        float maxDistanceSqr = maxDistance * maxDistance;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < colorCells.Count; i++)
        {
            PixelCubeCell cell = colorCells[i];
            if (cell == null || !cell.isActiveAndEnabled)
            {
                continue;
            }

            if (!IsCellEligibleForNewBullet(cell))
            {
                continue;
            }

            if (targetAreaCollider != null && !IsPointInsideCollider(targetAreaCollider, cell.transform.position))
            {
                continue;
            }

            Vector3 delta = cell.transform.position - origin;
            float distSqr = delta.sqrMagnitude;
            if (distSqr > maxDistanceSqr)
            {
                continue;
            }

            // We only need the nearest valid cell. Skip expensive checks for farther candidates.
            if (distSqr >= bestDistSqr)
            {
                continue;
            }

            if (forwardDirection.HasValue)
            {
                float deltaMagnitude = Mathf.Sqrt(distSqr);
                if (deltaMagnitude <= 0.0001f)
                {
                    continue;
                }

                float dot = Vector3.Dot(delta / deltaMagnitude, forwardDirection.Value);
                if (dot < minForwardDot)
                {
                    continue;
                }
            }

            if (enforceTargetHeightLine && !IsCellWithinShooterRangeByHeight(cell))
            {
                continue;
            }

            if (requireLineOfSightForTargeting && HasColorBlockingCell(origin, cell, raycastMask))
            {
                continue;
            }

            bestDistSqr = distSqr;
            nearest = cell;
        }

        return nearest != null;
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 worldPoint)
    {
        if (collider == null)
        {
            return true;
        }

        Vector3 closest = collider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude <= 0.0001f;
    }

    private bool IsCellWithinShooterRangeByHeight(PixelCubeCell cell)
    {
        if (cell == null || shooterTargetMinHeightLine == null)
        {
            return true;
        }

        float maxHeight = shooterTargetMinHeightLine.position.y + targetHeightTolerance;
        Collider targetCollider = cell.TargetCollider;
        float cellTopY = targetCollider != null
            ? targetCollider.bounds.max.y
            : cell.transform.position.y;
        return cellTopY <= maxHeight;
    }

    private bool HasColorBlockingCell(Vector3 origin, PixelCubeCell targetCell, LayerMask raycastMask)
    {
        if (targetCell == null)
        {
            return true;
        }

        Collider targetCollider = targetCell.TargetCollider;
        Vector3 targetPoint = targetCollider != null ? targetCollider.bounds.center : targetCell.transform.position;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        Vector3 direction = toTarget / distance;
        float rayLength = distance + 0.05f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            RaycastHitsBuffer,
            rayLength,
            raycastMask,
            QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
        {
            return true;
        }
        if (hitCount >= RaycastHitsBuffer.Length)
        {
            return true;
        }

        float nearestRelevantDistance = float.MaxValue;
        bool nearestRelevantIsTarget = false;
        bool foundRelevantHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = RaycastHitsBuffer[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.distance >= nearestRelevantDistance)
            {
                continue;
            }

            PixelCubeCell hitCell = hit.collider != null
                ? hit.collider.GetComponentInParent<PixelCubeCell>()
                : null;

            if (targetCollider != null && hit.collider == targetCollider)
            {
                nearestRelevantDistance = hit.distance;
                nearestRelevantIsTarget = true;
                foundRelevantHit = true;
                continue;
            }

            if (hitCell == null)
            {
                continue;
            }

            nearestRelevantDistance = hit.distance;
            nearestRelevantIsTarget = hitCell == targetCell;
            foundRelevantHit = true;
        }

        if (!foundRelevantHit)
        {
            return true;
        }

        return !nearestRelevantIsTarget;
    }

    private bool IsCellEligibleForNewBullet(PixelCubeCell cell)
    {
        if (cell == null || !cell.gameObject.activeInHierarchy || !cell.isActiveAndEnabled || cell.IsBreaking)
        {
            return false;
        }

        if (cell.IsTargetLocked)
        {
            return false;
        }

        if (reservedTargetCells.Contains(cell))
        {
            return false;
        }

        if (inboundBulletByCell.ContainsKey(cell))
        {
            return false;
        }

        Collider c = cell.TargetCollider;
        if (c != null && !c.enabled)
        {
            return false;
        }

        return true;
    }

    private void UnregisterCellInternal(PixelCubeCell cell, bool keepReserved)
    {
        allCells.RemoveAll(c => c == cell);

        PixelCubeColor color = cell.CubeColor;
        if (byColor.TryGetValue(color, out List<PixelCubeCell> list))
        {
            list.RemoveAll(c => c == cell);
        }

        if (!keepReserved)
        {
            reservedTargetCells.Remove(cell);
        }

        extractedForCollectionCells.Remove(cell);
    }
}
