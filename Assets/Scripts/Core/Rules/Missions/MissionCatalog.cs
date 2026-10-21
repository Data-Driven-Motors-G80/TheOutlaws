using System;
using System.Collections.Generic;

/// <summary>The kind of skill a mission tests (used for analytics and balancing).</summary>
public enum MissionCategory
{
    Distance,
    Shooting,
    Mystery,
    Clean,
    Frugal,
    Fuel
}

/// <summary>Everything a mission can ask about: this run, plus lifetime totals (saved + this run).</summary>
public sealed class MissionRunStats
{
    public float Distance;
    public int Hits;
    public int ObstaclesDestroyed;
    public int RedDestroyed;
    public int MysteryCollected;
    public int AmmoPickups;
    public int ShotsFired;
    public bool ReachedSnow;
    /// <summary>Lowest fuel seen this run, 0..1.</summary>
    public float MinFuelFraction = 1f;
    /// <summary>F cans driven past this run.</summary>
    public int FuelMissed;

    // Lifetime totals that persist between runs (saved totals + this run so far).
    public float TotalDistance;
    public int TotalDestroyed;
    public int TotalAmmoPickups;
}

public sealed class MissionDefinition
{
    public readonly int Id;
    public readonly string Title;
    public readonly MissionCategory Category;
    /// <summary>Progress adds up across runs (e.g. "Destroy 10 blocks (total)").</summary>
    public bool Cumulative => Title.Contains("(total)");
    private readonly Func<MissionRunStats, bool> complete;
    private readonly Func<MissionRunStats, bool> failed;
    private readonly Func<MissionRunStats, string> progress;

    public MissionDefinition(int id, string title, MissionCategory category,
        Func<MissionRunStats, bool> complete,
        Func<MissionRunStats, string> progress,
        Func<MissionRunStats, bool> failed = null)
    {
        Id = id;
        Title = title;
        Category = category;
        this.complete = complete;
        this.progress = progress;
        this.failed = failed;
    }

    public bool IsComplete(MissionRunStats stats) => complete(stats);
    /// <summary>True when this run can no longer complete the mission.</summary>
    public bool IsFailed(MissionRunStats stats) => failed != null && failed(stats) && !complete(stats);
    public string Progress(MissionRunStats stats) => progress(stats);
}

/// <summary>
/// Ten missions, three held at a time. After an easy starter, every mission is a challenge
/// that can be retried each run ("reach X while ..."), never a one-off milestone.
/// </summary>
public static class MissionCatalog
{
    public const int VisibleCount = 3;

    public static readonly MissionDefinition[] All =
    {
        // An easy starter; everything after it is a repeatable challenge ("reach X while ...").
        new MissionDefinition(1, "Reach 1,000 m", MissionCategory.Distance,
            s => s.Distance >= 1000f, s => Metres(s.Distance, 1000f)),
        new MissionDefinition(2, "Reach 750 m without getting hit", MissionCategory.Clean,
            s => s.Hits == 0 && s.Distance >= 750f,
            s => s.Hits > 0 ? "Hit! Try next run" : Metres(s.Distance, 750f),
            s => s.Hits > 0),
        new MissionDefinition(3, "Destroy 5 blocks in one run", MissionCategory.Shooting,
            s => s.ObstaclesDestroyed >= 5, s => Count(s.ObstaclesDestroyed, 5)),
        new MissionDefinition(4, "Reach the mountains without missing an F", MissionCategory.Fuel,
            s => s.ReachedSnow && s.FuelMissed == 0,
            s => s.FuelMissed > 0 ? "Missed an F" : (s.ReachedSnow ? "Done!" : "Find the portal"),
            s => s.FuelMissed > 0),
        new MissionDefinition(5, "Destroy 10 blocks in one run", MissionCategory.Shooting,
            s => s.ObstaclesDestroyed >= 10, s => Count(s.ObstaclesDestroyed, 10)),
        new MissionDefinition(6, "Grab 2 ? boxes in one run", MissionCategory.Mystery,
            s => s.MysteryCollected >= 2, s => Count(s.MysteryCollected, 2)),
        new MissionDefinition(7, "Reach 1,500 m without shooting", MissionCategory.Frugal,
            s => s.ShotsFired == 0 && s.Distance >= 1500f,
            s => s.ShotsFired > 0 ? "Fired a shot" : Metres(s.Distance, 1500f),
            s => s.ShotsFired > 0),
        new MissionDefinition(8, "Reach 1,200 m, fuel never under 25%", MissionCategory.Fuel,
            s => s.MinFuelFraction >= 0.25f && s.Distance >= 1200f,
            s => s.MinFuelFraction < 0.25f ? "Fuel dipped too low" : Metres(s.Distance, 1200f),
            s => s.MinFuelFraction < 0.25f),
        new MissionDefinition(9, "Destroy 15 blocks in one run", MissionCategory.Shooting,
            s => s.ObstaclesDestroyed >= 15, s => Count(s.ObstaclesDestroyed, 15)),
        new MissionDefinition(10, "Reach 1,500 m without a hit", MissionCategory.Clean,
            s => s.Hits == 0 && s.Distance >= 1500f,
            s => s.Hits > 0 ? "Hit! Try next run" : Metres(s.Distance, 1500f),
            s => s.Hits > 0),
    };

    public static MissionDefinition Find(int id)
    {
        foreach (MissionDefinition mission in All)
            if (mission.Id == id) return mission;
        return null;
    }

    /// <summary>The three active missions: the first unfinished ones in order.</summary>
    public static List<MissionDefinition> Visible(Func<int, bool> isDone, int count = VisibleCount)
    {
        var visible = new List<MissionDefinition>(count);
        foreach (MissionDefinition mission in All)
        {
            if (visible.Count >= count) break;
            if (!isDone(mission.Id)) visible.Add(mission);
        }
        return visible;
    }

    private static string Metres(float value, float goal)
    {
        return $"{Math.Min(value, goal):N0} / {goal:N0} m";
    }

    private static string Count(int value, int goal)
    {
        return $"{Math.Min(value, goal)} / {goal}";
    }
}
