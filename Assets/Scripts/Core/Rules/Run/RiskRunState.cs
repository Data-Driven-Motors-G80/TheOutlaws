// Owns pursuit, crash, boost, recovery and run-end rules.
using System;

public sealed class RiskRunState
{
    public const float RampBoostMultiplier = 1.5f;
    public const float RoadRecoverySeconds = 0.8f;
    public const float InitialPursuitGap = 45f;
    public const float TutorialInitialPursuitGap = 18f;
    public const float MaximumPursuitGap = 75f;
    public const float PickupProximityRecovery = MaximumPursuitGap * 0.5f;

    public const float CrashSlowDuration = 0f;

    public const float DefaultCrashProximityDrain = 0.2f;
    public const float CollisionProtectionDuration = 1f;
    public const float RecoveryWaitDuration = 2.6f;
    public const int PickupReward = 10;
    public const int CompletionBonus = 75;

    private double pursuitGap;
    private double crashSlowRemaining;
    private double collisionProtectionRemaining;
    private double recoveryWaitRemaining;
    private bool shieldAvailable;
    private int shieldUseCount;

    public PickupEffectState Effects { get; } = new PickupEffectState();
    public float PursuitGap => (float)pursuitGap;

    public float MinimumPursuitGap { get; set; }

    public float RecoveryCap { get; set; } = MaximumPursuitGap;
    public bool IsGameOver { get; private set; }
    public bool HasNitro { get; private set; }
    public float RoadRecoveryRemaining { get; private set; }
    public bool IsRoadRecovering => RoadRecoveryRemaining > 0f;
    public int RoadRecoveryCount { get; private set; }
    public float RoadFallProximityDrain { get; set; } = DefaultCrashProximityDrain;

    // Apply the fall penalty and start the temporary recovery state.
    public bool RecoverFromRoadFall()
    {
        if (IsGameOver || IsRoadRecovering) return false;
        RoadRecoveryCount++;
        pursuitGap = Math.Max(MinimumPursuitGap,
            pursuitGap - InitialPursuitGap * RoadFallProximityDrain);
        HasNitro = false;
        RampBoostRemaining = 0f;
        RoadRecoveryRemaining = RoadRecoverySeconds;
        collisionProtectionRemaining = RoadRecoverySeconds + CollisionProtectionDuration;
        recoveryWaitRemaining = RoadRecoverySeconds + RecoveryWaitDuration;
        if (pursuitGap <= 0d)
        {
            IsGameOver = true;
            Effects.Clear();
            PendingBonus = 0;
        }
        return true;
    }

    public float RampBoostRemaining { get; private set; }
    public bool HasRampBoost => RampBoostRemaining > 0f;
    public float RampBoostStrength { get; private set; } = RampBoostMultiplier;
    public float ForwardSpeedMultiplier => Math.Max(HasRampBoost ? RampBoostStrength : 1f,
        HasNitro ? 1.2f : Effects.ForwardSpeedMultiplier);

    // Keep ramp boosts separate from collected nitro.
    public bool TryActivateRampBoost(float seconds, float multiplier = RampBoostMultiplier)
    {
        if (IsGameOver || !IsFinite(seconds) || seconds <= 0f || !IsFinite(multiplier) || multiplier < 1f) return false;
        RampBoostStrength = HasRampBoost ? Math.Max(RampBoostStrength, multiplier) : multiplier;
        RampBoostRemaining = Math.Max(RampBoostRemaining, seconds);
        return true;
    }

    public bool TryActivateNitro()
    {
        if (IsGameOver) return false;
        HasNitro = true;
        return true;
    }

