using System;
using System.Collections.Generic;

/// <summary>One active attempt; scene transitions do not reset its counters.</summary>
public static class RunAnalyticsState
{
    public static string SessionId { get; private set; }
    public static string RunId { get; private set; }
    public static bool Active { get; private set; }
    public static float CitySeconds { get; private set; }
    public static float SnowSeconds { get; private set; }
    private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    public static void ResetSession()
    {
        SessionId = Guid.NewGuid().ToString("N");
        Active = false;
        RunId = null;
        counts.Clear();
        CitySeconds = SnowSeconds = 0f;
    }

    public static void Begin()
    {
        if (SessionId == null) ResetSession();
        RunId = Guid.NewGuid().ToString("N");
        counts.Clear();
        CitySeconds = SnowSeconds = 0f;
        Active = true;
    }

    public static void Count(string metric)
    {
        if (Active) counts[metric] = Get(metric) + 1;
    }

    public static int Get(string metric) => counts.TryGetValue(metric, out int value) ? value : 0;

    public static void Tick(float seconds, bool snow)
    {
        if (!Active || seconds <= 0f) return;
        if (snow) SnowSeconds += seconds;
        else CitySeconds += seconds;
    }

    public static bool End()
    {
        if (!Active) return false;
        Active = false;
        return true;
    }
}
