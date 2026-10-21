using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves obstacle rows towards the driver along the road, in any environment.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class ApproachingObstacleMotion : ObstacleMotion
{
    [SerializeField, Min(0f)] private float minObstacleSpeed = 20f;
    [SerializeField, Min(0f)] private float maxObstacleSpeed = 30f;

    private sealed class Mover
    {
        public Transform Transform;
        public InfiniteRoad Road;
        public float Distance, Speed, Lateral, HalfHeight;
    }

    private readonly List<Mover> movers = new List<Mover>();
    private int lastPlacementFrame = -1;
    private float rowSpeed;

    public override float ExtraClosingSpeed => Mathf.Max(minObstacleSpeed, maxObstacleSpeed);

    public override void Prepare(GameObject instance)
    {
        if (instance.GetComponent<Rigidbody>() != null) return;
        Rigidbody body = instance.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public override void Place(Transform obstacle, InfiniteRoad road, RoadPathPoint point,
        float lateral, float halfHeight)
    {
        if (road == null || !road.TryGetDistance(point.Position, out float distance)) return;
        if (Time.frameCount != lastPlacementFrame)
        {
            lastPlacementFrame = Time.frameCount;
            rowSpeed = Random.Range(Mathf.Min(minObstacleSpeed, maxObstacleSpeed), Mathf.Max(minObstacleSpeed, maxObstacleSpeed));
        }
        if (rowSpeed <= 0f) return;

        for (int i = movers.Count - 1; i >= 0; i--)
            if (movers[i].Transform == obstacle) movers.RemoveAt(i);
        movers.Add(new Mover { Transform = obstacle, Road = road, Distance = distance,
            Speed = rowSpeed, Lateral = lateral, HalfHeight = halfHeight });
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        for (int i = movers.Count - 1; i >= 0; i--)
        {
            Mover mover = movers[i];
            if (mover.Transform == null || !mover.Transform.gameObject.activeInHierarchy)
            {
                movers.RemoveAt(i);
                continue;
            }
            if (deltaTime <= 0f) continue;
            mover.Distance -= mover.Speed * deltaTime;
            if (!mover.Road.TrySample(mover.Distance, out RoadPathPoint point)) continue;
            mover.Transform.SetPositionAndRotation(
                point.Position + point.Right * mover.Lateral + point.Up * mover.HalfHeight,
                Quaternion.LookRotation(point.Forward, point.Up));
        }
    }
}
