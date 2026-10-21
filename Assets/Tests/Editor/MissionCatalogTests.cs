using System.Collections.Generic;
using NUnit.Framework;

public sealed class MissionCatalogTests
{
    [Test]
    public void TenMissionsWithUniqueIds()
    {
        Assert.That(MissionCatalog.All.Length, Is.EqualTo(10));
        var ids = new HashSet<int>();
        foreach (MissionDefinition mission in MissionCatalog.All)
            Assert.That(ids.Add(mission.Id), Is.True);
    }

    [Test]
    public void ThreeActiveAndCompletedOnesAreReplacedByTheNext()
    {
        Assert.That(MissionCatalog.Visible(id => false).ConvertAll(m => m.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        var done = new HashSet<int> { 1 };
        Assert.That(MissionCatalog.Visible(done.Contains).ConvertAll(m => m.Id), Is.EqualTo(new[] { 2, 3, 4 }));
        for (int id = 1; id <= 10; id++) done.Add(id);
        Assert.That(MissionCatalog.Visible(done.Contains), Is.Empty);
    }

    [Test]
    public void OnlyTheStarterIsAPlainMilestone()
    {
        // Every mission after the first needs something extra in the same run, not just distance.
        var plainRun = new MissionRunStats { Distance = 5000f, ReachedSnow = true, Hits = 3, AmmoPickups = 4, ShotsFired = 1,
            MinFuelFraction = 0.1f, FuelMissed = 1 };
        Assert.That(MissionCatalog.Find(1).IsComplete(plainRun), Is.True);
        for (int id = 2; id <= 10; id++)
            Assert.That(MissionCatalog.Find(id).IsComplete(plainRun), Is.False, "Mission " + id);
    }

    [Test]
    public void FifthMissionCanCompleteWithoutRedBlocks()
    {
        var stats = new MissionRunStats { ObstaclesDestroyed = 9, RedDestroyed = 0 };
        MissionDefinition mission = MissionCatalog.Find(5);
        Assert.That(mission.IsComplete(stats), Is.False);
        Assert.That(mission.Progress(stats), Is.EqualTo("9 / 10"));
        stats.ObstaclesDestroyed++;
        Assert.That(mission.IsComplete(stats), Is.True);
        Assert.That(mission.Progress(stats), Is.EqualTo("10 / 10"));
    }

    [Test]
    public void ConditionMissionsFailWhenTheConditionBreaks()
    {
        var stats = new MissionRunStats { Distance = 1600f };
        Assert.That(MissionCatalog.Find(10).IsComplete(stats), Is.True);
        stats.Hits = 1;
        Assert.That(MissionCatalog.Find(10).IsFailed(stats), Is.True);
        stats = new MissionRunStats { ReachedSnow = true, FuelMissed = 1 };
        Assert.That(MissionCatalog.Find(4).IsFailed(stats), Is.True);
        stats = new MissionRunStats { Distance = 1500f, ShotsFired = 1 };
        Assert.That(MissionCatalog.Find(7).IsFailed(stats), Is.True);
    }
}
