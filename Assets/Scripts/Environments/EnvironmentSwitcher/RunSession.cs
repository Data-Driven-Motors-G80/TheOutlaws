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

    public static float NextSwitchAt = -1f;

    /// <summary>The first switch (city to snow) happens at a random run distance, well after 1,000 m.</summary>
    public static float FirstSwitchDistance = -1f;
    public static bool FirstSwitchDone;

    /// <summary>Run distance at which the next portal gate stands (-1 = not scheduled yet).</summary>
    public static float NextPortalDistance = -1f;

    public static void ResetSwitchSchedule()
    {
        NextSwitchAt = -1f;
        FirstSwitchDistance = -1f;
        FirstSwitchDone = false;
        NextPortalDistance = -1f;
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