using System;

/// <summary>Scene-independent pursuit, crash recovery, and voluntary pickup rewards.</summary>
public sealed class RiskRunState
{
    public const float InitialPursuitGap = 45f;
    public const float MaximumPursuitGap = 75f;
    public const float PickupProximityRecovery = MaximumPursuitGap * 0.5f;
    // Collisions drain the proximity bar but do not slow the car.
    public const float CrashSlowDuration = 0f;
    // 20% of the initial 45 m gap, so each collision costs 9 m.
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

    // This run is the sole clock owner; callers read Effects but advance it through Tick.
    public PickupEffectState Effects { get; } = new PickupEffectState();
    public float PursuitGap => (float)pursuitGap;
    public bool IsGameOver { get; private set; }
    public bool HasNitro { get; private set; }
    public float ForwardSpeedMultiplier => HasNitro ? 1.35f : Effects.ForwardSpeedMultiplier;

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
    public float CrashSpeedMultiplier => IsGameOver ? 0f : 1f;

    public RiskRunState()
    {
        Reset();
    }

    /// <summary>A valid pickup banks its small reward and replaces the unearned survival bonus.</summary>
    public bool TryCollect(PickupEffectType effect, float duration = PickupEffectState.DefaultDurationSeconds)
    {
        return TryCollectInternal(effect, duration, awardScore: true);
    }

    /// <summary>Gameplay pickup entry point while the team decides whether pickup scoring belongs in the final design.</summary>
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

    /// <summary>Cancel the current risk, forfeiting only its unearned bonus.</summary>
    public bool TryBailOut()
    {
        if (IsGameOver || Effects.ActiveEffect == PickupEffectType.None)
            return false;

        Effects.Clear();
        PendingBonus = 0;
        return true;
    }

    /// <summary>Stores one collision shield earned from a pickup.</summary>
    public bool TryGrantShield()
    {
        if (IsGameOver) return false;
        shieldAvailable = true;
        return true;
    }

    /// <summary>Adds one half of the maximum pursuit gap, capped at the maximum.</summary>
    public bool TryRestorePursuitGap(float amount)
    {
        if (IsGameOver || !IsFinite(amount) || amount <= 0f)
            return false;

        pursuitGap = Math.Min(MaximumPursuitGap, pursuitGap + amount);
        return true;
    }

    /// <summary>Consumes the shield only when a new collision would actually start recovery.</summary>
    public bool TryConsumeShield()
    {
        if (IsGameOver || !shieldAvailable || collisionProtectionRemaining > 0d)
            return false;

        shieldAvailable = false;
        shieldUseCount++;
        return true;
    }

    /// <summary>Impact drains part of the proximity bar without slowing the car.</summary>
    public bool TryCrash(float normalizedProximityDrain = DefaultCrashProximityDrain)
    {
        if (IsGameOver || collisionProtectionRemaining > 0d
            || !IsFinite(normalizedProximityDrain) || normalizedProximityDrain <= 0f)
            return false;

        double clampedDrain = Math.Min(1d, normalizedProximityDrain);
        // The normalized cost is based on the 45 m starting gap. Subtract it
        // from the current distance so every hit costs the same number of
        // metres, including after the player has earned recovery distance.
        pursuitGap = Math.Max(
            0d,
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

    /// <summary>Ends the run immediately for non-collision failures such as fuel depletion.</summary>
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

    /// <summary>
    /// Integrates relative forward speed across every effect and recovery boundary.
    /// During the post-impact wait, distance may shrink but cannot increase.
    /// Invalid clock/speed input leaves the whole state unchanged.
    /// </summary>
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
            bool hadEffect = Effects.ActiveEffect != PickupEffectType.None;
            if (hadEffect)
                step = Math.Min(step, Effects.RemainingSeconds);
            if (crashSlowRemaining > 0d)
                step = Math.Min(step, crashSlowRemaining);
            if (recoveryWaitRemaining > 0d)
                step = Math.Min(step, recoveryWaitRemaining);

            double forwardSpeed = (double)baseForwardSpeed * ForwardSpeedMultiplier
                * CrashSpeedMultiplier;
            double relativeSpeed = forwardSpeed - policeSpeed;
            if (recoveryWaitRemaining > 0d && relativeSpeed > 0d)
                relativeSpeed = 0d;

            // Resolve capture before ticking Effects: an expiry at the capture instant earns no bonus.
            if (relativeSpeed < 0d && pursuitGap / -relativeSpeed <= step)
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
            pursuitGap = Math.Min(MaximumPursuitGap, pursuitGap + relativeSpeed * step);
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
                // Direct lifecycle clearing must never turn an abandoned effect into a paid bonus.
                PendingBonus = 0;
            }
        }
    }

    // Used when a countdown boundary splits one rendered frame into two ticks.
    public void TickAdditionalFrameTime(float elapsedSeconds, float baseForwardSpeed,
        float policeSpeed, float previousForwardDistance)
    {
        Tick(elapsedSeconds, baseForwardSpeed, policeSpeed);
        LastTickForwardDistance += previousForwardDistance;
    }

    public void Reset()
    {
        pursuitGap = InitialPursuitGap;
        IsGameOver = false;
        HasNitro = false;
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
