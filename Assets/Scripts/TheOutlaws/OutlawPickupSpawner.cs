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

    /// <summary>While true (the tutorial), scheduled ammo does not appear.</summary>
    public bool Suppressed { get; set; }

    /// <summary>Real-run distance, which excludes the tutorial and carries across scenes.</summary>
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
        // Remove the old free starting pickup; all ammo now follows the schedule.
        foreach (OutlawAmmoPickup pickup in GetComponentsInChildren<OutlawAmmoPickup>(true))
        {
            pickup.gameObject.SetActive(false);
            Destroy(pickup.gameObject);
        }
        carriedDistance = RunSession.TryGetPending(out RunSnapshot carried) ? carried.Distance : 0f;
        spawnIndex = 0;
        // Independent entropy keeps ammo unpredictable on restart without changing
        // the seeded obstacle layouts or power-up outcomes.
        spacingRandom = new System.Random(System.Guid.NewGuid().GetHashCode());
        // Reveal the pickup early so its actual road position follows the
        // distance-based schedule rather than appearing directly on the car.
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

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            OutlawAmmoPickup pickup = tracked[i];
            if (pickup.Collected)
            {
                tracked.RemoveAt(i);
            }
            else if (pickup == null || road.IsBehind(pickup.transform.position, player.position, 4f))
            {
                tracked.RemoveAt(i);
                GameEvents.RaisePickupMissed(PickupKind.Ammo);
            }
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            GameObject pickup = active[i];
            if (pickup == null)
            {
                active.RemoveAt(i);
                continue;
            }

            // Do not let missed pickups behind the car occupy all active slots.
            if (road.IsBehind(pickup.transform.position, player.position, 18f) ||
                Vector3.Distance(player.position, pickup.transform.position) > 140f)
            {
                Destroy(pickup);
                active.RemoveAt(i);
            }
        }

        // Missed or collected ammo must not trigger an immediate replacement.
        // Sample one distance-appropriate gap per successful spawn, never every frame.
        if (Suppressed || driver.RoadDistanceTravelled < nextSpawnAt || active.Count >= MaximumActive)
        {
            return;
        }

        if (TrySpawnAmmo(SpawnAheadDistance))
        {
            nextSpawnAt = driver.RoadDistanceTravelled + NextSpawnSpacing();
        }
    }

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
            // The infinite road may still be extending. Leave the schedule
            // unchanged so Update retries instead of skipping this pickup.
            return false;
        }

        float lateralLimit = Mathf.Max(0f, point.Width * 0.5f - 1.5f);
        float side = spawnIndex % 2 == 0 ? -0.9f : 0.9f;
        spawnIndex++;
        // Keep the pickup low enough to overlap the player's reduced collider.
        // The previous 0.75 offset let the car pass underneath the trigger.
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
