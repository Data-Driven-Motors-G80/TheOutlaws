using UnityEngine;
using System.Collections.Generic;

/// <summary>Small clear-weather crossings alternate with longer, storm-covered rock fields.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class DesertCrossingController : MonoBehaviour
{
    [Header("Encounter pacing")]
    [SerializeField, Min(3f)] private float firstCrossingSeconds = 4f;
    [SerializeField, Min(3f)] private float crossingInterval = 6f;
    [SerializeField, Min(3f)] private float ordinaryCrossingInterval = 3.5f;
    [Header("Clear-weather groups / storm regions")]
    [SerializeField, Range(1, 3)] private int minimumOrdinaryRocks = 1;
    [SerializeField, Range(1, 3)] private int maximumOrdinaryRocks = 3;
    [SerializeField, Range(10, 12)] private int minimumRegionRocks = 10;
    [SerializeField, Range(10, 12)] private int maximumRegionRocks = 12;
    [Header("Individual crossings")]
    [SerializeField, Min(0.8f)] private float warningSeconds = 1.5f;
    [SerializeField, Min(3f)] private float crossingSeconds = 4f;
    [SerializeField, Range(0.6f, 1.2f)] private float rockRadius = 1.05f;
    [SerializeField, Min(20f)] private float minimumRockSpacing = 28f;
    [SerializeField, Min(1f)] private float secondsBetweenCrossings = 1.25f;
    private EnvironmentRoot environment;
    private OutlawGameManager game;
    private DesertSandstorm storm;
    private readonly List<Crossing> crossings = new List<Crossing>();
    private readonly System.Random encounterRandom = new System.Random();
    private float timer, reservationWait, regionStart, nextRockAt, spacing;
    private int scheduled, targetRockCount;
    private int direction = 1;
    private int ordinaryGroupsCompleted;
    private bool mirroredPattern;
    private bool reserved, regionActive, nextIsStorm, isStormRegion;
    public bool CrossingActive => regionActive;
    public bool StormRegionActive => regionActive && isStormRegion;
    public int TargetRockCount => targetRockCount;
    public int ActiveRockCount => crossings.Count;
    public int RocksSpawnedInRegion { get; private set; }

    private sealed class Crossing
    {
        public DesertRollingRock Rock;
        public GameObject Warning;
        public float At, Span, WarningLeft, TravelToLaneSeconds;
        public int Direction;
        public bool Armed, Moving;
    }

    private void Awake()
    {
        environment = GetComponent<EnvironmentRoot>();
        game = GetComponent<OutlawGameManager>();
        storm = GetComponent<DesertSandstorm>();
        enabled = environment.IsDesert && environment.ValidateSetup() && game != null;
        timer = firstCrossingSeconds;
    }

    private void Update()
    {
        if (game.State == OutlawGameState.Lost || game.State == OutlawGameState.Won || game.IsTutorial)
        { ClearCrossing(); nextIsStorm = false; ordinaryGroupsCompleted = 0; timer = firstCrossingSeconds; return; }
        if (game.State != OutlawGameState.Running || Time.timeScale <= 0f || !environment.Player.IsDriveReady) return;
        float dt = Time.deltaTime;
        var car = environment.Player;
        var road = environment.Road;
        var obstacles = environment.Obstacles;
        float distance = car.RoadPositionDistance;
        if (!regionActive)
        {
            timer -= dt;
            if (timer > 0f) return;
            if (!reserved)
            {
                if (!obstacles.TryReserveSafeGap(this)) return;
                reserved = true;
                reservationWait = 0f;
            }
            reservationWait += dt;
            if (reservationWait > 10f) { ClearCrossing(); timer = 3f; return; }
            // The far-side lane takes longer to reach than the centre. Include its
            // travel time and keep the warning intact, also at boosted driving speeds.
            float lead = Mathf.Max(75f, EffectiveSpeed() * (warningSeconds + crossingSeconds * (5f / 6f) + 0.5f));
            regionStart = distance + lead;
            if (!road.TrySample(regionStart, out RoadPathPoint point) || !obstacles.IsSafeGap(point.Position, 24f)) return;
            regionActive = true;
            isStormRegion = nextIsStorm;
            int minimum = isStormRegion ? Mathf.Clamp(minimumRegionRocks, 10, 12) : Mathf.Clamp(minimumOrdinaryRocks, 1, 3);
            int maximum = isStormRegion ? Mathf.Clamp(maximumRegionRocks, minimum, 12) : Mathf.Clamp(maximumOrdinaryRocks, minimum, 3);
            targetRockCount = isStormRegion ? encounterRandom.Next(minimum, maximum + 1)
                : DesertCrossingPattern.OrdinaryCount(minimum, maximum, ordinaryGroupsCompleted > 0, encounterRandom.NextDouble());
            mirroredPattern = encounterRandom.Next(2) == 0;
            scheduled = RocksSpawnedInRegion = 0;
            nextRockAt = regionStart;
            spacing = Mathf.Max(minimumRockSpacing, EffectiveSpeed() * secondsBetweenCrossings);
        }

        // Stream the region with the road. Never spawn a late rock inside the reaction window.
        while (scheduled < targetRockCount)
        {
            if (nextRockAt - distance < EffectiveSpeed() * 2.5f)
            { scheduled++; nextRockAt += spacing; continue; }
            if (!road.TrySample(nextRockAt, out RoadPathPoint point) || !obstacles.IsSafeGap(point.Position, 24f)) break;
            float span = point.Width * 0.5f + rockRadius + 0.5f;
            int lane = DesertCrossingPattern.LaneAt(scheduled, mirroredPattern);
            float travelToLane = DesertCrossingPattern.TravelSeconds(point.Width, span, crossingSeconds, direction, lane);
            // A streamed section may arrive late at high speed. Skip it instead of
            // compressing the warning or spawning a rock halfway across the road.
            if (nextRockAt - distance < EffectiveSpeed() * (warningSeconds + travelToLane + 0.1f))
            { scheduled++; nextRockAt += spacing; continue; }
            crossings.Add(new Crossing
            {
                At = nextRockAt, Span = span, Direction = direction,
                TravelToLaneSeconds = travelToLane,
                Rock = DesertRollingRock.Create(obstacles.transform, point, -direction * span, rockRadius),
                Warning = CreateWarning(point, direction)
            });
            direction = -direction;
            scheduled++; RocksSpawnedInRegion++;
            // Include the possible nitro boost so later crossings never become tightly packed.
            spacing = Mathf.Max(minimumRockSpacing, Mathf.Max(EffectiveSpeed(), car.ForwardSpeed * 1.2f) * secondsBetweenCrossings);
            nextRockAt += spacing;
        }
        for (int i = crossings.Count - 1; i >= 0; i--)
        {
            var item = crossings[i];
            if (item.Rock == null) { if (item.Warning != null) Destroy(item.Warning); crossings.RemoveAt(i); continue; }
            // After the countdown starts, a rock's motion is independent of subsequent speed changes.
            if (!item.Armed && (item.At - distance) / EffectiveSpeed() <= warningSeconds + item.TravelToLaneSeconds)
            { item.Armed = true; item.WarningLeft = warningSeconds; }
            float movementSeconds = dt;
            if (item.Armed && !item.Moving)
            {
                item.WarningLeft -= dt;
                if (item.WarningLeft <= 0f)
                { item.Moving = true; movementSeconds = -item.WarningLeft; }
            }
            if (item.Moving) item.Rock.Advance(item.Direction * (2f * item.Span / crossingSeconds) * movementSeconds);
            if (distance <= item.At + 8f && Mathf.Abs(item.Rock.Lateral) <= item.Span + 0.1f && !item.Rock.Resolved) continue;
            item.Rock.ReportPassed();
            Destroy(item.Rock.gameObject); Destroy(item.Warning);
            crossings.RemoveAt(i);
        }
        bool finished = scheduled >= targetRockCount && crossings.Count == 0;
        if (storm != null) storm.SetStorm(isStormRegion && !finished && distance >= regionStart - EffectiveSpeed() * 3f);
        if (finished && (storm == null || storm.IsClear))
        {
            // A short, clear-weather group also appears between the long storm regions.
            // Ordinary rows resume during these gaps; pickups/portals retain the shared reservation.
            nextIsStorm = !isStormRegion;
            if (!isStormRegion) ordinaryGroupsCompleted++;
            ClearCrossing();
            timer = nextIsStorm ? crossingInterval : ordinaryCrossingInterval;
        }
    }

    private float EffectiveSpeed() => Mathf.Max(1f, environment.Player.ForwardSpeed *
        environment.Player.EnvironmentSpeedMultiplier * game.PickupEffects.RunState.ForwardSpeedMultiplier);

    private GameObject CreateWarning(RoadPathPoint point, int direction)
        => CreateWarning(transform, point, direction);

    public static GameObject CreateWarning(Transform parent, RoadPathPoint point, int direction, bool oscillating = false)
    {
        var marker = new GameObject(direction > 0 ? "ROCK >>>" : "<<< ROCK");
        marker.transform.SetParent(parent, false);
        for (int i = -3; i <= 3; i++)
        {
            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Crossing marker";
            dash.transform.SetParent(marker.transform, false);
            dash.transform.position = point.Position + point.Right * (i * point.Width / 7f) + point.Up * 0.06f;
            dash.transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);
            dash.transform.localScale = new Vector3(0.65f, 0.06f, 0.18f);
            Collider collider = dash.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            dash.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(new Color(1f, 0.85f, 0.18f));
        }
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Rock warning sign";
        sign.transform.SetParent(marker.transform, false);
        sign.transform.position = point.Position - point.Right * direction * (point.Width * 0.5f + 1f) + point.Up * 2.6f;
        sign.transform.rotation = Quaternion.LookRotation(point.Forward, point.Up);
        sign.transform.localScale = new Vector3(3.5f, 1.1f, 0.1f);
        var signCollider = sign.GetComponent<Collider>(); signCollider.enabled = false; Destroy(signCollider);
        var renderer = sign.GetComponent<Renderer>();
        renderer.sharedMaterial = RuntimeMaterials.Coloured(new Color(0.16f, 0.09f, 0.04f));
        WorldLabel.AttachToFrontFace(renderer, sign.transform, oscillating ? "ROCK < >" : direction > 0 ? "ROCK >>>" : "<<< ROCK", Color.yellow);
        return marker;
    }

    private void ClearCrossing()
    {
        foreach (var item in crossings)
        {
            if (item.Rock != null) Destroy(item.Rock.gameObject);
            if (item.Warning != null) Destroy(item.Warning);
        }
        crossings.Clear();
        regionActive = false;
        if (storm != null) storm.SetStorm(false);
        if (reserved && environment != null && environment.Obstacles != null) environment.Obstacles.ReleaseSafeGap(this);
        reserved = false;
    }
    private void OnDisable() => ClearCrossing();
}
