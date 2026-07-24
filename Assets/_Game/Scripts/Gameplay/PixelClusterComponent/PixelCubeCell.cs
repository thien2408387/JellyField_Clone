using System.Collections;
using TBN;
using UnityEngine;

public class PixelCubeCell : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private PixelCubeColor cubeColor = PixelCubeColor.None;
    [SerializeField] private Collider targetCollider;
    [Header("Visual")]
    [SerializeField, Range(0f, 0.25f)] private float cubeColorBrightnessJitter = 0f;
    [SerializeField, Range(1, 8)] private int batchedBrightnessSteps = 4;
    private const float DestroyPopScale = 1.2f;
    private const float DestroyPopUpDuration = 0.06f;
    private const float DestroyPopDownDuration = 0.08f;
    private const float SawTouchPopScale = 1.1f;
    private const float SawTouchPopUpDuration = 0.1f;
    private const float SawTouchPopDownDuration = 0.05f;
    private const float SawTouchPopCooldown = 0.08f;
    [Header("Performance")]
    [SerializeField] private bool enableSawTouchPop = true;
    [SerializeField] private bool useTriggerSawTouchDetection = true;
    private static bool isApplicationQuitting;
    private PixelClusterRigidbody cachedCluster;
    private PixelCubeManager cachedCubeManager;
    private Coroutine destroyRoutine;
    private Coroutine sawTouchPopRoutine;
    private Coroutine breakTimeoutRoutine;
    private float lastSawTouchPopTime = -999f;
    private float breakStartTime = -1f;
    private bool breakFinalized;
    private bool isBreaking;
    private bool isTargetLocked;
    private bool isExtractedForCollection;
    private Vector3 defaultLocalScale = Vector3.one;
    private MeshRenderer cachedMeshRenderer;
    private MaterialPropertyBlock colorPropertyBlock;
    private Color currentVisualColor = Color.white;

    public Vector2Int GridPosition => gridPosition;
    public PixelCubeColor CubeColor => cubeColor;
    public Collider TargetCollider => targetCollider;
    public bool IsBreaking => isBreaking;
    public bool IsTargetLocked => isTargetLocked;
    public bool IsExtractedForCollection => isExtractedForCollection;

    public void SetTargetColliderEnabled(bool enabled)
    {
        EnsureTargetCollider();
        if (targetCollider == null)
        {
            return;
        }

        if (isBreaking)
        {
            targetCollider.enabled = false;
            return;
        }

        targetCollider.enabled = enabled;
    }

    /// <summary>
    /// World center used for A* lattice snap and slab queries when the transform pivot differs from the visible obstruction/volume (e.g. TargetCollider bounds).
    /// </summary>
    public Vector3 GetLatticeSnapReferenceWorldCenter()
    {
        EnsureTargetCollider();
        if (targetCollider != null && targetCollider.enabled)
        {
            return targetCollider.bounds.center;
        }

        if (cachedMeshRenderer == null)
        {
            cachedMeshRenderer = GetComponent<MeshRenderer>();
        }

        if (cachedMeshRenderer == null)
        {
            cachedMeshRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        if (cachedMeshRenderer != null && cachedMeshRenderer.enabled)
        {
            return cachedMeshRenderer.bounds.center;
        }

        return transform.position;
    }

    public bool TryLockAsBulletTarget()
    {
        if (isTargetLocked || isBreaking || !gameObject.activeInHierarchy || !isActiveAndEnabled)
        {
            if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
            {
                Debug.LogWarning(
                    $"[Bullet↔Cell][CellTryLockReject] frame={Time.frameCount} t={Time.time:F3} " +
                    $"cell={name} id={GetInstanceID()} locked={isTargetLocked} breaking={isBreaking} " +
                    $"activeSelf={gameObject.activeSelf} activeEnabled={isActiveAndEnabled}");
            }
            return false;
        }

        if (targetCollider != null && !targetCollider.enabled)
        {
            if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
            {
                Debug.LogWarning(
                    $"[Bullet↔Cell][CellTryLockRejectColliderDisabled] frame={Time.frameCount} t={Time.time:F3} " +
                    $"cell={name} id={GetInstanceID()}");
            }
            return false;
        }

        isTargetLocked = true;
        if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
        {
            Debug.Log(
                $"[Bullet↔Cell][CellLocked] frame={Time.frameCount} t={Time.time:F3} " +
                $"cell={name} id={GetInstanceID()}");
        }
        return true;
    }

    public void ReleaseBulletTargetLock()
    {
        if (isBreaking)
        {
            if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
            {
                Debug.Log(
                    $"[Bullet↔Cell][CellUnlockSkipBreaking] frame={Time.frameCount} t={Time.time:F3} " +
                    $"cell={name} id={GetInstanceID()}");
            }
            return;
        }

        isTargetLocked = false;
        if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
        {
            Debug.Log(
                $"[Bullet↔Cell][CellUnlocked] frame={Time.frameCount} t={Time.time:F3} " +
                $"cell={name} id={GetInstanceID()}");
        }
    }

    public void SetGridPosition(Vector2Int value)
    {
        gridPosition = value;
    }

    public void SetCubeColor(PixelCubeColor color)
    {
        PixelCubeManager manager = cachedCubeManager != null
            ? cachedCubeManager
            : PixelCubeManager.Instance;
        bool shouldRefreshRegistration = manager != null &&
                                         isActiveAndEnabled &&
                                         gameObject.activeInHierarchy &&
                                         !isBreaking &&
                                         !isTargetLocked &&
                                         !isExtractedForCollection;
        if (shouldRefreshRegistration)
        {
            manager.UnregisterCell(this);
        }

        cubeColor = color;
        ApplyCubeMaterial();

        if (shouldRefreshRegistration)
        {
            manager.RegisterCell(this);
            cachedCubeManager = manager;
        }
    }

    public void InitializePrefabCell(Vector2Int position, PixelCubeColor color, Transform parent = null)
    {
        if (parent != null)
        {
            transform.SetParent(parent, true);
        }

        defaultLocalScale = transform.localScale;
        ResetRuntimeStateForPrefabReuse();
        gridPosition = position;
        cubeColor = color;
        ApplyCubeMaterial();
        RefreshClusterRegistration();
        RefreshManagerRegistration();
        AStarGridProvider.MarkActiveGridDirty();
    }

    public void Initialize(Vector2Int position, PixelCubeColor color, Transform parent = null)
    {
        InitializePrefabCell(position, color, parent);
    }

    private void ResetRuntimeStateForPrefabReuse()
    {
        StopDestroyRoutine();
        StopBreakTimeoutRoutine();
        StopSawTouchPopRoutine();
        breakStartTime = -1f;
        breakFinalized = false;
        isBreaking = false;
        isTargetLocked = false;
        isExtractedForCollection = false;
        transform.localRotation = Quaternion.identity;
        transform.localScale = defaultLocalScale;
        EnsureTargetCollider();
        SetTargetColliderEnabled(true);
    }

    /// <param name="finishingBullet">The bullet whose tween just completed; other in-flight bullets to this cell are aborted and refunded.</param>
    /// <returns>True only when this call actually started a new break sequence.</returns>
    public bool BreakCell(PixelBullet finishingBullet = null)
    {
        if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
        {
            Debug.Log(
                $"[Bullet↔Cell][BreakCellBegin] frame={Time.frameCount} t={Time.time:F3} " +
                $"cell={name} id={GetInstanceID()} finishingBulletId={(finishingBullet != null ? finishingBullet.GetInstanceID() : 0)} " +
                $"locked={isTargetLocked} breaking={isBreaking} activeSelf={gameObject.activeSelf}");
        }

        if (!gameObject.activeSelf || isBreaking)
        {
            if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
            {
                Debug.LogWarning(
                    $"[Bullet↔Cell] BreakCell DUPLICATE (already off — second bullet?): {name} id={GetInstanceID()} " +
                    $"grid={gridPosition} color={cubeColor}");
            }

            return false;
        }

        PixelCubeManager manager = cachedCubeManager != null
            ? cachedCubeManager
            : (PixelCubeManager.Instance != null ? PixelCubeManager.Instance : FindFirstObjectByType<PixelCubeManager>());
        manager?.CancelInboundBulletsExcept(this, finishingBullet);
        manager?.UnregisterCell(this);
        isBreaking = true;
        breakFinalized = false;
        breakStartTime = Time.time;
        isTargetLocked = true;

        // Sample bounds/rotation while collider is still enabled — disabled colliders can report invalid bounds (often world origin).
        Vector3 vfxSpawnPosition = transform.position;
        Quaternion vfxSpawnRotation = transform.rotation;
        if (targetCollider != null)
        {
            vfxSpawnPosition = targetCollider.bounds.center;
            vfxSpawnRotation = targetCollider.transform.rotation;
            targetCollider.enabled = false;
        }

        if (manager != null && manager.LogBulletCellDiagnosticsEnabled)
        {
            Debug.Log(
                $"[Bullet↔Cell] BreakCell applied: {name} id={GetInstanceID()} grid={gridPosition} color={cubeColor}");
        }

        manager?.SpawnCellDestroyVfx(vfxSpawnPosition, vfxSpawnRotation, currentVisualColor);

        StopDestroyRoutine();
        StartBreakTimeoutRoutine();
        destroyRoutine = StartCoroutine(DestroyPopRoutine());
        return true;
    }

    public bool TryExtractForCollection(Transform carryParent)
    {
        if (!gameObject.activeSelf || isBreaking || isExtractedForCollection || carryParent == null)
        {
            return false;
        }

        PixelCubeManager manager = cachedCubeManager != null
            ? cachedCubeManager
            : PixelCubeManager.Instance;
        PixelClusterRigidbody cluster = cachedCluster;

        manager?.CancelInboundBulletsExcept(this, null);
        manager?.UnregisterCell(this);

        isTargetLocked = true;
        isExtractedForCollection = true;
        manager?.RegisterExtractedCellForCollection(this);
        StopDestroyRoutine();
        StopBreakTimeoutRoutine();
        StopSawTouchPopRoutine();

        if (targetCollider != null)
        {
            targetCollider.enabled = false;
        }

        transform.SetParent(carryParent, true);

        if (cluster != null)
        {
            cluster.UnregisterCell(this);
            cachedCluster = null;
            cluster.RebuildDisconnectedIslands();
        }

        cachedCubeManager = null;
        return true;
    }

    public bool CompleteCollectedDelivery()
    {
        if (!isExtractedForCollection)
        {
            return false;
        }

        isExtractedForCollection = false;
        isTargetLocked = false;
        PixelCubeManager.Instance?.ResolveExtractedCellForCollection(this);
        gameObject.Recycle();
        GameEventHub.RaisePixelCubeDestroyed(this);
        return true;
    }

    private void FinalizeBreak(string reason)
    {
        if (breakFinalized)
        {
            return;
        }

        breakFinalized = true;
        if (PixelCubeManager.Instance != null && PixelCubeManager.Instance.LogBulletCellDiagnosticsEnabled)
        {
            Debug.Log(
                $"[Bullet↔Cell][BreakFinalize] frame={Time.frameCount} t={Time.time:F3} " +
                $"cell={name} id={GetInstanceID()} reason={reason} activeSelf={gameObject.activeSelf}");
        }

        gameObject.SetActive(false);
        GameEventHub.RaisePixelCubeDestroyed(this);
    }

    private void OnEnable()
    {
        ForceIdentityRotation();
        StopDestroyRoutine();
        breakStartTime = -1f;
        breakFinalized = false;
        isBreaking = false;
        isTargetLocked = false;
        isExtractedForCollection = false;
        transform.localScale = defaultLocalScale;
        EnsureTargetCollider();
        SetTargetColliderEnabled(true);
        RefreshClusterRegistration();
        RefreshManagerRegistration();
        ApplyCubeMaterial();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetQuitFlag()
    {
        isApplicationQuitting = false;
    }

    private void OnTransformParentChanged()
    {
        RefreshClusterRegistration();
        RefreshManagerRegistration();
    }

    private void OnDisable()
    {
        StopDestroyRoutine();
        StopBreakTimeoutRoutine();
        StopSawTouchPopRoutine();
        breakStartTime = -1f;
        breakFinalized = false;
        isTargetLocked = false;
        isExtractedForCollection = false;

        if (cachedCluster != null)
        {
            cachedCluster.OnCellDisabled(this);
        }

        if (cachedCubeManager != null)
        {
            cachedCubeManager.UnregisterCell(this);
        }

        isTargetLocked = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleSawTriggerTouch(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHandleSawTriggerTouch(other);
    }

    private void OnDestroy()
    {
        StopBreakTimeoutRoutine();
        if (cachedCluster != null)
        {
            cachedCluster.UnregisterCell(this);
        }

        if (cachedCubeManager != null)
        {
            cachedCubeManager.UnregisterCell(this);
        }

    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void TryHandleSawTriggerTouch(Collider other)
    {
        if (!useTriggerSawTouchDetection || other == null || isBreaking || !isActiveAndEnabled)
        {
            return;
        }

        Saw saw = other.GetComponentInParent<Saw>();
        if (saw == null)
        {
            return;
        }

        TryPlaySawTouchPop();
    }

    private IEnumerator DestroyPopRoutine()
    {
        StopSawTouchPopRoutine();
        Vector3 peakScale = defaultLocalScale * DestroyPopScale;

        if (DestroyPopUpDuration > 0f)
        {
            float elapsedUp = 0f;
            while (elapsedUp < DestroyPopUpDuration)
            {
                elapsedUp += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedUp / DestroyPopUpDuration);
                transform.localScale = Vector3.LerpUnclamped(defaultLocalScale, peakScale, EaseOutBack(t));
                yield return null;
            }
        }

        if (DestroyPopDownDuration > 0f)
        {
            float elapsedDown = 0f;
            while (elapsedDown < DestroyPopDownDuration)
            {
                elapsedDown += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedDown / DestroyPopDownDuration);
                transform.localScale = Vector3.LerpUnclamped(peakScale, Vector3.zero, EaseInBack(t));
                yield return null;
            }
        }

        transform.localScale = Vector3.zero;
        destroyRoutine = null;
        FinalizeBreak("CoroutineComplete");
    }

    private void StopDestroyRoutine()
    {
        if (destroyRoutine == null)
        {
            return;
        }

        StopCoroutine(destroyRoutine);
        destroyRoutine = null;
    }

    private void StartBreakTimeoutRoutine()
    {
        StopBreakTimeoutRoutine();
        breakTimeoutRoutine = StartCoroutine(BreakTimeoutRoutine());
    }

    private void StopBreakTimeoutRoutine()
    {
        if (breakTimeoutRoutine == null)
        {
            return;
        }

        StopCoroutine(breakTimeoutRoutine);
        breakTimeoutRoutine = null;
    }

    private IEnumerator BreakTimeoutRoutine()
    {
        float maxBreakDuration = Mathf.Max(0.1f, DestroyPopUpDuration + DestroyPopDownDuration + 0.2f);
        float elapsed = 0f;
        while (!breakFinalized && gameObject.activeSelf && elapsed < maxBreakDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        breakTimeoutRoutine = null;
        if (!breakFinalized && gameObject.activeSelf)
        {
            FinalizeBreak("TimeoutFallback");
        }
    }

    public void TryPlaySawTouchPop()
    {
        if (!enableSawTouchPop)
        {
            return;
        }

        if (isBreaking || !isActiveAndEnabled)
        {
            return;
        }

        if (Time.time - lastSawTouchPopTime < SawTouchPopCooldown)
        {
            return;
        }

        lastSawTouchPopTime = Time.time;
        PlaySawTouchPop();
    }

    private void PlaySawTouchPop()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopSawTouchPopRoutine();
        sawTouchPopRoutine = StartCoroutine(SawTouchPopRoutine());
    }

    private IEnumerator SawTouchPopRoutine()
    {
        Vector3 startScale = defaultLocalScale;
        Vector3 peakScale = defaultLocalScale * SawTouchPopScale;

        if (SawTouchPopUpDuration > 0f)
        {
            float elapsedUp = 0f;
            while (elapsedUp < SawTouchPopUpDuration)
            {
                elapsedUp += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedUp / SawTouchPopUpDuration);
                transform.localScale = Vector3.LerpUnclamped(startScale, peakScale, EaseOutBack(t));
                yield return null;
            }
        }

        if (SawTouchPopDownDuration > 0f)
        {
            float elapsedDown = 0f;
            while (elapsedDown < SawTouchPopDownDuration)
            {
                elapsedDown += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedDown / SawTouchPopDownDuration);
                transform.localScale = Vector3.LerpUnclamped(peakScale, defaultLocalScale, EaseInBack(t));
                yield return null;
            }
        }

        transform.localScale = defaultLocalScale;
        sawTouchPopRoutine = null;
    }

    private void StopSawTouchPopRoutine()
    {
        if (sawTouchPopRoutine == null)
        {
            return;
        }

        StopCoroutine(sawTouchPopRoutine);
        sawTouchPopRoutine = null;
        if (!isBreaking)
        {
            transform.localScale = defaultLocalScale;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + (c3 * p * p * p) + (c1 * p * p);
    }

    private static float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return (c3 * t * t * t) - (c1 * t * t);
    }

    private void Awake()
    {
        ForceIdentityRotation();
        EnsureTargetCollider();
        defaultLocalScale = transform.localScale;
        cachedMeshRenderer = GetComponent<MeshRenderer>();
    }

    private void ApplyCubeMaterial()
    {
        if (cachedMeshRenderer == null)
        {
            cachedMeshRenderer = GetComponent<MeshRenderer>();
        }

        if (cachedMeshRenderer == null)
        {
            return;
        }

        Material sharedMat = PixelColorMaterialProvider.GetMaterial(cubeColor);
        if (sharedMat == null)
        {
            sharedMat = PixelColorMaterialProvider.GetSharedTintMaterial();
        }

        if (sharedMat == null)
        {
            return;
        }

        Color appliedColor = PixelColorMaterialProvider.GetCanonicalVisualColor(cubeColor);
        if (cubeColor != PixelCubeColor.None && cubeColorBrightnessJitter > 0f)
        {
            int safeSteps = Mathf.Max(1, batchedBrightnessSteps);
            int shadeIndex = safeSteps <= 1 ? 0 : Random.Range(0, safeSteps);
            float t = safeSteps <= 1 ? 0.5f : (float)shadeIndex / (safeSteps - 1f);
            float brightnessScale = Mathf.Lerp(1f - cubeColorBrightnessJitter, 1f + cubeColorBrightnessJitter, t);
            appliedColor = PixelColorMaterialProvider.ScaleColorValue(appliedColor, brightnessScale);
        }

        cachedMeshRenderer.sharedMaterial = sharedMat;
        currentVisualColor = appliedColor;
        if (colorPropertyBlock == null)
        {
            colorPropertyBlock = new MaterialPropertyBlock();
        }

        cachedMeshRenderer.GetPropertyBlock(colorPropertyBlock);
        colorPropertyBlock.SetColor(BaseColorId, appliedColor);
        colorPropertyBlock.SetColor(ColorId, appliedColor);
        cachedMeshRenderer.SetPropertyBlock(colorPropertyBlock);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTargetCollider();
    }
