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
    private System.Random sizeRandom;
    private System.Random colorRandom;
    private System.Random roadblockRandom;
    private Vector3 previousCarPosition;
    private float distanceUntilSpawn;
    private AutoDriveCar driver;
    private bool useFairRows;
    private float density = 1f;
    private float difficultRowChance;
    private int previousOpenLane = 1;
    private float previousRoadDistance;
    private bool firstFairRow = true;
    private int tutorialRowsSpawned;
    private float nextRoadblockAt = TrafficRoadblockRules.FirstRoadblockDistance;

    public void ConfigureDifficulty(float obstacleDensity, float twoObstacleChance)
    {
        useFairRows = true;
        density = Mathf.Clamp(obstacleDensity, 0.5f, 2f);
        difficultRowChance = Mathf.Clamp01(twoObstacleChance);
    }

    private float NextSpacing()
    {
        if (!useFairRows) return NextRange(minSpacing, maxSpacing);
        // Allow steering and the length of both vehicles, even as the car speeds up.
        float speed = driver != null ? driver.ForwardSpeed : 15f;
        if (driver != null && driver.TryGetComponent(out CarPickupEffects effects))
            speed *= effects.RunState.ForwardSpeedMultiplier;
        return Mathf.Max(NextRange(32f, 42f) / density, speed * 1.3f + 4f);
    }

    private void Awake()
    {
        random = new System.Random(19019);
        sizeRandom = new System.Random(19023);
        colorRandom = new System.Random(19029);
        roadblockRandom = new System.Random(19033);

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

        driver = car.GetComponent<AutoDriveCar>();
        previousCarPosition = car.position;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        RecycleBehindCar();

        float travelled = driver != null
            ? Mathf.Max(0f, driver.RoadDistanceTravelled - previousRoadDistance)
            : Vector3.Distance(car.position, previousCarPosition);
        if (driver != null) previousRoadDistance = driver.RoadDistanceTravelled;
        distanceUntilSpawn -= travelled;
        previousCarPosition = car.position;

        if (distanceUntilSpawn <= 0f)
        {
            bool spawnedSpecialRow = TrySpawn();
            // After a long frame, do not stack catch-up rows at the same position.
            distanceUntilSpawn = spawnedSpecialRow ? 45f : NextSpacing();
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

    private bool TrySpawn()
    {
        if (!TryGetPointAhead(out RoadPathPoint point))
        {
            return false;
        }

        if (tutorialRowsSpawned < 2)
        {
            TrafficObstacleState tutorialState = tutorialRowsSpawned == 0
                ? TrafficObstacleState.Green
                : TrafficObstacleState.Yellow;
            SpawnFullWidthBarrier(point, tutorialState);
            tutorialRowsSpawned++;
            return true;
        }

        float distance = driver != null ? driver.RoadDistanceTravelled : 0f;
        if (distance >= nextRoadblockAt)
        {
            SpawnRoadblock(point, TrafficRoadblockRules.ChoosePattern(
                distance, roadblockRandom.NextDouble()));
            nextRoadblockAt = distance + TrafficRoadblockRules.GetNextSpacing(
                distance, roadblockRandom.NextDouble());
            return true;
        }

        if (useFairRows)
        {
            SpawnFairRow(point);
            return false;
        }

        float limit = Mathf.Max(0f, point.Width * 0.5f - edgeMargin);
        float firstOffset = NextRange(-limit, limit);

        PlaceObstacle(point, firstOffset);

        if (random.NextDouble() < doubleRowChance)
        {
            PlaceObstacle(point, GetSecondOffset(firstOffset, limit));
        }
        return false;
    }

    private void SpawnRoadblock(RoadPathPoint point, TrafficRoadblockPattern pattern)
    {
        float laneWidth = point.Width / 3f;
        switch (pattern)
        {
            case TrafficRoadblockPattern.TwoLaneGreen:
            case TrafficRoadblockPattern.TwoLaneYellow:
                TrafficObstacleState twoLaneState = pattern == TrafficRoadblockPattern.TwoLaneGreen
                    ? TrafficObstacleState.Green
                    : TrafficObstacleState.Yellow;
                float side = roadblockRandom.Next(2) == 0 ? -1f : 1f;
                PlaceObstacle(point, side * laneWidth * 0.5f,
                    laneWidth * 2f, twoLaneState);
                break;
            case TrafficRoadblockPattern.FullGreen:
                SpawnFullWidthBarrier(point, TrafficObstacleState.Green);
                break;
            case TrafficRoadblockPattern.FullYellow:
                SpawnFullWidthBarrier(point, TrafficObstacleState.Yellow);
                break;
            default:
                SpawnMixedBarrier(point);
                break;
        }
    }

    private void SpawnFullWidthBarrier(RoadPathPoint point, TrafficObstacleState state)
    {
        PlaceObstacle(point, 0f, Mathf.Max(0.8f, point.Width - 0.1f), state);
    }

    private void SpawnMixedBarrier(RoadPathPoint point)
    {
        float laneWidth = point.Width / 3f;
        TrafficObstacleState[] states =
        {
            TrafficObstacleState.Red,
            TrafficObstacleState.Yellow,
            TrafficObstacleState.Green
        };
        for (int i = states.Length - 1; i > 0; i--)
        {
            int swap = roadblockRandom.Next(i + 1);
            TrafficObstacleState value = states[i];
            states[i] = states[swap];
            states[swap] = value;
        }
        for (int lane = 0; lane < 3; lane++)
            PlaceObstacle(point, (lane - 1) * laneWidth,
                laneWidth + 0.08f, states[lane]);
    }

    private void SpawnFairRow(RoadPathPoint point)
    {
        // Keep the guaranteed route in the same or an adjacent lane, never force
        // a two-lane swerve between consecutive rows.
        int openLane = random.Next(Mathf.Max(0, previousOpenLane - 1),
            Mathf.Min(2, previousOpenLane + 1) + 1);
        int firstLane = (openLane + 1 + random.Next(2)) % 3;
        if (firstFairRow)
        {
            // Teach the first dodge explicitly instead of letting a stationary
            // player coast through a randomly empty centre lane.
            openLane = random.Next(2) == 0 ? 0 : 2;
            firstLane = 1;
            firstFairRow = false;
        }
        previousOpenLane = openLane;
        float laneWidth = point.Width / 3f;
        float gapCenter = (openLane - 1) * laneWidth;
        const float gapHalfWidth = 1.4f;
        bool doubleRow = random.NextDouble() < difficultRowChance;
        int secondLane = 3 - openLane - firstLane;
        PlaceWideObstacle(point, firstLane, doubleRow ? secondLane : -1, gapCenter, gapHalfWidth);
        if (doubleRow)
            PlaceWideObstacle(point, secondLane, firstLane, gapCenter, gapHalfWidth);
    }

    private void PlaceWideObstacle(RoadPathPoint point, int lane, int otherLane,
        float gapCenter, float gapHalfWidth)
    {
        float preferredCenter = (lane - 1) * point.Width / 3f;
        bool leftOfGap = preferredCenter < gapCenter;
        float minimum = leftOfGap ? -point.Width * 0.5f : gapCenter + gapHalfWidth;
        float maximum = leftOfGap ? gapCenter - gapHalfWidth : point.Width * 0.5f;
        if (otherLane >= 0)
        {
            float otherCenter = (otherLane - 1) * point.Width / 3f;
            if ((otherCenter < gapCenter) == leftOfGap)
            {
                // Two blocks on the same side share the space without overlapping.
                float split = (preferredCenter + otherCenter) * 0.5f;
                if (preferredCenter < otherCenter) maximum = Mathf.Min(maximum, split - 0.15f);
                else minimum = Mathf.Max(minimum, split + 0.15f);
            }
        }
        float availableWidth = maximum - minimum;
        if (availableWidth < 0.8f) return;
        float width = SizeRange(0.8f, Mathf.Min(4.8f, availableWidth));
        // Lane indices only guide the route. The blocks can straddle painted lines.
        float center = Mathf.Clamp(preferredCenter + SizeRange(-0.6f, 0.6f),
            minimum + width * 0.5f, maximum - width * 0.5f);
        PlaceObstacle(point, center, width);
    }

    private void PlaceObstacle(
        RoadPathPoint point,
        float lateralOffset,
        float worldWidth = -1f,
        TrafficObstacleState? forcedState = null)
    {
        Obstacle obstacle = pool.Get();
        // Always start from the prefab scale so pooled blocks do not keep growing.
        float width = worldWidth > 0f ? worldWidth : SizeRange(0.8f, 4.8f);
        float widthScale = width / Mathf.Max(0.01f, obstacle.BaseWidth);
        obstacle.Transform.localScale = Vector3.Scale(obstacle.BaseScale,
            new Vector3(widthScale, SizeRange(0.65f, 2f), SizeRange(0.8f, 1.2f)));
        obstacle.Transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);

        float halfHeight = obstacle.Renderer.bounds.extents.y;
        obstacle.Transform.position = point.Position + point.Right * lateralOffset + point.Up * halfHeight;
        float distance = driver != null ? driver.RoadDistanceTravelled : 0f;
        obstacle.TrafficLight.Initialize(forcedState ??
            TrafficObstacleRules.ChooseInitialState(distance, colorRandom.NextDouble()));

        activeObstacles.Add(obstacle);
    }

    public bool TryApplyShot(Collider hitCollider)
    {
        if (hitCollider == null) return false;
        TrafficLightObstacle trafficLight = hitCollider.GetComponentInParent<TrafficLightObstacle>();
        if (trafficLight == null || trafficLight.transform.parent != transform) return false;

        Obstacle obstacle = activeObstacles.Find(item => item.Transform == trafficLight.transform);
        if (obstacle == null) return false;

        if (trafficLight.ApplyShot())
        {
            OutlawShooting shooting = car != null ? car.GetComponent<OutlawShooting>() : null;
            ObstacleRewardSpawner.SpawnRedDestructionReward(trafficLight, shooting);
            activeObstacles.Remove(obstacle);
            pool.Release(obstacle);
        }

        return true;
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

        float distanceAhead = useFairRows ? Mathf.Min(spawnDistance, 50f) : spawnDistance;
        while (travelled < distanceAhead)
        {
            float step = Mathf.Min(SampleStep, distanceAhead - travelled);

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

    private float SizeRange(float minimum, float maximum)
    {
        return minimum + (maximum - minimum) * (float)sizeRandom.NextDouble();
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
            TrafficLight = gameObject.GetComponent<TrafficLightObstacle>();
            if (TrafficLight == null) TrafficLight = gameObject.AddComponent<TrafficLightObstacle>();
            BaseScale = Transform.localScale;
            BaseWidth = Renderer.bounds.size.x;
        }

        public Vector3 BaseScale { get; }
        public float BaseWidth { get; }
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Renderer Renderer { get; }
        public TrafficLightObstacle TrafficLight { get; }
    }
}
