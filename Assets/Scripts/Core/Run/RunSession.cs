// Carries run snapshots and portal timing across environment changes.

using UnityEngine;

public struct RunSnapshot
{
    public float Elapsed;
    public int SpeedScheduleSeed;
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
    public int EmptyGunBoxStreak;
    public float LastJumpOfferDistance;
    public RandomPickupOutcome BackpackItem;
    public float NextMysteryRowAt;
    public float SunnyRemaining;
    public float ReverseCountdown;
    public float PendingReverse;
    public RiskRunSnapshot Risk;
}

public static class RunSession
{
    private static RunSnapshot pending;

    public static bool HasPending { get; private set; }

    public const float MinimumPortalIntervalSeconds = 30f;
    public const float MaximumPortalIntervalSeconds = 45f;
    private static System.Random portalRandom = new System.Random();

    public static float NextPortalAt = -1f;

    // Choose a fresh 30-45 second target on the active gameplay clock.
    public static void ScheduleNextPortal(float elapsedSeconds)
    {
        float interval = MinimumPortalIntervalSeconds + (MaximumPortalIntervalSeconds - MinimumPortalIntervalSeconds)
            * (float)portalRandom.NextDouble();
        NextPortalAt = elapsedSeconds + interval;
    }

    public static float SecondsUntilPortal(float elapsedSeconds)
    {
        if (NextPortalAt < 0f) ScheduleNextPortal(0f);
        return System.Math.Max(0f, NextPortalAt - elapsedSeconds);
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
        portalRandom = new System.Random();
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

    // Read the pending snapshot; the caller clears it after restoring the run.
    public static bool TryGetPending(out RunSnapshot snapshot)
    {
        snapshot = pending;
        return HasPending;
    }
}
