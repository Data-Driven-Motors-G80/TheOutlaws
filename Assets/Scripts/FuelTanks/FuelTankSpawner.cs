using UnityEngine;

public class FuelTankSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;

    [Header("Spawning")]
    // Spawn distance is in front of the car 50m per fuel tank
    // Spawn direction is based on the car's forward vector
    [SerializeField, Min(0f)] private float spawnDistance = 50f;

    [Header("Fuel Tank")]
    [SerializeField] private GameObject fuelTankPrefab;
    [SerializeField, Min(0f)] private float lifetime = 10f;


    [SerializeField] private float heightOffset = 0.8f;
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

        previousCarPosition = car.position;
        distanceUntilSpawn = spawnDistance;

        // Keep the stationary pickup trigger in the player's driving path.
        // Only the visual model bobs; the car should never pass underneath it.
        heightOffset = Mathf.Min(heightOffset, 0.35f);
    }

    private void Update()
    {
        distanceUntilSpawn -= Vector3.Distance(car.position, previousCarPosition);
        previousCarPosition = car.position;

        if (distanceUntilSpawn <= 0f)
        {
            SpawnFuelTank();
            distanceUntilSpawn = spawnDistance;
        }
    }

    private void SpawnFuelTank()
    {
        GameObject tank = Instantiate(fuelTankPrefab, car.position + car.forward * spawnDistance + car.up * heightOffset, Quaternion.identity);
        Destroy(tank, lifetime); // Destroy the fuel tank after it has been passed by the car
    }
}
