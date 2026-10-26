using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps pickups reachable: F, A and ? boxes never sit inside or just behind a block, so
/// a player who collects every fuel tank can never run dry.
/// </summary>
/// <summary>How far ahead blocks and pickups appear, so nothing pops up as a surprise.</summary>
public static class PickupLookAhead
{
    public const float Distance = 95f;
}

public static class PickupPlacement
{
    private const float CheckRadius = 2f;
    private const float ApproachLength = 7f;
    private const float PickupSeparation = 4f;

    private static readonly Collider[] hits = new Collider[64];

    /// <summary>The lateral offset closest to <paramref name="preferred"/> whose approach is free of blocks.</summary>
    public static float ClearLateral(RoadPathPoint point, float preferred, Transform ignore = null)
    {
        float laneWidth = point.Width / 3f;
        var candidates = new List<float> { preferred, -laneWidth, 0f, laneWidth };
        candidates.Sort((a, b) => Mathf.Abs(a - preferred).CompareTo(Mathf.Abs(b - preferred)));
        Physics.SyncTransforms();
        foreach (float lateral in candidates)
            if (!IsBlocked(point, lateral, ignore)) return lateral;
        return preferred;
    }

    /// <summary>After a row of blocks is placed, moves any pickup it covers into the row's open lane.</summary>
    public static void ClearRow(RoadPathPoint point, float openLateral)
    {
        Physics.SyncTransforms();
        Vector3 centre = point.Position + point.Up;
        var halfExtents = new Vector3(point.Width * 0.5f + 1f, 2f, ApproachLength);
        int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, hits,
            Quaternion.LookRotation(point.Forward, point.Up), ~0, QueryTriggerInteraction.Collide);
        var moved = new HashSet<Transform>();
        int relocated = 0;
        for (int i = 0; i < count; i++)
        {
            Transform pickup = PickupRoot(hits[i]);
            if (pickup == null || !moved.Add(pickup)) continue;
            float lateral = Vector3.Dot(pickup.position - point.Position, point.Right);
            if (Mathf.Abs(lateral - openLateral) < point.Width / 6f) continue;
            pickup.position += point.Right * (openLateral - lateral);
            // Two pickups moved into the same lane are spread out along the road.
            pickup.position += point.Forward * (5f * relocated);
            relocated++;
        }
    }

    /// <summary>
    /// A pickup's first-frame check: a block placed in the same frame is invisible to physics
    /// until the next step, so pickups confirm their spot once and slide to a clear lane.
    /// </summary>
    public static void EnsureClear(Transform pickup)
    {
        InfiniteRoad road = Object.FindFirstObjectByType<InfiniteRoad>();
        if (pickup == null || road == null || !road.TryGetPathPoint(pickup.position, out RoadPathPoint point)) return;
        float lateral = Vector3.Dot(pickup.position - point.Position, point.Right);
        float clear = ClearLateral(point, lateral, pickup);
        if (!Mathf.Approximately(clear, lateral)) pickup.position += point.Right * (clear - lateral);
    }

    private static bool IsBlocked(RoadPathPoint point, float lateral, Transform ignore)
    {
        // Check the pickup's spot and the stretch of road leading up to it for blocks...
        for (float back = 0f; back <= ApproachLength; back += ApproachLength * 0.5f)
        {
            Vector3 position = point.Position + point.Right * lateral - point.Forward * back + point.Up * 0.6f;
            int count = Physics.OverlapSphereNonAlloc(position, CheckRadius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].GetComponentInParent<TrafficLightObstacle>() != null) return true;
        }

        // ...and never put two pickups on top of each other.
        Vector3 spot = point.Position + point.Right * lateral + point.Up * 0.6f;
        int pickups = Physics.OverlapSphereNonAlloc(spot, PickupSeparation, hits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < pickups; i++)
        {
            Transform other = PickupRoot(hits[i]);
            if (other != null && other != ignore) return true;
        }
        return false;
    }

    private static Transform PickupRoot(Collider collider)
    {
        if (collider == null || !collider.isTrigger) return null;
        FuelTank tank = collider.GetComponentInParent<FuelTank>();
        if (tank != null) return tank.transform;
        OutlawAmmoPickup ammo = collider.GetComponentInParent<OutlawAmmoPickup>();
        if (ammo != null) return ammo.transform;
        PickupItem mystery = collider.GetComponentInParent<PickupItem>();
        return mystery != null && mystery.ClaimRow == null ? mystery.transform : null;
    }
}
