public enum TrafficObstacleState
{
    Green,
    Yellow,
    Red
}

/// <summary>Scene-independent tuning rules for traffic-light obstacles.</summary>
public static class TrafficObstacleRules
{
    public const float GreenProximityDrain = 0.125f;
    public const float YellowProximityDrain = 0.25f;
    public const float RedProximityDrain = 0.4f;
    public const float RedFuelPenaltyFraction = 0.2f;

    public const float MidGameDistance = 300f;
    public const float LateGameDistance = 700f;

    public static TrafficObstacleState ChooseInitialState(float distanceTravelled, double roll)
    {
        if (double.IsNaN(roll) || double.IsInfinity(roll)) roll = 0d;
        roll = System.Math.Max(0d, System.Math.Min(0.999999d, roll));

        if (distanceTravelled < MidGameDistance)
            return roll < 0.8d ? TrafficObstacleState.Green : TrafficObstacleState.Yellow;

        if (distanceTravelled < LateGameDistance)
        {
            if (roll < 0.45d) return TrafficObstacleState.Green;
            if (roll < 0.85d) return TrafficObstacleState.Yellow;
            return TrafficObstacleState.Red;
        }

        if (roll < 0.25d) return TrafficObstacleState.Green;
        if (roll < 0.6d) return TrafficObstacleState.Yellow;
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
        return state == TrafficObstacleState.Red ? RedFuelPenaltyFraction : 0f;
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