    public void LoseNitro() => HasNitro = false;
    public float CrashSlowRemaining => (float)crashSlowRemaining;
    public float CollisionProtectionRemaining => (float)collisionProtectionRemaining;
    public float RecoveryWaitRemaining => (float)recoveryWaitRemaining;
    public bool HasShield => shieldAvailable;
    public int ShieldUseCount => shieldUseCount;
    public int BankedPickupScore { get; private set; }
    public int PendingBonus { get; private set; }
    public float LastTickForwardDistance { get; private set; }
    public float CrashSpeedMultiplier => IsGameOver || IsRoadRecovering ? 0f : 1f;

    public RiskRunState()
    {
        Reset();
    }

    public bool TryCollect(PickupEffectType effect, float duration = PickupEffectState.DefaultDurationSeconds)
    {
        return TryCollectInternal(effect, duration, awardScore: true);
    }

    public bool TryCollectWithoutReward(PickupEffectType effect, float duration = PickupEffectState.DefaultDurationSeconds)
    {
        return TryCollectInternal(effect, duration, awardScore: false);
    }

    private bool TryCollectInternal(PickupEffectType effect, float duration, bool awardScore)
    {
        if (IsGameOver || !IsPickupEffect(effect) || !IsFinite(duration) || duration <= 0f)
            return false;

        Effects.Apply(effect, duration);
        if (awardScore)
        {
            BankedPickupScore += PickupReward;
            PendingBonus = CompletionBonus;
        }
        else
        {
            PendingBonus = 0;
        }
        return true;
    }

    public bool TryBailOut()
    {
        if (IsGameOver || Effects.ActiveEffect == PickupEffectType.None)
            return false;

        Effects.Clear();
        PendingBonus = 0;
        return true;
    }

    public bool TryGrantShield()
    {
        if (IsGameOver) return false;
        shieldAvailable = true;
        return true;
    }

    public bool TryRestorePursuitGap(float amount)
    {
        if (IsGameOver || !IsFinite(amount) || amount <= 0f)
            return false;

        pursuitGap = Math.Min(MaximumPursuitGap, pursuitGap + amount);
        return true;
    }

    public bool TryConsumeShield()
    {
        if (IsGameOver || !shieldAvailable || collisionProtectionRemaining > 0d)
            return false;

        shieldAvailable = false;
        shieldUseCount++;
        return true;
    }

    public bool TryCrash(float normalizedProximityDrain = DefaultCrashProximityDrain)
    {
        if (IsGameOver || collisionProtectionRemaining > 0d
            || !IsFinite(normalizedProximityDrain) || normalizedProximityDrain <= 0f)
            return false;

        double clampedDrain = Math.Min(1d, normalizedProximityDrain);

        pursuitGap = Math.Max(
            MinimumPursuitGap,
            pursuitGap - InitialPursuitGap * clampedDrain);
        if (pursuitGap <= 0d)
        {
            IsGameOver = true;
            Effects.Clear();
            PendingBonus = 0;
            return true;
        }

        crashSlowRemaining = 0d;
        collisionProtectionRemaining = CollisionProtectionDuration;
        recoveryWaitRemaining = RecoveryWaitDuration;
        return true;
    }

    public bool EndRun()
    {
        if (IsGameOver)
            return false;

        pursuitGap = 0d;
        IsGameOver = true;
        Effects.Clear();
        PendingBonus = 0;
        return true;
    }

