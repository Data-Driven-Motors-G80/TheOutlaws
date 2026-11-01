// Places ammo offers and records uncollected pickups as missed.
using System.Collections.Generic;
using UnityEngine;

public sealed class OutlawPickupSpawner : MonoBehaviour
{
    private const float SpawnAheadDistance = PickupLookAhead.Distance;
    private const int MaximumActive = 1;

    private readonly List<GameObject> active = new List<GameObject>();
    private InfiniteRoad road;
    private Transform player;
    private AutoDriveCar driver;
    private OutlawShooting shooting;
    private CarPickupEffects pickupEffects;
    private float nextSpawnAt;
    private float carriedDistance;
    private int spawnIndex;
    private System.Random spacingRandom;
    private readonly List<OutlawAmmoPickup> tracked = new List<OutlawAmmoPickup>();

    public bool Suppressed { get; set; }

    public System.Func<float> RunDistance { get; set; }

    public void BeginRealRun()
    {
        Suppressed = false;
        nextSpawnAt = driver.RoadDistanceTravelled + NextSpawnSpacing() - SpawnAheadDistance;
    }

    public void Configure(
        InfiniteRoad infiniteRoad,
        Transform playerTransform,
        OutlawShooting playerShooting,
        CarPickupEffects effects)
    {
        road = infiniteRoad;
        player = playerTransform;
        driver = player != null ? player.GetComponent<AutoDriveCar>() : null;
        shooting = playerShooting;
        pickupEffects = effects;
        active.Clear();

        foreach (OutlawAmmoPickup pickup in GetComponentsInChildren<OutlawAmmoPickup>(true))
        {
            pickup.gameObject.SetActive(false);
            Destroy(pickup.gameObject);
        }
        carriedDistance = RunSession.TryGetPending(out RunSnapshot carried) ? carried.Distance : 0f;
        spawnIndex = 0;

        spacingRandom = new System.Random(System.Guid.NewGuid().GetHashCode());

        nextSpawnAt = (driver != null ? driver.RoadDistanceTravelled : 0f)
            + NextSpawnSpacing() - SpawnAheadDistance;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || road == null || player == null || driver == null || shooting == null ||
            (pickupEffects != null && pickupEffects.RunState.IsGameOver))
        {
            return;
        }

        PickupTracking.ReportMissed(tracked, road, player, PickupKind.Ammo, pickup => pickup.Collected);

        for (int i = active.Count - 1; i >= 0; i--)
        {
            GameObject pickup = active[i];
            if (pickup == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (road.IsBehind(pickup.transform.position, player.position, 18f) ||
                Vector3.Distance(player.position, pickup.transform.position) > 140f)
            {
                Destroy(pickup);
                active.RemoveAt(i);
            }
        }

        if (Suppressed || driver.RoadDistanceTravelled < nextSpawnAt || active.Count >= MaximumActive)
        {
            return;
        }

        if (TrySpawnAmmo(SpawnAheadDistance))
        {
            nextSpawnAt = driver.RoadDistanceTravelled + NextSpawnSpacing();
        }
    }

    // Choose the next distance using the current progress rules.
    private float NextSpawnSpacing()
    {
        float distance = RunDistance != null
            ? RunDistance()
            : carriedDistance + (driver != null ? driver.RoadDistanceTravelled : 0f);
        int minimum = AmmoSpawnSchedule.GetMinimumSpacing(distance);
        int maximum = AmmoSpawnSchedule.GetMaximumSpacing(distance);
        return spacingRandom.Next(minimum, maximum + 1);
    }

    private bool TrySpawnAmmo(float distanceAhead)
    {
        if (!TryGetPointAhead(distanceAhead, out RoadPathPoint point))
        {

            return false;
        }

        float lateralLimit = Mathf.Max(0f, point.Width * 0.5f - 1.5f);
        float side = spawnIndex % 2 == 0 ? -0.9f : 0.9f;
        spawnIndex++;

        float lateral = PickupPlacement.ClearLateral(point, lateralLimit * side);
        Vector3 position = point.Position + point.Right * lateral + point.Up * 0.32f;
        GameObject pickup = OutlawAmmoPickup.Create(position, point.Up, shooting);
        pickup.transform.SetParent(transform, true);
        active.Add(pickup);
        tracked.Add(pickup.GetComponent<OutlawAmmoPickup>());
        GameEvents.RaisePickupSpawned(PickupKind.Ammo);
        return true;
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        return road.TryGetPointAhead(player.position, distance, out point);
    }
}
