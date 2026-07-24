using System;
using UnityEngine;

/// <summary>
/// Singleton event bus for gameplay notifications. Observers can subscribe at any time;
/// static handlers do not require a scene object.
/// </summary>
public sealed class GameEventHub : MonoBehaviour
{
    public static GameEventHub Instance { get; private set; }
    public static bool IsApplicationQuitting => isApplicationQuitting;

    private static event Action<PixelCubeCell> PixelCubeDestroyedStatic;
    private static event Action<PixelShooter, PixelBullet> PixelBulletFiredStatic;
    private static event Action<Saw> SawDepletedStatic;
    private static event Action TopSlotsFullTapBlockedStatic;
    private static bool isApplicationQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        PixelCubeDestroyedStatic = null;
        PixelBulletFiredStatic = null;
        SawDepletedStatic = null;
        TopSlotsFullTapBlockedStatic = null;
        isApplicationQuitting = false;
    }

    public static void SubscribePixelCubeDestroyed(Action<PixelCubeCell> handler)
    {
        PixelCubeDestroyedStatic += handler;
    }

    public static void UnsubscribePixelCubeDestroyed(Action<PixelCubeCell> handler)
    {
        PixelCubeDestroyedStatic -= handler;
    }

    public static void SubscribePixelBulletFired(Action<PixelShooter, PixelBullet> handler)
    {
        PixelBulletFiredStatic += handler;
    }

    public static void UnsubscribePixelBulletFired(Action<PixelShooter, PixelBullet> handler)
    {
        PixelBulletFiredStatic -= handler;
    }

    public static void SubscribeSawDepleted(Action<Saw> handler)
    {
        SawDepletedStatic += handler;
    }

    public static void UnsubscribeSawDepleted(Action<Saw> handler)
    {
        SawDepletedStatic -= handler;
    }

    public static void SubscribeTopSlotsFullTapBlocked(Action handler)
    {
        TopSlotsFullTapBlockedStatic += handler;
    }

    public static void UnsubscribeTopSlotsFullTapBlocked(Action handler)
    {
        TopSlotsFullTapBlockedStatic -= handler;
    }

    public static void RaisePixelCubeDestroyed(PixelCubeCell cell)
    {
        if (isApplicationQuitting || !Application.isPlaying || cell == null)
        {
            return;
        }

        PixelCubeDestroyedStatic?.Invoke(cell);
    }

    public static void RaisePixelBulletFired(PixelShooter shooter, PixelBullet bullet)
    {
        if (isApplicationQuitting || !Application.isPlaying || shooter == null || bullet == null)
        {
            return;
        }

        PixelBulletFiredStatic?.Invoke(shooter, bullet);
    }

    public static void RaiseSawDepleted(Saw saw)
    {
        if (isApplicationQuitting || !Application.isPlaying || saw == null)
        {
            return;
        }

        SawDepletedStatic?.Invoke(saw);
    }

    public static void RaiseTopSlotsFullTapBlocked()
    {
        if (isApplicationQuitting || !Application.isPlaying)
        {
            return;
        }

        TopSlotsFullTapBlockedStatic?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }
}
