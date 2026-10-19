using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SnowObstacleSpawner : ObstacleSpawner
{
    [Header("Snow: Frequency")]
    [Tooltip("Multiplies the gap between obstacle rows. 1 = same as city, 2 = half as many rows.")]
    [SerializeField, Min(1f)] private float rowSpacingMultiplier = 2f;
    [Tooltip("Multiplies the gap between roadblocks. 1 = same as city.")]
    [SerializeField, Min(1f)] private float roadblockSpacingMultiplier = 2f;

    [Header("Snow: Movement")]
    [Tooltip("Slowest obstacle speed in metres per second, on top of the car's own speed. 0 = stationary.")]
    [SerializeField, Min(0f)] private float minObstacleSpeed = 20f;
    [Tooltip("Fastest obstacle speed in metres per second. Each row of obstacles picks a random speed between min and max.")]
    [SerializeField, Min(0f)] private float maxObstacleSpeed = 30f;

    private sealed class Mover
    {
        public Transform Transform;
        public float Distance;
        public float Speed;
        public float Lateral;
        public float HalfHeight;
    }

    private readonly List<Mover> movers = new List<Mover>();
    private int lastPlacementFrame = -1;
    private float rowSpeed;

    public InfiniteRoad RoadReference => Road;

    protected override float RowSpacingMultiplier => rowSpacingMultiplier;
    protected override float RoadblockSpacingMultiplier => roadblockSpacingMultiplier;
    protected override float ExtraClosingSpeed => Mathf.Max(minObstacleSpeed, maxObstacleSpeed);

    protected override void OnObstacleCreated(GameObject instance)
    {
        if (instance.GetComponent<Rigidbody>() != null) return;
        Rigidbody body = instance.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    protected override void OnObstaclePlaced(Transform obstacle, RoadPathPoint point, float lateralOffset, float halfHeight)
    {
        if (SuppressAutoSpawn || Road == null) return;
        if (!Road.TryGetDistance(point.Position, out float distance)) return;

        if (Time.frameCount != lastPlacementFrame)
        {
            lastPlacementFrame = Time.frameCount;
            rowSpeed = Random.Range(Mathf.Min(minObstacleSpeed, maxObstacleSpeed), Mathf.Max(minObstacleSpeed, maxObstacleSpeed));
        }
        if (rowSpeed <= 0f) return;

        for (int i = movers.Count - 1; i >= 0; i--)
            if (movers[i].Transform == obstacle) movers.RemoveAt(i);
        movers.Add(new Mover { Transform = obstacle, Distance = distance, Speed = rowSpeed, Lateral = lateralOffset, HalfHeight = halfHeight });
    }

    private void LateUpdate()
    {
        if (movers.Count == 0) return;
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
            if (!Road.TrySample(mover.Distance, out RoadPathPoint point)) continue;
            mover.Transform.SetPositionAndRotation(
                point.Position + point.Right * mover.Lateral + point.Up * mover.HalfHeight,
                Quaternion.LookRotation(point.Forward, point.Up));
        }
    }
}