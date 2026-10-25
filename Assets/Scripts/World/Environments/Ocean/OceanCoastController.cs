using System.Collections.Generic;
using UnityEngine;

/// <summary>Road-relative surf lanes and telegraphed coast guard wakes; uses the existing car models.</summary>
[DefaultExecutionOrder(-175)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class OceanCoastController : MonoBehaviour
{
    [Header("Surf lanes")]
    [SerializeField, Min(20f)] private float waveSpacing = 105f;
    [SerializeField, Min(10f)] private float waveLength = 42f;
    [SerializeField, Min(1f)] private float waveWidth = 3.6f;
    [SerializeField, Min(0.1f)] private float crestWidth = 0.9f;
    [Header("Coast guard")]
    [SerializeField, Min(3f)] private float wakeInterval = 8f;
    [SerializeField, Min(0.1f)] private float wakeWarningSeconds = 0.8f;
    [SerializeField, Min(0f)] private float wakeImpulse = 8f;

    private static readonly Color Water = new Color(0.08f, 0.65f, 0.72f);
    private static readonly Color Foam = new Color(0.82f, 1f, 1f);
    private static readonly Color Warning = new Color(1f, 0.65f, 0.12f);
    private readonly List<Wave> waves = new List<Wave>();
    private readonly List<Wake> wakes = new List<Wake>();
    private readonly OceanSurfState surf = new OceanSurfState();
    private EnvironmentRoot environment;
    private OutlawGameManager game;
    private AutoDriveCar car;
    private InfiniteRoad road;
    private float nextWaveAt;
    private float wakeTimer;
    private int waveIndex;
    private int wakeIndex;
    private bool started;

    public OceanSurfState Surf => surf;
    public bool WakeWarningActive => wakes.Exists(wake => wake.WarningRemaining > 0f);

    private void Awake()
    {
        environment = GetComponent<EnvironmentRoot>();
        game = GetComponent<OutlawGameManager>();
        car = environment.Player;
        road = environment.Road;
        enabled = environment.IsOcean && car != null && road != null && game != null;
    }

    private void Update()
    {
        if (game.State != OutlawGameState.Running || Time.timeScale <= 0f || !car.IsDriveReady) return;
        if (game.IsTutorial)
        {
            ClearSurface();
            return;
        }
        float distance = car.RoadPositionDistance;
        if (!started)
        {
            started = true;
            nextWaveAt = distance + 15f;
            wakeTimer = wakeInterval;
        }
        // Stop new hazards during scripted reservations, but let visible waves and
        // wakes finish naturally instead of popping out or abruptly resetting surf.
        bool suppressed = environment.Obstacles.SuppressAutoSpawn;
        if (!road.TrySample(distance, out RoadPathPoint point)) return;
        float lateral = Vector3.Dot(car.transform.position - point.Position, point.Right);
        if (!suppressed && distance >= nextWaveAt && SpawnWave(distance + 65f)) nextWaveAt = distance + waveSpacing;

        bool onWave = false;
        bool onCrest = false;
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            Wave wave = waves[i];
            if (distance > wave.End + 8f)
            {
                Destroy(wave.Visual);
                waves.RemoveAt(i);
                continue;
            }
            if (distance < wave.Start || distance > wave.End) continue;
            float offset = Mathf.Abs(lateral - wave.Lateral);
            onWave |= offset <= waveWidth * 0.5f;
            onCrest |= offset <= crestWidth * 0.5f;
        }
        surf.Tick(Time.deltaTime, onWave, onCrest);
        car.EnvironmentSpeedMultiplier = surf.SpeedMultiplier;
        car.EnvironmentSteeringGrip = surf.IsSurfing ? 0.65f : 1f;

        if (!suppressed) wakeTimer -= Time.deltaTime;
        if (!suppressed && wakeTimer <= 0f && SpawnWake(distance, lateral)) wakeTimer = wakeInterval;
        UpdateWakes(distance, lateral);
    }

    private bool SpawnWave(float start)
    {
        if (!road.TrySample(start, out RoadPathPoint point) || !road.TrySample(start + waveLength, out _)) return false;
        // Alternate outer lanes with a centre lane between them; keep each route reachable.
        int[] lanes = { -1, 0, 1, 0 };
        float lateral = lanes[waveIndex++ % lanes.Length] * point.Width / 3f;
        GameObject visual = new GameObject("Surf wave — follow the white crest");
        visual.transform.SetParent(transform, false);
        CreateRibbon(visual.transform, start, start + waveLength, lateral, lateral, waveWidth, Water);
        CreateRibbon(visual.transform, start, start + waveLength, lateral, lateral, 0.16f, Foam, 0.12f);
        waves.Add(new Wave { Start = start, End = start + waveLength, Lateral = lateral, Visual = visual });
        return true;
    }

    private bool SpawnWake(float distance, float lateral)
    {
        ChaseCar[] police = game.PoliceCars;
        if (police == null || police.Length == 0) return false;
        ChaseCar source = police[wakeIndex % police.Length];
        if (source == null || !source.isActiveAndEnabled) return false;
        float actualSpeed = car.BaseForwardSpeed * surf.SpeedMultiplier;
        if (game.PickupEffects != null) actualSpeed *= game.PickupEffects.ForwardSpeedMultiplier;
        float at = distance + Mathf.Clamp(actualSpeed * 2.5f, 45f, 125f);
        if (!road.TrySample(at, out RoadPathPoint point)) return false;
        float halfWidth = point.Width / 3f;
        float center = Mathf.Clamp(lateral, -point.Width / 6f, point.Width / 6f);
        float direction = wakeIndex++ % 2 == 0 ? 1f : -1f;
        GameObject visual = new GameObject("Coast guard wake — amber warning, white impact");
        visual.transform.SetParent(transform, false);
        LineRenderer line = CreateRibbon(visual.transform, at - halfWidth * 0.7f, at + halfWidth * 0.7f,
            center - halfWidth, center + halfWidth, 0.35f, Warning, 0.22f);
        wakes.Add(new Wake
        {
            Distance = at, Lateral = center, HalfWidth = halfWidth, Direction = direction,
            WarningRemaining = wakeWarningSeconds, Visual = visual, Line = line,
            PreviousLateral = lateral,
            PreviousSide = distance - at - (lateral - center) * 0.7f
        });
        return true;
    }

    private void UpdateWakes(float distance, float lateral)
    {
        for (int i = wakes.Count - 1; i >= 0; i--)
        {
            Wake wake = wakes[i];
            wake.WarningRemaining -= Time.deltaTime;
            if (wake.WarningRemaining <= 0f) wake.Line.sharedMaterial = RuntimeMaterials.Coloured(Foam);
            float side = distance - wake.Distance - (lateral - wake.Lateral) * 0.7f;
            if (!wake.Crossed && wake.PreviousSide < 0f && side >= 0f)
            {
                wake.Crossed = true;
                float fraction = -wake.PreviousSide / Mathf.Max(0.0001f, side - wake.PreviousSide);
                float crossingLateral = Mathf.Lerp(wake.PreviousLateral, lateral, fraction);
                if (wake.WarningRemaining <= 0f && Mathf.Abs(crossingLateral - wake.Lateral) <= wake.HalfWidth + 0.65f)
                {
                    CarPickupEffects effects = game.PickupEffects;
                    if (effects == null || (!effects.RunState.IsGameOver &&
                        effects.RunState.CollisionProtectionRemaining <= 0f && !effects.TryConsumeShield()))
                        car.ApplyLateralImpulse(wake.Direction * wakeImpulse);
                }
            }
            wake.PreviousSide = side;
            wake.PreviousLateral = lateral;
            if (distance <= wake.Distance + wake.HalfWidth + 10f) continue;
            Destroy(wake.Visual);
            wakes.RemoveAt(i);
        }
    }

    private LineRenderer CreateRibbon(Transform parent, float start, float end, float startLateral,
        float endLateral, float width, Color color, float height = 0.09f)
    {
        var ribbon = new GameObject("Surface ribbon");
        ribbon.transform.SetParent(parent, false);
        var line = ribbon.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = width;
        line.sharedMaterial = RuntimeMaterials.Coloured(color);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 17;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            if (!road.TrySample(Mathf.Lerp(start, end, t), out RoadPathPoint point)) continue;
            if (i == 0) ribbon.transform.rotation = Quaternion.LookRotation(point.Up, point.Forward);
            line.SetPosition(i, point.Position + point.Right * Mathf.Lerp(startLateral, endLateral, t) + point.Up * height);
        }
        return line;
    }

    private void OnDisable() => ClearSurface();

    private void ClearSurface()
    {
        foreach (Wave wave in waves) if (wave.Visual != null) Destroy(wave.Visual);
        foreach (Wake wake in wakes) if (wake.Visual != null) Destroy(wake.Visual);
        waves.Clear();
        wakes.Clear();
        surf.Reset();
        if (car == null) return;
        car.EnvironmentSpeedMultiplier = 1f;
        car.EnvironmentSteeringGrip = 1f;
    }

    private sealed class Wave
    {
        public float Start, End, Lateral;
        public GameObject Visual;
    }

    private sealed class Wake
    {
        public float Distance, Lateral, HalfWidth, Direction, WarningRemaining, PreviousSide, PreviousLateral;
        public bool Crossed;
        public GameObject Visual;
        public LineRenderer Line;
    }
}
