/// <summary>Tracks one temporary pickup effect without depending on a scene or vehicle.</summary>
public sealed class PickupEffectState
{
    public const float DefaultDurationSeconds = 5f;

    public PickupEffectType ActiveEffect { get; private set; }
    public float RemainingSeconds { get; private set; }

    public float SteeringMultiplier => ActiveEffect == PickupEffectType.ReverseSteering ? -1f : 1f;
    public float ForwardSpeedMultiplier => ActiveEffect == PickupEffectType.Boost ? 1.35f : 1f;
    public float LateralAccelerationMultiplier => ActiveEffect == PickupEffectType.SlipperySteering ? 0.3f : 1f;

    /// <summary>A new pickup replaces the current effect, including its remaining time.</summary>
    public void Apply(PickupEffectType effect, float duration = DefaultDurationSeconds)
    {
        if (!IsPickupEffect(effect) || !IsFinite(duration) || duration <= 0f)
        {
            Clear();
            return;
        }

        ActiveEffect = effect;
        RemainingSeconds = duration;
    }

    /// <summary>Advance with scaled game time; zero elapsed time leaves the effect paused.</summary>
    public void Tick(float elapsedSeconds)
    {
        if (ActiveEffect == PickupEffectType.None)
        {
            return;
        }

        // Invalid clock data must not poison the countdown with NaN or infinity.
        if (!IsFinite(elapsedSeconds))
        {
            Clear();
            return;
        }

        if (elapsedSeconds <= 0f)
        {
            return;
        }

        if (elapsedSeconds >= RemainingSeconds)
        {
            Clear();
            return;
        }

        RemainingSeconds -= elapsedSeconds;
    }

    public void Clear()
    {
        ActiveEffect = PickupEffectType.None;
        RemainingSeconds = 0f;
    }

    private static bool IsPickupEffect(PickupEffectType effect)
    {
        return effect == PickupEffectType.ReverseSteering
            || effect == PickupEffectType.Boost
            || effect == PickupEffectType.SlipperySteering;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
