// Accumulates gameplay events and measurements for the current run.
using System;
using System.Collections.Generic;
using UnityEngine;

public struct RunTickContext
{
    public float DeltaTime;
    public float Elapsed;
    public float Distance;
    public float FuelFraction;
    public float PursuitGap;
    public float Speed;
    public bool Snow;
    public bool Ocean;
    public bool Desert;

    public float RawSteer;
    public bool Reversed;
    public bool Nitro;
    public bool ShieldReady;
    public int Ammo;
    public int MaximumAmmo;
}

public static class RunTracker
{
    public const float CheckpointSpacing = 250f;

    public static bool Active { get; private set; }
    public static string RunId { get; private set; }
    public static int RunNumberInSession { get; private set; }
    public static bool TutorialWasOn { get; private set; }

    public static float StartedRealtime { get; private set; }
    public static readonly MissionRunStats Stats = new MissionRunStats();
    public static readonly List<MissionDefinition> ActiveMissions = new List<MissionDefinition>();
    public static readonly List<MissionDefinition> CompletedThisRun = new List<MissionDefinition>();

    public static event Action<MissionDefinition> MissionCompleted;

    public static int ShotsFired, ShotsHit, ShotsMissed, DryFires;
    public static readonly int[] DestroyedByColour = new int[3];
    public static readonly int[] ShotsOnColour = new int[3];

    public static readonly int[] CollisionsByColour = new int[3];
    public static readonly int[] DodgedByColour = new int[3];
    public static int CollisionsWithNitro;
    public static int ShieldedCollisions, PoliceContacts, CollisionsWhileReversed;
    public static float FirstHitDistance = -1f, LastHitElapsed = -1f;

    public static readonly int[] Spawned = new int[3];
    public static readonly int[] Collected = new int[3];
    public static readonly int[] Missed = new int[3];
    public static int AmmoWastedFull;
    public static int GunRewards, BackpackReplacements, DiscardedBullets, BackpackActivations;
    private static float backpackStoredAt;
    public static readonly Dictionary<string, int> MysteryOutcomes = new Dictionary<string, int>();
    public static int Roadblocks;

    public static float CitySeconds, SnowSeconds, OceanSeconds, DesertSeconds, SecondsBelowQuarterFuel, SecondsSteering;
    public static float SecondsReversed, SecondsNitro, SecondsShielded, SecondsAmmoFull, SecondsAmmoEmpty;
    public static float MinGap = float.MaxValue, MaxSpeed, SnowReachedAt = -1f;
    public static int SteerDirectionChanges, PoliceAlerts, EnvironmentSwitches;
    public static int Pauses, FocusLosses;
    public static float PausedSeconds;
    public static bool LowFuelReported;
    public static float LastDistance, LastElapsed;

    public static float AmmoSecondsSum, HoldSecondsSum;
    public static int HeldBulletsFired;
    public static readonly List<int> AmmoTimeline = new List<int>();
    public static readonly List<int> ShotsTimeline = new List<int>();
    public const float TimelineStep = 10f;

    public static bool ReversedNow, NitroNow, ShieldNow;
    public static int AmmoNow;
    public static float FuelNow = 1f;
    public static string LastHitColour = "";
    private static readonly List<float> hitTimes = new List<float>();
    private static readonly Queue<float> ammoPickupTimes = new Queue<float>();
    private static float nextTimelineSample;
    private static int shotsThisStep;

    private static int cpShots, cpAmmo, cpDestroyed, cpDodged, cpHits;