    public void Tick(float elapsedSeconds, float baseForwardSpeed = 15f, float policeSpeed = 14f)
    {
        LastTickForwardDistance = 0f;
        if (IsGameOver || !IsFinite(elapsedSeconds) || elapsedSeconds <= 0f
            || !IsFinite(baseForwardSpeed) || baseForwardSpeed < 0f
            || !IsFinite(policeSpeed) || policeSpeed < 0f)
            return;

        double remaining = elapsedSeconds;
        while (remaining > 0d && !IsGameOver)
        {
            double step = remaining;
            if (HasRampBoost) step = Math.Min(step, RampBoostRemaining);
            if (IsRoadRecovering) step = Math.Min(step, RoadRecoveryRemaining);
            bool hadEffect = Effects.ActiveEffect != PickupEffectType.None;
            if (hadEffect)
                step = Math.Min(step, Effects.RemainingSeconds);
            if (crashSlowRemaining > 0d)
                step = Math.Min(step, crashSlowRemaining);
            if (recoveryWaitRemaining > 0d)
                step = Math.Min(step, recoveryWaitRemaining);

            double forwardSpeed = (double)baseForwardSpeed * ForwardSpeedMultiplier
                * CrashSpeedMultiplier;
            if (IsRoadRecovering) forwardSpeed = 0d;

            double relativeSpeed = IsRoadRecovering ? 0d : forwardSpeed - policeSpeed;
            if (recoveryWaitRemaining > 0d && relativeSpeed > 0d)
                relativeSpeed = 0d;

            if (MinimumPursuitGap <= 0f && relativeSpeed < 0d && pursuitGap / -relativeSpeed <= step)
            {
                double timeUntilCapture = pursuitGap / -relativeSpeed;
                LastTickForwardDistance += (float)(forwardSpeed * timeUntilCapture);
                AdvanceCrashTimers(timeUntilCapture);
                pursuitGap = 0d;
                IsGameOver = true;
                Effects.Clear();
                PendingBonus = 0;
                return;
            }

            LastTickForwardDistance += (float)(forwardSpeed * step);
            double next = pursuitGap + relativeSpeed * step;
            if (relativeSpeed > 0d)
            {

                double cap = Math.Min(RecoveryCap, MaximumPursuitGap);
                next = pursuitGap >= cap ? pursuitGap : Math.Min(cap, next);
            }
            pursuitGap = Math.Max(MinimumPursuitGap, Math.Min(MaximumPursuitGap, next));
            AdvanceCrashTimers(step);
            Effects.Tick((float)step);
            remaining -= step;

            if (hadEffect && Effects.ActiveEffect == PickupEffectType.None)
            {
                BankedPickupScore += PendingBonus;
                PendingBonus = 0;
            }
            else if (!hadEffect)
            {

                PendingBonus = 0;
            }
        }
    }

    public void TickAdditionalFrameTime(float elapsedSeconds, float baseForwardSpeed,
        float policeSpeed, float previousForwardDistance)
    {
        Tick(elapsedSeconds, baseForwardSpeed, policeSpeed);
        LastTickForwardDistance += previousForwardDistance;
    }

    // Copy state needed to continue the same run after a transition.
    public RiskRunSnapshot Capture()
    {
        return new RiskRunSnapshot(
            (float)pursuitGap, shieldAvailable, shieldUseCount, HasNitro,
            BankedPickupScore, PendingBonus,
            Effects.ActiveEffect, Effects.RemainingSeconds,
            (float)collisionProtectionRemaining, (float)recoveryWaitRemaining, RampBoostRemaining,
            RoadRecoveryRemaining, RoadRecoveryCount, RampBoostStrength);
    }

    public void Restore(RiskRunSnapshot snapshot)
    {
        Reset();
        pursuitGap = Math.Max(0d, Math.Min(MaximumPursuitGap, snapshot.PursuitGap));
        shieldAvailable = snapshot.HasShield;
        shieldUseCount = snapshot.ShieldUseCount;
        HasNitro = snapshot.HasNitro;
        RampBoostRemaining = Math.Max(0f, snapshot.RampBoostRemaining);
        RoadRecoveryRemaining = Math.Max(0f, snapshot.RoadRecoveryRemaining);
        RoadRecoveryCount = snapshot.RoadRecoveryCount;
        RampBoostStrength = Math.Max(1f, snapshot.RampBoostStrength);
        BankedPickupScore = snapshot.BankedPickupScore;
        PendingBonus = snapshot.PendingBonus;
        collisionProtectionRemaining = snapshot.CollisionProtectionRemaining;
        recoveryWaitRemaining = snapshot.RecoveryWaitRemaining;
        if (snapshot.ActiveEffect != PickupEffectType.None && snapshot.EffectRemaining > 0f)
            Effects.Apply(snapshot.ActiveEffect, snapshot.EffectRemaining);
    }

