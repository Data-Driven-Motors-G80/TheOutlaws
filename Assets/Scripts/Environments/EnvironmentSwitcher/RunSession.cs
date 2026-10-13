// Preserves the current run when switching environment scenes.
using UnityEngine;

public struct RunSnapshot
{
    public float Elapsed;
    public float Distance;
    public float Score;
    public float MeterFuel;
    public float StateFuel;
    public float ForwardSpeed;
    public float SpeedTimer;
    public float StartingSpeed;
    public float MaximumSpeed;
    public float SpeedIncreaseAmount;
    public float SpeedIncreaseInterval;
    public int Ammo;
    public RiskRunSnapshot Risk;
}

public static class RunSession
{
    private static RunSnapshot pending;

    public static bool HasPending { get; private set; }

    public const float PortalIntervalSeconds = 60f;

    /// <summary>Active run time when the next gate appears (-1 = not scheduled yet).</summary>
    public static float NextPortalAt = -1f;

    public static void ScheduleNextPortal(float elapsedSeconds)
    {
        NextPortalAt = elapsedSeconds + PortalIntervalSeconds;
    }

    public static float SecondsUntilPortal(float elapsedSeconds)
    {
        float scheduledAt = NextPortalAt < 0f ? PortalIntervalSeconds : NextPortalAt;
        return System.Math.Max(0f, scheduledAt - elapsedSeconds);
    }

    public static void ResetSwitchSchedule()
    {
        NextPortalAt = -1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending = default;
        HasPending = false;
        ResetSwitchSchedule();
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
