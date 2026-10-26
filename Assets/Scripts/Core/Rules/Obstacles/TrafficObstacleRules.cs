public enum TrafficObstacleState
{
    Green,
    Yellow,
    Red
}

/// <summary>Scene-independent tuning rules for traffic-light obstacles.</summary>
public static class TrafficObstacleRules
{
    // The internal states still describe shots remaining, while the player-facing
    // colours are orange for the one-shot state and red for the two-shot state.
    // Fractions are taken from the 45 m starting gap: orange costs 15 m and red 22.5 m.
    public const float GreenProximityDrain = 1f / 3f;
    public const float YellowProximityDrain = 0.5f;
    public const float RedProximityDrain = 0.5f;
    public const float YellowFuelPenaltyFraction = 0f;
    public const float RedFuelPenaltyFraction = 0f;

    public const float MidGameDistance = 300f;
    public const float LateGameDistance = 700f;

    /// <summary>The same mix all run: 55% green (one shot), 45% yellow (two shots).</summary>
    public static TrafficObstacleState ChooseInitialState(float distanceTravelled, double roll)
    {
        if (double.IsNaN(roll) || double.IsInfinity(roll)) roll = 0d;
        roll = System.Math.Max(0d, System.Math.Min(0.999999d, roll));
        if (roll < 0.55d) return TrafficObstacleState.Green;
        return TrafficObstacleState.Yellow;
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
