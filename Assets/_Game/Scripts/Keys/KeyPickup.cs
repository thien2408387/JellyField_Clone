using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public enum State
    {
        IdleOnPixel,
        PendingHover,
        Flying,
    }

    public enum SupportSide
    {
        None,
        Above,
        Below,
        Left,
        Right,
    }

    [SerializeField] private Vector2Int anchorGridPosition;
    [SerializeField] private State currentState = State.IdleOnPixel;
    [Header("Attached Follow")]
    [SerializeField] private bool followSupportCellsWhileIdle = true;
    [SerializeField] private Vector3 idleFollowWorldOffset = Vector3.zero;
    [Header("Spin")]
    [SerializeField] private bool enableSpin = true;
    [SerializeField] private bool spinWhileIdle = true;
    [SerializeField] private bool spinWhilePendingHover = true;
    [SerializeField] private bool spinWhileFlying = true;
    [SerializeField] private Vector3 spinAxis = Vector3.forward;
    [SerializeField, Min(0f)] private float spinDegreesPerSecond = 180f;

    [Header("Flight")]
    [SerializeField, Min(0.05f)] private float flightDuration = 1.0f;
    [SerializeField] private float flightArcHeight = 1.25f;
    [SerializeField, Min(0f)] private float flightCurveStrength = 0.35f;

    [Header("Arrival Bounce")]
    [SerializeField] private float arrivalBounceHeight = 0.2f;
    [SerializeField, Min(0.05f)] private float arrivalBounceDuration = 0.2f;
    [SerializeField] private float arrivalBounceSquash = 0.08f;

    [Header("Pending Hover")]
    [SerializeField] private float hoverBobAmplitude = 0.08f;
    [SerializeField, Min(0.05f)] private float hoverBobPeriod = 1.1f;

    private Tween _flightTween;
    private Coroutine _hoverRoutine;
    private LockJellyBlock _currentTarget;
    private Action<KeyPickup, LockJellyBlock> _onArrival;
    private Action<KeyPickup, LockJellyBlock> _onTargetInvalid;
    private Rigidbody _rb3D;
    private RigidbodyConstraints _rb3DConstraints;
    private bool _rb3DWasKinematic;
    private bool _rb3DHadGravity;
    private bool _rb3DHasState;
    private bool _rb2DSimulated;
    private bool _rb2DHasState;
    private readonly List<PixelCubeCell> _supportCells = new(8);

    private void Awake()
    {
        _rb3D = GetComponent<Rigidbody>();
    }

    public Vector2Int AnchorGridPosition => anchorGridPosition;
    public State CurrentState => currentState;
    public LockJellyBlock CurrentTarget => _currentTarget;

    public void SetAnchorGridPosition(Vector2Int value)
    {
        anchorGridPosition = value;
    }

    public bool CoversGridPosition(Vector2Int cellGridPosition)
    {
        int dx = cellGridPosition.x - anchorGridPosition.x;
        int dy = cellGridPosition.y - anchorGridPosition.y;
        return (dx == 0 || dx == 1) && (dy == 0 || dy == 1);
    }

    public SupportSide GetSupportSide(Vector2Int cellGridPosition)
    {
        int dx = cellGridPosition.x - anchorGridPosition.x;
        int dy = cellGridPosition.y - anchorGridPosition.y;

        bool xInFootprint = dx == 0 || dx == 1;
        bool yInFootprint = dy == 0 || dy == 1;

        if (xInFootprint && dy == -1) return SupportSide.Below;
        if (xInFootprint && dy == 2) return SupportSide.Above;
        if (dx == -1 && yInFootprint) return SupportSide.Left;
        if (dx == 2 && yInFootprint) return SupportSide.Right;
        return SupportSide.None;
    }

    public void SetState(State value)
    {
        currentState = value;
        if (currentState == State.IdleOnPixel)
        {
            PrepareForAnimatedMotion();
            UpdateIdleFollowPosition(forceSnap: true);
        }
    }

    public void BindSupportCells(IEnumerable<PixelCubeCell> supportCells)
    {
        _supportCells.Clear();
        if (supportCells == null)
        {
            return;
        }

        foreach (PixelCubeCell cell in supportCells)
        {
            if (cell == null || _supportCells.Contains(cell))
            {
                continue;
            }

            _supportCells.Add(cell);
        }

        if (currentState == State.IdleOnPixel)
        {
            PrepareForAnimatedMotion();
            UpdateIdleFollowPosition(forceSnap: true);
        }
    }

    public void ClearSupportCells()
    {
        _supportCells.Clear();
    }

    public void BeginPendingHover()
    {
        StopFlight();
        PrepareForAnimatedMotion();
        currentState = State.PendingHover;
        if (_hoverRoutine == null && isActiveAndEnabled)
        {
            _hoverRoutine = StartCoroutine(HoverRoutine());
        }
    }

    public void BeginFlight(
        LockJellyBlock target,
        Action<KeyPickup, LockJellyBlock> onArrival,
        Action<KeyPickup, LockJellyBlock> onTargetInvalid)
    {
        if (target == null || !isActiveAndEnabled)
        {
            onTargetInvalid?.Invoke(this, target);
            return;
        }

        StopHover();
        StopFlight();
        PrepareForAnimatedMotion();

        _currentTarget = target;
        _onArrival = onArrival;
        _onTargetInvalid = onTargetInvalid;
        currentState = State.Flying;
        StartFlightTween();
    }

    public void StopFlight()
    {
        if (_flightTween != null && _flightTween.IsActive())
        {
            _flightTween.Kill();
        }
        _flightTween = null;

        RestorePhysicsState();
        _currentTarget = null;
        _onArrival = null;
        _onTargetInvalid = null;
    }

    private void StopHover()
    {
        if (_hoverRoutine != null)
        {
            StopCoroutine(_hoverRoutine);
            _hoverRoutine = null;
        }
    }

    private void StartFlightTween()
    {
        Vector3 startPos = transform.position;
        Vector3 baseScale = transform.localScale;
        Vector3 initialTargetPos = _currentTarget != null ? _currentTarget.KeyHolePosition : startPos;
        Vector3 directDirection = initialTargetPos - startPos;
        float directDistance = directDirection.magnitude;
        Vector3 sideAxis = Vector3.Cross(
            directDirection.sqrMagnitude > 0.0001f ? directDirection.normalized : Vector3.forward,
            Vector3.up);
        if (sideAxis.sqrMagnitude < 0.0001f)
            sideAxis = Vector3.right;
        else
            sideAxis.Normalize();
        float sideOffset = directDistance * flightCurveStrength;
        Vector3 control1 = startPos + directDirection * 0.35f + Vector3.up * (flightArcHeight * 0.9f) + sideAxis * sideOffset;
        // Keep second handle higher so the final approach does not read as "diving" through the level.
        Vector3 control2 = startPos + directDirection * 0.75f + Vector3.up * (flightArcHeight * 0.52f) - sideAxis * sideOffset * 0.4f;
        float arrivalWindow = Mathf.Clamp01(arrivalBounceDuration / Mathf.Max(flightDuration, 0.0001f));

        Sequence seq = DOTween.Sequence();

        // Ease in-out along the curve: soft launch, faster mid-flight, then ease into the lock.
        seq.Append(DOVirtual.Float(0f, 1f, flightDuration, rawT =>
        {
            if (_currentTarget == null || !_currentTarget.IsLocked)
            {
                CancelFlightBecauseTargetInvalid();
                return;
            }

            Vector3 curTarget = _currentTarget.KeyHolePosition;
            Vector3 delta = curTarget - initialTargetPos;
            float bezierT = DOVirtual.EasedValue(0f, 1f, rawT, Ease.InOutSine);
            transform.position = EvaluateCubicBezier(
                startPos, control1 + delta * 0.35f, control2 + delta * 0.75f, curTarget, bezierT);

            float arrivalT = arrivalWindow > 0f
                ? Mathf.InverseLerp(1f - arrivalWindow, 1f, rawT)
                : 1f;
            float squashStrength = arrivalBounceSquash * Mathf.Sin(arrivalT * Mathf.PI * 0.5f);
            transform.localScale = BuildSquashedScale(baseScale, squashStrength);
        })).SetEase(Ease.Linear);
        seq.AppendCallback(() =>
        {
            if (_currentTarget != null)
            {
                transform.position = _currentTarget.KeyHolePosition;
            }
            transform.localScale = baseScale;
        });
        seq.AppendCallback(CompleteFlight);

        seq.SetUpdate(UpdateType.Normal);
        _flightTween = seq;
    }

    private IEnumerator HoverRoutine()
    {
        Vector3 basePosition = transform.position;
        float phase = 0f;
        while (currentState == State.PendingHover)
        {
            phase += Time.deltaTime;
            float offset = Mathf.Sin(phase * (Mathf.PI * 2f / hoverBobPeriod)) * hoverBobAmplitude;
            transform.position = basePosition + Vector3.up * offset;
            yield return null;
        }

        _hoverRoutine = null;
    }

    private void OnDisable()
    {
        StopFlight();
        StopHover();
    }

    private void LateUpdate()
    {
        ApplySpin(Time.deltaTime);

        if (currentState != State.IdleOnPixel)
        {
            return;
        }

        UpdateIdleFollowPosition(forceSnap: false);
    }

    private void CompleteFlight()
    {
        LockJellyBlock arrivedTarget = _currentTarget;
        Action<KeyPickup, LockJellyBlock> arrived = _onArrival;
        _flightTween = null;
        //RestorePhysicsState();
        _currentTarget = null;
        _onArrival = null;
        _onTargetInvalid = null;

        if (arrivedTarget == null || !arrivedTarget.IsLocked)
        {
            return;
        }

        transform.position = arrivedTarget.KeyHolePosition;
        arrived?.Invoke(this, arrivedTarget);
    }

    private void CancelFlightBecauseTargetInvalid()
    {
        Action<KeyPickup, LockJellyBlock> abort = _onTargetInvalid;
        LockJellyBlock abortedTarget = _currentTarget;

        if (_flightTween != null && _flightTween.IsActive())
        {
            _flightTween.Kill();
        }
        _flightTween = null;
        RestorePhysicsState();
        _currentTarget = null;
        _onArrival = null;
        _onTargetInvalid = null;
        abort?.Invoke(this, abortedTarget);
    }

    private void PrepareForAnimatedMotion()
    {
        if (_rb3D != null)
        {
            if (!_rb3DHasState)
            {
                _rb3DConstraints = _rb3D.constraints;
                _rb3DWasKinematic = _rb3D.isKinematic;
                _rb3DHadGravity = _rb3D.useGravity;
                _rb3DHasState = true;
            }

            _rb3D.linearVelocity = Vector3.zero;
            _rb3D.angularVelocity = Vector3.zero;
            _rb3D.isKinematic = true;
            _rb3D.useGravity = false;
            _rb3D.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void UpdateIdleFollowPosition(bool forceSnap)
    {
        if (!followSupportCellsWhileIdle || _supportCells.Count == 0)
        {
            return;
        }

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        bool hasBounds = false;

        for (int i = _supportCells.Count - 1; i >= 0; i--)
        {
            PixelCubeCell cell = _supportCells[i];
            if (cell == null || !cell.gameObject.activeInHierarchy || cell.IsBreaking)
            {
                _supportCells.RemoveAt(i);
                continue;
            }

            Vector3 position = cell.transform.position;
            if (!hasBounds)
            {
                min = position;
                max = position;
                hasBounds = true;
                continue;
            }

            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        if (!hasBounds)
        {
            return;
        }

        Vector3 targetPosition = ((min + max) * 0.5f) + idleFollowWorldOffset;
        if (forceSnap || _rb3D == null || _rb3D.isKinematic)
        {
            transform.position = targetPosition;
            return;
        }

        _rb3D.MovePosition(targetPosition);
    }

    private void ApplySpin(float deltaTime)
    {
        if (!enableSpin || deltaTime <= 0f || spinDegreesPerSecond <= 0f)
        {
            return;
        }

        if (!ShouldSpinInCurrentState())
        {
            return;
        }

        Vector3 axis = spinAxis.sqrMagnitude > 0.0001f ? spinAxis.normalized : Vector3.forward;
        transform.Rotate(axis, spinDegreesPerSecond * deltaTime, Space.Self);
    }

    private bool ShouldSpinInCurrentState()
    {
        switch (currentState)
        {
            case State.IdleOnPixel:
                return spinWhileIdle;

            case State.PendingHover:
                return spinWhilePendingHover;

            case State.Flying:
                return spinWhileFlying;

            default:
                return false;
        }
    }

    private void RestorePhysicsState()
    {
        if (_rb3D != null && _rb3DHasState)
        {
            _rb3D.constraints = _rb3DConstraints;
            _rb3D.isKinematic = _rb3DWasKinematic;
            _rb3D.useGravity = _rb3DHadGravity;
            _rb3DHasState = false;
        }
    }

    private static Vector3 BuildSquashedScale(Vector3 baseScale, float squashStrength)
    {
        float clamped = Mathf.Clamp(squashStrength, 0f, 0.4f);
        return new Vector3(
            baseScale.x * (1f + clamped),
            baseScale.y * (1f - clamped),
            baseScale.z * (1f + clamped));
    }

    private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float omt = 1f - t;
        float omt2 = omt * omt;
        float t2 = t * t;
        return (omt2 * omt) * p0
             + (3f * omt2 * t) * p1
             + (3f * omt * t2) * p2
             + (t2 * t) * p3;
    }
}
