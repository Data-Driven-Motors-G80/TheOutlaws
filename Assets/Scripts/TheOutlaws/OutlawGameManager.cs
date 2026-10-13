using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class OutlawGameManager : MonoBehaviour
{
    private const float TutorialPoliceFloor = 30f;
    private const float TutorialSpeed = 14f;
    private const int StartingAmmo = 2;

    private static int runsThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => runsThisSession = 0;

    private AutoDriveCar driver;
    private CarPickupEffects pickupEffects;
    private FuelMeter fuel;
    private RiskRunController pursuit;
    private InfiniteRoad road;
    private ObstacleSpawner obstacles;
    private OutlawShooting shooting;
    private OutlawHUD hud;
    private TutorialDirector tutorial;
    private FuelTankSpawner fuelTanks;
    private OutlawPickupSpawner ammoSpawner;
    private PickupSpawner mysterySpawner;
    private ChaseCar[] policeCars;
    private float elapsed;
    private float previousPursuitGap;
    private float policeAlertRemaining;
    private float carriedDistance;
    private float distanceOrigin;
    private float titleShownAt;
    private float pausedAt;
    private bool tutorialToggled;
    private string endReason;

    public OutlawGameState State { get; private set; } = OutlawGameState.Ready;
    /// <summary>Real-run distance: excludes the tutorial and carries across environment scenes.</summary>
    public float DistanceTravelled => carriedDistance + (driver != null ? driver.RoadDistanceTravelled - distanceOrigin : 0f);
    public float ElapsedSeconds => elapsed;
    public OutlawShooting Shooting => shooting;
    public FuelMeter Fuel => fuel;
    public CarPickupEffects PickupEffects => pickupEffects;
    public TutorialDirector Tutorial => tutorial;
    public bool IsTutorial => tutorial != null && tutorial.Active;
    public bool IsOnIce => driver != null && driver.IsOnIce;
    public bool PoliceAlertActive => policeAlertRemaining > 0f;
    public int PoliceCarCount => policeCars != null ? policeCars.Length : 0;
    public ChaseCar[] PoliceCars => policeCars;
    public Transform Player => driver != null ? driver.transform : null;
    public float PursuitGap => pickupEffects != null ? pickupEffects.RunState.PursuitGap : 0f;
    public string EndReason => endReason;
    /// <summary>Seconds left on the "GO!" banner shown when the real run begins.</summary>
    public float RunStartBannerRemaining { get; private set; }
    public bool TutorialSelected { get; private set; }
    public bool IsNewBest { get; private set; }

    private void Awake()
    {
        GameObject player = GameObject.Find("PlayerCar");
        if (player == null)
        {
            Debug.LogError("The Outlaws could not find Team 19's PlayerCar.", this);
            enabled = false;
            return;
        }

        driver = player.GetComponent<AutoDriveCar>();
        pickupEffects = player.GetComponent<CarPickupEffects>();
        fuel = player.GetComponent<FuelMeter>();
        pursuit = player.GetComponent<RiskRunController>();
        road = Object.FindFirstObjectByType<InfiniteRoad>();
        obstacles = Object.FindFirstObjectByType<ObstacleSpawner>();
        fuelTanks = Object.FindFirstObjectByType<FuelTankSpawner>();
        mysterySpawner = Object.FindFirstObjectByType<PickupSpawner>();

        policeCars = Object.FindObjectsByType<ChaseCar>(FindObjectsSortMode.None);
        System.Array.Sort(policeCars, (a, b) => string.CompareOrdinal(a.name, b.name));
        // Active police and their appearance are authored in the scene.
        shooting = player.GetComponent<OutlawShooting>();
        ammoSpawner = GetComponent<OutlawPickupSpawner>();
        hud = GetComponent<OutlawHUD>();

        if (!ValidateRequiredSystems() || shooting == null || ammoSpawner == null || hud == null || fuelTanks == null)
        {
            Debug.LogError("The Outlaws requires saved Shooting, Pickup Spawner, Fuel Tank Spawner and HUD components. Check the scene setup.", this);
            enabled = false;
            return;
        }

        shooting.Configure(pickupEffects, obstacles);
        ammoSpawner.Configure(road, player.transform, shooting, pickupEffects);
        obstacles.RunDistance = () => DistanceTravelled;
        ammoSpawner.RunDistance = () => DistanceTravelled;

        tutorial = GetComponent<TutorialDirector>();
        if (tutorial == null) tutorial = gameObject.AddComponent<TutorialDirector>();
        tutorial.Configure(road, player.transform, shooting, obstacles, fuelTanks, fuel);
        tutorial.Completed += () => StartRealRun(true);

        TutorialSelected = OutlawProfile.TutorialDefault;
        WeeWooAnalytics.EnsureStarted();
        WeeWooAnalytics.ContextProvider = AnalyticsContext;
        WeeWooAnalytics.ExitRowsProvider = ExitRows;
        RunTracker.MissionCompleted += OnMissionCompleted;
        hud.Configure(this);

        previousPursuitGap = pickupEffects.RunState.PursuitGap;
        titleShownAt = Time.realtimeSinceStartup;

        // A fresh run (not a continuation from another environment) restarts the switch schedule.
        if (!RunSession.HasPending) RunSession.ResetSwitchSchedule();

        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        RunTracker.MissionCompleted -= OnMissionCompleted;
        if (WeeWooAnalytics.ContextProvider == (System.Func<Dictionary<string, object>>)AnalyticsContext)
            WeeWooAnalytics.ContextProvider = null;
    }

    private void Start()
    {
        // Continuing a run that was carried over from the previous environment scene.
        if (!RunSession.TryGetPending(out RunSnapshot carried))
        {
            WeeWooAnalytics.Track("title_shown", "tutorial_default", TutorialSelected,
                "missions_visible", MissionIds(OutlawProfile.VisibleMissions()));
            return;
        }

        elapsed = carried.Elapsed;
        carriedDistance = carried.Distance;
        shooting.SetAmmo(carried.Ammo);
        pickupEffects.RestoreRun(carried.Risk, carried.StateFuel);
        previousPursuitGap = pickupEffects.RunState.PursuitGap;
        obstacles.BeginRealRun();

        // No "press Enter" screen and no tutorial: the run is already underway.
        UpdateDifficulty();
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
        RunTracker.EnvironmentSwitches++;
        WeeWooAnalytics.Track("env_switch", "to", SceneManager.GetActiveScene().name);
    }

    /// <summary>Everything the next environment scene needs to continue this run.</summary>
    public RunSnapshot CaptureRun()
    {
        ScoreMeter score = Object.FindFirstObjectByType<ScoreMeter>();
        return new RunSnapshot
        {
            Elapsed = elapsed,
            Distance = DistanceTravelled,
            Score = score != null ? score.Score : 0f,
            MeterFuel = fuel != null ? fuel.CurrentFuel : 0f,
            StateFuel = pickupEffects.CurrentFuel,
            ForwardSpeed = driver.ForwardSpeed,
            SpeedTimer = driver.SpeedIncreaseTimer,
            StartingSpeed = driver.StartingSpeed,
            MaximumSpeed = driver.MaximumSpeed,
            SpeedIncreaseAmount = driver.SpeedIncreaseAmount,
            SpeedIncreaseInterval = driver.SpeedIncreaseInterval,
            Ammo = shooting.CurrentAmmo,
            Risk = pickupEffects.CaptureRun()
        };
    }

    private bool ValidateRequiredSystems()
    {
        bool valid = driver != null && pickupEffects != null && fuel != null &&
                     pursuit != null && road != null && obstacles != null &&
                     policeCars != null && policeCars.Length > 0;
        if (!valid)
        {
            Debug.LogError(
                "The Outlaws setup is incomplete. The player, road, obstacle spawner, " +
                "fuel system, pursuit controller, and at least one police car are required.",
                this);
        }
        return valid;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool pausePressed = keyboard != null &&
            (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);

        if (State == OutlawGameState.Ready)
        {
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                BeginGame();
            }
            return;
        }

        if (State == OutlawGameState.Paused)
        {
            if (pausePressed) Resume();
            return;
        }

        if (State == OutlawGameState.Won || State == OutlawGameState.Lost)
        {
            if (keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                Restart();
            }
            return;
        }

        if (pausePressed)
        {
            Pause("key");
            return;
        }

        if (RunStartBannerRemaining > 0f)
            RunStartBannerRemaining = Mathf.Max(0f, RunStartBannerRemaining - Time.deltaTime);

        if (IsTutorial)
        {
            // The tutorial never ends the run: the police hold position and fuel cannot empty.
            driver.SetTutorialSpeed(TutorialSpeed);
            pursuit.SetClosingSpeed(0f);
            return;
        }

        elapsed += Time.deltaTime;
        UpdateDifficulty();
        UpdatePoliceFeedback();
        RunTracker.Tick(TickContext());

        if ((pickupEffects != null && pickupEffects.RunState.IsGameOver) ||
                 (fuel != null && fuel.IsEmpty))
        {
            EndGame(OutlawGameState.Lost);
        }
    }

    private RunTickContext TickContext()
    {
        Keyboard keyboard = Keyboard.current;
        float raw = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) raw -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) raw += 1f;
        }

        return new RunTickContext
        {
            DeltaTime = Time.deltaTime,
            Elapsed = elapsed,
            Distance = DistanceTravelled,
            FuelFraction = fuel.NormalizedFuel,
            PursuitGap = pickupEffects.RunState.PursuitGap,
            Speed = driver.ForwardSpeed * pickupEffects.ForwardSpeedMultiplier,
            Snow = SceneManager.GetActiveScene().name == "Env_Snow",
            RawSteer = raw,
            Reversed = pickupEffects.ActiveEffect == PickupEffectType.ReverseSteering,
            Nitro = pickupEffects.RunState.HasNitro,
            ShieldReady = pickupEffects.ShieldReady,
            Ammo = shooting.CurrentAmmo,
            MaximumAmmo = shooting.MaximumAmmo
        };
    }

    private void UpdateDifficulty()
    {
        // Total run time carries across scenes: changing environments never resets pressure.
        driver.SetDifficultySpeed(RunDifficultyState.DrivingSpeed(elapsed));
        obstacles.ConfigureDifficulty(
            RunDifficultyState.ObstacleDensity(elapsed),
            RunDifficultyState.DoubleRowChance(elapsed),
            elapsed);
        pursuit.SetClosingSpeed(RunDifficultyState.PoliceClosingSpeed(elapsed));
        // Pulling away only gets you back to the starting gap: the police keep tailing you.
        pickupEffects.RunState.RecoveryCap = RiskRunState.InitialPursuitGap;
    }

    private void UpdatePoliceFeedback()
    {
        float currentGap = pickupEffects.RunState.PursuitGap;
        if (currentGap < previousPursuitGap - 1f)
        {
            if (policeAlertRemaining <= 0f) RunTracker.PoliceAlerts++;
            policeAlertRemaining = 2.25f;
        }
        else
        {
            policeAlertRemaining = Mathf.Max(0f, policeAlertRemaining - Time.deltaTime);
        }
        previousPursuitGap = currentGap;
    }

    public void SetTutorialSelected(bool selected)
    {
        if (State != OutlawGameState.Ready || selected == TutorialSelected) return;
        TutorialSelected = selected;
        tutorialToggled = true;
        WeeWooAnalytics.Track("tutorial_toggled", "on", selected);
    }

    public void BeginGame()
    {
        if (State != OutlawGameState.Ready)
        {
            return;
        }

        bool defaultTutorial = OutlawProfile.TutorialDefault;
        OutlawProfile.MarkGameStarted();
        WeeWooAnalytics.Track("start_pressed", "tutorial_on", TutorialSelected, "tutorial_default", defaultTutorial,
            "toggled", tutorialToggled, "title_s", RunTracker.Round(Time.realtimeSinceStartup - titleShownAt));

        if (TutorialSelected) StartTutorial();
        else StartRealRun(false);
    }

    private void StartTutorial()
    {
        obstacles.SuppressAutoSpawn = true;
        fuelTanks.Suppressed = true;
        ammoSpawner.Suppressed = true;
        if (mysterySpawner != null) mysterySpawner.ClearAndSuppress();
        pickupEffects.RunState.MinimumPursuitGap = TutorialPoliceFloor;
        pursuit.SetClosingSpeed(0f);
        driver.SetTutorialSpeed(TutorialSpeed);
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
        tutorial.Begin();
    }

    /// <summary>Starts counting the real run: from the title, or seamlessly after the tutorial.</summary>
    private void StartRealRun(bool afterTutorial)
    {
        distanceOrigin = driver.RoadDistanceTravelled;
        carriedDistance = 0f;
        elapsed = 0f;
        pickupEffects.RunState.ResetPursuit();
        previousPursuitGap = pickupEffects.RunState.PursuitGap;
        fuel.SetFuel(fuel.MaximumFuel);
        fuel.DrainMode = FuelDrainMode.Normal;
        pickupEffects.RefillFuel(FuelState.MaximumFuel);
        shooting.SetAmmo(StartingAmmo);
        obstacles.BeginRealRun();
        fuelTanks.BeginRealRun();
        ammoSpawner.BeginRealRun();
        if (mysterySpawner != null) mysterySpawner.BeginRealRun();
        RunSession.ResetSwitchSchedule();

        runsThisSession++;
        State = OutlawGameState.Running;
        int totalRuns = OutlawProfile.RegisterRun();
        RunTracker.Begin(afterTutorial);
        WeeWooAnalytics.Track("run_start", "run_number_session", runsThisSession, "run_number_total", totalRuns,
            "after_tutorial", afterTutorial, "missions_active", MissionIds(RunTracker.ActiveMissions));

        UpdateDifficulty();
        RunStartBannerRemaining = afterTutorial ? 2.6f : 1.6f;
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
    }

    public void Pause(string source)
    {
        if (State != OutlawGameState.Running) return;
        State = OutlawGameState.Paused;
        Time.timeScale = 0f;
        pausedAt = Time.realtimeSinceStartup;
        if (RunTracker.Active)
        {
            RunTracker.Pauses++;
            if (source == "focus_lost") RunTracker.FocusLosses++;
        }
        WeeWooAnalytics.Track("pause", "source", source, "in_tutorial", IsTutorial,
            "tutorial_step", IsTutorial ? tutorial.Step.ToString() : "");
        WeeWooAnalytics.Flush();
    }

    public void Resume()
    {
        if (State != OutlawGameState.Paused) return;
        float seconds = Time.realtimeSinceStartup - pausedAt;
        if (RunTracker.Active) RunTracker.PausedSeconds += seconds;
        WeeWooAnalytics.Track("resume", "paused_s", RunTracker.Round(seconds));
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
    }

