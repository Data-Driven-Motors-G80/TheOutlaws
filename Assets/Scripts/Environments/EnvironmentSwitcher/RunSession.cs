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
    public int Ammo;
    public RiskRunSnapshot Risk;
}

public static class RunSession
{
    private static RunSnapshot pending;

    public static bool HasPending { get; private set; }

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