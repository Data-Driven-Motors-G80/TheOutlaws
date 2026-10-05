using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PickupSpawner : MonoBehaviour
{
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform car;
    [SerializeField] private PickupItem pickupPrefab;
    [SerializeField] private ObstacleSpawner obstacles;
    [SerializeField, Min(5f)] private float spawnDistance = 55f;
    [SerializeField, Min(5f)] private float spacing = 160f;
    [SerializeField, Min(0.1f)] private float effectDuration = 5f;
    [SerializeField] private bool cycleEffectsForTesting;

    private readonly List<PickupItem> active = new List<PickupItem>();
    private readonly Collider[] overlaps = new Collider[32];
    private System.Random random;
    private Vector3 previousPosition;
    private float untilNext;
    private int spawnCount;
    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private float previousRoadDistance;

    private void Start()
    {
        random = new System.Random(19022);

        if (road == null || car == null || pickupPrefab == null)
        {
            Debug.LogError("PickupSpawner needs the road, car and pickup prefab.", this);
            enabled = false;
            return;
        }
        previousPosition = car.position;
        driver = car.GetComponent<AutoDriveCar>();
        pickupEffects = car.GetComponent<CarPickupEffects>();
        previousRoadDistance = driver != null ? driver.RoadDistanceTravelled : 0f;
        SpawnAhead(Mathf.Min(20f, spawnDistance));
        untilNext = spacing;
    }

    private void Update()
    {
        if (pickupEffects != null && pickupEffects.isActiveAndEnabled && pickupEffects.RunState.IsGameOver)
            return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            PickupItem item = active[i];
            if (item != null && Vector3.Dot(car.position - item.transform.position, car.forward) > 20f)
                Destroy(item.gameObject);
            else if (item != null) continue;
            active.RemoveAt(i);
        }

        float travelled = driver != null
            ? Mathf.Max(0f, driver.RoadDistanceTravelled - previousRoadDistance)
            : Mathf.Max(0f, Vector3.Dot(car.position - previousPosition, car.forward));
        untilNext -= travelled;
        previousPosition = car.position;
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
        if (untilNext > 0f) return;
        SpawnAhead(spawnDistance);
        untilNext = Mathf.Max(5f, spacing);
    }

    private void SpawnAhead(float distance)
    {
        if (active.Count >= 8 || !TryGetPointAhead(distance, out RoadPathPoint point)) return;
        float limit = Mathf.Max(0f, point.Width * 0.5f - 1.5f);
        // The first pickup is deliberately off the starting line: collecting it is a choice.
        float desired = spawnCount == 0 ? limit * 0.75f : ((float)random.NextDouble() * 2f - 1f) * limit;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            float lateral = attempt == 0 ? desired : (attempt == 1 ? -limit : limit);
            Vector3 position = point.Position + point.Right * lateral + point.Up * 0.6f;
            if (BlockedByObstacle(position)) continue;

            PickupItem item = Instantiate(pickupPrefab, position, Quaternion.identity, transform);
            // Four equally likely outcomes: nitro, jammer, shield, or reverse steering.
            item.Configure(PickupEffectType.RandomFuelOrReverse, effectDuration);
            active.Add(item);
            spawnCount++;
            return;
        }
    }

    private bool BlockedByObstacle(Vector3 position)
    {
        if (obstacles == null) return false;
        int count = Physics.OverlapSphereNonAlloc(position, 2.5f, overlaps, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return true;
        for (int i = 0; i < count; i++)
            if (overlaps[i].transform.IsChildOf(obstacles.transform)) return true;
        return false;
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        if (!road.TryGetPathPoint(car.position, out point)) return false;
        for (float travelled = 0f; travelled < distance; travelled += 5f)
        {
            float step = Mathf.Min(5f, distance - travelled);
            Vector3 previous = point.Position;
            if (!road.TryGetPathPoint(previous + point.Forward * step, out point)
                || Vector3.Distance(previous, point.Position) < step * 0.5f) return false;
        }
        return true;
    }
}
