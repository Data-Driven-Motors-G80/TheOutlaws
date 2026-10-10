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
            Is.Zero);
    }

    [Test]
    public void EarlyGameNeverChoosesRedAndLateGameCanChooseEveryState()
    {
        Assert.That(TrafficObstacleRules.ChooseInitialState(0f, 0.1d),
            Is.EqualTo(TrafficObstacleState.Green));
        Assert.That(TrafficObstacleRules.ChooseInitialState(0f, 0.9d),
            Is.EqualTo(TrafficObstacleState.Yellow));
        Assert.That(TrafficObstacleRules.ChooseInitialState(800f, 0.1d),
            Is.EqualTo(TrafficObstacleState.Green));
        Assert.That(TrafficObstacleRules.ChooseInitialState(800f, 0.4d),
            Is.EqualTo(TrafficObstacleState.Yellow));
        Assert.That(TrafficObstacleRules.ChooseInitialState(800f, 0.9d),
            Is.EqualTo(TrafficObstacleState.Red));
    }

    [Test]
    public void AmmoSpacingTightensInLateGame()
    {
        Assert.That(AmmoSpawnSchedule.GetMinimumSpacing(800f),
            Is.LessThan(AmmoSpawnSchedule.GetMinimumSpacing(0f)));
        Assert.That(AmmoSpawnSchedule.GetMaximumSpacing(800f),
            Is.LessThan(AmmoSpawnSchedule.GetMaximumSpacing(0f)));
    }

    [Test]
    public void MidGameAmmoIsMoreFrequentThanEarlyGame()
    {
        Assert.That(AmmoSpawnSchedule.GetMinimumSpacing(400f),
            Is.LessThan(AmmoSpawnSchedule.GetMinimumSpacing(0f)));
        Assert.That(AmmoSpawnSchedule.GetMaximumSpacing(400f),
            Is.LessThan(AmmoSpawnSchedule.GetMaximumSpacing(0f)));
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
        Assert.That(TrafficRoadblockRules.GetNextSpacing(400f, 0d), Is.EqualTo(240f));
        Assert.That(TrafficRoadblockRules.GetNextSpacing(400f, 0.999d), Is.LessThan(330f));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.37d),
            Is.EqualTo(TrafficRoadblockPattern.TwoLaneGreen));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.70d),
            Is.EqualTo(TrafficRoadblockPattern.TwoLaneYellow));
        Assert.That(TrafficRoadblockRules.ChoosePattern(400f, 0.80d),
            Is.EqualTo(TrafficRoadblockPattern.FullGreen));
    }

    [Test]
    public void UltraLateGameProducesMoreFullWallsAtShorterIntervals()
    {
        Assert.That(TrafficRoadblockRules.GetNextSpacing(2100f, 0d), Is.EqualTo(160f));
        Assert.That(TrafficRoadblockRules.GetNextSpacing(2100f, 0.999d), Is.LessThan(240f));
        Assert.That(TrafficRoadblockRules.ChoosePattern(2100f, 0.2d),
            Is.EqualTo(TrafficRoadblockPattern.FullGreen));
        Assert.That(TrafficRoadblockRules.ChoosePattern(2100f, 0.6d),
            Is.EqualTo(TrafficRoadblockPattern.FullYellow));
        Assert.That(TrafficRoadblockRules.ChoosePattern(2100f, 0.9d),
            Is.EqualTo(TrafficRoadblockPattern.MixedRedYellowGreen));
    }

}
