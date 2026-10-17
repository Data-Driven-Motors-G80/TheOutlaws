using NUnit.Framework;

public sealed class RunDifficultyStateTests
{
    [Test]
    public void SpeedScheduleAndDensityRemainBounded()
    {
        Assert.That(RunDifficultyState.DrivingSpeed(0f), Is.EqualTo(20f));
        float first = RunDifficultyState.SpeedIncreaseInterval(1, 0);
        Assert.That(RunDifficultyState.DrivingSpeed(first - 0.01f), Is.EqualTo(20f));
        Assert.That(RunDifficultyState.DrivingSpeed(first + 1.25f), Is.EqualTo(20.5f).Within(0.001f));
        Assert.That(RunDifficultyState.DrivingSpeed(first + 2.5f), Is.EqualTo(21f));
        Assert.That(RunDifficultyState.DrivingSpeed(1600f), Is.EqualTo(55f));
        Assert.That(RunDifficultyState.DrivingSpeed(9999f), Is.EqualTo(55f));
        for (int step = 0; step < 25; step++)
        {
            Assert.That(RunDifficultyState.SpeedIncreaseInterval(1, step), Is.InRange(30f, 45f));
            Assert.That(RunDifficultyState.SpeedIncreaseInterval(82, step), Is.InRange(30f, 45f));
        }
        Assert.That(RunDifficultyState.SpeedIncreaseInterval(1, 0),
            Is.Not.EqualTo(RunDifficultyState.SpeedIncreaseInterval(82, 0)));
        Assert.That(RunDifficultyState.ObstacleDensity(0f), Is.EqualTo(0.6f));
        Assert.That(RunDifficultyState.ObstacleDensity(9999f), Is.EqualTo(4.5f).Within(0.001f));
        Assert.That(RunDifficultyState.DoubleRowChance(9999f), Is.EqualTo(0.9f).Within(0.001f));
        // Warm-up: sparse at first, normal after 30 s, densest after another two minutes.
        Assert.That(RunDifficultyState.ObstacleDensity(30f), Is.EqualTo(1.3f).Within(0.001f));
        Assert.That(RunDifficultyState.ObstacleDensity(90f), Is.EqualTo(1.85f).Within(0.001f));
        Assert.That(RunDifficultyState.DoubleRowChance(0f), Is.EqualTo(0.1f).Within(0.001f));
        // Keeps rising after the build-up: denser at 4 minutes than at 3.
        Assert.That(RunDifficultyState.ObstacleDensity(240f), Is.GreaterThan(RunDifficultyState.ObstacleDensity(180f)));
    }

    [Test]
    public void DifficultyNeverDecreasesOverTime()
    {
        for (float t = 0f; t < 300f; t += 1f)
        {
            Assert.That(RunDifficultyState.DrivingSpeed(t + 1f), Is.GreaterThanOrEqualTo(RunDifficultyState.DrivingSpeed(t)));
            Assert.That(RunDifficultyState.ObstacleDensity(t + 1f), Is.GreaterThanOrEqualTo(RunDifficultyState.ObstacleDensity(t)));
            Assert.That(RunDifficultyState.PoliceClosingSpeed(t + 1f), Is.GreaterThanOrEqualTo(RunDifficultyState.PoliceClosingSpeed(t)));
        }
    }

    [Test]
    public void PlayerAlwaysOutpacesThePoliceButLessSoLater()
    {
        Assert.That(RunDifficultyState.PoliceClosingSpeed(0f), Is.EqualTo(-1.5f));
        Assert.That(RunDifficultyState.PoliceClosingSpeed(100f), Is.EqualTo(-0.9f).Within(0.001f));
        Assert.That(RunDifficultyState.PoliceClosingSpeed(9999f), Is.EqualTo(-0.6f));
        for (float t = 0f; t < 600f; t += 10f)
            Assert.That(RunDifficultyState.PoliceClosingSpeed(t), Is.LessThan(0f));
    }

    [Test]
    public void OneHitIsRecoveredButHitsCloseTogetherAreFatal()
    {
        // One red hit, then clean driving: the gap recovers to the start (and no further).
        var run = new RiskRunState { RecoveryCap = RiskRunState.InitialPursuitGap };
        run.TryCrash(TrafficObstacleRules.RedProximityDrain);
        Assert.That(run.PursuitGap, Is.LessThan(RiskRunState.InitialPursuitGap));
        for (int i = 0; i < 600; i++) run.Tick(0.1f, 25f, 25f - 0.6f);
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.PursuitGap, Is.EqualTo(RiskRunState.InitialPursuitGap).Within(0.001f));

        // Two red hits a couple of seconds apart: caught.
        var careless = new RiskRunState { RecoveryCap = RiskRunState.InitialPursuitGap };
        careless.TryCrash(TrafficObstacleRules.RedProximityDrain);
        careless.Tick(1.5f, 25f, 25f - 0.6f);
        careless.TryCrash(TrafficObstacleRules.RedProximityDrain);
        Assert.That(careless.IsGameOver, Is.True);
    }

    [Test]
    public void OpeningIsForgivingAndRampCaps()
    {
        Assert.That(RunDifficultyState.Evaluate(0f).Progress, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(5f).Progress, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(40f).Progress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(RunDifficultyState.Evaluate(75f).Progress, Is.EqualTo(1f));
        Assert.That(RunDifficultyState.Evaluate(900f).Progress, Is.EqualTo(1f));
    }

    [Test]
    public void ReliefIsBriefAndRepeatsAfterOpening()
    {
        Assert.That(RunDifficultyState.Evaluate(5f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(23f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(26f).Relief, Is.EqualTo(1f).Within(0.001f));
        Assert.That(RunDifficultyState.Evaluate(29f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(50f).Relief, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void CleanNinetySecondRunSurvivesWithoutMandatoryShooting()
    {
        var run = new RiskRunState();
        for (int i = 0; i < 900; i++)
        {
            var difficulty = RunDifficultyState.Evaluate(i * 0.1f);
            float closing = 0.25f + 0.4f * difficulty.Progress;
            closing += (-0.35f - closing) * difficulty.Relief;
            run.Tick(0.1f, 15f, 15f + closing);
        }
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.PursuitGap, Is.GreaterThan(5f));
    }
}
