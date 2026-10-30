using NUnit.Framework;

public sealed class GetawayRunStateTests
{
    [Test]
    public void ShieldDoesNotStackAndCarriesUntilOneHitOrSettlement()
    {
        var run = new GetawayRunState(); run.Begin();
        Assert.That(run.CollectShield(), Is.True);
        Assert.That(run.CollectShield(), Is.False);
        run.Continue();
        Assert.That(run.HasShield, Is.True);
        Assert.That(run.AbsorbHit(), Is.True);
        Assert.That(run.AbsorbHit(), Is.False);
        run.CollectShield(); run.Bank();
        Assert.That(run.HasShield, Is.False);
        Assert.That(run.CollectShield(), Is.False);
        run.Begin(); run.CollectShield(); run.Lose("FELL OFF ROAD");
        Assert.That(run.HasShield, Is.False);
    }

    [Test]
    public void ContinuingRisksExistingHaulAndRaisesFutureRewards()
    {
        var run = new GetawayRunState();
        run.Begin();
        Assert.That(run.Collect(100), Is.EqualTo(100));
        Assert.That(run.Continue(), Is.True);
        Assert.That(run.Carried, Is.EqualTo(100));
        Assert.That(run.Collect(100), Is.EqualTo(150));
        Assert.That(run.ExtraPursuitSpeed, Is.GreaterThan(0f));
        Assert.That(run.Bank(), Is.EqualTo(250));
        Assert.That(run.Carried, Is.Zero);
        Assert.That(run.Bank(), Is.Zero, "Banking is a one-time settlement.");
        Assert.That(run.Collect(100), Is.Zero);
        Assert.That(run.Continue(), Is.False);
    }

    [Test]
    public void FailureAndAbandonmentLoseTheWholeUnbankedHaul()
    {
        var run = new GetawayRunState();
        run.Begin(); run.Collect(300); run.Continue(); run.Collect(200);
        run.Lose("CAUGHT");
        Assert.That(run.Carried, Is.Zero);
        Assert.That(run.SettledHaul, Is.EqualTo(600));
        Assert.That(run.Bank(), Is.Zero);
        run.Begin(); run.Collect(100); run.Leave();
        Assert.That(run.Status, Is.EqualTo(GetawayRunState.RunStatus.Lost));
        Assert.That(run.Carried, Is.Zero);
        run.Begin();
        Assert.That(run.Leg, Is.Zero);
        Assert.That(run.SettledHaul, Is.Zero);
    }

    [Test]
    public void LongRunsBoundDifficultyAndCannotOverflowTheHaul()
    {
        var run = new GetawayRunState(); run.Begin();
        for (int i = 0; i < 200; i++) run.Continue();
        Assert.That(run.ExtraPursuitSpeed, Is.EqualTo(.15f).Within(.0001f));
        run.Collect(int.MaxValue); run.Collect(100);
        Assert.That(run.Carried, Is.EqualTo(int.MaxValue));
    }
}
