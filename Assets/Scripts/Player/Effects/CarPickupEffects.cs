// Connects pickups, the backpack and temporary effects to the player.
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
    // Activate the stored item before consuming it.
    public bool UseBackpack()
    {
        if (!simulationActive || runState.IsGameOver || runState.IsRoadRecovering || BackpackChangedFrame == Time.frameCount) return false;
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
            case RandomPickupOutcome.ProximityRecovery: return runState.TryRestorePursuitGap(HarderDifficultyActive ? 22.5f : RiskRunState.PickupProximityRecovery);
            case RandomPickupOutcome.SunnyDay: sunnyDayRemaining = HarderDifficultyActive ? 10f : 15f; return true;
            case RandomPickupOutcome.ReverseSteering: return TryApplyTemporaryEffect(PickupEffectType.ReverseSteering, EffectiveReverseDuration);
            default: return false;
        }
    }
    public bool HarderDifficultyActive { get; private set; }
    private float EffectiveReverseDuration => HarderDifficultyActive ? 10f : reverseControlDuration;
    public void ConfigureHarderDifficulty(bool active)
    {
        HarderDifficultyActive = active;
        randomOutcome.HarderDifficulty = active;
        runState.RoadFallProximityDrain = active ? 0.28f : RiskRunState.DefaultCrashProximityDrain;
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
    public float ReverseExtensionSeconds => EffectiveReverseDuration;
    [SerializeField, Min(0.1f)] private float reverseControlDuration = 8f;
    [SerializeField, Min(0f)] private float fuelConsumptionPerSecond = FuelState.DefaultConsumptionPerSecond;

    public RiskRunState RunState => runState;

    public bool HasActivePowerUp => ActiveEffect != PickupEffectType.None || reverseCountdownRemaining > 0f
        || runState.HasNitro || runState.HasRampBoost || runState.HasShield || sunnyDayRemaining > 0f;
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
    public bool EnableGunGuarantee { get; set; }
    public int EmptyGunBoxStreak { get; private set; }
    public void RestoreLootPacing(int streak) => EmptyGunBoxStreak = Mathf.Clamp(streak, 0, 3);

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

    // Direct positive pickups enter the backpack; reverse control activates separately.
    private bool TryApplyCore(PickupEffectType effect, float duration)
    {
        if (!isActiveAndEnabled) return false;

        if (runState.IsGameOver || runState.IsRoadRecovering) return false;
        bool emptyBefore = Backpack.Shots <= 0;
        RandomPickupOutcome outcome;
        switch (effect)
        {
            case PickupEffectType.RandomFuelOrReverse:
                outcome = EnableGunGuarantee && emptyBefore && EmptyGunBoxStreak >= (HarderDifficultyActive ? 3 : 2)
                    ? RandomPickupOutcome.Gun : randomOutcome.Next();
                break;
            case PickupEffectType.Gun: outcome = RandomPickupOutcome.Gun; break;
            case PickupEffectType.Shield: outcome = RandomPickupOutcome.Shield; break;
            case PickupEffectType.ProximityRecovery: outcome = RandomPickupOutcome.ProximityRecovery; break;
            case PickupEffectType.Boost: outcome = RandomPickupOutcome.Nitro; break;
            case PickupEffectType.ReverseSteering: outcome = RandomPickupOutcome.ReverseSteering; break;
            default: return TryApplyTemporaryEffect(effect, duration);
        }
        bool accepted = outcome == RandomPickupOutcome.ReverseSteering ? ActivateOutcome(outcome) : Backpack.Receive(outcome);
        if (accepted)
        {
            LastOutcome = outcome;
            PickupCount++;
            if (EnableGunGuarantee && effect == PickupEffectType.RandomFuelOrReverse)
                EmptyGunBoxStreak = outcome == RandomPickupOutcome.Gun || !emptyBefore
                    ? 0 : Mathf.Min(3, EmptyGunBoxStreak + 1);
        }
        return accepted;
    }

    public bool TryCrash(float proximityDrain = RiskRunState.DefaultCrashProximityDrain)
    {
        return isActiveAndEnabled && runState.TryCrash(proximityDrain * (HarderDifficultyActive ? 1.25f : 1f));
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
                RemainingSeconds + EffectiveReverseDuration);
            ReverseExtensionCount++;
            return true;
        }

        if (reverseCountdownRemaining > 0f)
        {

            pendingReverseDuration += EffectiveReverseDuration;
            ReverseExtensionCount++;
            return true;
        }

        runState.TryBailOut();
        pendingReverseDuration = EffectiveReverseDuration;

        reverseCountdownRemaining = 3f;
        return true;
    }

    // Advance effects and fuel on the same simulation clock.
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

            if (elapsed > 0f)
                runState.TickAdditionalFrameTime(elapsed, baseForwardSpeed, policeSpeed, countdownDistance);
        }
        else runState.Tick(elapsed, baseForwardSpeed, policeSpeed);
        if (!runState.IsGameOver && ActiveEffect == PickupEffectType.None
            && (reverseWasActive || ReverseActivationCount != activationsBeforeTick))
            ReverseCompletionCount++;
        fuelState.Tick(Time.deltaTime, HarderDifficultyActive ? 3.8f : fuelConsumptionPerSecond);
    }

    private void OnDisable()
    {
        Backpack.Clear();
        runState.Reset();
        fuelState.Reset();
        LastOutcome = RandomPickupOutcome.None;
        PickupCount = 0;
        EmptyGunBoxStreak = 0;
        reverseCountdownRemaining = 0f;
        pendingReverseDuration = 0f;
        sunnyDayRemaining = 0f;
        ReverseActivationCount = 0;
        ReverseCompletionCount = 0;
        ReverseExtensionCount = 0;
    }
}