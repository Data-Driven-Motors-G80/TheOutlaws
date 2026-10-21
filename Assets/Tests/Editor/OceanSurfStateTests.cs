using NUnit.Framework;

public sealed class OceanSurfStateTests
{
    [Test]
    public void CrestBuildsMoreSpeedThanWaveShoulder()
    {
        var crest = new OceanSurfState();
        var shoulder = new OceanSurfState();
        crest.Tick(3f, true, true);
        shoulder.Tick(3f, true, false);
        Assert.That(crest.SpeedMultiplier, Is.EqualTo(1.35f).Within(0.0001f));
        Assert.That(shoulder.SpeedMultiplier, Is.EqualTo(1.21f).Within(0.0001f));
        Assert.That(crest.IsOnCrest, Is.True);
        Assert.That(shoulder.IsOnCrest, Is.False);
    }

    [Test]
    public void LeavingWaveCarriesMomentumThenReturnsToNormal()
    {
        var surf = new OceanSurfState();
        surf.Tick(2f, true, true);
        surf.Tick(OceanSurfState.ExitSeconds * 0.5f, false, false);
        Assert.That(surf.SpeedMultiplier, Is.EqualTo(1.175f).Within(0.0001f));
        Assert.That(surf.IsSurfing, Is.False);
        surf.Tick(OceanSurfState.ExitSeconds, false, false);
        Assert.That(surf.SpeedMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void PauseAndInvalidTimeDoNotSpendSurfMomentum()
    {
        var surf = new OceanSurfState();
        surf.Tick(1f, true, true);
        float before = surf.Boost;
        foreach (float time in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            surf.Tick(time, false, false);
        Assert.That(surf.Boost, Is.EqualTo(before));
        surf.Reset();
        Assert.That(surf.SpeedMultiplier, Is.EqualTo(1f));
        Assert.That(surf.IsSurfing, Is.False);
    }

    [Test]
    public void SurfSpeedAdvancesDistanceAndPursuitTogetherWithoutReplacingNitro()
    {
        var surf = new OceanSurfState();
        surf.Tick(2f, true, true);
        var run = new RiskRunState();
        run.TryActivateNitro();
        run.Tick(1f, 20f * surf.SpeedMultiplier, 20f);
        Assert.That(run.LastTickForwardDistance, Is.EqualTo(32.4f).Within(0.0001f));
        Assert.That(run.PursuitGap, Is.EqualTo(RiskRunState.InitialPursuitGap + 12.4f).Within(0.0001f));
        Assert.That(run.HasNitro, Is.True);
    }
}
