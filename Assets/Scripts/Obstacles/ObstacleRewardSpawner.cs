using UnityEngine;

public static class ObstacleRewardSpawner
{
    public static void SpawnRedDestructionReward(
        TrafficLightObstacle obstacle,
        OutlawShooting shooting)
    {
        if (obstacle == null || shooting == null || !obstacle.StartedRed) return;
        GameObject reward = OutlawAmmoPickup.Create(
            obstacle.GetRewardPosition(), obstacle.transform.up, shooting);
        Object.Destroy(reward, 12f);
    }
}
