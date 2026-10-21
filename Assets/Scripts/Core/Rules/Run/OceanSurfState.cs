using System;

/// <summary>Surf momentum without scene dependencies. The environment owns its active-play clock.</summary>
public sealed class OceanSurfState
{
    public const float MaximumBoost = 0.35f;
    public const float BuildSeconds = 1.5f;
    public const float ExitSeconds = 1.8f;

    public float Boost { get; private set; }
    public bool IsSurfing { get; private set; }
    public bool IsOnCrest { get; private set; }
    public float SpeedMultiplier => 1f + Boost;

    public void Tick(float seconds, bool onWave, bool onCrest)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
        IsSurfing = onWave;
        IsOnCrest = onWave && onCrest;
        float target = onWave ? MaximumBoost * (IsOnCrest ? 1f : 0.6f) : 0f;
        float step = MaximumBoost * seconds / (onWave ? BuildSeconds : ExitSeconds);
        Boost += Math.Max(-step, Math.Min(step, target - Boost));
    }

    public void Reset()
    {
        Boost = 0f;
        IsSurfing = false;
        IsOnCrest = false;
    }
}
