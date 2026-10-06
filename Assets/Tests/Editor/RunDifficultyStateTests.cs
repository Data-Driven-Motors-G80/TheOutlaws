using NUnit.Framework;

public sealed class RunDifficultyStateTests
{
    [Test]
    public void OpeningIsForgivingAndRampCaps()
    {
        Assert.That(RunDifficultyState.Evaluate(0f).Progress, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(20f).Progress, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(55f).Progress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(RunDifficultyState.Evaluate(90f).Progress, Is.EqualTo(1f));
        Assert.That(RunDifficultyState.Evaluate(900f).Progress, Is.EqualTo(1f));
    }

    [Test]
    public void ReliefIsBriefAndRepeatsAfterOpening()
    {
        Assert.That(RunDifficultyState.Evaluate(20f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(38f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(41f).Relief, Is.EqualTo(1f).Within(0.001f));
        Assert.That(RunDifficultyState.Evaluate(44f).Relief, Is.Zero);
        Assert.That(RunDifficultyState.Evaluate(65f).Relief, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void CleanNinetySecondRunSurvivesWithoutMandatoryShooting()
    {
        var run = new RiskRunState();
        for (int i = 0; i < 900; i++)
        {
            var difficulty = RunDifficultyState.Evaluate(i * 0.1f);
            float closing = 0.1f + 0.55f * difficulty.Progress;
            closing += (-0.35f - closing) * difficulty.Relief;
            run.Tick(0.1f, 15f, 15f + closing);
        }
        Assert.That(run.IsGameOver, Is.False);
        Assert.That(run.PursuitGap, Is.GreaterThan(15f));
    }
}
