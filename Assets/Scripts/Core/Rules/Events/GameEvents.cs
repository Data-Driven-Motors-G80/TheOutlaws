using System;
using UnityEngine;

public enum PickupKind
{
    Fuel,
    Ammo,
    Mystery
}

/// <summary>
/// Gameplay notifications shared by the tutorial, missions, HUD feedback and analytics.
/// Raisers never depend on who is listening.
/// </summary>
public static class GameEvents
{
    public static event Action<RandomPickupOutcome, RandomPickupOutcome, int> BackpackReplaced;
    public static event Action<RandomPickupOutcome> BackpackUsed;
    public static void RaiseBackpackReplaced(RandomPickupOutcome old, RandomPickupOutcome item, int lost) => BackpackReplaced?.Invoke(old, item, lost);
    public static void RaiseBackpackUsed(RandomPickupOutcome item) => BackpackUsed?.Invoke(item);
    public static event Action ShotFired;
    public static event Action DryFire;
    public static event Action ShotMissed;
    /// <summary>Colour before the hit, and whether the hit destroyed the block.</summary>
    public static event Action<TrafficObstacleState, bool> ObstacleShot;
    /// <summary>Colour the destroyed block started as.</summary>
    public static event Action<TrafficObstacleState> ObstacleDestroyed;
    /// <summary>Colour at impact, and whether a shield absorbed it.</summary>
    public static event Action<TrafficObstacleState, bool> ObstacleCollision;
    public static event Action PoliceContact;
    /// <summary>A block went past the car without being hit or destroyed (colour as it passed).</summary>
    public static event Action<TrafficObstacleState> ObstacleDodged;
    public static event Action<PickupKind> PickupSpawned;
    public static event Action<PickupKind> PickupMissed;
    /// <summary>Kind, plus an outcome name for mystery boxes.</summary>
    public static event Action<PickupKind, string> PickupCollected;
    /// <summary>The car drove through an ammo box while already carrying the maximum.</summary>
    public static event Action AmmoPickupWastedFull;
    public static event Action<TrafficRoadblockPattern> RoadblockSpawned;

    public static void RaiseShotFired() => ShotFired?.Invoke();
    public static void RaiseDryFire() => DryFire?.Invoke();
    public static void RaiseShotMissed() => ShotMissed?.Invoke();
    public static void RaiseObstacleShot(TrafficObstacleState before, bool destroyed) => ObstacleShot?.Invoke(before, destroyed);
    public static void RaiseObstacleDestroyed(TrafficObstacleState startedAs) => ObstacleDestroyed?.Invoke(startedAs);
    public static void RaiseObstacleCollision(TrafficObstacleState state, bool shielded) => ObstacleCollision?.Invoke(state, shielded);
    public static void RaisePoliceContact() => PoliceContact?.Invoke();
    public static void RaiseObstacleDodged(TrafficObstacleState state) => ObstacleDodged?.Invoke(state);
    public static void RaisePickupSpawned(PickupKind kind) => PickupSpawned?.Invoke(kind);
    public static void RaisePickupMissed(PickupKind kind) => PickupMissed?.Invoke(kind);
    public static void RaisePickupCollected(PickupKind kind, string outcome = null) => PickupCollected?.Invoke(kind, outcome);
    public static void RaiseAmmoPickupWastedFull() => AmmoPickupWastedFull?.Invoke();
    public static void RaiseRoadblockSpawned(TrafficRoadblockPattern pattern) => RoadblockSpawned?.Invoke(pattern);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BackpackReplaced = null;
        BackpackUsed = null;
        ShotFired = null;
        DryFire = null;
        ShotMissed = null;
        ObstacleShot = null;
        ObstacleDestroyed = null;
        ObstacleCollision = null;
        PoliceContact = null;
        ObstacleDodged = null;
        PickupSpawned = null;
        PickupMissed = null;
        PickupCollected = null;
        AmmoPickupWastedFull = null;
        RoadblockSpawned = null;
    }
}
