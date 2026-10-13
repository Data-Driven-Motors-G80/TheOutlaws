public enum TrafficObstacleState
{
    Green,
    Yellow,
    Red
}

/// <summary>Scene-independent tuning rules for traffic-light obstacles.</summary>
public static class TrafficObstacleRules
{
    // Fractions of the 45 m starting gap: green 9 m, yellow 15.75 m, red 24.75 m.
    public const float GreenProximityDrain = 0.2f;
    public const float YellowProximityDrain = 0.35f;
    public const float RedProximityDrain = 0.55f;
    public const float YellowFuelPenaltyFraction = 0.1f;
    public const float RedFuelPenaltyFraction = 0.2f;

    public const float MidGameDistance = 300f;
    public const float LateGameDistance = 700f;

    /// <summary>The same mix of 1s, 2s and 3s for the whole run: 55% green, 30% yellow, 15% red.</summary>
    public static TrafficObstacleState ChooseInitialState(float distanceTravelled, double roll)
    {
        if (double.IsNaN(roll) || double.IsInfinity(roll)) roll = 0d;
        roll = System.Math.Max(0d, System.Math.Min(0.999999d, roll));
        if (roll < 0.55d) return TrafficObstacleState.Green;
        if (roll < 0.85d) return TrafficObstacleState.Yellow;
        return TrafficObstacleState.Red;
    }

    public static float GetProximityDrain(TrafficObstacleState state)
    {
        switch (state)
        {
            case TrafficObstacleState.Red:
                return RedProximityDrain;
            case TrafficObstacleState.Yellow:
                return YellowProximityDrain;
            default:
                return GreenProximityDrain;
        }
    }

    public static float GetFuelPenaltyFraction(TrafficObstacleState state)
    {
        return state == TrafficObstacleState.Red ? RedFuelPenaltyFraction
            : state == TrafficObstacleState.Yellow ? YellowFuelPenaltyFraction : 0f;
    }

    public static TrafficObstacleState ApplyShot(
        TrafficObstacleState state,
        out bool destroyed)
    {
        destroyed = state == TrafficObstacleState.Green;
        if (state == TrafficObstacleState.Red) return TrafficObstacleState.Yellow;
        if (state == TrafficObstacleState.Yellow) return TrafficObstacleState.Green;
        return TrafficObstacleState.Green;
    }
}
