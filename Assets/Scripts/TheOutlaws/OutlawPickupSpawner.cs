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
    private float nextSpawnAt = FirstSpawnDistance;
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
            if (active[i] == null)
            {
                active.RemoveAt(i);
            }
        }

        if (driver.RoadDistanceTravelled < nextSpawnAt || active.Count >= MaximumActive)
        {
            return;
        }

        SpawnAmmo();
        nextSpawnAt += SpawnSpacing;
    }

    private void SpawnAmmo()
    {
        if (!TryGetPointAhead(65f, out RoadPathPoint point))
        {
            return;
        }

        float lateralLimit = Mathf.Max(0f, point.Width * 0.5f - 1.5f);
        float side = spawnIndex++ % 2 == 0 ? -0.65f : 0.65f;
        Vector3 position = point.Position + point.Right * lateralLimit * side + point.Up * 0.75f;
        GameObject pickup = OutlawAmmoPickup.Create(position, point.Up, shooting);
        pickup.transform.SetParent(transform, true);
        active.Add(pickup);
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
