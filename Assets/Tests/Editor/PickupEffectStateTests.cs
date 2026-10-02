using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PickupEffectStateTests
{
    [Test]
    public void NewState_HasNormalDrivingMultipliers()
    {
        AssertNormal(new PickupEffectState());
    }

    [TestCase(PickupEffectType.ReverseSteering, -1f, 1f, 1f)]
    [TestCase(PickupEffectType.Boost, 1f, 1.35f, 1f)]
    [TestCase(PickupEffectType.SlipperySteering, 1f, 1f, 0.3f)]
    public void Pickup_ChangesOnlyItsIntendedDrivingProperty(
        PickupEffectType effect, float steering, float speed, float acceleration)
    {
        var state = new PickupEffectState();
        state.Apply(effect);

        Assert.That(state.ActiveEffect, Is.EqualTo(effect));
        Assert.That(state.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(state.SteeringMultiplier, Is.EqualTo(steering));
        Assert.That(state.ForwardSpeedMultiplier, Is.EqualTo(speed));
        Assert.That(state.LateralAccelerationMultiplier, Is.EqualTo(acceleration));
    }

    [Test]
    public void Effect_RemainsActiveUntilItsExactExpiryBoundary()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.ReverseSteering);
        state.Tick(4.5f);

        Assert.That(state.ActiveEffect, Is.EqualTo(PickupEffectType.ReverseSteering));
        Assert.That(state.RemainingSeconds, Is.EqualTo(0.5f));
        Assert.That(state.SteeringMultiplier, Is.EqualTo(-1f));

        state.Tick(0.5f);
        AssertNormal(state);
    }

    [Test]
    public void FrameLongerThanRemainingDuration_ExpiresWithoutNegativeTime()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.Boost, 0.1f);
        state.Tick(2f);
        state.Tick(2f);

        AssertNormal(state);
    }

    [Test]
    public void SamePickup_RefreshesDurationInsteadOfStackingOrToggling()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.ReverseSteering);
        state.Tick(4f);
        state.Apply(PickupEffectType.ReverseSteering);
        state.Tick(1f);

        Assert.That(state.RemainingSeconds, Is.EqualTo(4f));
        Assert.That(state.SteeringMultiplier, Is.EqualTo(-1f));
        state.Tick(4f);
        AssertNormal(state);
    }

    [Test]
    public void DifferentPickup_ReplacesEveryModifierAndStartsANewTimer()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.ReverseSteering);
        state.Tick(3f);
        state.Apply(PickupEffectType.Boost, 4f);

        Assert.That(state.RemainingSeconds, Is.EqualTo(4f));
        Assert.That(state.SteeringMultiplier, Is.EqualTo(1f));
        Assert.That(state.ForwardSpeedMultiplier, Is.EqualTo(1.35f));
        Assert.That(state.LateralAccelerationMultiplier, Is.EqualTo(1f));

        state.Apply(PickupEffectType.SlipperySteering);
        Assert.That(state.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(state.SteeringMultiplier, Is.EqualTo(1f));
        Assert.That(state.ForwardSpeedMultiplier, Is.EqualTo(1f));
        Assert.That(state.LateralAccelerationMultiplier, Is.EqualTo(0.3f));
    }

    [Test]
    public void PausedGameTime_DoesNotConsumeEffectDuration()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.Boost);
        state.Tick(2f);
        for (int frame = 0; frame < 120; frame++)
        {
            state.Tick(0f);
        }

        Assert.That(state.ActiveEffect, Is.EqualTo(PickupEffectType.Boost));
        Assert.That(state.RemainingSeconds, Is.EqualTo(3f));
        state.Tick(3f);
        AssertNormal(state);
    }

    [Test]
    public void Clear_IsSafeToRepeatAndAllowsANewPickup()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.SlipperySteering);
        state.Clear();
        state.Clear();
        AssertNormal(state);

        state.Apply(PickupEffectType.Boost);
        Assert.That(state.RemainingSeconds, Is.EqualTo(5f));
        Assert.That(state.ForwardSpeedMultiplier, Is.EqualTo(1.35f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void InvalidDuration_ClearsThePreviousEffect(float duration)
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.Boost);
        state.Apply(PickupEffectType.ReverseSteering, duration);
        AssertNormal(state);
    }

    [TestCase(PickupEffectType.None)]
    [TestCase((PickupEffectType)(-1))]
    [TestCase((PickupEffectType)99)]
    public void InvalidOrNoneEffect_ClearsThePreviousEffect(PickupEffectType effect)
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.SlipperySteering);
        state.Apply(effect);
        AssertNormal(state);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFiniteClockValue_ClearsSafely(float elapsedSeconds)
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.Boost);
        state.Tick(elapsedSeconds);
        AssertNormal(state);
    }

    [Test]
    public void NegativeElapsedTime_DoesNotExtendTheEffect()
    {
        var state = new PickupEffectState();
        state.Apply(PickupEffectType.ReverseSteering);
        state.Tick(-2f);
        Assert.That(state.RemainingSeconds, Is.EqualTo(5f));
        state.Tick(5f);
        AssertNormal(state);
    }

    [UnityTest]
    public IEnumerator DisablingTheComponent_ClearsEffectsAndDoesNotRestoreThemOnEnable()
    {
        // This component's lifecycle callbacks run in Play Mode, unlike the pure state tests.
        yield return new EnterPlayMode();
        var car = new GameObject("Pickup effect test");
        try
        {
            var effects = car.AddComponent<CarPickupEffects>();
            effects.Apply(PickupEffectType.ReverseSteering);
            Assert.That(effects.SteeringMultiplier, Is.EqualTo(-1f));

            effects.enabled = false;
            Assert.That(effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
            Assert.That(effects.RemainingSeconds, Is.Zero);
            Assert.That(effects.SteeringMultiplier, Is.EqualTo(1f));

            effects.Apply(PickupEffectType.Boost);
            effects.enabled = true;
            Assert.That(effects.ActiveEffect, Is.EqualTo(PickupEffectType.None));
            Assert.That(effects.ForwardSpeedMultiplier, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(car);
        }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditModeAfterLifecycleTest()
    {
        // TearDown also runs after a failed assertion, so later tests remain in Edit Mode.
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    private static void AssertNormal(PickupEffectState state)
    {
        Assert.That(state.ActiveEffect, Is.EqualTo(PickupEffectType.None));
        Assert.That(state.RemainingSeconds, Is.Zero);
        Assert.That(state.SteeringMultiplier, Is.EqualTo(1f));
        Assert.That(state.ForwardSpeedMultiplier, Is.EqualTo(1f));
        Assert.That(state.LateralAccelerationMultiplier, Is.EqualTo(1f));
    }
}