    public void BeginTutorialPursuit()
    {
        Reset();
        pursuitGap = TutorialInitialPursuitGap;
        MinimumPursuitGap = 6f;
        RecoveryCap = InitialPursuitGap;
    }

    public void ResetPursuit()
    {
        if (IsGameOver) return;
        pursuitGap = InitialPursuitGap;
        MinimumPursuitGap = 0f;
        crashSlowRemaining = 0d;
        collisionProtectionRemaining = 0d;
        recoveryWaitRemaining = 0d;
    }

    public void Reset()
    {
        pursuitGap = InitialPursuitGap;
        MinimumPursuitGap = 0f;
        RecoveryCap = MaximumPursuitGap;
        RoadRecoveryRemaining = 0f;
        RoadRecoveryCount = 0;
        RampBoostStrength = RampBoostMultiplier;
        IsGameOver = false;
        HasNitro = false;
        RampBoostRemaining = 0f;
        crashSlowRemaining = 0d;
        collisionProtectionRemaining = 0d;
        recoveryWaitRemaining = 0d;
        shieldAvailable = false;
        shieldUseCount = 0;
        BankedPickupScore = 0;
        PendingBonus = 0;
        LastTickForwardDistance = 0f;
        Effects.Clear();
    }

    private void AdvanceCrashTimers(double elapsedSeconds)
    {
        crashSlowRemaining = Math.Max(0d, crashSlowRemaining - elapsedSeconds);
        collisionProtectionRemaining = Math.Max(0d, collisionProtectionRemaining - elapsedSeconds);
        recoveryWaitRemaining = Math.Max(0d, recoveryWaitRemaining - elapsedSeconds);
        RampBoostRemaining = Math.Max(0f, RampBoostRemaining - (float)elapsedSeconds);
        RoadRecoveryRemaining = Math.Max(0f, RoadRecoveryRemaining - (float)elapsedSeconds);
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

public readonly struct RiskRunSnapshot
{
    public readonly float PursuitGap;
    public readonly bool HasShield;
    public readonly int ShieldUseCount;
    public readonly bool HasNitro;
    public readonly float RampBoostRemaining;
    public readonly float RoadRecoveryRemaining;
    public readonly int RoadRecoveryCount;
    public readonly float RampBoostStrength;
    public readonly int BankedPickupScore;
    public readonly int PendingBonus;
    public readonly PickupEffectType ActiveEffect;
    public readonly float EffectRemaining;
    public readonly float CollisionProtectionRemaining;
    public readonly float RecoveryWaitRemaining;

    public RiskRunSnapshot(float pursuitGap, bool hasShield, int shieldUseCount, bool hasNitro,
        int bankedPickupScore, int pendingBonus, PickupEffectType activeEffect, float effectRemaining,
        float collisionProtectionRemaining, float recoveryWaitRemaining, float rampBoostRemaining = 0f,
        float roadRecoveryRemaining = 0f, int roadRecoveryCount = 0, float rampBoostStrength = RiskRunState.RampBoostMultiplier)
    {
        PursuitGap = pursuitGap;
        HasShield = hasShield;
        ShieldUseCount = shieldUseCount;
        HasNitro = hasNitro;
        RampBoostRemaining = rampBoostRemaining;
        RoadRecoveryRemaining = roadRecoveryRemaining;
        RoadRecoveryCount = roadRecoveryCount;
        RampBoostStrength = rampBoostStrength;
        BankedPickupScore = bankedPickupScore;
        PendingBonus = pendingBonus;
        ActiveEffect = activeEffect;
        EffectRemaining = effectRemaining;
        CollisionProtectionRemaining = collisionProtectionRemaining;
        RecoveryWaitRemaining = recoveryWaitRemaining;
    }
}
