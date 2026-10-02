using NUnit.Framework;

public sealed class FuelAndRandomPickupTests
{
    [Test]
    public void FuelState_ConsumesAndClampsAtZero()
    {
        var fuel = new FuelState(40f);

        fuel.Tick(5f, 4f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(80f));

        fuel.Tick(30f, 4f);
        Assert.That(fuel.CurrentFuel, Is.Zero);
    }

    [Test]
    public void FuelState_RefillNeverExceedsMaximumAndFullRefillRestoresMaximum()
    {
        var fuel = new FuelState();
        fuel.Tick(10f, 4f);

        fuel.Refill(50f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(90f));

        fuel.RefillToFull();
        Assert.That(fuel.CurrentFuel, Is.EqualTo(FuelState.MaximumFuel));
    }

    [Test]
    public void RandomPickupOutcomeState_ProducesBothOutcomesWithASeed()
    {
        var outcomes = new RandomPickupOutcomeState(12345);
        int reverse = 0;
        int proximity = 0;
        int shield = 0;

        for (int i = 0; i < 1000; i++)
        {
            switch (outcomes.Next())
            {
                case RandomPickupOutcome.ReverseSteering:
                    reverse++;
                    break;
                case RandomPickupOutcome.ProximityRecovery:
                    proximity++;
                    break;
                case RandomPickupOutcome.Shield:
                    shield++;
                    break;
                default:
                    Assert.Fail("A random special pickup must always have a valid outcome.");
                    break;
            }
        }

        Assert.That(reverse, Is.GreaterThan(0));
        Assert.That(proximity, Is.GreaterThan(0));
        Assert.That(shield, Is.GreaterThan(0));
        Assert.That(reverse + proximity + shield, Is.EqualTo(1000));
    }

    [Test]
    public void RandomPickupOutcomeState_IsDeterministicWhenSeeded()
    {
        var first = new RandomPickupOutcomeState(7);
        var second = new RandomPickupOutcomeState(7);

        for (int i = 0; i < 50; i++)
            Assert.That(first.Next(), Is.EqualTo(second.Next()));
    }
}
