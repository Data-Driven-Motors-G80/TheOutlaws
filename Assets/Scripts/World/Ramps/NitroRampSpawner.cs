// Builds nitro ramps and offers positive icon pickups during a jump.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NitroRampSpawner : MonoBehaviour
{
    [Header("Pacing")]
    [SerializeField, Min(1f)] private float firstRampSeconds = 4f;
    [SerializeField, Min(1f)] private float rampIntervalSeconds = 6f;
    [SerializeField, Min(40f)] private float lookAhead = 85f;
    [Header("Ramp")]
    [SerializeField, Min(4f)] private float rampLength = 10f;
    [SerializeField, Min(0.5f)] private float rampHeight = 1.2f;
    [SerializeField, Range(0.4f, 0.95f)] private float laneWidthFraction = 0.85f;
    [SerializeField, Min(0.1f)] private float boostSeconds = 4f;
    [SerializeField, Min(0f)] private float extraFuelCost = 5f;
    [SerializeField, Min(1f)] private float launchSpeed = 9f;

    private OutlawGameManager game;
    private AutoDriveCar car;
    private InfiniteRoad road;
    private ObstacleSpawner obstacles;
    private CarPickupEffects effects;
    private GameObject ramp;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly List<Vector2> spans = new List<Vector2>();
    private float nextAt;
    private float nextPlacementCheckAt;
    private float startDistance, endDistance, lateral, width;
    private float riddenHeight;
    private bool riding, used;
    private bool flatBoostPad;
    private bool wasRunning;
    private GameObject airPickup;
    private Sprite airPickupSprite;
    private Texture2D generatedAirIcon;
    private PickupEffectType airPickupEffect;
    private Vector3 previousPickupCheckPosition;
    private float airPickupDistance;
    private static readonly PickupEffectType[] AirRewards = {
        PickupEffectType.Shield, PickupEffectType.ProximityRecovery, PickupEffectType.Gun };
    private static readonly string[] AirRewardIcons = { "shield", "jammer", "gun" };

    public void Configure(OutlawGameManager manager, AutoDriveCar driver,
        InfiniteRoad infiniteRoad, ObstacleSpawner obstacleSpawner)
    {
        game = manager;
        car = driver;
        road = infiniteRoad;
        obstacles = obstacleSpawner;
        effects = car.GetComponent<CarPickupEffects>();
        car.Ramps = this;
    }

    private bool Running => game != null && game.State == OutlawGameState.Running
        && !game.IsTutorial && effects != null && effects.SimulationActive && !effects.RunState.IsGameOver;

    private void Update()
    {

        if (!Running || Time.timeScale <= 0f) return;
        UpdateAirPickup();
        if (!wasRunning)
        {
            wasRunning = true;
            nextAt = game.ElapsedSeconds + firstRampSeconds;
        }
        if (ramp != null)
        {
            if (car.RoadPositionDistance > endDistance + 15f)
            {
                ClearRamp();
                float approachSeconds = Mathf.Max(lookAhead / game.ActualDrivingSpeed, 2.5f);
                nextAt = game.ElapsedSeconds + (game.UsesCoordinatedPacing
                    ? Mathf.Max(1f, Random.Range(8f, 12f) - approachSeconds) : rampIntervalSeconds);
            }
            return;
        }
        if (game.ElapsedSeconds < nextAt || !car.IsDriveReady || car.IsAirborne
            || game.ElapsedSeconds < nextPlacementCheckAt) return;
        nextPlacementCheckAt = game.ElapsedSeconds + 0.25f;
        float start = car.RoadPositionDistance + Mathf.Max(lookAhead, car.ForwardSpeed * 2.5f);
        float minimumStart = start;
        float end = start + rampLength;
        float boostedSpeed = Mathf.Max(1f, car.ForwardSpeed * car.EnvironmentSpeedMultiplier)
            * Mathf.Max(RiskRunState.RampBoostMultiplier, effects.RunState.ForwardSpeedMultiplier) * 1.1f;
        const float maximumFlightSeconds = 4f;
        const float landingRunoutSeconds = 0.5f;
        bool overdueJump = game.JumpRampOverdue;
        float searchDistance = game.UsesCoordinatedPacing ? Mathf.Max(180f, boostedSpeed * 3f) : 0f;
        if (!road.EnsureDistanceAvailable(end + searchDistance + boostedSpeed * (maximumFlightSeconds + landingRunoutSeconds))) return;
        RoadPathPoint entry = default;
        RoadPathPoint exit = default;
        bool foundJump = false;

        for (float offset = 0f; offset <= searchDistance; offset += 10f)
        {
            float candidateStart = minimumStart + offset;
            float candidateEnd = candidateStart + rampLength;
            if (!road.TrySample(candidateStart, out entry) || !road.TrySample(candidateEnd, out exit)) continue;
            if (!HasSafeJumpCorridor(candidateStart, candidateEnd, exit, boostedSpeed,
                maximumFlightSeconds, landingRunoutSeconds)) continue;
            if (!game.CanScheduleEncounter(this, RunEncounterKind.Ramp, candidateStart - 5f,
                candidateEnd + boostedSpeed * 3f)) continue;
            start = candidateStart;
            end = candidateEnd;
            foundJump = true;
            break;
        }
        if (!foundJump)
        {

            if (overdueJump) return;
            start = minimumStart;
            end = start + rampLength;
            if (!road.TrySample(start, out entry) || !road.TrySample(end, out exit)) return;
        }
        flatBoostPad = !foundJump;
        if (!road.TrySample(start + rampLength * 0.5f, out RoadPathPoint middle)) return;
        float encounterEnd = end + (flatBoostPad ? game.ActualDrivingSpeed : boostedSpeed * 3f);
        if (!game.CanScheduleEncounter(this, RunEncounterKind.Ramp, start - 5f, encounterEnd)) return;
        if (!obstacles.TryReserveRampGap(this, middle.Position, rampLength * 0.5f + 8f,
            overdueJump && foundJump)) return;
        startDistance = start;
        endDistance = end;
        float laneWidth = Mathf.Min(entry.Width, exit.Width) / 3f;

        spans.Clear();
        int mask = Random.Range(1, 7);
        if (game.UsesCoordinatedPacing)
        {
            float roll = Random.value;
            if (roll < 0.45f) mask = 1 << Random.Range(0, 3);
            else if (roll < 0.9f) mask = Random.value < 0.5f ? 3 : 6;
            else mask = 5;
        }
        for (int lane = 0; lane < 3; lane++)
        {
            if ((mask & (1 << lane)) == 0) continue;
            int first = lane;
            while (lane + 1 < 3 && (mask & (1 << (lane + 1))) != 0) lane++;
            int count = lane - first + 1;
            float center = ((first + lane) * 0.5f - 1f) * laneWidth;
            float spanWidth = (count - 1 + laneWidthFraction) * laneWidth;
            spans.Add(new Vector2(center, spanWidth));
        }
        used = riding = false;
        BuildRamp();
        if (!flatBoostPad && game.UsesCoordinatedPacing)
        {
            game.RecordJumpRampOffer(start);
            if (Random.value < 0.5f)
            {
                float previewSpeed = Mathf.Max(1f, car.ForwardSpeed * car.EnvironmentSpeedMultiplier)
                    * RiskRunState.RampBoostMultiplier;
                float upwardSpeed = Mathf.Clamp(exit.Forward.y * previewSpeed + rampHeight / rampLength * previewSpeed,
                    launchSpeed, launchSpeed * 1.4f);
                float carHeight = road.TrySample(car.RoadPositionDistance, out RoadPathPoint current)
                    ? Vector3.Dot(car.transform.position - current.Position, current.Up) : 0f;
                Vector2 rewardSpan = spans[Random.Range(0, spans.Count)];
                SpawnAirPickup(exit, rewardSpan.x, exit.Position.y + rampHeight + carHeight * exit.Up.y,
                    upwardSpeed, previewSpeed);
            }
        }
        game.BeginEncounter(this, RunEncounterKind.Ramp, start - 5f, encounterEnd);
    }

    // Charge ramp fuel once, then follow the ramp slope until takeoff.
    public void FollowSurface(float from, float to, float carLateral,
        ref RoadPathPoint point, ref Vector3 groundPosition, float speed)
    {
        if (!Running || ramp == null || used) return;
        if (!riding)
        {
            width = 0f;
            lateral = float.PositiveInfinity;
            foreach (Vector2 span in spans)
            {
                if (Mathf.Abs(carLateral - span.x) > span.y * 0.5f) continue;
                lateral = span.x;
                width = span.y;
                break;
            }
        }
        bool inLane = Mathf.Abs(carLateral - lateral) <= width * 0.5f;
        bool crossing = from <= endDistance && to >= startDistance;
        bool enters = crossing && inLane && !car.IsAirborne;
        if (!riding && !enters) return;
        if (!riding)
        {
            riding = true;
            if (effects.RunState.TryActivateRampBoost(boostSeconds,
                flatBoostPad && game.UsesCoordinatedPacing ? 1.225f : RiskRunState.RampBoostMultiplier))
            {
                effects.ConsumeFuel(extraFuelCost);
                if (game.Fuel != null) game.Fuel.ChangeFuel(-extraFuelCost);
            }
        }
        if (flatBoostPad)
        {

            used = true;
            riding = false;
            return;
        }
        if (!inLane || to >= endDistance)
        {

            float at = inLane ? endDistance : Mathf.Clamp(from, startDistance, endDistance);
            if (road.TrySample(at, out RoadPathPoint lip))
            {
                float height = inLane ? rampHeight : riddenHeight;
                float worldY = lip.Position.y + height + (groundPosition.y - point.Position.y);
                float upwardSpeed = inLane
                    ? Mathf.Clamp(point.Forward.y * speed + rampHeight / rampLength * speed, launchSpeed, launchSpeed * 1.4f) : 0f;
                car.LaunchFromRamp(worldY - groundPosition.y, upwardSpeed, groundPosition.y);
            }
            used = true;
            riding = false;
            if (game.UsesCoordinatedPacing)
            {
                obstacles.ReleasePickupGap(this);
                game.EndEncounter(this);
            }
            return;
        }
        riddenHeight = Mathf.Clamp01((to - startDistance) / rampLength) * rampHeight;
        groundPosition += Vector3.up * riddenHeight;

        point = new RoadPathPoint(point.Position,
            (point.Forward + Vector3.up * (rampHeight / rampLength)).normalized, point.Width);
    }

    // Spawn the bonus with the ramp so its fixed airborne position is visible on approach.
    private void SpawnAirPickup(RoadPathPoint lip, float carLateral, float worldY,
        float upwardSpeed, float speed)
    {
        ClearAirPickup();
        const float flightTime = 0.65f;
        float targetDistance = endDistance + speed * flightTime;
        if (!road.TrySample(targetDistance, out RoadPathPoint target)) return;
        airPickupDistance = targetDistance;
        float velocity = upwardSpeed;
        const float step = 1f / 120f;
        for (float time = 0f; time < flightTime; time += step)
        {
            float dt = Mathf.Min(step, flightTime - time);
            float acceleration = car.Gravity * (velocity < 0f ? car.FallGravityMultiplier : 1f);
            worldY += velocity * dt - 0.5f * acceleration * dt * dt;
            velocity -= acceleration * dt;
        }
        int reward = Random.Range(0, AirRewards.Length);
        Texture2D icon = LoadAirRewardIcon(reward);
        airPickupEffect = AirRewards[reward];
        airPickupSprite = Sprite.Create(icon, new Rect(0f, 0f, icon.width, icon.height),
            new Vector2(0.5f, 0.5f), Mathf.Max(icon.width, icon.height) / 2f);
        airPickup = new GameObject("Airborne " + AirRewardIcons[reward]);
        airPickup.transform.SetParent(road.transform, true);
        Vector3 position = target.Position + target.Right * carLateral;
        position.y = worldY + 0.65f;
        airPickup.transform.position = position;
        Camera view = Camera.main;
        airPickup.transform.rotation = view != null ? view.transform.rotation
            : Quaternion.LookRotation(lip.Forward, Vector3.up);
        airPickup.AddComponent<SpriteRenderer>().sprite = airPickupSprite;
        previousPickupCheckPosition = car.transform.position + Vector3.up * 0.65f;
    }

    // Reuse HUD artwork; generate a simple matching icon if that asset is absent.
    private Texture2D LoadAirRewardIcon(int reward)
    {
        Texture2D icon = Resources.Load<Texture2D>("BackpackIcons/" + AirRewardIcons[reward]);
        if (icon != null) return icon;
        const int size = 64;
        generatedAirIcon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedAirIcon.name = "Air Reward " + AirRewardIcons[reward];
        generatedAirIcon.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                bool filled;
                if (reward == 0)
                    filled = v > -0.8f && v < 0.75f && Mathf.Abs(u) < Mathf.Min(0.65f, (v + 0.8f) * 0.9f);
                else if (reward == 1)
                {
                    float radius = Mathf.Sqrt(u * u + v * v);
                    filled = radius < 0.15f || (u > 0.12f && Mathf.Abs(v) < u * 1.5f
                        && (Mathf.Abs(radius - 0.42f) < 0.07f || Mathf.Abs(radius - 0.72f) < 0.07f));
                }
                else
                    filled = (u > -0.75f && u < 0.8f && v > 0.1f && v < 0.48f)
                        || (u > -0.55f && u < -0.18f && v > -0.65f && v < 0.2f)
                        || (u > -0.18f && u < 0.2f && v > -0.12f && v < 0.1f);
                pixels[y * size + x] = filled ? new Color(0.3f, 1f, 0.55f, 1f) : Color.clear;
            }
        generatedAirIcon.SetPixels(pixels);
        generatedAirIcon.Apply();
        return generatedAirIcon;
    }

    // Sweep the collection path so boosted movement cannot skip the icon.
    private void UpdateAirPickup()
    {
        if (airPickup == null) return;
        // Keep the icon alive during long approaches; retire it only after the car passes it.
        if (effects.RunState.IsRoadRecovering || car.RoadPositionDistance > airPickupDistance + 8f)
        {
            ClearAirPickup();
            return;
        }
        Camera view = Camera.main;
        if (view != null) airPickup.transform.rotation = view.transform.rotation;
        Vector3 current = car.transform.position + Vector3.up * 0.65f;
        Vector3 motion = current - previousPickupCheckPosition;
        float t = motion.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(airPickup.transform.position - previousPickupCheckPosition, motion) / motion.sqrMagnitude) : 0f;
        Vector3 nearest = previousPickupCheckPosition + motion * t;
        previousPickupCheckPosition = current;
        if (car.IsAirborne && Vector3.SqrMagnitude(nearest - airPickup.transform.position) <= 1.25f * 1.25f
            && effects.TryApply(airPickupEffect)) ClearAirPickup();
    }

    private void ClearAirPickup()
    {
        if (airPickup != null) Destroy(airPickup);
        if (airPickupSprite != null) Destroy(airPickupSprite);
        if (generatedAirIcon != null) Destroy(generatedAirIcon);
        generatedAirIcon = null;
        airPickup = null;
        airPickupSprite = null;
    }

    // Check the full flight and landing runout before offering a jump.
    private bool HasSafeJumpCorridor(float start, float end, RoadPathPoint lip,
        float speed, float maxFlightSeconds, float runoutSeconds)
    {
        Vector3 takeoffDirection = Vector3.ProjectOnPlane(lip.Forward, Vector3.up).normalized;
        if (takeoffDirection.sqrMagnitude < 0.5f) return false;
        Vector3 previousDirection = takeoffDirection;

        float guardStart = Mathf.Max(road.FirstDistance, start - Mathf.Max(15f, speed * 0.4f));
        for (float at = guardStart; at < end; at += 2f)
        {
            if (!road.TrySample(at, out RoadPathPoint sample)) return false;
            Vector3 heading = Vector3.ProjectOnPlane(sample.Forward, Vector3.up).normalized;
            if (Vector3.Angle(takeoffDirection, heading) > 8f) return false;
        }
        float flatLength = Vector3.ProjectOnPlane(lip.Forward, Vector3.up).magnitude;
        float roadSlope = lip.Forward.y / Mathf.Max(0.001f, flatLength);
        float velocity = Mathf.Clamp(roadSlope * speed + rampHeight / rampLength * speed,
            launchSpeed, launchSpeed * 1.4f);
        float worldY = lip.Position.y + rampHeight;
        float landingAt = -1f;
        const float step = 1f / 60f;
        for (float time = step; time <= maxFlightSeconds + runoutSeconds; time += step)
        {
            if (!road.TrySample(end + time * speed, out RoadPathPoint sample)) return false;
            Vector3 heading = Vector3.ProjectOnPlane(sample.Forward, Vector3.up).normalized;

            if (Vector3.Angle(takeoffDirection, heading) > 10f ||
                Vector3.Angle(previousDirection, heading) > speed * step * 0.5f) return false;
            previousDirection = heading;
            Vector3 predicted = lip.Position + takeoffDirection * (time * speed);
            float sidewaysDeparture = Mathf.Abs(Vector3.Dot(predicted - sample.Position, sample.Right));
            if (sidewaysDeparture > sample.Width / 3f * 0.35f) return false;
            float acceleration = car.Gravity * (velocity < 0f ? car.FallGravityMultiplier : 1f);
            worldY += velocity * step - 0.5f * acceleration * step * step;
            velocity -= acceleration * step;
            if (landingAt < 0f && time > 0.1f && worldY <= sample.Position.y)
                landingAt = time;
            if (landingAt >= 0f && time >= landingAt + runoutSeconds) return true;
            if (landingAt < 0f && time >= maxFlightSeconds) return false;
        }
        return false;
    }

    private void BuildRamp()
    {
        ramp = new GameObject(flatBoostPad ? "Nitro Boost Pad" : "Nitro Ramp");
        ramp.transform.SetParent(road.transform, false);
        foreach (Vector2 span in spans) BuildSpan(span.x, span.y);
    }

    private void BuildSpan(float lateral, float width)
    {
        GameObject surface = new GameObject("Joined Ramp Surface");
        surface.transform.SetParent(ramp.transform, false);

        const int sections = 12;
        var vertices = new Vector3[(sections + 1) * 4];
        var top = new int[sections * 6];
        var sides = new int[sections * 18 + 12];
        int sideIndex = 0;
        for (int i = 0; i <= sections; i++)
        {
            float t = (float)i / sections;
            road.TrySample(Mathf.Lerp(startDistance, endDistance, t), out RoadPathPoint p);
            Vector3 center = p.Position + p.Right * lateral;
            Vector3 left = center - p.Right * (width * 0.5f);
            Vector3 right = center + p.Right * (width * 0.5f);
            float topHeight = flatBoostPad ? 0.035f : t * rampHeight + 0.03f;
            int v = i * 4;
            vertices[v] = surface.transform.InverseTransformPoint(left + Vector3.up * (topHeight));
            vertices[v + 1] = surface.transform.InverseTransformPoint(right + Vector3.up * (topHeight));
            vertices[v + 2] = surface.transform.InverseTransformPoint(left - Vector3.up * 0.03f);
            vertices[v + 3] = surface.transform.InverseTransformPoint(right - Vector3.up * 0.03f);
            if (i == sections) continue;
            int tri = i * 6;
            top[tri] = v; top[tri + 1] = v + 4; top[tri + 2] = v + 1;
            top[tri + 3] = v + 1; top[tri + 4] = v + 4; top[tri + 5] = v + 5;
            AddQuad(sides, ref sideIndex, v, v + 2, v + 6, v + 4);
            AddQuad(sides, ref sideIndex, v + 1, v + 5, v + 7, v + 3);
            AddQuad(sides, ref sideIndex, v + 2, v + 3, v + 7, v + 6);
        }
        AddQuad(sides, ref sideIndex, 0, 1, 3, 2);
        int last = sections * 4;
        AddQuad(sides, ref sideIndex, last, last + 2, last + 3, last + 1);
        Mesh mesh = new Mesh { name = "Nitro Ramp Mesh" };
        meshes.Add(mesh);
        mesh.vertices = vertices;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(top, 0);
        mesh.SetTriangles(sides, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
        surface.AddComponent<MeshRenderer>().sharedMaterials = new[] {
            RuntimeMaterials.Coloured(new Color(0.12f, 0.8f, 0.25f)),
            RuntimeMaterials.Coloured(new Color(0.12f, 0.15f, 0.2f)) };

        for (int row = 0; row < 4; row++)
        {
            float t = 0.18f + row * 0.2f;
            road.TrySample(startDistance + rampLength * t, out RoadPathPoint p);
            Vector3 forward = flatBoostPad ? p.Forward
                : (p.Forward + Vector3.up * (rampHeight / rampLength)).normalized;
            Vector3 center = p.Position + p.Right * lateral + Vector3.up * (flatBoostPad ? 0.075f : t * rampHeight + 0.07f);
            int columns = Mathf.Clamp(Mathf.CeilToInt(width / (p.Width / 3f)), 1, 3);
            float arrowWidth = width / columns;
            for (int column = 0; column < columns; column++)
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stripe.name = "Boost Chevron";
                    stripe.transform.SetParent(surface.transform, true);
                    stripe.transform.position = center + p.Right * ((column - (columns - 1) * 0.5f) * arrowWidth + side * arrowWidth * 0.18f);
                    stripe.transform.rotation = Quaternion.LookRotation(forward, p.Up) * Quaternion.Euler(0f, -side * 55f, 0f);
                    stripe.transform.localScale = new Vector3(0.16f, 0.04f, arrowWidth * 0.55f);
                    Destroy(stripe.GetComponent<Collider>());
                    stripe.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(Color.white);
                }
        }
    }

    private static void AddQuad(int[] triangles, ref int index, int a, int b, int c, int d)
    {
        triangles[index++] = a; triangles[index++] = b; triangles[index++] = c;
        triangles[index++] = a; triangles[index++] = c; triangles[index++] = d;
    }

    // The airborne bonus outlives the ramp mesh until collected or expired.
    private void ClearRamp()
    {
        if (obstacles != null) obstacles.ReleasePickupGap(this);
        if (ramp != null) Destroy(ramp);
        foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        meshes.Clear();
        spans.Clear();
        ramp = null;
        riding = false;
    }

    public void PrepareRoadRecovery(Vector3 position)
    {
        ClearRamp();
        ClearAirPickup();
        game?.EndEncounter(this);
        if (obstacles != null) obstacles.ClearNear(position, 12f);
        nextAt = (game != null ? game.ElapsedSeconds : 0f) + rampIntervalSeconds;
    }

    private void OnDisable()
    {
        ClearRamp();
        ClearAirPickup();
        game?.EndEncounter(this);
    }
    private void OnDestroy()
    {
        ClearRamp();
        ClearAirPickup();
        if (car != null && car.Ramps == this) car.Ramps = null;
    }
}
