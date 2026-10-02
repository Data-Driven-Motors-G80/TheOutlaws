using System.Collections.Generic;
using UnityEngine;

public sealed class OutlawPickupSpawner : MonoBehaviour
{
    private const float FirstSpawnDistance = 45f;
    private const float SpawnSpacing = 120f;
    private const int MaximumActive = 4;

    private readonly List<GameObject> active = new List<GameObject>();
    private InfiniteRoad road;
    private Transform player;
    private AutoDriveCar driver;
    private OutlawShooting shooting;
    private CarPickupEffects pickupEffects;
    private float nextSpawnAt;
    private int spawnIndex;

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
    }

    private void Update()
    {
        if (road == null || player == null || driver == null || shooting == null ||
            (pickupEffects != null && pickupEffects.RunState.IsGameOver))
        {
            return;
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
            if (Vector3.Dot(player.position - pickup.transform.position, player.forward) > 18f ||
                Vector3.Distance(player.position, pickup.transform.position) > 140f)
            {
                Destroy(pickup);
                active.RemoveAt(i);
            }
        }

        bool needsGuaranteedPickup = active.Count == 0;
        bool reachedScheduledSpawn = driver.RoadDistanceTravelled >= nextSpawnAt;
        if ((!needsGuaranteedPickup && !reachedScheduledSpawn) || active.Count >= MaximumActive)
        {
            return;
        }

        float distanceAhead = needsGuaranteedPickup ? FirstSpawnDistance : 65f;
        if (TrySpawnAmmo(distanceAhead))
        {
            nextSpawnAt = driver.RoadDistanceTravelled + SpawnSpacing;
        }
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
        float side = spawnIndex++ % 2 == 0 ? -0.65f : 0.65f;
        // Keep the pickup low enough to overlap the player's reduced collider.
        // The previous 0.75 offset let the car pass underneath the trigger.
        Vector3 position = point.Position + point.Right * lateralLimit * side + point.Up * 0.32f;
        GameObject pickup = OutlawAmmoPickup.Create(position, point.Up, shooting);
        pickup.transform.SetParent(transform, true);
        active.Add(pickup);
        return true;
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        if (!road.TryGetPathPoint(player.position, out point))
        {
            return false;
        }

        for (float travelled = 0f; travelled < distance; travelled += 5f)
        {
            float step = Mathf.Min(5f, distance - travelled);
            if (!road.TryGetPathPoint(point.Position + point.Forward * step, out point))
            {
                return false;
            }
        }
        return true;
    }
}
