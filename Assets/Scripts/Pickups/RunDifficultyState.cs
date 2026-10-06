using System;

/// <summary>Bounded difficulty and periodic relief, driven by active gameplay seconds.</summary>
public readonly struct RunDifficultyState
{
    public float Progress { get; }
    public float Relief { get; }

    private RunDifficultyState(float progress, float relief)
    {
        Progress = progress;
        Relief = relief;
    }

    public static RunDifficultyState Evaluate(float elapsed, float opening = 20f, float ramp = 70f)
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
