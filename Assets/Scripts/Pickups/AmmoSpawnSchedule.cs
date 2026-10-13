/// <summary>Distance-based ammo pacing used by the endless chase.</summary>
public static class AmmoSpawnSchedule
{
    public static int GetMinimumSpacing(float distanceTravelled)
    {
        if (distanceTravelled < TrafficObstacleRules.MidGameDistance) return 90;
        if (distanceTravelled < TrafficObstacleRules.LateGameDistance) return 120;
        return 150;
    }

    public static int GetMaximumSpacing(float distanceTravelled)
    {
        if (distanceTravelled < TrafficObstacleRules.MidGameDistance) return 140;
        if (distanceTravelled < TrafficObstacleRules.LateGameDistance) return 180;
        return 220;
    }
}
