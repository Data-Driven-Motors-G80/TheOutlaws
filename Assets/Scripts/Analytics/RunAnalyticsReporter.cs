using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds the existing analytics payloads without owning gameplay or run state.</summary>
public sealed class RunAnalyticsReporter : IDisposable
{
    private readonly OutlawGameManager game;
    private readonly Func<int> sessionRuns;
    private float elapsed => game.ElapsedSeconds;
    private float DistanceTravelled => game.DistanceTravelled;
    private bool IsTutorial => game.IsTutorial;
    private bool IsNewBest => game.IsNewBest;
    private OutlawGameState State => game.State;
    private TutorialDirector tutorial => game.Tutorial;
    private OutlawShooting shooting => game.Shooting;
    private int runsThisSession => sessionRuns();

    public RunAnalyticsReporter(OutlawGameManager game, Func<int> sessionRuns)
    {
        this.game = game;
        this.sessionRuns = sessionRuns;
    }

    public void Attach()
    {
        WeeWooAnalytics.ContextProvider = AnalyticsContext;
        WeeWooAnalytics.ExitRowsProvider = ExitRows;
    }

    public void Dispose()
    {
        if (WeeWooAnalytics.ContextProvider == (Func<Dictionary<string, object>>)AnalyticsContext)
            WeeWooAnalytics.ContextProvider = null;
        if (WeeWooAnalytics.ExitRowsProvider == (Func<List<KeyValuePair<string, Dictionary<string, object>>>>)ExitRows)
            WeeWooAnalytics.ExitRowsProvider = null;
    }

    public void RecordRunEnd(string reason)
    {
        if (!RunTracker.Active) return;
        RunTracker.CheckMissions();
        Dictionary<string, object> row = BuildRunRow(reason);
        RunTracker.End();
        WeeWooAnalytics.AddRow("runs", row);
        WeeWooAnalytics.Track("run_end", "reason", reason, "distance_m", Mathf.RoundToInt(DistanceTravelled),
            "survival_s", RunTracker.Round(elapsed), "missions_completed", MissionIds(RunTracker.CompletedThisRun));
        WeeWooAnalytics.Flush();
    }

    private Dictionary<string, object> AnalyticsContext()
    {
        return new Dictionary<string, object>
        {
            ["scene"] = SceneManager.GetActiveScene().name,
            ["state"] = IsTutorial ? "Tutorial" : State.ToString(),
            ["run_id"] = RunTracker.Active ? RunTracker.RunId : "",
            ["run_t"] = RunTracker.Round(elapsed),
            ["dist"] = Mathf.RoundToInt(State == OutlawGameState.Ready || IsTutorial ? 0f : DistanceTravelled)
        };
    }

    /// <summary>Rows the browser beacon delivers if the tab is closed right now.</summary>
    private List<KeyValuePair<string, Dictionary<string, object>>> ExitRows()
    {
        var rows = new List<KeyValuePair<string, Dictionary<string, object>>>();
        string where = State == OutlawGameState.Paused ? (IsTutorial ? "paused_tutorial" : "paused_run")
            : IsTutorial ? "tutorial" : State == OutlawGameState.Ready ? "title"
            : State == OutlawGameState.Running ? "mid_run" : "game_over_screen";
        rows.Add(new KeyValuePair<string, Dictionary<string, object>>("sessions", new Dictionary<string, object>
        {
            ["session_s"] = RunTracker.Round(WeeWooAnalytics.SessionSeconds),
            ["runs_this_session"] = runsThisSession,
            ["quit_at"] = where,
            ["tutorial_completed_ever"] = OutlawProfile.TutorialEverCompleted,
            ["missions_done"] = OutlawProfile.MissionsDoneCount,
            ["total_runs"] = OutlawProfile.TotalRuns
        }));
        if (RunTracker.Active)
            rows.Add(new KeyValuePair<string, Dictionary<string, object>>("runs", BuildRunRow("quit_tab_closed")));
        if (IsTutorial)
            rows.Add(new KeyValuePair<string, Dictionary<string, object>>("tutorials", ExitTutorialRow("abandoned")));
        return rows;
    }

    public Dictionary<string, object> ExitTutorialRow(string status)
    {
        Dictionary<string, object> row = tutorial.AbandonRow();
        row["status"] = status;
        return row;
    }

