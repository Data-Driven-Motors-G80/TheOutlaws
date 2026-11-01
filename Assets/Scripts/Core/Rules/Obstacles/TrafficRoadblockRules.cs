// Chooses roadblock spacing and patterns from run progress.
public enum TrafficRoadblockPattern
{
    TwoLaneGreen,
    TwoLaneYellow,

    SplitChoice
}

public static class TrafficRoadblockRules
{
    public const float FirstRoadblockDistance = 400f;
    public const float UltraLateDistance = 2000f;

    public static float GetNextSpacing(float distanceTravelled, double roll)
    {
        roll = ClampRoll(roll);
        float d = System.Math.Max(0f, distanceTravelled);
        float minimum = System.Math.Max(45f, 150f - d * 0.025f);
        float maximum = System.Math.Max(65f, 210f - d * 0.035f);
        return minimum + (maximum - minimum) * (float)roll;
    }

    // Progress changes the available challenge without changing the supplied roll.
    public static TrafficRoadblockPattern ChoosePattern(float distanceTravelled, double roll)
    {
        roll = ClampRoll(roll);
        if (roll < 0.4d) return TrafficRoadblockPattern.TwoLaneGreen;
        if (roll < 0.75d) return TrafficRoadblockPattern.TwoLaneYellow;
        return TrafficRoadblockPattern.SplitChoice;
    }

    private static double ClampRoll(double roll)
    {
        if (double.IsNaN(roll) || double.IsInfinity(roll)) return 0d;
        return System.Math.Max(0d, System.Math.Min(0.999999d, roll));
    }
}
