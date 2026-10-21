using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public class ObstacleSpawner : MonoBehaviour
{

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

    [Header("Environment Behaviour")]
    [SerializeField, Min(1f)] private float rowSpacingMultiplier = 1f;
    [SerializeField, Min(1f)] private float roadblockSpacingMultiplier = 1f;
    [SerializeField] private ObstacleMotion motion;

    public InfiniteRoad RoadReference => road;

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
    private float nextRoadblockAt = TrafficRoadblockRules.FirstRoadblockDistance;

    /// <summary>While true (the tutorial), only scripted rows are placed.</summary>
    public bool SuppressAutoSpawn { get; set; }

    /// <summary>Real-run distance, which excludes the tutorial and carries across scenes.</summary>
    public System.Func<float> RunDistance { get; set; }

    private float CurrentRunDistance => RunDistance != null
        ? RunDistance()
        : (driver != null ? driver.RoadDistanceTravelled : 0f);

    /// <summary>Clears scripted rows and restarts pacing from the start of the real run.</summary>
    public void BeginRealRun()
    {
        SuppressAutoSpawn = false;
        firstFairRow = true;
        previousOpenLane = 1;
        nextRoadblockAt = CurrentRunDistance + TrafficRoadblockRules.FirstRoadblockDistance;
        distanceUntilSpawn = 60f;
    }

    /// <summary>Removes blocks near a point (keeps the portal gate's doorway clear).</summary>
    public void ClearNear(Vector3 position, float radius)
    {
        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obstacle = activeObstacles[i];
            if (obstacle.GameObject == null || (obstacle.Transform.position - position).sqrMagnitude > radius * radius) continue;
            pool.Release(obstacle);
            activeObstacles.RemoveAt(i);
        }
    }

    public bool IsActive(TrafficLightObstacle obstacle)
    {
        if (obstacle == null || !obstacle.gameObject.activeInHierarchy) return false;
        return activeObstacles.Exists(item => item.TrafficLight == obstacle);
    }

    /// <summary>Tutorial: a full-width wall the player must shoot through.</summary>
    public TrafficLightObstacle SpawnTutorialWall(float distanceAhead, TrafficObstacleState state)
    {
        if (!road.TryGetPointAhead(car.position, distanceAhead, out RoadPathPoint point)) return null;
        return SpawnFullWidthBarrier(point, state);
    }

    /// <summary>Tutorial: blocks two lanes, leaving open the lane farthest from the car.</summary>
    public List<TrafficLightObstacle> SpawnTutorialDodgeRow(float distanceAhead) =>
        SpawnTutorialDodgeRow(distanceAhead, out _);

    /// <summary>As above; <paramref name="openLateral"/> is the open lane's offset from the road centre.</summary>
    public List<TrafficLightObstacle> SpawnTutorialDodgeRow(float distanceAhead, out float openLateral)
    {
        var placed = new List<TrafficLightObstacle>();
        openLateral = 0f;
        if (!road.TryGetPointAhead(car.position, distanceAhead, out RoadPathPoint point)) return placed;
        float laneWidth = point.Width / 3f;
        float lateral = road.TryGetPathPoint(car.position, out RoadPathPoint current)
            ? Vector3.Dot(car.position - current.Position, current.Right)
            : 0f;
        float side = lateral >= 0f ? -1f : 1f;
        // A two-lane block on the player's side; the open lane is on the far side.
        placed.Add(PlaceObstacle(point, -side * laneWidth * 0.5f, laneWidth * 2f, TrafficObstacleState.Green));
        openLateral = side * laneWidth;
        return placed;
    }

    private float difficultyElapsed;

    public void ConfigureDifficulty(float obstacleDensity, float twoObstacleChance, float elapsedSeconds = 0f)
    {
        difficultyElapsed = elapsedSeconds;
        useFairRows = true;
        density = Mathf.Clamp(obstacleDensity, 0.5f, 4.5f);
        difficultRowChance = Mathf.Clamp01(twoObstacleChance);
    }

    protected virtual float RowSpacingMultiplier => rowSpacingMultiplier;
    protected virtual float RoadblockSpacingMultiplier => roadblockSpacingMultiplier;
    protected virtual float ExtraClosingSpeed => motion != null && motion.isActiveAndEnabled ? motion.ExtraClosingSpeed : 0f;
    protected virtual void OnObstacleCreated(GameObject instance)
    {
        if (motion != null && motion.isActiveAndEnabled) motion.Prepare(instance);
    }
    protected virtual void OnObstaclePlaced(Transform obstacle, RoadPathPoint point, float lateralOffset, float halfHeight)
    {
        if (!SuppressAutoSpawn && motion != null && motion.isActiveAndEnabled)
            motion.Place(obstacle, road, point, lateralOffset, halfHeight);
    }
    protected InfiniteRoad Road => road;

    private float NextSpacing()
    {
        if (!useFairRows) return NextRange(minSpacing, maxSpacing) * RowSpacingMultiplier;
        // Allow steering and the length of both vehicles, even as the car speeds up.
        float speed = driver != null ? driver.ForwardSpeed : 15f;
        if (driver != null && driver.TryGetComponent(out CarPickupEffects effects))
            speed *= effects.RunState.ForwardSpeedMultiplier;
        // Still leaves about a second between rows to reach the (adjacent) open lane.
        // Rows can come about 0.85 s apart at most (the open lane is always the same or the next one over).
        return Mathf.Max(NextRange(32f, 42f) / density, (speed + ExtraClosingSpeed) * 0.85f + 4f) * RowSpacingMultiplier;
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
            createFunc: () => { GameObject instance = Instantiate(obstaclePrefab, transform); OnObstacleCreated(instance); return new Obstacle(instance); },
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
        if (SuppressAutoSpawn)
        {
            distanceUntilSpawn = Mathf.Max(distanceUntilSpawn, 0f);
            return;
        }

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

        float distance = CurrentRunDistance;
        if (distance >= nextRoadblockAt)
        {
            TrafficRoadblockPattern pattern = TrafficRoadblockRules.ChoosePattern(
                distance, roadblockRandom.NextDouble());
            SpawnRoadblock(point, pattern);
            PickupPlacement.ClearRow(point, (previousOpenLane - 1) * point.Width / 3f);
            GameEvents.RaiseRoadblockSpawned(pattern);
            nextRoadblockAt = distance + TrafficRoadblockRules.GetNextSpacing(
                distance, roadblockRandom.NextDouble()) * RoadblockSpacingMultiplier;
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
        // Every roadblock leaves one lane open, next to the previous open lane so it
        // is always reachable without ammo.
        float laneWidth = point.Width / 3f;
        switch (pattern)
        {
            case TrafficRoadblockPattern.TwoLaneGreen:
            case TrafficRoadblockPattern.TwoLaneYellow:
                {
                    TrafficObstacleState twoLaneState = pattern == TrafficRoadblockPattern.TwoLaneGreen
                        ? TrafficObstacleState.Green
                        : TrafficObstacleState.Yellow;
                    int openLane = previousOpenLane == 1
                        ? (roadblockRandom.Next(2) == 0 ? 0 : 2)
                        : previousOpenLane;
                    float side = openLane == 0 ? 1f : -1f;
                    PlaceObstacle(point, side * laneWidth * 0.5f, laneWidth * 2f, twoLaneState);
                    previousOpenLane = openLane;
                    break;
                }
            default:
                {
                    int openLane = Mathf.Clamp(previousOpenLane + roadblockRandom.Next(3) - 1, 0, 2);
                    if (openLane != 1)
                    {
                        // Neighbouring lanes are blocked: one big two-lane block of a single colour.
                        float side = openLane == 0 ? 1f : -1f;
                        PlaceObstacle(point, side * laneWidth * 0.5f, laneWidth * 2f,
                            TrafficObstacleRules.ChooseInitialState(CurrentRunDistance, colorRandom.NextDouble()));
                        previousOpenLane = openLane;
                        break;
                    }
                    // Split choice offers one- and two-shot blocks, with no three-shot blocks.
                    bool greenFirst = roadblockRandom.Next(2) == 0;
                    int stateIndex = 0;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        if (lane == openLane) continue;
                        TrafficObstacleState state = (stateIndex == 0) == greenFirst
                            ? TrafficObstacleState.Green : TrafficObstacleState.Yellow;
                        PlaceObstacle(point, (lane - 1) * laneWidth, laneWidth - 0.3f, state);
                        stateIndex++;
                    }
                    previousOpenLane = openLane;
                    break;
                }
        }
    }

    private TrafficLightObstacle SpawnFullWidthBarrier(RoadPathPoint point, TrafficObstacleState state)
    {
        return PlaceObstacle(point, 0f, Mathf.Max(0.8f, point.Width - 0.1f), state);
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
        // The open gap always fits the car, but is sometimes tight enough that shooting is easier.
        float gapHalfWidth = RunDifficultyState.GapHalfWidth(difficultyElapsed, random.NextDouble());
        bool doubleRow = random.NextDouble() < difficultRowChance;
        int secondLane = 3 - openLane - firstLane;
        if (doubleRow && openLane != 1)
        {
            // Both blocked lanes sit side by side: make them one big block from the gap to the wall.
            float minimum = openLane == 0 ? gapCenter + gapHalfWidth : -point.Width * 0.5f;
            float maximum = openLane == 0 ? point.Width * 0.5f : gapCenter - gapHalfWidth;
            PlaceObstacle(point, (minimum + maximum) * 0.5f, maximum - minimum - 0.1f);
        }
        else
        {
            PlaceWideObstacle(point, firstLane, doubleRow ? secondLane : -1, gapCenter, gapHalfWidth);
            if (doubleRow)
                PlaceWideObstacle(point, secondLane, firstLane, gapCenter, gapHalfWidth);
        }
        PickupPlacement.ClearRow(point, gapCenter);
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
        // Bigger blocks than before: at least 1.8 m wide where there is room.
        float width = SizeRange(Mathf.Min(1.8f, availableWidth), Mathf.Min(5.4f, availableWidth));
        // Lane indices only guide the route. The blocks can straddle painted lines.
        float center = Mathf.Clamp(preferredCenter + SizeRange(-0.6f, 0.6f),
            minimum + width * 0.5f, maximum - width * 0.5f);
        PlaceObstacle(point, center, width);
    }

    private TrafficLightObstacle PlaceObstacle(
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
            new Vector3(widthScale, SizeRange(1f, 2.3f), SizeRange(0.9f, 1.3f)));
        obstacle.Transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);

        float halfHeight = obstacle.Renderer.bounds.extents.y;
        obstacle.Transform.position = point.Position + point.Right * lateralOffset + point.Up * halfHeight;
        obstacle.TrafficLight.Initialize(forcedState ??
            TrafficObstacleRules.ChooseInitialState(CurrentRunDistance, colorRandom.NextDouble()));

        activeObstacles.Add(obstacle);
        OnObstaclePlaced(obstacle.Transform, point, lateralOffset, halfHeight);
        return obstacle.TrafficLight;
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
        // Far enough ahead that blocks are visible well before they matter (no surprises).
        float distanceAhead = useFairRows ? PickupLookAhead.Distance : spawnDistance;
        return road.TryGetPointAhead(car.position, distanceAhead, out point);
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

            if (road.IsBehind(obstacle.Transform.position, car.position, despawnDistance))
            {
                if (!obstacle.TrafficLight.WasHit)
                    GameEvents.RaiseObstacleDodged(obstacle.TrafficLight.CurrentState);
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