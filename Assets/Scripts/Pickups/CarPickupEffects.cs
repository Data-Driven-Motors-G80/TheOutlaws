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
    [SerializeField, Min(0f)] private float fuelConsumptionPerSecond = FuelState.DefaultConsumptionPerSecond;

    public RiskRunState RunState => runState;
    public FuelState FuelState => fuelState;
    public bool SimulationActive => simulationActive;
    public PickupEffectType ActiveEffect => runState.Effects.ActiveEffect;
    public float RemainingSeconds => runState.Effects.RemainingSeconds;
    public float SteeringMultiplier => runState.Effects.SteeringMultiplier;
    public float ForwardSpeedMultiplier => runState.Effects.ForwardSpeedMultiplier * runState.CrashSpeedMultiplier;
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
        if (!isActiveAndEnabled) return false;

        LastOutcome = RandomPickupOutcome.None;
        switch (effect)
        {
            case PickupEffectType.RandomFuelOrReverse:
                RandomPickupOutcome outcome = randomOutcome.Next();
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
                bool reversed = runState.TryCollectWithoutReward(PickupEffectType.ReverseSteering, duration);
                if (reversed)
                    PickupCount++;
                return reversed;


            case PickupEffectType.Shield:
                bool shieldedPickup = runState.TryGrantShield();
                if (shieldedPickup)
                {
                    LastOutcome = RandomPickupOutcome.Shield;
                    PickupCount++;
                }
                return shieldedPickup;

            default:
                bool applied = runState.TryCollectWithoutReward(effect, duration);
                if (applied)
                    PickupCount++;
                return applied;
        }
    }

    public bool TryCrash(float proximityDrain = RiskRunState.DefaultCrashProximityDrain)
    {
        return isActiveAndEnabled && runState.TryCrash(proximityDrain);
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

    /// <summary>
    /// Pushes the pursuing police back after a successful rear shot.
    /// This keeps Team 21's defensive shooting mechanic inside Team 19's
    /// existing chase-distance model.
    /// </summary>
    public bool RepelPolice(float distance)
    {
        return isActiveAndEnabled && runState.TryRestorePursuitGap(distance);
    }

    public void Clear()
    {
        runState.TryBailOut();
    }

    private void Update()
    {
        if (!simulationActive) return;
        runState.Tick(Time.deltaTime, baseForwardSpeed, policeSpeed);
        fuelState.Tick(Time.deltaTime, fuelConsumptionPerSecond);
    }

    private void OnDisable()
    {
        runState.Reset();
        fuelState.Reset();
        LastOutcome = RandomPickupOutcome.None;
        PickupCount = 0;
    }
}
