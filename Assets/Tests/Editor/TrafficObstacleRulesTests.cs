using NUnit.Framework;

public sealed class TrafficObstacleRulesTests
{
    [Test]
    public void ShotsProgressFromRedToYellowToGreenToDestroyed()
    {
        Assert.That(TrafficObstacleRules.ApplyShot(
                TrafficObstacleState.Red, out bool redDestroyed),
            Is.EqualTo(TrafficObstacleState.Yellow));
        Assert.That(redDestroyed, Is.False);
        Assert.That(TrafficObstacleRules.ApplyShot(
                TrafficObstacleState.Yellow, out bool yellowDestroyed),
            Is.EqualTo(TrafficObstacleState.Green));
        Assert.That(yellowDestroyed, Is.False);
        Assert.That(TrafficObstacleRules.ApplyShot(
                TrafficObstacleState.Green, out bool greenDestroyed),
            Is.EqualTo(TrafficObstacleState.Green));
        Assert.That(greenDestroyed, Is.True);
    }

    [Test]
    public void CollisionPenaltiesIncreaseWithDanger()
    {
        Assert.That(TrafficObstacleRules.GreenProximityDrain,
            Is.LessThan(TrafficObstacleRules.YellowProximityDrain));
        Assert.That(TrafficObstacleRules.YellowProximityDrain,
            Is.LessThan(TrafficObstacleRules.RedProximityDrain));
        Assert.That(TrafficObstacleRules.GetFuelPenaltyFraction(TrafficObstacleState.Red),
            Is.EqualTo(0.2f));
        Assert.That(TrafficObstacleRules.GetFuelPenaltyFraction(TrafficObstacleState.Yellow),
            Is.EqualTo(0.1f));
        Assert.That(TrafficObstacleRules.GetFuelPenaltyFraction(TrafficObstacleState.Green),
            Is.Zero);
    }

    [Test]
    public void ColourMixStaysTheSameAllRun()
    {
        foreach (float distance in new[] { 0f, 800f, 5000f })
        {
            Assert.That(TrafficObstacleRules.ChooseInitialState(distance, 0.1d), Is.EqualTo(TrafficObstacleState.Green));
            Assert.That(TrafficObstacleRules.ChooseInitialState(distance, 0.7d), Is.EqualTo(TrafficObstacleState.Yellow));
            Assert.That(TrafficObstacleRules.ChooseInitialState(distance, 0.9d), Is.EqualTo(TrafficObstacleState.Yellow));
            Assert.That(TrafficRoadblockRules.ChoosePattern(distance, 0.9d), Is.EqualTo(TrafficRoadblockPattern.SplitChoice));
        }
    }

    [Test]
    public void RegularBlocksNeverRequireThreeShots()
    {
        foreach (float distance in new[] { 0f, 800f, 5000f })
        {
            for (int i = 0; i <= 1000; i++)
                Assert.That(TrafficObstacleRules.ChooseInitialState(distance, i / 1000d),
                    Is.Not.EqualTo(TrafficObstacleState.Red));
            foreach (double roll in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, -1d, 2d })
                Assert.That(TrafficObstacleRules.ChooseInitialState(distance, roll),
                    Is.Not.EqualTo(TrafficObstacleState.Red));
        }
        Assert.That(TrafficObstacleRules.ChooseInitialState(0f, 0.55d), Is.EqualTo(TrafficObstacleState.Yellow));
    }

    [Test]
    public void AmmoSpacingPreservesOpeningAndBecomesScarcer()
    {
        Assert.That(AmmoSpawnSchedule.GetMinimumSpacing(0f), Is.EqualTo(90));
        Assert.That(AmmoSpawnSchedule.GetMaximumSpacing(299f), Is.EqualTo(140));
        Assert.That(AmmoSpawnSchedule.GetMinimumSpacing(300f), Is.EqualTo(120));
        Assert.That(AmmoSpawnSchedule.GetMaximumSpacing(699f), Is.EqualTo(180));
        Assert.That(AmmoSpawnSchedule.GetMinimumSpacing(700f), Is.EqualTo(150));
        Assert.That(AmmoSpawnSchedule.GetMaximumSpacing(10000f), Is.EqualTo(220));
    }

    [Test]
    public void RoadblockRulesNeverProduceAnAllRedPattern()
    {
        for (int i = 0; i < 100; i++)
        {
            double roll = i / 100d;
            Assert.That(System.Enum.IsDefined(typeof(TrafficRoadblockPattern),
                TrafficRoadblockRules.ChoosePattern(400f, roll)), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(TrafficRoadblockPattern),
                TrafficRoadblockRules.ChoosePattern(800f, roll)), Is.True);
        }
        Assert.That(System.Enum.IsDefined(typeof(TrafficRoadblockPattern), "FullRed"), Is.False);
    }

    [Test]
    public void MidGameFavorsTwoLaneGreenAndYellowRoadblocks()
    {
        Assert.That(TrafficRoadblockRules.GetNextSpacing(400f, 0d), Is.EqualTo(140f));
        Assert.That(TrafficRoadblockRules.GetNextSpacing(400f, 0.999d), Is.LessThan(196f));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.30d),
            Is.EqualTo(TrafficRoadblockPattern.TwoLaneGreen));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.60d),
            Is.EqualTo(TrafficRoadblockPattern.TwoLaneYellow));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.90d),
            Is.EqualTo(TrafficRoadblockPattern.SplitChoice));
    }

    [Test]
    public void UltraLateGameUsesShorterIntervalsWithoutFullWidthWalls()
    {
        Assert.That(TrafficRoadblockRules.GetNextSpacing(2100f, 0d), Is.EqualTo(97.5f));
        Assert.That(TrafficRoadblockRules.GetNextSpacing(4000f, 0d), Is.LessThan(TrafficRoadblockRules.GetNextSpacing(2100f, 0d)));
        foreach (string name in System.Enum.GetNames(typeof(TrafficRoadblockPattern)))
            Assert.That(name.StartsWith("Full"), Is.False, "Roadblocks must always leave a lane open.");
    }

}
