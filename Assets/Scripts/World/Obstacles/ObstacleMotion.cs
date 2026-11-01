// Placement and approach-speed contract for obstacle motion behaviours.
using UnityEngine;

public abstract class ObstacleMotion : MonoBehaviour
{
    public virtual float ApproachSpeed(Transform obstacle) => 0f;
    public virtual float NextRowSpeed => 0f;
    public abstract float ExtraClosingSpeed { get; }
    public abstract void Prepare(GameObject instance);
    public abstract void Place(Transform obstacle, InfiniteRoad road, RoadPathPoint point,
        float lateral, float halfHeight);
}
