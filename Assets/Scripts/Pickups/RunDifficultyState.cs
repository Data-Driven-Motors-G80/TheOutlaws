using System;

/// <summary>Smooth, bounded difficulty driven by active gameplay seconds.</summary>
public readonly struct RunDifficultyState
{
    public const float StartSpeed = 20f;
    public const float MaximumSpeed = 60f;
    public const float SpeedStep = 1.5f;
    public const float SpeedBlendSeconds = 2.5f;
    public const float MinimumIncreaseInterval = 12f;
    public const float MaximumIncreaseInterval = 18f;

    /// <summary>Retained for code that still reasons in 25-second steps.</summary>
    public static int PressureSteps(float elapsedSeconds)
    {
        return (int)(Math.Max(0f, elapsedSeconds) / 25f);
    }

    /// <summary>A seeded schedule keeps the same timing across frames and scene changes.</summary>
    public static float SpeedIncreaseInterval(int seed, int step)
    {
        uint value = unchecked((uint)seed + (uint)(step + 1) * 0x9E3779B9u);
        value = unchecked((value ^ (value >> 16)) * 0x85EBCA6Bu);
        value = unchecked((value ^ (value >> 13)) * 0xC2B2AE35u);
        value ^= value >> 16;
        return MinimumIncreaseInterval + (value & 0xFFFFFFu) / 16777215f
            * (MaximumIncreaseInterval - MinimumIncreaseInterval);
    }

    /// <summary>
    /// Gain 1.5 m/s every 12–18 seconds, blended over 2.5 seconds, capped at 60
    /// (about 32 m/s at two minutes, 44 m/s at four, the cap after roughly seven).
    /// </summary>
    public static float DrivingSpeed(float elapsedSeconds, int seed = 1)
    {
        if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) return StartSpeed;
        float nextIncrease = 0f;
        float speed = StartSpeed;
        int steps = (int)Math.Ceiling((MaximumSpeed - StartSpeed) / SpeedStep);
        for (int step = 0; step < steps; step++)
        {
            nextIncrease += SpeedIncreaseInterval(seed, step);
            if (elapsedSeconds < nextIncrease) break;
            float progress = Math.Min(1f, (elapsedSeconds - nextIncrease) / SpeedBlendSeconds);
            speed = Math.Min(MaximumSpeed, StartSpeed + (step + progress) * SpeedStep);
            if (progress < 1f) break;
        }
        return speed;
    }

    /// <summary>Seconds of gentle warm-up at the start of every run.</summary>
    public const float WarmUpSeconds = 30f;
    /// <summary>Seconds after the warm-up until obstacles reach their densest.</summary>
    public const float BuildUpSeconds = 120f;

    /// <summary>
    /// How tightly obstacle rows are packed: a gentle 0.6 at the start, 1.3 after the
    /// 30 s warm-up, then building to 2.4 over the next two minutes. Spacing still respects speed.
    /// </summary>
    public static float ObstacleDensity(float elapsedSeconds)
    {
        // Never stops growing: after the build-up, +0.5 density per minute (up to 4.5).
        return Math.Min(4.5f, Phase(elapsedSeconds, 0.6f, 1.3f, 2.4f) + LateMinutes(elapsedSeconds) * 0.5f);
    }

    /// <summary>Minutes driven after the warm-up and build-up (keeps difficulty rising all run).</summary>
    public static float LateMinutes(float elapsedSeconds)
    {
        return Math.Max(0f, elapsedSeconds - WarmUpSeconds - BuildUpSeconds) / 60f;
    }

    /// <summary>Half-width of the guaranteed open gap: roomy at first, tighter (never closed) later on.</summary>
    public static float GapHalfWidth(float elapsedSeconds, double roll)
    {
        float tightness = Math.Min(1f, Math.Max(0f, elapsedSeconds) / 240f);
        float widest = 1.5f - 0.25f * tightness;
        float narrowest = 1.25f - 0.12f * tightness;
        return narrowest + (widest - narrowest) * (float)Math.Max(0d, Math.Min(1d, roll));
    }

    /// <summary>Chance that a row blocks two lanes instead of one (always leaves a lane open).</summary>
    public static float DoubleRowChance(float elapsedSeconds)
    {
        return Math.Min(0.9f, Phase(elapsedSeconds, 0.1f, 0.3f, 0.65f) + LateMinutes(elapsedSeconds) * 0.1f);
    }

    private static float Phase(float elapsedSeconds, float start, float afterWarmUp, float maximum)
    {
        float t = Math.Max(0f, elapsedSeconds);
        if (t < WarmUpSeconds) return start + (afterWarmUp - start) * (t / WarmUpSeconds);
        return afterWarmUp + (maximum - afterWarmUp) * Math.Min(1f, (t - WarmUpSeconds) / BuildUpSeconds);
    }

    /// <summary>
    /// Police speed minus player speed. Always negative: a player who stops getting hit always pulls
    /// away again. You pull away at 1.5 m/s at first, slowing to 0.6 m/s after 150 s, so later on
    /// hits close together are what gets you caught.
    /// </summary>
    public static float PoliceClosingSpeed(float elapsedSeconds)
    {
        return -Math.Max(0.6f, 1.5f - Math.Max(0f, elapsedSeconds) * 0.006f);
    }

    public float Progress { get; }
    public float Relief { get; }

    private RunDifficultyState(float progress, float relief)
    {
        Progress = progress;
        Relief = relief;
    }

    public static RunDifficultyState Evaluate(float elapsed, float opening = 5f, float ramp = 70f)
    {
        float active = Math.Max(0f, elapsed - Math.Max(0f, opening));
        float t = Math.Min(1f, active / Math.Max(1f, ramp));
        float progress = t * t * (3f - 2f * t);
        // 18 seconds of pressure, followed by a six-second rest with soft edges.
        float phase = active % 24f;
        float relief = phase <= 18f ? 0f : (float)Math.Sin((phase - 18f) / 6f * Math.PI);
        return new RunDifficultyState(progress, relief);
    }
}
