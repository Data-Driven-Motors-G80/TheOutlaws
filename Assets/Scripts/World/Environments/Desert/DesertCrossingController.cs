// Schedules warned rock crossings while respecting protected fuel routes.
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class DesertCrossingController : MonoBehaviour
{
    [Header("Encounter pacing")]
    [SerializeField, Min(3f)] private float firstCrossingSeconds = 6f;
    [SerializeField, Min(3f)] private float crossingInterval = 5f;
    [SerializeField, Min(3f)] private float ordinaryCrossingInterval = 3.5f;
    [Header("Clear-weather groups / storm regions")]
    [SerializeField, Range(1, 3)] private int minimumOrdinaryRocks = 1;
    [SerializeField, Range(1, 3)] private int maximumOrdinaryRocks = 3;
    [SerializeField, Range(8, 10)] private int minimumRegionRocks = 8;
    [SerializeField, Range(8, 10)] private int maximumRegionRocks = 10;
    [Header("Individual crossings")]
    [SerializeField, Min(0.8f)] private float warningSeconds = 1.5f;
    [SerializeField, Min(3f)] private float crossingSeconds = 4f;
    [SerializeField, Range(0.6f, 1.2f)] private float rockRadius = 1.05f;
    [SerializeField, Min(20f)] private float minimumRockSpacing = 28f;
    [SerializeField, Min(1f)] private float secondsBetweenCrossings = 1.4f;
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
        firstCrossingSeconds = 6f;
        crossingInterval = 5f;
        ordinaryCrossingInterval = 3.5f;
        minimumOrdinaryRocks = 1; maximumOrdinaryRocks = 3;
        minimumRegionRocks = 8; maximumRegionRocks = 10;
        secondsBetweenCrossings = 1.4f;
        warningSeconds = 1.25f;
        rockRadius = 1.15f;
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
                float plannedStart = distance + Mathf.Max(75f,
                    EffectiveSpeed() * (warningSeconds + crossingSeconds * (5f / 6f) + 0.5f));
                if (!game.CanScheduleEncounter(this, RunEncounterKind.Desert,
                    plannedStart - 8f, plannedStart + EffectiveSpeed() * 12f)) return;
                if (!obstacles.TryReserveSafeGap(this)) return;
                game.BeginEncounter(this, RunEncounterKind.Desert, plannedStart - 8f,
                    plannedStart + EffectiveSpeed() * 12f);
                reserved = true;
                reservationWait = 0f;
            }
            reservationWait += dt;
            if (reservationWait > 10f) { ClearCrossing(); timer = 3f; return; }

            float lead = Mathf.Max(75f, EffectiveSpeed() * (warningSeconds + crossingSeconds * (5f / 6f) + 0.5f));
            regionStart = distance + lead;
            if (!road.TrySample(regionStart, out RoadPathPoint point) || !obstacles.IsSafeGap(point.Position, 24f)) return;
            regionActive = true;
            isStormRegion = nextIsStorm;
            int minimum = isStormRegion ? Mathf.Clamp(minimumRegionRocks, 8, 10) : Mathf.Clamp(minimumOrdinaryRocks, 1, 3);
            int maximum = isStormRegion ? Mathf.Clamp(maximumRegionRocks, minimum, 10) : Mathf.Clamp(maximumOrdinaryRocks, minimum, 3);
            targetRockCount = isStormRegion ? encounterRandom.Next(minimum, maximum + 1)
                : DesertCrossingPattern.OrdinaryCount(minimum, maximum, ordinaryGroupsCompleted > 0, encounterRandom.NextDouble());
            mirroredPattern = encounterRandom.Next(2) == 0;
            scheduled = RocksSpawnedInRegion = 0;
            nextRockAt = regionStart;
            spacing = Mathf.Max(minimumRockSpacing,
                Mathf.Max(EffectiveSpeed(), car.ForwardSpeed * RiskRunState.RampBoostMultiplier) * secondsBetweenCrossings);
            game.BeginEncounter(this, RunEncounterKind.Desert, regionStart - 8f,
                regionStart + spacing * (targetRockCount - 1) + 15f);
        }

        while (scheduled < targetRockCount)
        {
            if (nextRockAt - distance < EffectiveSpeed() * 2.5f)
            { scheduled++; nextRockAt += spacing; continue; }
            if (!road.TrySample(nextRockAt, out RoadPathPoint point) || !obstacles.IsSafeGap(point.Position, 24f)) break;
            if (game.IsSharpRoadSection(nextRockAt, 25f) || game.ConflictsWithFuel(nextRockAt - 12f, nextRockAt + 12f))
            { scheduled++; nextRockAt += spacing; continue; }
            float span = point.Width * 0.5f + rockRadius + 0.5f;
            int lane = DesertCrossingPattern.LaneAt(scheduled, mirroredPattern);
            float travelToLane = DesertCrossingPattern.TravelSeconds(point.Width, span, crossingSeconds, direction, lane);

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

            spacing = Mathf.Max(minimumRockSpacing, Mathf.Max(EffectiveSpeed(), car.ForwardSpeed * RiskRunState.RampBoostMultiplier) * secondsBetweenCrossings);
            nextRockAt += spacing;
        }
        for (int i = crossings.Count - 1; i >= 0; i--)
        {
            var item = crossings[i];
            if (item.Rock == null) { if (item.Warning != null) Destroy(item.Warning); crossings.RemoveAt(i); continue; }

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

            nextIsStorm = !isStormRegion;
            if (!isStormRegion) ordinaryGroupsCompleted++;
            ClearCrossing();
            timer = nextIsStorm ? crossingInterval : ordinaryCrossingInterval;
        }
    }

    // Keep scheduled crossing rocks away from protected fuel routes.
    public bool IsFuelPositionSafe(float distance)
    {
        foreach (var item in crossings)
            if (Mathf.Abs(item.At - distance) < 12f) return false;
        for (int i = scheduled; i < targetRockCount; i++)
            if (Mathf.Abs(nextRockAt + (i - scheduled) * spacing - distance) < 12f) return false;
        return true;
    }

    private float EffectiveSpeed() => Mathf.Max(1f, environment.Player.ForwardSpeed *
        environment.Player.EnvironmentSpeedMultiplier * game.PickupEffects.RunState.ForwardSpeedMultiplier);

    private GameObject CreateWarning(RoadPathPoint point, int direction)
    {
        var marker = new GameObject(direction > 0 ? "ROCK >>>" : "<<< ROCK");
        marker.transform.SetParent(transform, false);
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
        WorldLabel.AttachToFrontFace(renderer, sign.transform, direction > 0 ? "ROCK >>>" : "<<< ROCK", Color.yellow);
        return marker;
    }

    // Remove the warning and remaining rocks together.
    private void ClearCrossing()
    {
        if (game != null) game.EndEncounter(this);
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
