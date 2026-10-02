using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class ObstacleSpawner : MonoBehaviour
{
    private const float SampleStep = 5f;

    [Header("References")]
    [SerializeField] private InfiniteRoad road;
    [SerializeField] private Transform car;
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Spawning")]
    [SerializeField, Min(1f)] private float spawnDistance = 80f;
    [SerializeField, Min(1f)] private float minSpacing = 12;
    [SerializeField, Min(1f)] private float maxSpacing = 20;
    [SerializeField, Range(0f, 1f)] private float doubleRowChance = 0.35f;
    [SerializeField, Min(0f)] private float minRowSeparation = 4f;
    [SerializeField, Min(0f)] private float edgeMargin = 1f;
    [SerializeField, Min(0f)] private float despawnDistance = 15f;

    private readonly List<Obstacle> activeObstacles = new List<Obstacle>();
    private ObjectPool<Obstacle> pool;
    private System.Random random;
    private Vector3 previousCarPosition;
    private float distanceUntilSpawn;

    private void Awake()
    {
        random = new System.Random(19019);

        if (!HasValidSetup())
        {
            enabled = false;
            return;
        }

        pool = new ObjectPool<Obstacle>(
            createFunc: () => new Obstacle(Instantiate(obstaclePrefab, transform)),
            actionOnGet: obstacle => obstacle.GameObject.SetActive(true),
            actionOnRelease: obstacle => obstacle.GameObject.SetActive(false),
            actionOnDestroy: obstacle => Destroy(obstacle.GameObject));

        previousCarPosition = car.position;
    }

    private void Update()
    {
        RecycleBehindCar();

        distanceUntilSpawn -= Vector3.Distance(car.position, previousCarPosition);
        previousCarPosition = car.position;

        while (distanceUntilSpawn <= 0f)
        {
            TrySpawn();
            distanceUntilSpawn += NextRange(minSpacing, maxSpacing);
        }
    }

    private bool HasValidSetup()
    {
        if (road == null)
        {
            LogError("road reference is missing.");
            return false;
        }

        if (car == null)
        {
            LogError("car reference is missing.");
            return false;
        }

        if (obstaclePrefab == null)
        {
            LogError("obstacle prefab reference is missing.");
            return false;
        }

        if (obstaclePrefab.GetComponentInChildren<Renderer>() == null)
        {
            LogError("the obstacle prefab needs a Renderer.", obstaclePrefab);
            return false;
        }

        return true;
    }

    private void TrySpawn()
    {
        if (!TryGetPointAhead(out RoadPathPoint point))
        {
            return;
        }

        float limit = Mathf.Max(0f, point.Width * 0.5f - edgeMargin);
        float firstOffset = NextRange(-limit, limit);

        PlaceObstacle(point, firstOffset);

        if (random.NextDouble() < doubleRowChance)
        {
            PlaceObstacle(point, GetSecondOffset(firstOffset, limit));
        }
    }

    private void PlaceObstacle(RoadPathPoint point, float lateralOffset)
    {
        Obstacle obstacle = pool.Get();
        obstacle.Transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);

        float halfHeight = obstacle.Renderer.bounds.extents.y;
        obstacle.Transform.position = point.Position + point.Right * lateralOffset + point.Up * halfHeight;

        activeObstacles.Add(obstacle);
    }

    private float GetSecondOffset(float firstOffset, float limit)
    {
        float offset = NextRange(-limit, limit);

        if (Mathf.Abs(offset - firstOffset) >= minRowSeparation)
        {
            return offset;
        }

        float direction = offset >= firstOffset ? 1f : -1f;
        float pushed = firstOffset + direction * minRowSeparation;

        if (Mathf.Abs(pushed) > limit)
        {
            pushed = firstOffset - direction * minRowSeparation;
        }

        return Mathf.Clamp(pushed, -limit, limit);
    }

    private bool TryGetPointAhead(out RoadPathPoint point)
    {
        if (!road.TryGetPathPoint(car.position, out point))
        {
            return false;
        }

        float travelled = 0f;

        while (travelled < spawnDistance)
        {
            float step = Mathf.Min(SampleStep, spawnDistance - travelled);

            if (!road.TryGetPathPoint(point.Position + point.Forward * step, out point))
            {
                return false;
            }

            travelled += step;
        }

        return true;
    }

    private void RecycleBehindCar()
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obstacle = activeObstacles[i];

            if (obstacle.GameObject == null)
            {
                activeObstacles.RemoveAt(i);
                continue;
            }

            if (Vector3.Dot(car.position - obstacle.Transform.position, car.forward) > despawnDistance)
            {
                pool.Release(obstacle);
                activeObstacles.RemoveAt(i);
            }
        }
    }

    private float NextRange(float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)random.NextDouble();
    }

    private void LogError(string message, Object context = null)
    {
        Debug.LogError($"{nameof(ObstacleSpawner)}: {message}", context != null ? context : this);
    }

    private sealed class Obstacle
    {
        public Obstacle(GameObject gameObject)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = gameObject.GetComponentInChildren<Renderer>();
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Renderer Renderer { get; }
    }
}
