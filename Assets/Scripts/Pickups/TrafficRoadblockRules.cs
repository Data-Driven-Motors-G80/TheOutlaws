public enum TrafficRoadblockPattern
{
    TwoLaneGreen,
    TwoLaneYellow,
    FullGreen,
    FullYellow,
    MixedRedYellowGreen
}

/// <summary>Scene-independent pacing for occasional mid/late-game roadblocks.</summary>
public static class TrafficRoadblockRules
{
    public const float FirstRoadblockDistance = 330f;
    public const float UltraLateDistance = 2000f;

    public static float GetNextSpacing(float distanceTravelled, double roll)
    {
        roll = ClampRoll(roll);
        float minimum;
        float maximum;
        if (distanceTravelled >= UltraLateDistance)
        {
            minimum = 160f;
            maximum = 240f;
        }
        else if (distanceTravelled < TrafficObstacleRules.LateGameDistance)
        {
            minimum = 240f;
            maximum = 330f;
        }
        else
        {
            minimum = 220f;
            maximum = 320f;
        }
        return minimum + (maximum - minimum) * (float)roll;
    }

    public static TrafficRoadblockPattern ChoosePattern(float distanceTravelled, double roll)
    {
        roll = ClampRoll(roll);
        if (distanceTravelled >= UltraLateDistance)
        {
            if (roll < 0.05d) return TrafficRoadblockPattern.TwoLaneGreen;
            if (roll < 0.1d) return TrafficRoadblockPattern.TwoLaneYellow;
            if (roll < 0.45d) return TrafficRoadblockPattern.FullGreen;
            if (roll < 0.8d) return TrafficRoadblockPattern.FullYellow;
            return TrafficRoadblockPattern.MixedRedYellowGreen;
        }

        if (distanceTravelled < TrafficObstacleRules.LateGameDistance)
        {
            if (roll < 0.38d) return TrafficRoadblockPattern.TwoLaneGreen;
            if (roll < 0.72d) return TrafficRoadblockPattern.TwoLaneYellow;
            if (roll < 0.86d) return TrafficRoadblockPattern.FullGreen;
            if (roll < 0.96d) return TrafficRoadblockPattern.FullYellow;
            return TrafficRoadblockPattern.MixedRedYellowGreen;
        }

        if (roll < 0.2d) return TrafficRoadblockPattern.TwoLaneGreen;
        if (roll < 0.4d) return TrafficRoadblockPattern.TwoLaneYellow;
        if (roll < 0.55d) return TrafficRoadblockPattern.FullGreen;
        if (roll < 0.75d) return TrafficRoadblockPattern.FullYellow;
        return TrafficRoadblockPattern.MixedRedYellowGreen;
    }

    private static double ClampRoll(double roll)
    {
        if (double.IsNaN(roll) || double.IsInfinity(roll)) return 0d;
        return System.Math.Max(0d, System.Math.Min(0.999999d, roll));
    }
}
