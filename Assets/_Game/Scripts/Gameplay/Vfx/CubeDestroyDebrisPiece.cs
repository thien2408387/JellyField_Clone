using UnityEngine;

/// <summary>
/// Gắn lên từng GameObject mảnh trong prefab nổ (sprite/quad). Di chuyển bằng transform, không Rigidbody — nhẹ cho mobile.
/// </summary>
[DisallowMultipleComponent]
public sealed class CubeDestroyDebrisPiece : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private MeshRenderer meshRenderer;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;
    private MaterialPropertyBlock _propertyBlock;

    private Vector3 _velocity;
    private Vector3 _angularVelocityDegPerSec;
    private Vector3 _gravity;
    private float _drag;
    private bool _simulating;

    private float _burstElapsed;
    private float _peakScaleMul = 1.5f;
    private float _popDuration;
    private float _shrinkDuration;
    private float _endScaleMul;

    private void Awake()
    {
        CacheInitialTransform();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void OnDisable()
    {
        _simulating = false;
    }

    private void Update()
    {
        if (!_simulating)
        {
            return;
        }

        float dt = Time.deltaTime;
        _burstElapsed += dt;

        _velocity += _gravity * dt;
        _velocity *= Mathf.Exp(-_drag * dt);
        transform.position += _velocity * dt;

        if (_angularVelocityDegPerSec.sqrMagnitude > 0.0001f)
        {
            transform.Rotate(_angularVelocityDegPerSec * dt, Space.Self);
        }

        ApplyBurstScaleJuice();
    }

    private void ApplyBurstScaleJuice()
    {
        if (_popDuration <= 0f && _shrinkDuration <= 0f)
        {
            return;
        }

        float mul;
        if (_popDuration > 0.001f && _burstElapsed <= _popDuration)
        {
            float u = Mathf.Clamp01(_burstElapsed / _popDuration);
            mul = Mathf.SmoothStep(1f, _peakScaleMul, u);
        }
        else if (_shrinkDuration > 0.001f)
        {
            float afterPop = Mathf.Max(0f, _burstElapsed - _popDuration);
            float u = Mathf.Clamp01(afterPop / _shrinkDuration);
            mul = Mathf.SmoothStep(_peakScaleMul, _endScaleMul, u);
        }
        else
        {
            mul = _peakScaleMul;
        }

        transform.localScale = _initialLocalScale * mul;
    }

    public void CacheInitialTransform()
    {
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialLocalScale = transform.localScale;
    }

    public void ResetPiece()
    {
        _simulating = false;
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
        transform.localScale = _initialLocalScale;
        _velocity = Vector3.zero;
        _angularVelocityDegPerSec = Vector3.zero;
        _burstElapsed = 0f;
        _peakScaleMul = 1.5f;
        _popDuration = 0f;
        _shrinkDuration = 0f;
        _endScaleMul = 0.1f;
    }

    public void ApplyTint(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }

        if (meshRenderer == null)
        {
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(_propertyBlock);
    }

    public void BeginBurst(
        Vector3 worldVelocity,
        Vector3 angularVelocityDegPerSec,
        Vector3 gravity,
        float drag,
        float peakScaleMultiplier,
        float scalePopDuration,
        float scaleShrinkDuration,
        float endScaleMultiplier)
    {
        _velocity = worldVelocity;
        _angularVelocityDegPerSec = angularVelocityDegPerSec;
        _gravity = gravity;
        _drag = Mathf.Max(0f, drag);
        _burstElapsed = 0f;
        _peakScaleMul = Mathf.Max(1f, peakScaleMultiplier);
        _popDuration = Mathf.Max(0f, scalePopDuration);
        _shrinkDuration = Mathf.Max(0f, scaleShrinkDuration);
        _endScaleMul = Mathf.Clamp(endScaleMultiplier, 0f, 1f);
        _simulating = true;
        ApplyBurstScaleJuice();
    }
}
