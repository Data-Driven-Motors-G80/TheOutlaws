// Tracks the active temporary driving effect and its remaining time.

public sealed class PickupEffectState
{
    public const float DefaultDurationSeconds = 5f;

    public PickupEffectType ActiveEffect { get; private set; }
    public float RemainingSeconds { get; private set; }

    public float SteeringMultiplier => ActiveEffect == PickupEffectType.ReverseSteering ? -1f : 1f;
    public float ForwardSpeedMultiplier => ActiveEffect == PickupEffectType.Boost ? 1.2f : 1f;
    public float LateralAccelerationMultiplier => ActiveEffect == PickupEffectType.SlipperySteering ? 0.3f : 1f;

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

    // Expire an effect when its remaining duration reaches zero.
    public void Tick(float elapsedSeconds)
    {
        if (ActiveEffect == PickupEffectType.None)
        {
            return;
        }

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
