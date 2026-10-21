using UnityEngine;

/// <summary>Optional behaviour used by the shared obstacle spawner.</summary>
public abstract class ObstacleMotion : MonoBehaviour
{
    public abstract float ExtraClosingSpeed { get; }
    public abstract void Prepare(GameObject instance);
    public abstract void Place(Transform obstacle, InfiniteRoad road, RoadPathPoint point,
        float lateral, float halfHeight);
}
