using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class InfiniteRoad : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;
    [SerializeField] private Material roadSurfaceMaterial;
    [SerializeField] private RoadSegment[] segmentPrefabs;

    [Header("Streaming")]
    [SerializeField, Min(2)] private int segmentCount = 8;
    [SerializeField, Min(0f)] private float behindBuffer = 20f;
    [SerializeField, Min(100f)] private float aheadBuffer = 180f;
    [Tooltip("Straight road at the start, before any bends, hairpins, or hills.")]
    [SerializeField, Min(0f)] private float straightIntroDistance = 160f;

    [Header("Bends")]
    [Tooltip("Chance of a bend after each straight piece.")]
    [SerializeField, Range(0f, 1f)] private float curveFrequency = 1f;
    [Tooltip("Bend or hill length in meters (min, max).")]
    [SerializeField] private Vector2 curveLength = new Vector2(70f, 90f);
    [Tooltip("Bend turn in degrees (min, max).")]
    [SerializeField] private Vector2 bendAngle = new Vector2(15f, 25f);
    [Tooltip("Max degrees the road may stray from straight ahead.")]
    [SerializeField, Range(20f, 50f)] private float maximumHeading = 35f;

    [Header("Hairpins")]
    [Tooltip("Chance of a hairpin. They come in opposite pairs. 0 = none.")]
    [SerializeField, Range(0f, 1f)] private float hairpinFrequency = 0.15f;
    [Tooltip("Turn radius in meters (min, max).")]
    [SerializeField] private Vector2 hairpinRadius = new Vector2(24f, 32f);
    [Tooltip("Hairpin turn in degrees (min, max).")]
    [SerializeField] private Vector2 hairpinAngle = new Vector2(126f, 180f);

    [Header("Hills")]
    [Tooltip("Chance of a hill. 0 = flat. Hairpins can also climb or descend.")]
    [SerializeField, Range(0f, 1f)] private float hillFrequency = 0.5f;
    [Tooltip("Hill steepness in degrees (min, max).")]
    [SerializeField] private Vector2 hillPitch = new Vector2(4f, 8f);
    [Tooltip("Max meters the road may climb or sink from its start.")]
    [SerializeField, Min(2f)] private float altitudeRange = 15f;

    private readonly Queue<ActiveSegment> activeSegments = new Queue<ActiveSegment>();
    private readonly Dictionary<RoadSegment, ObjectPool<RoadSegment>> pools = new Dictionary<RoadSegment, ObjectPool<RoadSegment>>();
    private System.Random random;
    private Pose nextEntry;
    private float nextDistance;
    private float routeHeading;
    private float routeSlope;
    private float baseAltitude;
    private float pendingHairpinAngle;
    private float pendingHairpinRadius;
    private int curveIndex;
    private bool lastWasFeature;
    private bool lastWasCurve;

    public float FirstDistance => activeSegments.Count > 0 ? activeSegments.Peek().StartDistance : 0f;
    public float LastDistance => nextDistance;

    private void OnValidate()
    {
        curveLength = ValidRange(curveLength, 40f, 300f);
        bendAngle = ValidRange(bendAngle, 5f, 45f);
        hairpinRadius = ValidRange(hairpinRadius, 15f, 60f);
        hairpinAngle = ValidRange(hairpinAngle, 90f, 180f);
        hillPitch = ValidRange(hillPitch, 1f, 20f);
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        EnsureInitialized();
        if (!TryGetDistance(car.position, out float carDistance)) return;
        while (activeSegments.Count > 1 && activeSegments.Peek().EndDistance < carDistance - behindBuffer)
            activeSegments.Dequeue().Release();
        while (activeSegments.Count < segmentCount || nextDistance < carDistance + aheadBuffer)
            SpawnSegment();
    }

    private void OnDestroy()
    {
        foreach (ObjectPool<RoadSegment> pool in pools.Values) pool.Clear();
        pools.Clear();
        activeSegments.Clear();
    }

    public bool TryGetDistance(Vector3 worldPosition, out float distance)
    {
        EnsureInitialized();
        distance = 0f;
        float closestSqrDistance = float.MaxValue;
        foreach (ActiveSegment active in activeSegments)
        {
            float local = active.Segment.ProjectDistance(worldPosition, out float sqrDistance);
            if (sqrDistance >= closestSqrDistance) continue;
            closestSqrDistance = sqrDistance;
            distance = active.StartDistance + local;
        }
        return closestSqrDistance < float.MaxValue;
    }

    public bool TrySample(float distance, out RoadPathPoint point)
    {
        EnsureInitialized();
        point = default;
        if (distance < FirstDistance - 0.001f || distance > nextDistance + 0.001f) return false;
        foreach (ActiveSegment active in activeSegments)
        {
            if (distance > active.EndDistance) continue;
            point = active.Segment.Sample(distance - active.StartDistance);
            return true;
        }
        return false;
    }

    public bool TryGetPathPoint(Vector3 worldPosition, out RoadPathPoint point)
    {
        point = default;
        return TryGetDistance(worldPosition, out float distance) && TrySample(distance, out point);
    }

    public bool TryGetPointAhead(Vector3 worldPosition, float distance, out RoadPathPoint point)
    {
        point = default;
        return TryGetDistance(worldPosition, out float from) && TrySample(from + distance, out point);
    }

    public bool IsBehind(Vector3 worldPosition, Vector3 reference, float margin)
    {
        return TryGetDistance(worldPosition, out float at) && TryGetDistance(reference, out float from)
            && from - at > margin;
    }

    private void EnsureInitialized()
    {
        if (activeSegments.Count > 0) return;
        random = new System.Random(19020);

        if (!HasValidSetup())
        {
            enabled = false;
            return;
        }

        foreach (RoadSegment segment in GetComponentsInChildren<RoadSegment>())
        {
            segment.gameObject.SetActive(false);
            Destroy(segment.gameObject);
        }
        pools.Clear();
        nextDistance = 0f;
        routeHeading = 0f;
        routeSlope = 0f;
        curveIndex = 0;
        lastWasFeature = false;
        lastWasCurve = false;
        pendingHairpinAngle = 0f;
        pendingHairpinRadius = 0f;
        Vector3 origin = new Vector3(transform.position.x, transform.position.y, car.position.z - behindBuffer);
        baseAltitude = origin.y;
        nextEntry = new Pose(origin, Quaternion.identity);

        while (activeSegments.Count < segmentCount || nextDistance < behindBuffer + aheadBuffer)
            SpawnSegment();
    }

    private bool HasValidSetup()
    {
        if (car == null)
        {
            LogError("car reference is missing.");
            return false;
        }

        if (segmentPrefabs == null || segmentPrefabs.Length == 0)
        {
            LogError("no segment prefabs assigned.");
            return false;
        }

        foreach (RoadSegment prefab in segmentPrefabs)
        {
            if (prefab == null)
            {
                LogError("a segment prefab slot is empty.");
                return false;
            }
            if (!prefab.IsConfigured)
            {
                LogError($"'{prefab.name}' has a missing start point, end point or waypoint.", prefab);
                return false;
            }
            if (prefab.Length <= 0.01f)
            {
                LogError($"'{prefab.name}' needs a path with nonzero length.", prefab);
                return false;
            }
        }

        return true;
    }

    private void SpawnSegment()
    {
        ObjectPool<RoadSegment> pool = GetPool(SelectPrefab());
        RoadSegment segment = pool.Get();

        Transform surface = segment.transform.Find("Road");
        if (roadSurfaceMaterial != null && surface != null &&
            surface.TryGetComponent(out Renderer surfaceRenderer))
            surfaceRenderer.sharedMaterial = roadSurfaceMaterial;

        float angle = 0f;
        float length = 20f;
        float startSlope = routeSlope;
        float endSlope = routeSlope;
        bool sCurve = false;
        bool hairpin = false;
        bool curve = false;
        bool hill = false;
        bool pastIntro = nextDistance >= behindBuffer + straightIntroDistance;

        if (pastIntro && !lastWasCurve && pendingHairpinAngle != 0f)
        {
            angle = pendingHairpinAngle;
            pendingHairpinAngle = 0f;
            hairpin = curve = true;
            hill = Roll(hillFrequency);
            length = HairpinLength(angle, pendingHairpinRadius);
        }
        else if (pastIntro && !lastWasCurve && Roll(hairpinFrequency))
        {
            float radius = RandomIn(hairpinRadius);
            angle = (random.Next(2) == 0 ? -1f : 1f) * RandomIn(hairpinAngle);
            pendingHairpinAngle = -angle;
            pendingHairpinRadius = radius;
            hairpin = curve = true;
            hill = Roll(hillFrequency);
            length = HairpinLength(angle, radius);
        }
        else if (pastIntro && !lastWasFeature)
        {
            curve = Roll(curveFrequency);
            hill = Roll(hillFrequency);
            if (curve || hill) length = RandomIn(curveLength);

            if (curve)
            {
                sCurve = curveIndex++ % 3 == 1;
                float sign = routeHeading > 5f ? -1f : routeHeading < -5f ? 1f
                    : random.Next(2) == 0 ? -1f : 1f;
                angle = sign * Mathf.Min(RandomIn(bendAngle), maximumHeading - sign * routeHeading);
                if (!sCurve) routeHeading += angle;
            }
        }

        if (hill)
        {
            float altitude = nextEntry.position.y - baseAltitude;
            float chanceUp = Mathf.Clamp(0.5f - altitude / (2f * altitudeRange), 0.1f, 0.9f);
            float sign = random.NextDouble() < chanceUp ? 1f : -1f;
            endSlope = sign * Mathf.Tan(RandomIn(hillPitch) * Mathf.Deg2Rad);
        }
        else if (!curve) endSlope = 0f;

        lastWasCurve = curve;
        lastWasFeature = curve || hill;
        segment.ConfigureCurve(length, angle, sCurve, startSlope, endSlope, hairpin);
        routeSlope = endSlope;

        segment.AlignStartTo(nextEntry);
        nextEntry = segment.ExitPose;

        activeSegments.Enqueue(new ActiveSegment(segment, pool, nextDistance));
        nextDistance += segment.Length;
    }

    private static float HairpinLength(float angleDegrees, float radius)
    {
        return Mathf.Max(40f, radius * Mathf.Abs(angleDegrees) * Mathf.Deg2Rad
            / (1f - RoadSegment.HairpinRampFraction));
    }

    private static Vector2 ValidRange(Vector2 range, float min, float max)
    {
        return new Vector2(
            Mathf.Clamp(Mathf.Min(range.x, range.y), min, max),
            Mathf.Clamp(Mathf.Max(range.x, range.y), min, max));
    }

    private float RandomIn(Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
    }

    private bool Roll(float frequency)
    {
        return frequency > 0f && (frequency >= 1f || random.NextDouble() < frequency);
    }

    private RoadSegment SelectPrefab()
    {
        return segmentPrefabs[activeSegments.Count == 0 ? 0 : random.Next(0, segmentPrefabs.Length)];
    }

    private ObjectPool<RoadSegment> GetPool(RoadSegment prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<RoadSegment> existing)) return existing;

        ObjectPool<RoadSegment> pool = new ObjectPool<RoadSegment>(
            createFunc: () => Instantiate(prefab, transform),
            actionOnGet: segment => segment.gameObject.SetActive(true),
            actionOnRelease: segment => segment.gameObject.SetActive(false),
            actionOnDestroy: DestroySegment);

        pools.Add(prefab, pool);
        return pool;
    }

    private static void DestroySegment(RoadSegment segment)
    {
        if (segment == null) return;
        if (Application.isPlaying) Destroy(segment.gameObject);
        else DestroyImmediate(segment.gameObject);
    }

    private void LogError(string message, Object context = null)
    {
        Debug.LogError($"{nameof(InfiniteRoad)}: {message}", context != null ? context : this);
    }

    private readonly struct ActiveSegment
    {
        private readonly ObjectPool<RoadSegment> pool;

        public ActiveSegment(RoadSegment segment, ObjectPool<RoadSegment> pool, float startDistance)
        {
            Segment = segment;
            StartDistance = startDistance;
            this.pool = pool;
        }

        public RoadSegment Segment { get; }
        public float StartDistance { get; }
        public float EndDistance => StartDistance + Segment.Length;

        public void Release()
        {
            pool.Release(Segment);
        }
    }
}