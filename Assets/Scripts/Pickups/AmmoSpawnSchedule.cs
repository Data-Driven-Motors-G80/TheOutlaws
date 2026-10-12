/// <summary>Distance-based ammo pacing used by the endless chase.</summary>
public static class AmmoSpawnSchedule
{
    public static int GetMinimumSpacing(float distanceTravelled)
    {
        if (distanceTravelled < TrafficObstacleRules.MidGameDistance) return 180;
        if (distanceTravelled < TrafficObstacleRules.LateGameDistance) return 300;
        return 400;
    }

    public static int GetMaximumSpacing(float distanceTravelled)
    {
        if (distanceTravelled < TrafficObstacleRules.MidGameDistance) return 260;
        if (distanceTravelled < TrafficObstacleRules.LateGameDistance) return 450;
        return 600;
    }
}
