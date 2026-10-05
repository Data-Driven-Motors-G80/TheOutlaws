using NUnit.Framework;

public sealed class RiskRunStateTests
{
    [Test]
    public void Nitro_PersistsThroughTimeAndReverseEffectsUntilLost()
    {
        var run = new RiskRunState();
        Assert.That(run.TryActivateNitro(), Is.True);
        run.TryCollectWithoutReward(PickupEffectType.ReverseSteering, 8f);
        run.Tick(30f);
        Assert.That(run.HasNitro, Is.True);
        Assert.That(run.ForwardSpeedMultiplier, Is.EqualTo(1.35f));
        run.TryActivateNitro();
        Assert.That(run.ForwardSpeedMultiplier, Is.EqualTo(1.35f));
        run.LoseNitro();
        Assert.That(run.ForwardSpeedMultiplier, Is.EqualTo(1f));
        run.TryActivateNitro();
        run.Reset();
        Assert.That(run.HasNitro, Is.False);
    }

    [Test]
    public void NewRun_StartsWithRecoverableDistanceAndNoRewardsOrEffects()
    {
        var run = new RiskRunState();
        AssertReset(run);
    }

    [Test]
    public void FirstCrash_DrainsNineMetresWithoutSlowingOrEndingTheRun()
    {
        var run = new RiskRunState();
        Assert.That(run.TryCrash(), Is.True);
        Assert.That(run.PursuitGap, Is.EqualTo(36f));
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.CrashSpeedMultiplier, Is.EqualTo(1f));

        run.Tick(1.6f);
        Assert.That(run.PursuitGap, Is.EqualTo(36f).Within(0.0001f));
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.CrashSpeedMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void Crash_DrainsExactlyNineMetresAfterRecoveryBuffer()
    {
        var run = new RiskRunState();
        run.TryRestorePursuitGap(30f);
        Assert.That(run.PursuitGap, Is.EqualTo(75f));
        Assert.That(run.TryCrash(), Is.True);
        Assert.That(run.PursuitGap, Is.EqualTo(66f).Within(0.0001f));
    }

