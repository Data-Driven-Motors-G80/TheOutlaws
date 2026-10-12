using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrafficObstacleTutorial : MonoBehaviour
{
    private static readonly float[] AmmoDistances = { 8f, 18f, 28f };

    private readonly List<GameObject> spawnedAmmo = new List<GameObject>();
    private InfiniteRoad road;
    private Transform player;
    private AutoDriveCar driver;
    private OutlawShooting shooting;
    private bool started;

    public string CurrentMessage
    {
        get
        {
            if (!started || driver == null) return null;
            float distance = driver.RoadDistanceTravelled;
            if (distance < 45f)
                return "GREEN ROADBLOCK  -  PRESS SPACE ONCE TO BREAK THROUGH";
            if (distance < 100f)
                return "YELLOW ROADBLOCK  -  SHOOT TWICE, OR ONCE TO REDUCE THE PENALTY";
            if (distance < 145f)
                return "RED NEEDS THREE SHOTS  -  DESTROYING RED REFUNDS ONE AMMO";
            return null;
        }
    }

    public void Configure(
        InfiniteRoad infiniteRoad,
        Transform playerTransform,
        AutoDriveCar playerDriver,
        OutlawShooting playerShooting)
    {
        road = infiniteRoad;
        player = playerTransform;
        driver = playerDriver;
        shooting = playerShooting;
    }

    public void BeginTutorial()
    {
        if (started || road == null || player == null || shooting == null) return;
        started = true;
        foreach (float distance in AmmoDistances)
        {
            if (!TryGetPointAhead(distance, out RoadPathPoint point)) continue;
            GameObject ammo = OutlawAmmoPickup.Create(
                point.Position + point.Up * 0.32f, point.Up, shooting);
            ammo.transform.SetParent(transform, true);
            spawnedAmmo.Add(ammo);
            Destroy(ammo, 20f);
        }
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        return road.TryGetPointAhead(player.position, distance, out point);
    }

    private void OnDestroy()
    {
        spawnedAmmo.Clear();
    }
}
