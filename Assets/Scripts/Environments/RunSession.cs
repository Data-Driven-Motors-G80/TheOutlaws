using UnityEngine;

/// <summary>Everything that must survive switching from one environment scene to another.</summary>
public struct RunSnapshot
{
    public float Elapsed;
    public float Distance;
    public float Score;
    public float MeterFuel;
    public float StateFuel;
    public float ForwardSpeed;
    public float SpeedTimer;
    public int Ammo;
    public RiskRunSnapshot Risk;
}

/// <summary>
/// Static hand-over between two environment scenes. The old scene captures its state, the new
/// scene's components read it in Start(), then SceneTransition clears it. A normal restart never
/// sets it, so restarts always begin a fresh run.
/// </summary>
public static class RunSession
{
    private static RunSnapshot pending;

    public static bool HasPending { get; private set; }

    /// <summary>Gameplay-seconds (OutlawGameManager.ElapsedSeconds) of the next environment pick; -1 = not scheduled.</summary>
    public static float NextSwitchAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending = default;
        HasPending = false;
        NextSwitchAt = -1f;
    }

    public static void SetPending(RunSnapshot snapshot)
    {
        pending = snapshot;
        HasPending = true;
    }

    public static void ClearPending()
    {
        pending = default;
        HasPending = false;
    }

    public static bool TryGetPending(out RunSnapshot snapshot)
    {
        snapshot = pending;
        return HasPending;
    }
}
