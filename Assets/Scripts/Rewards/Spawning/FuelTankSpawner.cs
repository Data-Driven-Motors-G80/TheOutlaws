// Offers fuel along clear routes, increasing frequency as fuel drops.
using System.Collections.Generic;
using UnityEngine;

public class FuelTankSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;

    [Header("Spawning")]

    [SerializeField, Min(1f)] private float spawnSpacing = 125f;
    [SerializeField, Min(0f)] private float spawnDistance = 50f;

    [Header("Fuel Tank")]
    [SerializeField] private GameObject fuelTankPrefab;
    [SerializeField, Min(0f)] private float lifetime = 10f;

    [SerializeField] private float heightOffset = 0.8f;
    private OutlawGameManager game;
    private float nextFuelEncounterAt = -1f;
    private float fuelScheduleAnchor;
    private float fuelScheduleBaseGap = 6.5f;
    private float FuelIntervalMultiplier => Mathf.Lerp(0.7f, 1f,
        Mathf.Clamp01(game.Fuel.NormalizedFuel / 0.75f));
    public void ConfigurePacing(OutlawGameManager manager) => game = manager;

    public bool ConflictsWithFuelRoute(float from, float to)
    {
        if (road == null) return false;
        foreach (FuelTank tank in tracked)
            if (tank != null && !tank.Collected && road.TryGetDistance(tank.transform.position, out float at)
                && at >= from - 10f && at <= to + 10f) return true;
        return false;
    }

    // Shorten future intervals as the tank empties without delaying an existing offer.
    private void UpdateTimedFuel()
    {
        if (game.State != OutlawGameState.Running || game.IsTutorial || driver.IsAirborne
            || game.PickupEffects.RunState.IsRoadRecovering) return;
        float now = game.ElapsedSeconds;
        if (nextFuelEncounterAt < 0f)
        {
            fuelScheduleAnchor = now;
            fuelScheduleBaseGap = 6.5f;
            nextFuelEncounterAt = now + fuelScheduleBaseGap * FuelIntervalMultiplier;
        }
        nextFuelEncounterAt = Mathf.Min(nextFuelEncounterAt,
            fuelScheduleAnchor + fuelScheduleBaseGap * FuelIntervalMultiplier);
        float speed = game.ActualDrivingSpeed;
        bool critical = !game.PickupEffects.HarderDifficultyActive && game.Fuel.CurrentFuel < 25f;
        float previewDistance = critical ? Mathf.Min(PickupLookAhead.Distance, speed * 2f) : PickupLookAhead.Distance;
        if (critical) nextFuelEncounterAt = Mathf.Min(nextFuelEncounterAt, now + 2f);
        if (now + previewDistance / speed < nextFuelEncounterAt) return;
        int ahead = 0;
        foreach (FuelTank tank in tracked)
            if (tank != null && !tank.Collected && !road.IsBehind(tank.transform.position, car.position, 1f)) ahead++;
        if (ahead >= 2) return;
        float lead = Mathf.Max(previewDistance, (nextFuelEncounterAt - now) * speed);
        for (float extra = 0f; extra <= 40f; extra += 10f)
        {
            if (!road.TryGetPointAhead(car.position, lead + extra, out RoadPathPoint point)
                || !road.TryGetDistance(point.Position, out float at)) return;
            if (!game.IsFuelRouteClear(at)) continue;
            if (SpawnAhead(lead + extra) != null)
            {
                fuelScheduleAnchor = now + (lead + extra) / speed;
                fuelScheduleBaseGap = Random.Range(6.3f, 6.7f);
                nextFuelEncounterAt = fuelScheduleAnchor + fuelScheduleBaseGap * FuelIntervalMultiplier;
            }
            return;
        }
    }

    private InfiniteRoad road;
    private AutoDriveCar driver;
    private float previousRoadDistance;
    private Vector3 previousCarPosition;
    private float distanceUntilSpawn;
    private readonly List<FuelTank> tracked = new List<FuelTank>();

    public bool Suppressed { get; set; }

    public void BeginRealRun()
    {
        Suppressed = false;
        distanceUntilSpawn = spawnSpacing;
        nextFuelEncounterAt = -1f;
    }

    private void Awake()
    {
        if (car == null || fuelTankPrefab == null)
        {
            Debug.LogError($"{nameof(FuelTankSpawner)}: the car and the fuel tank prefab must be assigned.", this);
            enabled = false;
            return;
        }

        road = FindFirstObjectByType<InfiniteRoad>();
        driver = car.GetComponent<AutoDriveCar>();
        previousRoadDistance = driver != null ? driver.RoadDistanceTravelled : 0f;
        previousCarPosition = car.position;
        distanceUntilSpawn = spawnSpacing;

        heightOffset = Mathf.Min(heightOffset, 0.35f);
    }

    private void Update()
    {
        distanceUntilSpawn -= driver != null
            ? Mathf.Max(0f, driver.RoadDistanceTravelled - previousRoadDistance)
            : Vector3.Distance(car.position, previousCarPosition);
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
        previousCarPosition = car.position;
        TrackMissedTanks();
        if (Suppressed || Time.timeScale <= 0f)
        {
            distanceUntilSpawn = Mathf.Max(distanceUntilSpawn, 0f);
            return;
        }

        if (game != null && game.UsesCoordinatedPacing)
        {
            UpdateTimedFuel();
            return;
        }
        if (distanceUntilSpawn <= 0f)
        {
            if (SpawnAhead(Mathf.Max(spawnDistance, PickupLookAhead.Distance)) != null) distanceUntilSpawn = spawnSpacing;
        }
    }

    private void TrackMissedTanks()
    {
        PickupTracking.ReportMissed(tracked, road, car, PickupKind.Fuel, tank => tank.Collected);
    }

    // Place fuel on the road and avoid blocked collection routes.
    public GameObject SpawnAhead(float distanceAhead, float? lateralOverride = null)
    {
        if (road == null || !road.TryGetPointAhead(car.position, distanceAhead, out RoadPathPoint point)) return null;
        road.TryGetPathPoint(car.position, out RoadPathPoint current);
        float lateral = Mathf.Clamp(lateralOverride ?? Vector3.Dot(car.position - current.Position, current.Right),
            -point.Width * 0.5f + 1f, point.Width * 0.5f - 1f);

        if (lateralOverride == null) lateral = PickupPlacement.ClearLateral(point, lateral);
        GameObject tank = Instantiate(fuelTankPrefab,
            point.Position + point.Right * lateral + point.Up * heightOffset,
            Quaternion.LookRotation(point.Forward, point.Up));
        Destroy(tank, Mathf.Max(lifetime, 14f));
        FuelTank fuelTank = tank.GetComponent<FuelTank>();
        if (fuelTank != null) tracked.Add(fuelTank);
        GameEvents.RaisePickupSpawned(PickupKind.Fuel);
        return tank;
    }
}
