using UnityEngine;

/// <summary>
/// Đặt trên root prefab VFX phá cube (thay ParticleSystem). Spawn/pool vẫn do <see cref="PixelCubeManager"/> + <see cref="TBN.PoolExtensions"/>.
/// Prefab: root có component này; các child gắn <see cref="CubeDestroyDebrisPiece"/> (layout vị trí "hợp khối" khi local về 0).
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledCubeDestroyBurst : MonoBehaviour
{
    public enum VelocityDistribution
    {
        RandomUnitSphere = 0,
        RandomCircleXY = 1,
    }

    [Header("Speed")]
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 6f;

    [Header("Tumble 3D (deg/s, local)")]
    [SerializeField] private float tumbleSpeedMin = -720f;
    [SerializeField] private float tumbleSpeedMax = 720f;
    [SerializeField, Range(0.2f, 1f)] private float tumbleYawPitchWeight = 1f;
    [SerializeField, Range(0.2f, 1f)] private float tumbleRollWeight = 0.85f;

    [Header("Sprite scale juice")]
    [SerializeField] private float peakScaleMultiplier = 1.65f;
    [SerializeField, Min(0f)] private float scalePopDuration = 0.07f;
    [SerializeField, Min(0f)] private float scaleShrinkDuration = 0.38f;
    [SerializeField, Range(0f, 1f)] private float endScaleMultiplier = 0.06f;

    [Header("Motion")]
    [SerializeField] private VelocityDistribution distribution = VelocityDistribution.RandomCircleXY;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -18f, 0f);
    [SerializeField, Min(0f)] private float drag = 3f;

    private CubeDestroyDebrisPiece[] _shards;

    private void Awake()
    {
        CacheShards();
    }

    private void CacheShards()
    {
        _shards = GetComponentsInChildren<CubeDestroyDebrisPiece>(true);
    }

    /// <summary>Gọi từ <see cref="PixelCubeManager"/> sau khi spawn/pool và đặt transform.</summary>
    public void ApplyColorAndPlay(Color tint)
    {
        if (_shards == null || _shards.Length == 0)
        {
            CacheShards();
        }

        if (_shards == null)
        {
            return;
        }

        for (int i = 0; i < _shards.Length; i++)
        {
            CubeDestroyDebrisPiece piece = _shards[i];
            if (piece == null)
            {
                continue;
            }

            piece.ResetPiece();
            piece.ApplyTint(tint);
            Vector3 velocity = SampleVelocity();
            Vector3 tumble = SampleTumbleAngularVelocity();
            piece.BeginBurst(
                velocity,
                tumble,
                gravity,
                drag,
                peakScaleMultiplier,
                scalePopDuration,
                scaleShrinkDuration,
                endScaleMultiplier);
        }
    }

    private Vector3 SampleTumbleAngularVelocity()
    {
        float rx = Random.Range(tumbleSpeedMin, tumbleSpeedMax) * tumbleYawPitchWeight;
        float ry = Random.Range(tumbleSpeedMin, tumbleSpeedMax) * tumbleYawPitchWeight;
        float rz = Random.Range(tumbleSpeedMin, tumbleSpeedMax) * tumbleRollWeight;
        return new Vector3(rx, ry, rz);
    }

    private Vector3 SampleVelocity()
    {
        Vector3 dir;
        switch (distribution)
        {
            case VelocityDistribution.RandomCircleXY:
            {
                Vector2 c = Random.insideUnitCircle;
                if (c.sqrMagnitude < 0.0001f)
                {
                    c = Vector2.right;
                }

                c.Normalize();
                dir = new Vector3(c.x, c.y, 0f);
                break;
            }
            default:
                dir = Random.onUnitSphere;
                break;
        }

        float speed = Random.Range(minSpeed, maxSpeed);
        return dir * speed;
    }
}
