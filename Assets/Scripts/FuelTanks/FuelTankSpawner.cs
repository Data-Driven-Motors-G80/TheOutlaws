using UnityEngine;

public class FuelTankSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;

    [Header("Spawning")]
    // Keep spacing independent of how far ahead the pickup appears.
    [SerializeField, Min(1f)] private float spawnSpacing = 125f;
    [SerializeField, Min(0f)] private float spawnDistance = 50f;

    [Header("Fuel Tank")]
    [SerializeField] private GameObject fuelTankPrefab;
    [SerializeField, Min(0f)] private float lifetime = 10f;


    [SerializeField] private float heightOffset = 0.8f;
    private InfiniteRoad road;
    private AutoDriveCar driver;
    private float previousRoadDistance;
    private Vector3 previousCarPosition;
    private float distanceUntilSpawn;


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

        // Keep the stationary pickup trigger in the player's driving path.
        // Only the visual model bobs; the car should never pass underneath it.
        heightOffset = Mathf.Min(heightOffset, 0.35f);
    }

    private void Update()
    {
        distanceUntilSpawn -= driver != null
            ? Mathf.Max(0f, driver.RoadDistanceTravelled - previousRoadDistance)
            : Vector3.Distance(car.position, previousCarPosition);
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
        previousCarPosition = car.position;

        if (distanceUntilSpawn <= 0f)
        {
            if (SpawnFuelTank()) distanceUntilSpawn = spawnSpacing;
        }
    }

    private bool SpawnFuelTank()
    {
        if (road == null || !road.TryGetPointAhead(car.position, spawnDistance, out RoadPathPoint point)) return false;
        road.TryGetPathPoint(car.position, out RoadPathPoint current);
        float lateral = Mathf.Clamp(Vector3.Dot(car.position - current.Position, current.Right),
            -point.Width * 0.5f + 1f, point.Width * 0.5f - 1f);
        GameObject tank = Instantiate(fuelTankPrefab,
            point.Position + point.Right * lateral + point.Up * heightOffset,
            Quaternion.LookRotation(point.Forward, point.Up));
        Destroy(tank, lifetime);
        return true;
    }
}
