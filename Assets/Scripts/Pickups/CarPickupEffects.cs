using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class CarPickupEffects : MonoBehaviour
{
    private readonly RiskRunState runState = new RiskRunState();
    private readonly FuelState fuelState = new FuelState();
    private readonly RandomPickupOutcomeState randomOutcome = new RandomPickupOutcomeState();
    private float baseForwardSpeed = 15f;
    private float policeSpeed = 14f;
    private bool simulationActive = true;
    private float reverseCountdownRemaining;
    private float pendingReverseDuration;
    private float sunnyDayRemaining;
    private float sunnyDaySeconds;
    public float ReverseCountdownRemaining => reverseCountdownRemaining;
    public int ReverseActivationCount { get; private set; }
    public int ReverseCompletionCount { get; private set; }
    public int ReverseExtensionCount { get; private set; }
    public float ReverseExtensionSeconds => reverseControlDuration;
    [SerializeField, Min(0.1f)] private float reverseControlDuration = 8f;
    [SerializeField, Min(0f)] private float fuelConsumptionPerSecond = FuelState.DefaultConsumptionPerSecond;

    public RiskRunState RunState => runState;
    /// <summary>Any power-up in play (including a pending control swap): no new ? boxes until it ends.</summary>
    public bool HasActivePowerUp => ActiveEffect != PickupEffectType.None || reverseCountdownRemaining > 0f
        || runState.HasNitro || runState.HasShield || sunnyDayRemaining > 0f;
    public bool SunnyDayActive => sunnyDayRemaining > 0f;
    public float SunnyDayRemaining => sunnyDayRemaining;
    public FuelState FuelState => fuelState;
    public bool SimulationActive => simulationActive;
    public PickupEffectType ActiveEffect => runState.Effects.ActiveEffect;
    public float RemainingSeconds => runState.Effects.RemainingSeconds;
    public float SteeringMultiplier => runState.Effects.SteeringMultiplier;
    public float ForwardSpeedMultiplier => runState.ForwardSpeedMultiplier * runState.CrashSpeedMultiplier;
    public float LateralAccelerationMultiplier => runState.Effects.LateralAccelerationMultiplier;
    public float CurrentFuel => fuelState.CurrentFuel;
    public bool ShieldReady => runState.HasShield;
    public int ShieldUseCount => runState.ShieldUseCount;
    public RandomPickupOutcome LastOutcome { get; private set; }
    public int PickupCount { get; private set; }

    // The pursuit model needs the unmodified speeds; it applies the active effects itself.
    public void ConfigurePursuit(float vehicleBaseSpeed, float pursuerSpeed)
    {
        baseForwardSpeed = vehicleBaseSpeed;
        policeSpeed = pursuerSpeed;
    }

    public void ConfigureSunnyDay(float chance, float seconds)
    {
        randomOutcome.SunnyDayChance = Mathf.Clamp01(chance);
        sunnyDaySeconds = Mathf.Max(0f, seconds);
        if (randomOutcome.SunnyDayChance <= 0f) sunnyDayRemaining = 0f;
    }

    public void SetSimulationActive(bool active)
    {
        simulationActive = active;
    }

    public void Apply(PickupEffectType effect, float duration = PickupEffectState.DefaultDurationSeconds)
    {
        TryApply(effect, duration);
    }

    public bool TryApply(PickupEffectType effect, float duration = PickupEffectState.DefaultDurationSeconds)
    {
        bool accepted = TryApplyCore(effect, duration);
        if (accepted)
        {
            string outcome = LastOutcome != RandomPickupOutcome.None ? LastOutcome.ToString()
                : effect == PickupEffectType.ReverseSteering ? RandomPickupOutcome.ReverseSteering.ToString()
                : effect.ToString();
            GameEvents.RaisePickupCollected(PickupKind.Mystery, outcome);
        }
        return accepted;
    }

    private bool TryApplyCore(PickupEffectType effect, float duration)
    {
        if (!isActiveAndEnabled) return false;

        LastOutcome = RandomPickupOutcome.None;
        switch (effect)
        {
            case PickupEffectType.RandomFuelOrReverse:
                RandomPickupOutcome outcome = randomOutcome.Next();
                if (outcome == RandomPickupOutcome.SunnyDay)
                {
                    sunnyDayRemaining = sunnyDaySeconds;
                    LastOutcome = outcome;
                    PickupCount++;
                    return true;
                }
                if (outcome == RandomPickupOutcome.ProximityRecovery)
                {
                    bool restored = runState.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery);
                    if (restored)
                    {
                        LastOutcome = outcome;
                        PickupCount++;
                    }
                    return restored;
                }

                if (outcome == RandomPickupOutcome.Shield)
                {
                    bool shielded = runState.TryGrantShield();
                    if (shielded)
                    {
                        LastOutcome = outcome;
                        PickupCount++;
                    }
                    return shielded;
                }

                LastOutcome = outcome;
                PickupEffectType temporaryEffect = outcome == RandomPickupOutcome.Nitro
                    ? PickupEffectType.Boost : PickupEffectType.ReverseSteering;
                bool collected = TryApplyTemporaryEffect(temporaryEffect, duration);
                if (collected)
                    PickupCount++;
                return collected;


            case PickupEffectType.ProximityRecovery:
                bool recovered = runState.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery);
                if (recovered)
                {
                    LastOutcome = RandomPickupOutcome.ProximityRecovery;
                    PickupCount++;
                }
                return recovered;

            case PickupEffectType.Shield:
                bool shieldedPickup = runState.TryGrantShield();
                if (shieldedPickup)
                {
                    LastOutcome = RandomPickupOutcome.Shield;
                    PickupCount++;
                }
                return shieldedPickup;

            default:
                bool applied = TryApplyTemporaryEffect(effect, duration);
                if (applied)
                    PickupCount++;
                return applied;
        }
    }

    public bool TryCrash(float proximityDrain = RiskRunState.DefaultCrashProximityDrain)
    {
        return isActiveAndEnabled && runState.TryCrash(proximityDrain);
    }

    public void HitObstacle()
    {
        if (isActiveAndEnabled) runState.LoseNitro();
    }

    public bool EndRun()
    {
        return isActiveAndEnabled && runState.EndRun();
    }

    public bool TryConsumeShield()
    {
        return isActiveAndEnabled && runState.TryConsumeShield();
    }

    public void RefillFuel(float amount)
    {
        fuelState.Refill(amount);
    }

    public void ConsumeFuel(float amount)
    {
        fuelState.Consume(amount);
    }

    /// <summary>
    /// Pushes the pursuing police back after a successful rear shot.
    /// This keeps Team 21's defensive shooting mechanic inside Team 19's
    /// existing chase-distance model.
    /// </summary>
    public bool RepelPolice(float distance)
    {
        return isActiveAndEnabled && runState.TryRestorePursuitGap(distance);
    }

    public RiskRunSnapshot CaptureRun() => runState.Capture();

    public void RestoreRun(RiskRunSnapshot snapshot, float fuel)
    {
        runState.Restore(snapshot);
        fuelState.Reset(fuel);
    }

    public void Clear()
    {
        reverseCountdownRemaining = 0f;
        runState.TryBailOut();
    }

    private bool TryApplyTemporaryEffect(PickupEffectType effect, float duration)
    {
        if (effect == PickupEffectType.Boost)
        {
            bool activated = runState.TryActivateNitro();
            if (activated) LastOutcome = RandomPickupOutcome.Nitro;
            return activated;
        }
        if (effect != PickupEffectType.ReverseSteering)
        {
            bool applied = runState.TryCollectWithoutReward(effect, duration);
            if (applied) reverseCountdownRemaining = 0f;
            return applied;
        }

        if (runState.IsGameOver || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            return false;

        if (ActiveEffect == PickupEffectType.ReverseSteering)
        {
            runState.Effects.Apply(PickupEffectType.ReverseSteering,
                RemainingSeconds + reverseControlDuration);
            ReverseExtensionCount++;
            return true;
        }

        if (reverseCountdownRemaining > 0f)
        {
            // Keep the original countdown; queue the extra time for activation.
            pendingReverseDuration += reverseControlDuration;
            ReverseExtensionCount++;
            return true;
        }

        runState.TryBailOut();
        // The controls swap immediately (the old "3, 2, 1" warning is gone).
        pendingReverseDuration = 0f;
        bool swapped = runState.TryCollectWithoutReward(PickupEffectType.ReverseSteering, reverseControlDuration);
        if (swapped) ReverseActivationCount++;
        return swapped;
    }

    private void Update()
    {
        if (!simulationActive || Time.timeScale <= 0f) return;
        if (sunnyDayRemaining > 0f) sunnyDayRemaining = Mathf.Max(0f, sunnyDayRemaining - Time.deltaTime);
        bool reverseWasActive = ActiveEffect == PickupEffectType.ReverseSteering;
        int activationsBeforeTick = ReverseActivationCount;
        float elapsed = Time.deltaTime;
        if (reverseCountdownRemaining > 0f)
        {
            float countdownStep = Mathf.Min(elapsed, reverseCountdownRemaining);
            runState.Tick(countdownStep, baseForwardSpeed, policeSpeed);
            reverseCountdownRemaining = Mathf.Max(0f, reverseCountdownRemaining - countdownStep);
            elapsed -= countdownStep;
            float countdownDistance = runState.LastTickForwardDistance;
            if (runState.IsGameOver) reverseCountdownRemaining = 0f;
            else if (reverseCountdownRemaining <= 0f)
            {
                runState.TryCollectWithoutReward(PickupEffectType.ReverseSteering, pendingReverseDuration);
                ReverseActivationCount++;
            }
            // Preserve total movement across both parts of this frame.
            if (elapsed > 0f)
                runState.TickAdditionalFrameTime(elapsed, baseForwardSpeed, policeSpeed, countdownDistance);
        }
        else runState.Tick(elapsed, baseForwardSpeed, policeSpeed);
        if (!runState.IsGameOver && ActiveEffect == PickupEffectType.None
            && (reverseWasActive || ReverseActivationCount != activationsBeforeTick))
            ReverseCompletionCount++;
        fuelState.Tick(Time.deltaTime, fuelConsumptionPerSecond);
    }

    private void OnDisable()
    {
        runState.Reset();
        fuelState.Reset();
        LastOutcome = RandomPickupOutcome.None;
        PickupCount = 0;
        reverseCountdownRemaining = 0f;
        pendingReverseDuration = 0f;
        sunnyDayRemaining = 0f;
        ReverseActivationCount = 0;
        ReverseCompletionCount = 0;
        ReverseExtensionCount = 0;
    }
}