    private Dictionary<string, object> BuildRunRow(string reason)
    {
        float minutes = Mathf.Max(elapsed / 60f, 0.01f);
        float sinceHit = RunTracker.SecondsSinceLastHit;
        int[] spawned = RunTracker.Spawned, collected = RunTracker.Collected, missed = RunTracker.Missed;
        var row = new Dictionary<string, object>
        {
            ["run_id"] = RunTracker.RunId,
            ["run_number_session"] = RunTracker.RunNumberInSession,
            ["run_number_total"] = OutlawProfile.TotalRuns,
            ["tutorial_was_on"] = RunTracker.TutorialWasOn,
            ["end_reason"] = reason,
            ["distance_m"] = Mathf.RoundToInt(DistanceTravelled),
            ["survival_s"] = RunTracker.Round(elapsed),
            ["run_minutes"] = RunTracker.Round(elapsed / 60f),
            ["wallclock_s"] = RunTracker.Round(Time.realtimeSinceStartup - RunTracker.StartedRealtime),
            ["max_speed"] = RunTracker.Round(RunTracker.MaxSpeed),
            ["end_scene"] = SceneManager.GetActiveScene().name,
            ["reached_snow"] = RunTracker.Stats.ReachedSnow,
            ["snow_at_m"] = Mathf.RoundToInt(RunTracker.SnowReachedAt),
            ["city_s"] = RunTracker.Round(RunTracker.CitySeconds),
            ["snow_s"] = RunTracker.Round(RunTracker.SnowSeconds),
            ["gun_rewards"] = RunTracker.GunRewards,
            ["backpack_replacements"] = RunTracker.BackpackReplacements,
            ["discarded_bullets"] = RunTracker.DiscardedBullets,
            ["backpack_uses"] = RunTracker.BackpackActivations,
            ["backpack_at_end"] = game.PickupEffects.Backpack.Item.ToString(),
            ["shots_fired"] = RunTracker.ShotsFired,
            ["shots_hit"] = RunTracker.ShotsHit,
            ["shots_missed"] = RunTracker.ShotsMissed,
            ["accuracy_pct"] = RunTracker.ShotsFired > 0 ? Mathf.RoundToInt(100f * RunTracker.ShotsHit / RunTracker.ShotsFired) : -1,
            ["dry_fires"] = RunTracker.DryFires,
            ["destroyed_green"] = RunTracker.DestroyedByColour[0],
            ["destroyed_yellow"] = RunTracker.DestroyedByColour[1],
            ["destroyed_red"] = RunTracker.DestroyedByColour[2],
            ["hits_green"] = RunTracker.CollisionsByColour[0],
            ["hits_yellow"] = RunTracker.CollisionsByColour[1],
            ["hits_red"] = RunTracker.CollisionsByColour[2],
            ["hits_total"] = RunTracker.TotalCollisions,
            ["hits_shielded"] = RunTracker.ShieldedCollisions,
            ["hits_while_reversed"] = RunTracker.CollisionsWhileReversed,
            ["police_contacts"] = RunTracker.PoliceContacts,
            ["first_hit_m"] = Mathf.RoundToInt(RunTracker.FirstHitDistance),
            ["s_since_last_hit"] = RunTracker.Round(sinceHit),
            ["caught_within_5s_of_hit"] = reason == "police" && sinceHit >= 0f && sinceHit <= 5f,
            ["fuel_spawned"] = spawned[(int)PickupKind.Fuel],
            ["fuel_collected"] = collected[(int)PickupKind.Fuel],
            ["fuel_missed"] = missed[(int)PickupKind.Fuel],
            ["ammo_spawned"] = spawned[(int)PickupKind.Ammo],
            ["ammo_collected"] = collected[(int)PickupKind.Ammo],
            ["ammo_missed"] = missed[(int)PickupKind.Ammo],
            ["ammo_wasted_full"] = RunTracker.AmmoWastedFull,
            ["mystery_spawned"] = spawned[(int)PickupKind.Mystery],
            ["mystery_collected"] = collected[(int)PickupKind.Mystery],
            ["mystery_missed"] = missed[(int)PickupKind.Mystery],
            ["mystery_shield"] = Outcome("Shield"),
            ["mystery_nitro"] = Outcome("Nitro"),
            ["mystery_recovery"] = Outcome("ProximityRecovery"),
            ["mystery_reverse"] = Outcome("ReverseSteering"),
            ["mystery_sunny"] = Outcome("SunnyDay"),
            ["roadblocks"] = RunTracker.Roadblocks,
            ["min_fuel_pct"] = Mathf.RoundToInt(RunTracker.Stats.MinFuelFraction * 100f),
            ["s_below_25_fuel"] = RunTracker.Round(RunTracker.SecondsBelowQuarterFuel),
            ["min_police_gap"] = RunTracker.MinGap < float.MaxValue ? RunTracker.Round(RunTracker.MinGap) : -1f,
            ["police_alerts"] = RunTracker.PoliceAlerts,
            ["steer_pct"] = elapsed > 0f ? Mathf.RoundToInt(100f * RunTracker.SecondsSteering / elapsed) : 0,
            ["steer_changes_per_min"] = RunTracker.Round(RunTracker.SteerDirectionChanges / minutes),
            ["s_reversed"] = RunTracker.Round(RunTracker.SecondsReversed),
            ["s_nitro"] = RunTracker.Round(RunTracker.SecondsNitro),
            ["s_shield_ready"] = RunTracker.Round(RunTracker.SecondsShielded),
            ["s_ammo_full"] = RunTracker.Round(RunTracker.SecondsAmmoFull),
            ["s_ammo_empty"] = RunTracker.Round(RunTracker.SecondsAmmoEmpty),
            ["ammo_end"] = shooting.CurrentAmmo,
            ["pauses"] = RunTracker.Pauses,
            ["paused_s"] = RunTracker.Round(RunTracker.PausedSeconds),
            ["focus_losses"] = RunTracker.FocusLosses,
            ["env_switches"] = RunTracker.EnvironmentSwitches,
            ["missions_active"] = MissionIds(RunTracker.ActiveMissions),
            ["missions_completed"] = MissionIds(RunTracker.CompletedThisRun),
            ["missions_completed_count"] = RunTracker.CompletedThisRun.Count,
            ["mission_progress_end"] = RunTracker.MissionProgress(),
            ["total_distance_all_runs"] = Mathf.RoundToInt(RunTracker.Stats.TotalDistance),
            ["total_destroyed_all_runs"] = RunTracker.Stats.TotalDestroyed,
            ["missions_done_total"] = OutlawProfile.MissionsDoneCount,
            ["best_m"] = OutlawProfile.BestDistance,
            ["new_best"] = IsNewBest,

            // Cause of death, with the situation at the moment the run ended.
            ["death_cause"] = DeathCause(reason, sinceHit),
            ["last_hit_colour"] = RunTracker.LastHitColour,
            ["hits_last_10s"] = RunTracker.HitsInLast(10f),
            ["fuel_pct_at_end"] = Mathf.RoundToInt(RunTracker.FuelNow * 100f),
            ["ammo_at_end"] = RunTracker.AmmoNow,
            ["reversed_at_end"] = RunTracker.ReversedNow,
            ["nitro_at_end"] = RunTracker.NitroNow,
            ["shield_at_end"] = RunTracker.ShieldNow,

            // Every block that reached the car was dodged, hit, or destroyed first, by its colour.
            ["dodged_green"] = RunTracker.DodgedByColour[0],
            ["dodged_yellow"] = RunTracker.DodgedByColour[1],
            ["dodged_red"] = RunTracker.DodgedByColour[2],
            ["shots_on_green"] = RunTracker.ShotsOnColour[0],
            ["shots_on_yellow"] = RunTracker.ShotsOnColour[1],
            ["shots_on_red"] = RunTracker.ShotsOnColour[2],
            ["hits_with_nitro"] = RunTracker.CollisionsWithNitro,

            // Bullet holding: average ammo carried, and how long a bullet is held before use.
            ["avg_ammo_held"] = elapsed > 0f ? RunTracker.Round(RunTracker.AmmoSecondsSum / elapsed) : 0f,
            ["avg_hold_s"] = RunTracker.HeldBulletsFired > 0
                ? RunTracker.Round(RunTracker.HoldSecondsSum / RunTracker.HeldBulletsFired) : -1f,
            ["ammo_timeline_10s"] = RunTracker.Series(RunTracker.AmmoTimeline),
            ["shots_timeline_10s"] = RunTracker.Series(RunTracker.ShotsTimeline)
        };
        return row;
    }

    /// <summary>A readable cause of death for analysis.</summary>
    private static string DeathCause(string reason, float sinceHit)
    {
        if (reason == "fuel")
        {
            if (RunTracker.Missed[(int)PickupKind.Fuel] > 0) return "fuel_missed_tanks";
            if (RunTracker.CollisionsByColour[(int)TrafficObstacleState.Red] > 0) return "fuel_red_block_hits";
            return "fuel_other";
        }
        if (reason == "police")
        {
            if (sinceHit >= 0f && sinceHit <= 5f) return "police_right_after_hit";
            if (RunTracker.TotalCollisions >= 3) return "police_after_many_hits";
            return "police_slow_pressure";
        }
        return reason;
    }

    private static int Outcome(string name)
    {
        return RunTracker.MysteryOutcomes.TryGetValue(name, out int count) ? count : 0;
    }

    public static string MissionIds(List<MissionDefinition> missions)
    {
        var ids = new List<string>();
        foreach (MissionDefinition mission in missions) ids.Add(mission.Id.ToString());
        return string.Join("|", ids);
    }
}