    private static float lastSteerSign;
    private static float baseTotalDistance;
    private static bool ammoSeeded;
    private static int nextCheckpoint;
    private static bool reversedNow;
    private static int lastAmmo;
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
        subscribed = false;
        RunNumberInSession = 0;
        MissionCompleted = null;
    }

    public static void Begin(bool tutorialWasOn)
    {
        Subscribe();
        Active = true;
        RunId = Guid.NewGuid().ToString("N").Substring(0, 16);
        RunNumberInSession++;
        TutorialWasOn = tutorialWasOn;
        StartedRealtime = Time.realtimeSinceStartup;

        Stats.Distance = 0f;
        Stats.Hits = 0;
        Stats.ObstaclesDestroyed = 0;
        Stats.RedDestroyed = 0;
        Stats.MysteryCollected = 0;
        Stats.AmmoPickups = 0;
        Stats.ReachedSnow = false;
        Stats.MinFuelFraction = 1f;
        Stats.FuelMissed = 0;

        Stats.TotalDistance = baseTotalDistance = OutlawProfile.TotalDistance;
        Stats.TotalDestroyed = OutlawProfile.TotalDestroyed;
        Stats.TotalAmmoPickups = OutlawProfile.TotalAmmoPickups;
        OutlawProfile.ResetRunMissions();
        ActiveMissions.Clear();
        ActiveMissions.AddRange(OutlawProfile.VisibleMissions());
        CompletedThisRun.Clear();

        ShotsFired = ShotsHit = ShotsMissed = DryFires = 0;
        Array.Clear(DestroyedByColour, 0, 3);
        Array.Clear(CollisionsByColour, 0, 3);
        Array.Clear(DodgedByColour, 0, 3);
        Array.Clear(ShotsOnColour, 0, 3);
        CollisionsWithNitro = 0;
        AmmoSecondsSum = HoldSecondsSum = 0f;
        HeldBulletsFired = 0;
        AmmoTimeline.Clear();
        ShotsTimeline.Clear();
        ReversedNow = NitroNow = ShieldNow = false;
        AmmoNow = 0;
        FuelNow = 1f;
        LastHitColour = "";
        hitTimes.Clear();
        ammoPickupTimes.Clear();
        nextTimelineSample = 0f;
        shotsThisStep = 0;
        cpShots = cpAmmo = cpDestroyed = cpDodged = cpHits = 0;
        ammoSeeded = false;
        ShieldedCollisions = PoliceContacts = CollisionsWhileReversed = 0;
        FirstHitDistance = LastHitElapsed = -1f;
        Array.Clear(Spawned, 0, 3);
        Array.Clear(Collected, 0, 3);
        Array.Clear(Missed, 0, 3);
        GunRewards = BackpackReplacements = DiscardedBullets = BackpackActivations = 0;
        backpackStoredAt = 0f;
        Stats.ShotsFired = 0;
        AmmoWastedFull = 0;
        MysteryOutcomes.Clear();
        Roadblocks = 0;
        CitySeconds = SnowSeconds = OceanSeconds = DesertSeconds = SecondsBelowQuarterFuel = SecondsSteering = 0f;
        SecondsReversed = SecondsNitro = SecondsShielded = SecondsAmmoFull = SecondsAmmoEmpty = 0f;
        MinGap = float.MaxValue;
        MaxSpeed = 0f;
        SnowReachedAt = -1f;
        SteerDirectionChanges = PoliceAlerts = EnvironmentSwitches = 0;
        Pauses = FocusLosses = 0;
        PausedSeconds = 0f;
        LowFuelReported = false;
        LastDistance = LastElapsed = 0f;
        lastSteerSign = 0f;
        nextCheckpoint = 1;
        reversedNow = false;
        lastAmmo = 0;
    }

    public static void End()
    {
        if (!Active) return;
        Active = false;
        OutlawProfile.AddRunTotals(Stats.Distance, Stats.ObstaclesDestroyed, Stats.AmmoPickups);
    }

    public static void Tick(RunTickContext c)
    {
        if (!Active) return;
        float dt = c.DeltaTime;
        LastDistance = c.Distance;
        LastElapsed = c.Elapsed;
        Stats.Distance = c.Distance;
        Stats.TotalDistance = baseTotalDistance + c.Distance;
        Stats.MinFuelFraction = Mathf.Min(Stats.MinFuelFraction, c.FuelFraction);
        if (c.Snow)
        {
            SnowSeconds += dt;
            if (!Stats.ReachedSnow)
            {
                Stats.ReachedSnow = true;
                SnowReachedAt = c.Distance;
            }
        }
        else if (c.Ocean) OceanSeconds += dt;
        else if (c.Desert) DesertSeconds += dt;
        else CitySeconds += dt;

        if (c.FuelFraction < 0.25f) SecondsBelowQuarterFuel += dt;
        if (!LowFuelReported && c.FuelFraction < 0.2f)
        {
            LowFuelReported = true;
            WeeWooAnalytics.Track("low_fuel", "fuel_pct", Mathf.RoundToInt(c.FuelFraction * 100f));
        }

        MinGap = Mathf.Min(MinGap, c.PursuitGap);
        MaxSpeed = Mathf.Max(MaxSpeed, c.Speed);
        reversedNow = c.Reversed;
        if (c.Reversed) SecondsReversed += dt;
        if (c.Nitro) SecondsNitro += dt;
        if (c.ShieldReady) SecondsShielded += dt;
        lastAmmo = c.Ammo;
        ReversedNow = c.Reversed;
        NitroNow = c.Nitro;
        ShieldNow = c.ShieldReady;
        AmmoNow = c.Ammo;
        FuelNow = c.FuelFraction;
        AmmoSecondsSum += c.Ammo * dt;
        if (!ammoSeeded)
        {

            for (int i = 0; i < c.Ammo; i++) ammoPickupTimes.Enqueue(0f);
            ammoSeeded = true;
        }
        while (c.Elapsed >= nextTimelineSample)
        {
            AmmoTimeline.Add(c.Ammo);
            ShotsTimeline.Add(shotsThisStep);
            shotsThisStep = 0;
            nextTimelineSample += TimelineStep;
        }
        if (c.Ammo >= c.MaximumAmmo) SecondsAmmoFull += dt;
        if (c.Ammo <= 0) SecondsAmmoEmpty += dt;

        float sign = Mathf.Abs(c.RawSteer) > 0.1f ? Mathf.Sign(c.RawSteer) : 0f;
        if (sign != 0f)
        {
            SecondsSteering += dt;
            if (lastSteerSign != 0f && sign != lastSteerSign) SteerDirectionChanges++;
            lastSteerSign = sign;
        }

        while (c.Distance >= nextCheckpoint * CheckpointSpacing)
        {
            WeeWooAnalytics.Track("checkpoint", "m", nextCheckpoint * CheckpointSpacing,
                "gap", Round(c.PursuitGap), "fuel_pct", Mathf.RoundToInt(c.FuelFraction * 100f),
                "ammo", c.Ammo, "speed", Round(c.Speed), "hits", Stats.Hits,
                "shots_since", cpShots, "ammo_picked_since", cpAmmo, "destroyed_since", cpDestroyed,
                "dodged_since", cpDodged, "hits_since", cpHits);
            cpShots = cpAmmo = cpDestroyed = cpDodged = cpHits = 0;
            nextCheckpoint++;
        }

        CheckMissions();
    }

    // Evaluate goals against the latest run statistics.
    public static void CheckMissions()
    {
        bool unlocked = false;
        foreach (MissionDefinition mission in ActiveMissions)
        {
            if (CompletedThisRun.Contains(mission) || !mission.IsComplete(Stats)) continue;
            CompletedThisRun.Add(mission);
            unlocked = true;
            OutlawProfile.CompleteMission(mission.Id);
            WeeWooAnalytics.Track("mission_complete", "mission_id", mission.Id, "mission", mission.Title,
                "run_number_total", OutlawProfile.TotalRuns);
            MissionCompleted?.Invoke(mission);
        }
        if (unlocked)
        {
            ActiveMissions.Clear();
            ActiveMissions.AddRange(OutlawProfile.VisibleMissions());
        }
    }

    public static float SecondsSinceLastHit => LastHitElapsed < 0f ? -1f : LastElapsed - LastHitElapsed;
    public static int TotalCollisions => CollisionsByColour[0] + CollisionsByColour[1] + CollisionsByColour[2];

    // Connect event counters once per tracking session.
    private static void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        GameEvents.ShotFired += OnShotFired;
        GameEvents.ObstacleDodged += OnDodged;
        GameEvents.DryFire += OnDryFire;
        GameEvents.ShotMissed += () => { if (Active) ShotsMissed++; };
        GameEvents.ObstacleShot += (colour, destroyed) =>
        {
            if (!Active) return;
            ShotsHit++;
            ShotsOnColour[(int)colour]++;
        };
        GameEvents.ObstacleDestroyed += OnDestroyed;
        GameEvents.ObstacleCollision += OnCollision;
        GameEvents.PoliceContact += OnPoliceContact;
        GameEvents.PickupSpawned += kind => { if (Active) Spawned[(int)kind]++; };
        GameEvents.PickupMissed += OnMissed;
        GameEvents.PickupCollected += OnCollected;
        GameEvents.BackpackReplaced += (old, item, lost) => {
            if (!Active) return;
            if (old != RandomPickupOutcome.None) BackpackReplacements++;
            DiscardedBullets += lost;
            ammoPickupTimes.Clear();
            if (item == RandomPickupOutcome.Gun) {
                GunRewards++;
                for (int i = 0; i < BackpackState.GunShots; i++) ammoPickupTimes.Enqueue(LastElapsed);
            }
            WeeWooAnalytics.Track("backpack_received", "item", item.ToString(), "replaced", old.ToString(),
                "discarded_bullets", lost, "previous_hold_s", Round(LastElapsed - backpackStoredAt));
            backpackStoredAt = LastElapsed;
        };
        GameEvents.BackpackUsed += item => {
            if (!Active) return;
            BackpackActivations++;
            WeeWooAnalytics.Track("backpack_used", "item", item.ToString(), "held_s", Round(LastElapsed - backpackStoredAt));
        };
        GameEvents.AmmoPickupWastedFull += () => { if (Active) AmmoWastedFull++; };
        GameEvents.RoadblockSpawned += pattern => { if (Active) Roadblocks++; };
    }

    private static void OnShotFired()
    {
        if (!Active) return;
        ShotsFired++;
        Stats.ShotsFired++;
        shotsThisStep++;
        cpShots++;

        if (ammoPickupTimes.Count > 0)
        {
            HoldSecondsSum += LastElapsed - ammoPickupTimes.Dequeue();
            HeldBulletsFired++;
        }
    }

    private static void OnDodged(TrafficObstacleState colour)
    {
        if (!Active) return;
        DodgedByColour[(int)colour]++;
        cpDodged++;
    }

    public static int HitsInLast(float seconds)
    {
        int count = 0;
        foreach (float t in hitTimes)
            if (LastElapsed - t <= seconds) count++;
        return count;
    }

    public static string MissionProgress()
    {
        var parts = new List<string>();
        foreach (MissionDefinition mission in ActiveMissions)
            parts.Add(mission.Id + ":" + (CompletedThisRun.Contains(mission) ? "done" : mission.Progress(Stats)));
        return string.Join("|", parts);
    }

    public static string Series(List<int> values) => string.Join(",", values);

    private static void OnDryFire()
    {
        if (!Active) return;
        DryFires++;
        if (DryFires == 1) WeeWooAnalytics.Track("dry_fire_first");
    }

    private static void OnDestroyed(TrafficObstacleState startedAs)
    {
        if (!Active) return;
        DestroyedByColour[(int)startedAs]++;
        Stats.ObstaclesDestroyed++;
        Stats.TotalDestroyed++;
        cpDestroyed++;
        if (startedAs == TrafficObstacleState.Red) Stats.RedDestroyed++;
        WeeWooAnalytics.Track("obstacle_destroyed", "colour", startedAs.ToString());
        CheckMissions();
    }

    private static void OnCollision(TrafficObstacleState colour, bool shielded)
    {
        if (!Active) return;
        CollisionsByColour[(int)colour]++;
        if (shielded) ShieldedCollisions++;
        if (NitroNow) CollisionsWithNitro++;
        LastHitColour = colour.ToString();
        hitTimes.Add(LastElapsed);
        cpHits++;
        if (reversedNow) CollisionsWhileReversed++;
        RegisterHit();
        WeeWooAnalytics.Track("collision", "colour", colour.ToString(), "shielded", shielded,
            "reversed", reversedNow, "ammo", lastAmmo, "could_shoot", lastAmmo >= (int)colour + 1);
    }

    private static void OnPoliceContact()
    {
        if (!Active) return;
        PoliceContacts++;
        RegisterHit();
    }

    private static void RegisterHit()
    {
        Stats.Hits++;
        if (FirstHitDistance < 0f) FirstHitDistance = LastDistance;
        LastHitElapsed = LastElapsed;
    }

    private static void OnMissed(PickupKind kind)
    {
        if (!Active) return;
        Missed[(int)kind]++;
        if (kind == PickupKind.Fuel) Stats.FuelMissed++;
        WeeWooAnalytics.Track("pickup_missed", "kind", kind.ToString());
    }

    private static void OnCollected(PickupKind kind, string outcome)
    {
        if (!Active) return;
        Collected[(int)kind]++;
        if (kind == PickupKind.Ammo)
        {
            Stats.AmmoPickups++;
            Stats.TotalAmmoPickups++;
            ammoPickupTimes.Enqueue(LastElapsed);
            cpAmmo++;
        }
        if (kind == PickupKind.Mystery)
        {
            Stats.MysteryCollected++;
            string key = string.IsNullOrEmpty(outcome) ? "Unknown" : outcome;
            MysteryOutcomes[key] = MysteryOutcomes.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        WeeWooAnalytics.Track("pickup", "kind", kind.ToString(), "outcome", outcome ?? "");
        CheckMissions();
    }

    public static float Round(float value) => Mathf.Round(value * 10f) / 10f;
}