#if !UNITY_EDITOR
    private void OnApplicationFocus(bool focused)
    {
        // Clicking away or switching tabs should never cost a run.
        if (!focused) Pause("focus_lost");
    }
#endif

    public void Restart()
    {
        // Abandoning from the pause menu is a meaningful signal (frustration, wants a retry).
        if (RunTracker.Active) RecordRunEnd(State == OutlawGameState.Paused ? "restart_from_pause" : "restart");
        else if (IsTutorial)
        {
            WeeWooAnalytics.AddRow("tutorials", ExitTutorialRow("restarted"));
            WeeWooAnalytics.Track("tutorial_restart", "step", tutorial.Step.ToString());
        }
        WeeWooAnalytics.Track("restart_pressed", "from", State.ToString());
        WeeWooAnalytics.Flush();

        Time.timeScale = 1f;
        RunSession.ClearPending();
        RunSession.ResetSwitchSchedule();
        // Every new run starts in the city, whatever environment the last one ended in.
        string firstScene = SceneUtility.GetScenePathByBuildIndex(0);
        if (string.IsNullOrEmpty(firstScene)) firstScene = SceneManager.GetActiveScene().path;
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            firstScene,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(firstScene);
#endif
    }

    private void EndGame(OutlawGameState result)
    {
        if (State != OutlawGameState.Running)
        {
            return;
        }

        endReason = fuel.IsEmpty ? "fuel" : "police";
        IsNewBest = OutlawProfile.RecordDistance(DistanceTravelled);
        RecordRunEnd(endReason);
        State = result;
        Time.timeScale = 0f;
    }

    private void RecordRunEnd(string reason)
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

    private void OnMissionCompleted(MissionDefinition mission)
    {
        if (hud != null) hud.ShowMissionComplete(mission);
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

    private Dictionary<string, object> ExitTutorialRow(string status)
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

    private static string MissionIds(List<MissionDefinition> missions)
    {
        var ids = new List<string>();
        foreach (MissionDefinition mission in missions) ids.Add(mission.Id.ToString());
        return string.Join("|", ids);
    }
}
