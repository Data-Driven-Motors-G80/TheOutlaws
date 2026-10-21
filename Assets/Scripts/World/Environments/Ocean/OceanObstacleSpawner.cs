using UnityEngine;

/// <summary>Reuses fair rows, shooting, shield handling and pooling for prototype reef/buoy obstacles.</summary>
[DisallowMultipleComponent]
public sealed class OceanObstacleSpawner : ObstacleSpawner
{
    protected override float RowSpacingMultiplier => 1.15f;

    protected override void OnObstaclePlaced(Transform obstacle, RoadPathPoint point, float lateralOffset, float halfHeight)
    {
        base.OnObstaclePlaced(obstacle, point, lateralOffset, halfHeight);
        Renderer body = obstacle.GetComponentInChildren<Renderer>();
        TrafficLightObstacle traffic = obstacle.GetComponent<TrafficLightObstacle>();
        if (traffic != null && body != null)
            traffic.SetEnvironmentLabel(body.localBounds.size.x * Mathf.Abs(body.transform.lossyScale.x) >= 2.8f ? "REEF" : "BUOY");
    }
}
