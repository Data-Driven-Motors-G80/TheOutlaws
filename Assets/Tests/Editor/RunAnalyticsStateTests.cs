using NUnit.Framework;
public sealed class RunAnalyticsStateTests
{
    [Test] public void CountersSurviveEnvironmentChangeAndEndOnlyOnce()
    {
        RunAnalyticsState.ResetSession(); RunAnalyticsState.Begin();
        string id = RunAnalyticsState.RunId;
        RunAnalyticsState.Count("shots_fired");
        RunAnalyticsState.Tick(25f, false); RunAnalyticsState.Tick(10f, true);
        Assert.That(RunAnalyticsState.RunId, Is.EqualTo(id));
        Assert.That(RunAnalyticsState.CitySeconds, Is.EqualTo(25));
        Assert.That(RunAnalyticsState.SnowSeconds, Is.EqualTo(10));
        Assert.That(RunAnalyticsState.End(), Is.True);
        Assert.That(RunAnalyticsState.End(), Is.False);
        RunAnalyticsState.Count("shots_fired");
        Assert.That(RunAnalyticsState.Get("shots_fired"), Is.EqualTo(1));
        RunAnalyticsState.Begin();
        Assert.That(RunAnalyticsState.RunId, Is.Not.EqualTo(id));
        Assert.That(RunAnalyticsState.Get("shots_fired"), Is.Zero);
        Assert.That(RunAnalyticsState.CitySeconds, Is.Zero);
    }
}
