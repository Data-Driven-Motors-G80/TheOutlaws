// Adds desert-specific shot handling to normal obstacle spawning.
using UnityEngine;

public sealed class DesertObstacleSpawner : ObstacleSpawner
{
    protected override float RowSpacingMultiplier => 1f;

    public override bool TryApplyShot(Collider hitCollider)
    {
        if (hitCollider != null && hitCollider.transform.IsChildOf(transform))
        {
            DesertRollingRock rock = hitCollider.GetComponentInParent<DesertRollingRock>();
            if (rock != null) return rock.TakeShot();
        }
        return base.TryApplyShot(hitCollider);
    }
}
