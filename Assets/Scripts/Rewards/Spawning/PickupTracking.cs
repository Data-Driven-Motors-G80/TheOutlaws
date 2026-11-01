// Reports passed pickups once and removes expired tracking entries.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PickupTracking
{
    public static void ReportMissed<T>(List<T> tracked, InfiniteRoad road, Transform player,
        PickupKind kind, Func<T, bool> collected) where T : Component
    {
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            T pickup = tracked[i];

            if (collected(pickup))
                tracked.RemoveAt(i);
            else if (pickup == null || (road != null && road.IsBehind(pickup.transform.position, player.position, 4f)))
            {
                tracked.RemoveAt(i);
                GameEvents.RaisePickupMissed(kind);
            }
        }
    }
}
