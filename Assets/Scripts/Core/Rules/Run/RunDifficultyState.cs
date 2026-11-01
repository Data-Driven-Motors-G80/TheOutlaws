// Shared difficulty curves based on elapsed gameplay time.
using System;

public readonly struct RunDifficultyState
{
    public const float StartSpeed = 20f;
    public const float MaximumSpeed = 55f;
    public const float SpeedBlendSeconds = 2.5f;

    public static int PressureSteps(float elapsedSeconds)
    {
        return (int)(Math.Max(0f, elapsedSeconds) / 25f);
    }

    public static float SpeedIncreaseInterval(int seed, int step, bool harder = false)
    {
        uint value = unchecked((uint)seed + (uint)(step + 1) * 0x9E3779B9u);
        value = unchecked((value ^ (value >> 16)) * 0x85EBCA6Bu);
        value = unchecked((value ^ (value >> 13)) * 0xC2B2AE35u);
        value ^= value >> 16;
        float roll = (value & 0xFFFFFFu) / 16777215f;
        return harder ? 16f + roll * 8f : 30f + roll * 15f;
    }

    // Recreate speed progression deterministically from the seed and elapsed time.
    public static float DrivingSpeed(float elapsedSeconds, int seed = 1, bool harder = false)
    {
        if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) return StartSpeed;
        float nextIncrease = 0f;
        float initialSpeed = harder ? 22f : StartSpeed;
        float gain = harder ? 1.25f : 1f;
        float speed = initialSpeed;
        for (int step = 0; step < (int)Math.Ceiling((MaximumSpeed - initialSpeed) / gain); step++)
        {
            nextIncrease += SpeedIncreaseInterval(seed, step, harder);
            if (elapsedSeconds < nextIncrease) break;
            float progress = Math.Min(1f, (elapsedSeconds - nextIncrease) / SpeedBlendSeconds);
            speed = Math.Min(MaximumSpeed, initialSpeed + (step + progress) * gain);
            if (progress < 1f) break;
        }
        return speed;
    }

    public const float WarmUpSeconds = 30f;

    public const float BuildUpSeconds = 120f;

    public static float ObstacleDensity(float elapsedSeconds)
    {

        return Math.Min(4.5f, Phase(elapsedSeconds, 0.6f, 1.3f, 2.4f) + LateMinutes(elapsedSeconds) * 0.5f);
    }

    public static float LateMinutes(float elapsedSeconds)
    {
        return Math.Max(0f, elapsedSeconds - WarmUpSeconds - BuildUpSeconds) / 60f;
    }

    public static float GapHalfWidth(float elapsedSeconds, double roll)
    {
        float tightness = Math.Min(1f, Math.Max(0f, elapsedSeconds) / 240f);
        float widest = 1.5f - 0.25f * tightness;
        float narrowest = 1.25f - 0.12f * tightness;
        return narrowest + (widest - narrowest) * (float)Math.Max(0d, Math.Min(1d, roll));
    }

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

    // Select the pursuit curve for the active difficulty profile.
    public static float PoliceClosingSpeed(float elapsedSeconds, bool harder = false)
    {
        return harder ? -Math.Max(0.2f, 0.85f - Math.Max(0f, elapsedSeconds) * 0.006f)
            : -Math.Max(0.6f, 1.5f - Math.Max(0f, elapsedSeconds) * 0.006f);
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

        float phase = active % 24f;
        float relief = phase <= 18f ? 0f : (float)Math.Sin((phase - 18f) / 6f * Math.PI);
        return new RunDifficultyState(progress, relief);
    }
}