    [Test]
    public void RepeatedCollision_DuringProtectionDoesNotRestartAnyTimer()
    {
        var run = new RiskRunState();
        run.TryCrash();
        run.Tick(0.5f);
        Assert.That(run.TryCrash(), Is.False);
        Assert.That(run.CrashSlowRemaining, Is.Zero);
        Assert.That(run.CollisionProtectionRemaining, Is.EqualTo(0.5f));
        Assert.That(run.RecoveryWaitRemaining, Is.EqualTo(2.1f).Within(0.0001f));

        run.Tick(0.5f);
        Assert.That(run.TryCrash(), Is.True);
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.PursuitGap, Is.EqualTo(27f).Within(0.0001f));
        Assert.That(run.CrashSlowRemaining, Is.Zero);
        Assert.That(run.CollisionProtectionRemaining, Is.EqualTo(1f));
        Assert.That(run.RecoveryWaitRemaining, Is.EqualTo(2.6f).Within(0.0001f));
    }

    [Test]
    public void FuelDepletion_EndsRunEvenAfterProximityRecovery()
    {
        var run = new RiskRunState();
        Assert.That(run.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery), Is.True);
        Assert.That(run.PursuitGap, Is.EqualTo(RiskRunState.MaximumPursuitGap));

        Assert.That(run.EndRun(), Is.True);
        Assert.That(run.IsGameOver, Is.True);
        Assert.That(run.PursuitGap, Is.Zero);
        Assert.That(run.CrashSpeedMultiplier, Is.Zero);
        Assert.That(run.EndRun(), Is.False);
    }

    [Test]
    public void Shield_BlocksOneNewCollisionWithoutStartingRecovery()
    {
        var run = new RiskRunState();
        Assert.That(run.TryGrantShield(), Is.True);
        Assert.That(run.HasShield, Is.True);
        Assert.That(run.TryConsumeShield(), Is.True);
        Assert.That(run.HasShield, Is.False);
        Assert.That(run.ShieldUseCount, Is.EqualTo(1));
        Assert.That(run.CrashSlowRemaining, Is.Zero);
        Assert.That(run.RecoveryWaitRemaining, Is.Zero);
        Assert.That(run.TryConsumeShield(), Is.False);
    }

    [Test]
    public void Shield_IsPreservedDuringExistingCollisionProtection()
    {
        var run = new RiskRunState();
        Assert.That(run.TryCrash(), Is.True);
        Assert.That(run.TryGrantShield(), Is.True);
        Assert.That(run.TryConsumeShield(), Is.False);
        Assert.That(run.HasShield, Is.True);
        run.Tick(RiskRunState.CollisionProtectionDuration);
        Assert.That(run.TryConsumeShield(), Is.True);
        Assert.That(run.HasShield, Is.False);
    }

    [Test]
    public void ProximityRecovery_AddsHalfCapacityAndClampsAtMaximum()
    {
        var fullRun = new RiskRunState();
        Assert.That(fullRun.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery), Is.True);
        Assert.That(fullRun.PursuitGap, Is.EqualTo(RiskRunState.MaximumPursuitGap));

        var run = new RiskRunState();
        run.TryCrash();
        run.Tick(RiskRunState.CrashSlowDuration);
        Assert.That(run.PursuitGap, Is.EqualTo(36f).Within(0.0001f));
        Assert.That(run.TryRestorePursuitGap(RiskRunState.PickupProximityRecovery), Is.True);
        Assert.That(run.PursuitGap, Is.EqualTo(73.5f).Within(0.0001f));
    }

    [Test]
    public void RecoveryWait_IsMeasuredFromImpactAndOnlyBlocksPositiveDistance()
    {
        var run = new RiskRunState();
        run.TryCrash();
        run.Tick(1.6f);
        run.Tick(0.5f);
        Assert.That(run.PursuitGap, Is.EqualTo(36f).Within(0.0001f));
        run.Tick(1.5f);
        Assert.That(run.PursuitGap, Is.EqualTo(37f).Within(0.0001f));
    }

    [Test]
    public void SameCrashCount_DifferentRecoveryTimeProducesDifferentContinuousDistances()
    {
        var littleRecovery = new RiskRunState();
        var moreRecovery = new RiskRunState();
        littleRecovery.TryCrash();
        moreRecovery.TryCrash();
        littleRecovery.Tick(2.6f);
        moreRecovery.Tick(6.85f);
        littleRecovery.TryCrash();
        moreRecovery.TryCrash();
        littleRecovery.Tick(1.6f);
        moreRecovery.Tick(1.6f);

        Assert.That(littleRecovery.IsGameOver, Is.False);
        Assert.That(littleRecovery.PursuitGap, Is.EqualTo(27f).Within(0.0001f));
        Assert.That(moreRecovery.PursuitGap, Is.EqualTo(31.25f).Within(0.0001f));
        Assert.That(moreRecovery.IsGameOver, Is.False);
    }

    [Test]
    public void CrashAndEffectExpiry_ProduceSameDistanceForOneTickAndManySlices()
    {
        var oneTick = new RiskRunState();
        var sliced = new RiskRunState();
        oneTick.TryCollect(PickupEffectType.Boost, 2f);
        sliced.TryCollect(PickupEffectType.Boost, 2f);
        oneTick.TryCrash();
        sliced.TryCrash();

        oneTick.Tick(4f);
        for (int i = 0; i < 40; i++)
            sliced.Tick(0.1f);

        Assert.That(oneTick.PursuitGap, Is.EqualTo(37.4f).Within(0.001f));
        Assert.That(sliced.PursuitGap, Is.EqualTo(oneTick.PursuitGap).Within(0.001f));
        Assert.That(sliced.CrashSlowRemaining, Is.EqualTo(oneTick.CrashSlowRemaining));
        Assert.That(sliced.RecoveryWaitRemaining, Is.EqualTo(oneTick.RecoveryWaitRemaining));
        Assert.That(sliced.BankedPickupScore, Is.EqualTo(oneTick.BankedPickupScore));
    }

    [Test]
    public void Boost_ChangesActualPursuitSpeedAndStopsAtItsExactExpiry()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost, 2f);
        run.Tick(3f);

        // Two seconds at 20.25 - 14, then one second at 15 - 14.
        Assert.That(run.PursuitGap, Is.EqualTo(58.5f).Within(0.0001f));
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
    }

    [Test]
    public void TickAcrossBoostExpiry_ReturnsDistanceFromBothForwardSpeeds()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost, 0.1f);
        run.Tick(0.2f);
        Assert.That(run.LastTickForwardDistance,
            Is.EqualTo(20.25f * 0.1f + 15f * 0.1f).Within(0.0001f));
    }

    [Test]
    public void TickAcrossCrashRecovery_ReturnsDistanceFromBothForwardSpeeds()
    {
        var run = new RiskRunState();
        run.TryCrash();
        run.Tick(1.5f);
        run.Tick(0.2f);
        Assert.That(run.LastTickForwardDistance,
            Is.EqualTo(15f * 0.2f).Within(0.0001f));
    }

    [Test]
    public void Capture_ReturnsOnlyDistanceTravelledBeforeCaptureAndZeroOnFollowingTick()
    {
        var run = new RiskRunState();
        // Relative speed is -45, so capture occurs one second into this two-second frame.
        run.Tick(2f, 15f, 60f);
        Assert.That(run.IsGameOver, Is.True);
        Assert.That(run.LastTickForwardDistance, Is.EqualTo(15f).Within(0.0001f));
        run.Tick(1f);
        Assert.That(run.LastTickForwardDistance, Is.Zero);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidTick_ClearsPreviousForwardDistance(float elapsedSeconds)
    {
        var run = new RiskRunState();
        run.Tick(1f);
        Assert.That(run.LastTickForwardDistance, Is.EqualTo(15f));
        run.Tick(elapsedSeconds);
        Assert.That(run.LastTickForwardDistance, Is.Zero);
    }

    [Test]
    public void BoostDuringCrash_ReducesDistanceLossWithoutIgnoringRecoveryWait()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        run.TryCrash();
        run.Tick(2.6f);
        Assert.That(run.PursuitGap, Is.EqualTo(36f).Within(0.0001f));
        run.Tick(1f);
        Assert.That(run.PursuitGap, Is.EqualTo(42.25f).Within(0.0001f));
    }

    [Test]
    public void StableDriving_CannotBuildAnUnlimitedDistanceBuffer()
    {
        var run = new RiskRunState();
        run.Tick(1000f);
        Assert.That(run.PursuitGap, Is.EqualTo(75f));
    }

    [Test]
    public void NormalExpiry_BanksThePendingBonusExactlyOnce()
    {
        var run = new RiskRunState();
        Assert.That(run.TryCollect(PickupEffectType.ReverseSteering), Is.True);
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        Assert.That(run.PendingBonus, Is.EqualTo(75));
        run.Tick(4f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        run.Tick(1f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(85));
        Assert.That(run.PendingBonus, Is.Zero);
        run.Tick(100f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(85));
    }

    [Test]
    public void BailOut_ForfeitsPendingBonusButKeepsImmediateReward()
    {
        var run = new RiskRunState();
        Assert.That(run.TryBailOut(), Is.False);
        run.TryCollect(PickupEffectType.ReverseSteering);
        run.Tick(4f);
        Assert.That(run.TryBailOut(), Is.True);
        Assert.That(run.TryBailOut(), Is.False);
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
        Assert.That(run.PendingBonus, Is.Zero);
        run.Tick(10f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
    }

    [Test]
    public void Replacement_ForfeitsOldBonusAndCanOnlyCompleteTheNewBonus()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.ReverseSteering);
        run.Tick(4f);
        run.TryCollect(PickupEffectType.SlipperySteering);
        Assert.That(run.BankedPickupScore, Is.EqualTo(20));
        run.Tick(1f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(20));
        Assert.That(run.Effects.RemainingSeconds, Is.EqualTo(4f));
        run.Tick(4f);
        Assert.That(run.BankedPickupScore, Is.EqualTo(95));
    }

    [Test]
    public void Capture_ClearsRiskAndRejectsAllFurtherInputsUntilReset()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.ReverseSteering);
        run.Tick(1f, 0f, 45f);
        Assert.That(run.IsGameOver, Is.True);
        Assert.That(run.PursuitGap, Is.Zero);
        Assert.That(run.CrashSpeedMultiplier, Is.Zero);
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
        Assert.That(run.PendingBonus, Is.Zero);
        Assert.That(run.TryCollect(PickupEffectType.Boost), Is.False);
        Assert.That(run.TryCrash(), Is.False);
        Assert.That(run.TryBailOut(), Is.False);
        run.Tick(100f);
        Assert.That(run.PursuitGap, Is.Zero);
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
    }

    [TestCase(1f)]
    [TestCase(5f)]
    public void LargeTick_CaptureBeforeOrExactlyAtExpiryDoesNotPaySurvivalBonus(float duration)
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.SlipperySteering, duration);
        run.Tick(10f, 0f, 45f);
        Assert.That(run.IsGameOver, Is.True);
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        Assert.That(run.PendingBonus, Is.Zero);
    }

    [Test]
    public void ExpiryBeforeCapture_KeepsTheAlreadyEarnedBonus()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.SlipperySteering, 0.5f);
        run.Tick(10f, 0f, 45f);
        Assert.That(run.IsGameOver, Is.True);
        Assert.That(run.BankedPickupScore, Is.EqualTo(85));
    }

    [Test]
    public void Reset_AfterCaptureRestoresAllInitialStateAndAcceptsNewInput()
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        run.TryCrash();
        run.Tick(1f, 0f, 100f);
        run.Reset();
        AssertReset(run);
        Assert.That(run.TryCollect(PickupEffectType.ReverseSteering), Is.True);
        Assert.That(run.TryCrash(), Is.True);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidElapsedTime_DoesNotChangeActiveState(float elapsedSeconds)
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        run.TryCrash();
        run.Tick(elapsedSeconds);
        Assert.That(run.PursuitGap, Is.EqualTo(36f));
        Assert.That(run.CrashSlowRemaining, Is.Zero);
        Assert.That(run.CollisionProtectionRemaining, Is.EqualTo(1f));
        Assert.That(run.RecoveryWaitRemaining, Is.EqualTo(2.6f));
        Assert.That(run.Effects.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        Assert.That(run.PendingBonus, Is.EqualTo(75));
    }

    [TestCase(PickupEffectType.None)]
    [TestCase((PickupEffectType)99)]
    public void InvalidPickup_DoesNotRewardOrReplaceTheCurrentEffect(PickupEffectType effect)
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        Assert.That(run.TryCollect(effect), Is.False);
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.Boost));
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        Assert.That(run.PendingBonus, Is.EqualTo(75));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidPickupDuration_DoesNotRewardOrReplaceTheCurrentEffect(float duration)
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        Assert.That(run.TryCollect(PickupEffectType.ReverseSteering, duration), Is.False);
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.Boost));
        Assert.That(run.Effects.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
        Assert.That(run.PendingBonus, Is.EqualTo(75));
    }

    [TestCase(float.NaN, 14f)]
    [TestCase(15f, float.PositiveInfinity)]
    [TestCase(-1f, 14f)]
    [TestCase(15f, -1f)]
    public void InvalidSpeed_DoesNotConsumeTimeOrPoisonDistance(float carSpeed, float policeSpeed)
    {
        var run = new RiskRunState();
        run.TryCollect(PickupEffectType.Boost);
        run.Tick(1f, carSpeed, policeSpeed);
        Assert.That(run.PursuitGap, Is.EqualTo(45f));
        Assert.That(run.Effects.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(run.BankedPickupScore, Is.EqualTo(10));
    }

    private static void AssertReset(RiskRunState run)
    {
        Assert.That(run.PursuitGap, Is.EqualTo(45f));
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.CrashSlowRemaining, Is.Zero);
        Assert.That(run.CollisionProtectionRemaining, Is.Zero);
        Assert.That(run.RecoveryWaitRemaining, Is.Zero);
        Assert.That(run.HasShield, Is.False);
        Assert.That(run.ShieldUseCount, Is.Zero);
        Assert.That(run.CrashSpeedMultiplier, Is.EqualTo(1f));
        Assert.That(run.BankedPickupScore, Is.Zero);
        Assert.That(run.PendingBonus, Is.Zero);
        Assert.That(run.Effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
        Assert.That(run.Effects.RemainingSeconds, Is.Zero);
        Assert.That(run.LastTickForwardDistance, Is.Zero);
    }
}
