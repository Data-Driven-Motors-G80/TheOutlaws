using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class CarPickupEffects : MonoBehaviour
{
    private readonly RiskRunState runState = new RiskRunState();
    private readonly FuelState fuelState = new FuelState();
    private readonly RandomPickupOutcomeState randomOutcome = new RandomPickupOutcomeState();
    public BackpackState Backpack { get; } = new BackpackState();
    public int BackpackChangedFrame { get; private set; } = -1;
    public event System.Action<RandomPickupOutcome> ItemActivated;
    public event System.Action EmptyBackpackUsed;
    public string BackpackNotice { get; private set; }
    public int BackpackNoticeVersion { get; private set; }
    private void Awake()
    {
        Backpack.Replaced += (old, item, lost) => {
            BackpackChangedFrame = Time.frameCount;
            BackpackNotice = item + " STORED" + (old != RandomPickupOutcome.None ? " - replaced " + old : "")
                + (lost > 0 ? " (" + lost + " shots lost)" : "");
            BackpackNoticeVersion++;
            GameEvents.RaiseBackpackReplaced(old, item, lost);
        };
    }
    public bool UseBackpack()
    {
        if (!simulationActive || runState.IsGameOver || BackpackChangedFrame == Time.frameCount) return false;
        var item = Backpack.Item;
        if (item == RandomPickupOutcome.None) { EmptyBackpackUsed?.Invoke(); return false; }
        if (item != RandomPickupOutcome.Gun && !ActivateOutcome(item)) return false;
        Backpack.Use();
        ItemActivated?.Invoke(item);
        GameEvents.RaiseBackpackUsed(item);
        return true;
    }
    private bool ActivateOutcome(RandomPickupOutcome item)
    {
        switch (item)
        {
            case RandomPickupOutcome.Shield: return runState.TryGrantShield();
            case RandomPickupOutcome.Nitro: return runState.TryActivateNitro();
            case RandomPickupOutcome.ProximityRecovery: return runState.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery);
            case RandomPickupOutcome.SunnyDay: sunnyDayRemaining = 15f; return true;
            case RandomPickupOutcome.ReverseSteering: return TryApplyTemporaryEffect(PickupEffectType.ReverseSteering, reverseControlDuration);
            default: return false;
        }
    }
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

    public void ConfigureRewards(RewardTable table)
    {
        randomOutcome.Table = table;
        ConfigureSunnyDay(table != null ? table.SunnyDayChance : 0f,
            table != null ? table.SunnyDaySeconds : 0f);
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

        if (runState.IsGameOver) return false;
        RandomPickupOutcome outcome;
        switch (effect)
        {
            case PickupEffectType.RandomFuelOrReverse: outcome = randomOutcome.Next(); break;
            case PickupEffectType.Gun: outcome = RandomPickupOutcome.Gun; break;
            case PickupEffectType.Shield: outcome = RandomPickupOutcome.Shield; break;
            case PickupEffectType.ProximityRecovery: outcome = RandomPickupOutcome.ProximityRecovery; break;
            case PickupEffectType.Boost: outcome = RandomPickupOutcome.Nitro; break;
            case PickupEffectType.ReverseSteering: outcome = RandomPickupOutcome.ReverseSteering; break;
            default: return TryApplyTemporaryEffect(effect, duration);
        }
        bool accepted = outcome == RandomPickupOutcome.ReverseSteering ? ActivateOutcome(outcome) : Backpack.Receive(outcome);
        if (accepted) { LastOutcome = outcome; PickupCount++; }
        return accepted;
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

    public float PendingReverseDuration => pendingReverseDuration;
    public void RestoreTransientEffects(float sunny, float countdown, float pending)
    {
        sunnyDayRemaining = Mathf.Max(0f, sunny);
        reverseCountdownRemaining = Mathf.Max(0f, countdown);
        pendingReverseDuration = Mathf.Max(0f, pending);
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
        pendingReverseDuration = reverseControlDuration;
        // A full "3, 2, 1" warning before the controls swap.
        reverseCountdownRemaining = 3f;
        return true;
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
        Backpack.Clear();
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