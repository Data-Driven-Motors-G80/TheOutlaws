using NUnit.Framework;

public sealed class RunDifficultyStateTests
{
    [Test]
    public void DrivingPressureStartsAtTwentyFiveSeconds()
    {
        Assert.That(RunDifficultyState.DrivingSpeed(15f), Is.EqualTo(15f));
        Assert.That(RunDifficultyState.DrivingSpeed(24.9f), Is.EqualTo(15f));
        Assert.That(RunDifficultyState.DrivingSpeed(25f), Is.EqualTo(16f));
        Assert.That(RunDifficultyState.ObstacleDensity(50f), Is.EqualTo(1.16f).Within(0.001f));
        Assert.That(RunDifficultyState.DrivingSpeed(9999f), Is.EqualTo(30f));
        Assert.That(RunDifficultyState.ObstacleDensity(9999f), Is.EqualTo(1.6f));
    }

    [Test]
    public void CarriedElapsedTimeKeepsDifficultyAcrossEnvironmentChanges()
    {
        // A switch at 74 seconds keeps the existing pressure, with the next step at 75.
        Assert.That(RunDifficultyState.DrivingSpeed(74f), Is.EqualTo(17f));
        Assert.That(RunDifficultyState.ObstacleDensity(74f), Is.EqualTo(1.16f).Within(0.001f));
        Assert.That(RunDifficultyState.DrivingSpeed(75f), Is.EqualTo(18f));
        Assert.That(RunDifficultyState.ObstacleDensity(75f), Is.EqualTo(1.24f).Within(0.001f));
    }

    [Test]
    public void PolicePressureKeepsOpeningThenRampsToNewCap()
    {
        Assert.That(RunDifficultyState.PoliceClosingSpeed(24.9f), Is.EqualTo(0.15f));
        Assert.That(RunDifficultyState.PoliceClosingSpeed(25f), Is.EqualTo(0.23f).Within(0.001f));
        Assert.That(RunDifficultyState.PoliceClosingSpeed(100f), Is.EqualTo(0.47f).Within(0.001f));
        Assert.That(RunDifficultyState.PoliceClosingSpeed(9999f), Is.EqualTo(0.8f));
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
