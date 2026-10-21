using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class InfiniteRoad : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;
    [SerializeField] private Material roadSurfaceMaterial;

    [Header("Segments")]
    [SerializeField] private RoadSegment[] segmentPrefabs;

    [Header("Streaming")]
    [SerializeField, Min(2)] private int segmentCount = 8;
    [SerializeField, Min(0f)] private float behindBuffer = 20f;

    [Header("Curves")]
    [SerializeField] private bool enableCurves;
    [SerializeField, Min(40f)] private float curveLength = 80f;
    [SerializeField, Range(5f, 30f)] private float bendAngle = 25f;
    [SerializeField, Range(20f, 50f)] private float maximumHeading = 35f;
    [SerializeField, Min(0f)] private float straightIntroDistance = 160f;
    [SerializeField, Min(100f)] private float aheadBuffer = 180f;

    private float nextDistance;
    private float routeHeading;
    private int curveIndex;
    private bool lastWasCurve;
    public float FirstDistance => activeSegments.Count > 0 ? activeSegments.Peek().StartDistance : 0f;
    public float LastDistance => nextDistance;

    private readonly Queue<ActiveSegment> activeSegments = new Queue<ActiveSegment>();
    private readonly Dictionary<RoadSegment, ObjectPool<RoadSegment>> pools = new Dictionary<RoadSegment, ObjectPool<RoadSegment>>();
    private System.Random random;
    private Pose nextEntry;

    private void Awake()
    {
        EnsureInitialized();
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

        // Managed queues/pools are lost when scripts reload during Play mode.
        // Rebuild their generated road objects together instead of using an empty queue.
        foreach (RoadSegment segment in GetComponentsInChildren<RoadSegment>())
        {
            segment.gameObject.SetActive(false);
            Destroy(segment.gameObject);
        }
        pools.Clear();
        nextDistance = 0f;
        routeHeading = 0f;
        curveIndex = 0;
        lastWasCurve = false;
        Vector3 origin = new Vector3(transform.position.x, transform.position.y, car.position.z - behindBuffer);
        nextEntry = new Pose(origin, Quaternion.identity);

        while (activeSegments.Count < segmentCount || nextDistance < behindBuffer + aheadBuffer)
        {
            SpawnSegment();
        }
    }

    private void Update()
    {
        EnsureInitialized();
        if (!TryGetDistance(car.position, out float carDistance)) return;
        // Use progress along the route: an end-point plane is unsafe on bends.
        while (activeSegments.Count > 1 && activeSegments.Peek().EndDistance < carDistance - behindBuffer)
            activeSegments.Dequeue().Release();
        while (activeSegments.Count < segmentCount || nextDistance < carDistance + aheadBuffer)
            SpawnSegment();
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

        // Apply the scene's palette to every streamed segment, including reused ones.
        Transform surface = segment.transform.Find("Road");
        if (roadSurfaceMaterial != null && surface != null &&
            surface.TryGetComponent(out Renderer surfaceRenderer))
            surfaceRenderer.sharedMaterial = roadSurfaceMaterial;

        if (enableCurves)
        {
            float angle = 0f;
            bool sCurve = false;
            float length = 20f;
            if (nextDistance >= behindBuffer + straightIntroDistance && !lastWasCurve)
            {
                sCurve = curveIndex++ % 3 == 1;
                float sign = routeHeading > 5f ? -1f : routeHeading < -5f ? 1f
                    : random.Next(2) == 0 ? -1f : 1f;
                angle = sign * Mathf.Min(bendAngle, maximumHeading - sign * routeHeading);
                length = Mathf.Max(40f, curveLength);
                if (!sCurve) routeHeading += angle;
                lastWasCurve = true;
            }
            else lastWasCurve = false;
            segment.ConfigureCurve(length, angle, sCurve);
        }
        segment.AlignStartTo(nextEntry);
        nextEntry = segment.ExitPose;

        activeSegments.Enqueue(new ActiveSegment(segment, pool, nextDistance));
        nextDistance += segment.Length;
    }

    private RoadSegment SelectPrefab()
    {
        if (activeSegments.Count == 0)
        {
            return segmentPrefabs[0];
        }

        return segmentPrefabs[random.Next(0, segmentPrefabs.Length)];
    }

    private ObjectPool<RoadSegment> GetPool(RoadSegment prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<RoadSegment> existing))
        {
            return existing;
        }

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
        // Scene unloading may destroy pooled children before Unity clears its
        // pool registry, especially with domain reload disabled in the editor.
        if (segment == null) return;
        if (Application.isPlaying) Destroy(segment.gameObject);
        else DestroyImmediate(segment.gameObject);
    }

    private void OnDestroy()
    {
        foreach (ObjectPool<RoadSegment> pool in pools.Values) pool.Clear();
        pools.Clear();
        activeSegments.Clear();
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
