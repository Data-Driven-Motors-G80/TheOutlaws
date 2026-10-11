using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class InfiniteRoad : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform car;

    [Header("Segments")]
    [SerializeField] private RoadSegment[] segmentPrefabs;

    [Header("Streaming")]
    [SerializeField, Min(2)] private int segmentCount = 8;
    [SerializeField, Min(0f)] private float behindBuffer = 20f;

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
        Vector3 origin = new Vector3(transform.position.x, transform.position.y, car.position.z - behindBuffer);
        nextEntry = new Pose(origin, Quaternion.identity);

        for (int i = 0; i < segmentCount; i++)
        {
            SpawnSegment();
        }
    }

    private void Update()
    {
        EnsureInitialized();
        while (activeSegments.Count > 0 && activeSegments.Peek().Segment.DistancePastEnd(car.position) > behindBuffer)
        {
            activeSegments.Dequeue().Release();
            SpawnSegment();
        }
    }

    public bool TryGetPathPoint(Vector3 worldPosition, out RoadPathPoint pathPoint)
    {
        EnsureInitialized();
        pathPoint = default;
        float closestSqrDistance = float.MaxValue;
        bool found = false;

        foreach (ActiveSegment active in activeSegments)
        {
            RoadSegment segment = active.Segment;
            Vector3 from = segment.GetPathPoint(0);

            for (int i = 1; i < segment.PathPointCount; i++)
            {
                Vector3 to = segment.GetPathPoint(i);
                Vector3 direction = to - from;
                float sqrLength = direction.sqrMagnitude;

                if (sqrLength > Mathf.Epsilon)
                {
                    float t = Mathf.Clamp01(Vector3.Dot(worldPosition - from, direction) / sqrLength);
                    Vector3 closest = from + direction * t;
                    float sqrDistance = (worldPosition - closest).sqrMagnitude;

                    if (sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        pathPoint = new RoadPathPoint(closest, direction / Mathf.Sqrt(sqrLength), segment.Width);
                        found = true;
                    }
                }

                from = to;
            }
        }

        return found;
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
        }

        return true;
    }

    private void SpawnSegment()
    {
        ObjectPool<RoadSegment> pool = GetPool(SelectPrefab());
        RoadSegment segment = pool.Get();

        segment.AlignStartTo(nextEntry);
        nextEntry = segment.ExitPose;

        activeSegments.Enqueue(new ActiveSegment(segment, pool));
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
            actionOnDestroy: segment => Destroy(segment.gameObject));

        pools.Add(prefab, pool);
        return pool;
    }

    private void LogError(string message, Object context = null)
    {
        Debug.LogError($"{nameof(InfiniteRoad)}: {message}", context != null ? context : this);
    }

    private readonly struct ActiveSegment
    {
        private readonly ObjectPool<RoadSegment> pool;

        public ActiveSegment(RoadSegment segment, ObjectPool<RoadSegment> pool)
        {
            Segment = segment;
            this.pool = pool;
        }

        public RoadSegment Segment { get; }

        public void Release()
        {
            pool.Release(Segment);
        }
    }
}