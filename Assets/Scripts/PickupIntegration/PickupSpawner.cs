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
    private System.Random random;
    private Vector3 previousPosition;
    private float untilNext;
    private int spawnCount;
    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private float previousRoadDistance;
    private readonly HashSet<PickupItem> reported = new HashSet<PickupItem>();
    private readonly List<PickupItem> placementChecks = new List<PickupItem>();

    /// <summary>While true (the tutorial), no mystery boxes appear.</summary>
    public bool Suppressed { get; set; }

    /// <summary>Removes boxes already on the road and pauses spawning (tutorial start).</summary>
    public void ClearAndSuppress()
    {
        Suppressed = true;
        foreach (PickupItem item in GetComponentsInChildren<PickupItem>(true))
            Destroy(item.gameObject);
        active.Clear();
    }

    /// <summary>No ? boxes for the first 500 m of a run, then one every ~320 m (and only one at a time).</summary>
    private const float FirstSpawnDistance = 500f;
    private const float MinimumSpacing = 320f;

    public void BeginRealRun()
    {
        Suppressed = false;
        untilNext = FirstSpawnDistance;
        previousPosition = car.position;
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
    }

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
        // A run carried over from the other environment keeps normal spacing; a new run waits 500 m.
        untilNext = RunSession.HasPending ? Mathf.Max(spacing, MinimumSpacing) : FirstSpawnDistance;
    }

    private void Update()
    {
        // Confirm last frame's spawns are clear of blocks placed in the same frame.
        foreach (PickupItem item in placementChecks)
            if (item != null) PickupPlacement.EnsureClear(item.transform);
        placementChecks.Clear();

        if (pickupEffects != null && pickupEffects.isActiveAndEnabled && pickupEffects.RunState.IsGameOver)
            return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            PickupItem item = active[i];
            if (item != null && !item.Collected && !reported.Contains(item)
                && road.IsBehind(item.transform.position, car.position, 4f))
            {
                reported.Add(item);
                GameEvents.RaisePickupMissed(PickupKind.Mystery);
            }
            if (item != null && road.IsBehind(item.transform.position, car.position, 20f))
                Destroy(item.gameObject);
            else if (item != null) continue;
            reported.Remove(item);
            active.RemoveAt(i);
        }
        if (Suppressed || Time.timeScale <= 0f) return;

        float travelled = driver != null
            ? Mathf.Max(0f, driver.RoadDistanceTravelled - previousRoadDistance)
            : Mathf.Max(0f, Vector3.Dot(car.position - previousPosition, car.forward));
        untilNext -= travelled;
        previousPosition = car.position;
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
        if (untilNext > 0f) return;
        // One power-up at a time: wait while one is active or a ? box is still on the road.
        if (active.Count > 0 || (pickupEffects != null && pickupEffects.HasActivePowerUp)) return;
        SpawnAhead(Mathf.Max(spawnDistance, PickupLookAhead.Distance));
        untilNext = Mathf.Max(spacing, MinimumSpacing);
    }

    private void SpawnAhead(float distance)
    {
        if (active.Count >= 8 || !TryGetPointAhead(distance, out RoadPathPoint point)) return;
        float limit = Mathf.Max(0f, point.Width * 0.5f - 1.5f);
        // The first pickup is deliberately off the starting line: collecting it is a choice.
        float desired = spawnCount == 0 ? limit * 0.75f : ((float)random.NextDouble() * 2f - 1f) * limit;
        float lateral = PickupPlacement.ClearLateral(point, desired);
        Vector3 position = point.Position + point.Right * lateral + point.Up * 0.6f;

        PickupItem item = Instantiate(pickupPrefab, position, Quaternion.identity, transform);
        // Weighted outcomes: 30% each for nitro, recovery and shield; 10% reverse steering.
        item.Configure(PickupEffectType.RandomFuelOrReverse, effectDuration);
        Renderer box = item.GetComponentInChildren<Renderer>();
        if (box != null) WorldLabel.Attach(box, "?", new Color(1f, 0.85f, 0.2f));
        active.Add(item);
        placementChecks.Add(item);
        GameEvents.RaisePickupSpawned(PickupKind.Mystery);
        spawnCount++;
    }

    private bool TryGetPointAhead(float distance, out RoadPathPoint point)
    {
        return road.TryGetPointAhead(car.position, distance, out point);
    }
}