#endif

    private void EnsureTargetCollider()
    {
        if (targetCollider == null)
        {
            targetCollider = GetComponentInChildren<Collider>(true);
        }
    }

    private void ForceIdentityRotation()
    {
        if (transform.localRotation != Quaternion.identity)
        {
            transform.localRotation = Quaternion.identity;
        }
    }

    private void RefreshClusterRegistration()
    {
        PixelClusterRigidbody previousCluster = cachedCluster;
        PixelClusterRigidbody currentCluster = GetComponentInParent<PixelClusterRigidbody>();

        if (isExtractedForCollection)
        {
            if (previousCluster != null)
            {
                previousCluster.UnregisterCell(this);
            }

            cachedCluster = null;
            return;
        }

        cachedCluster = currentCluster;

        if (previousCluster != null && previousCluster != currentCluster)
        {
            previousCluster.UnregisterCell(this);
        }

        if (currentCluster != null && isActiveAndEnabled)
        {
            currentCluster.RegisterCell(this);
        }
    }

    private void RefreshManagerRegistration()
    {
        PixelCubeManager previousManager = cachedCubeManager;
        PixelCubeManager currentManager = PixelCubeManager.Instance != null
            ? PixelCubeManager.Instance
            : FindFirstObjectByType<PixelCubeManager>();

        if (isExtractedForCollection)
        {
            if (previousManager != null)
            {
                previousManager.UnregisterCell(this);
            }

            cachedCubeManager = null;
            return;
        }

        cachedCubeManager = currentManager;

        if (previousManager != null && previousManager != currentManager)
        {
            previousManager.UnregisterCell(this);
        }

        if (currentManager != null && isActiveAndEnabled)
        {
            currentManager.RegisterCell(this);
        }
    }
}
