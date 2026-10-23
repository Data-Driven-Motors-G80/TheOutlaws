using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public sealed class OutlawGameManager : MonoBehaviour
{
    private const float TutorialSpeed = 16f;
    private const int StartingAmmo = 0;

    private static int runsThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => runsThisSession = 0;

    private EnvironmentRoot environment;
    private RunAnalyticsReporter analytics;
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
    private int speedScheduleSeed;
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
    public bool StartedAfterTutorial { get; private set; }
    public bool IsTutorialHandoff => StartedAfterTutorial && elapsed < 4f;
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
        environment = GetComponent<EnvironmentRoot>();
        if (environment == null || !environment.ValidateSetup())
        {
            Debug.LogError("The Outlaws requires a configured EnvironmentRoot on the game systems object.", this);
            enabled = false;
            return;
        }
        GameObject player = environment.Player.gameObject;
        driver = environment.Player;
        pickupEffects = player.GetComponent<CarPickupEffects>();
        fuel = player.GetComponent<FuelMeter>();
        pursuit = player.GetComponent<RiskRunController>();
        road = environment.Road;
        obstacles = environment.Obstacles;
        fuelTanks = environment.FuelSpawner;
        mysterySpawner = environment.MysterySpawner;
        policeCars = System.Array.FindAll(environment.PoliceCars, police => police.gameObject.activeInHierarchy);
        System.Array.Sort(policeCars, (a, b) => string.CompareOrdinal(a.name, b.name));
        shooting = player.GetComponent<OutlawShooting>();
        ammoSpawner = GetComponent<OutlawPickupSpawner>();
        hud = GetComponent<OutlawHUD>();

        if (!ValidateRequiredSystems() || shooting == null || ammoSpawner == null || hud == null || fuelTanks == null)
        {
            Debug.LogError("The Outlaws requires saved Shooting, Pickup Spawner, Fuel Tank Spawner and HUD components. Check the scene setup.", this);
            enabled = false;
            return;
        }

        pickupEffects.ConfigureRewards(environment.Rewards);
        shooting.Configure(pickupEffects, obstacles);
        ammoSpawner.Configure(road, player.transform, shooting, pickupEffects);
        ammoSpawner.enabled = false;
        mysterySpawner.RunTime = () => elapsed;
        mysterySpawner.RunSpeed = () => driver.ForwardSpeed * driver.EnvironmentSpeedMultiplier;
        obstacles.RunDistance = () => DistanceTravelled;
        ammoSpawner.RunDistance = () => DistanceTravelled;

        tutorial = GetComponent<TutorialDirector>();
        if (tutorial == null) tutorial = gameObject.AddComponent<TutorialDirector>();
        tutorial.Configure(road, player.transform, shooting, obstacles, fuelTanks, fuel);
        tutorial.Completed += () => StartRealRun(true);

        if (!RunSession.HasPending) OutlawProfile.ResetRunMissions();
        TutorialSelected = OutlawProfile.TutorialDefault;
        WeeWooAnalytics.EnsureStarted();
        analytics = new RunAnalyticsReporter(this, () => runsThisSession);
        analytics.Attach();
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
        analytics?.Dispose();
    }

    private void Start()
    {
        // Continuing a run that was carried over from the previous environment scene.
        if (!RunSession.TryGetPending(out RunSnapshot carried))
        {
            WeeWooAnalytics.Track("title_shown", "tutorial_default", TutorialSelected,
                "missions_visible", RunAnalyticsReporter.MissionIds(OutlawProfile.VisibleMissions()));
            return;
        }

        elapsed = carried.Elapsed;
        speedScheduleSeed = carried.SpeedScheduleSeed;
        carriedDistance = carried.Distance;
        pickupEffects.Backpack.Restore(carried.BackpackItem, carried.Ammo);
        mysterySpawner.RestoreSchedule(carried.NextMysteryRowAt);
        pickupEffects.RestoreTransientEffects(carried.SunnyRemaining, carried.ReverseCountdown, carried.PendingReverse);
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
        ScoreMeter score = environment.Score;
        return new RunSnapshot
        {
            Elapsed = elapsed,
            SpeedScheduleSeed = speedScheduleSeed,
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
            BackpackItem = pickupEffects.Backpack.Item,
            NextMysteryRowAt = mysterySpawner.NextRowAt,
            SunnyRemaining = pickupEffects.SunnyDayRemaining,
            ReverseCountdown = pickupEffects.ReverseCountdownRemaining,
            PendingReverse = pickupEffects.PendingReverseDuration,
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
            Speed = driver.ForwardSpeed * driver.EnvironmentSpeedMultiplier * pickupEffects.ForwardSpeedMultiplier,
            Snow = environment.IsSnow,
            Ocean = environment.IsOcean,
            Desert = environment.IsDesert,
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
        float targetSpeed = RunDifficultyState.DrivingSpeed(elapsed, speedScheduleSeed);
        if (IsTutorialHandoff)
            driver.SetTutorialSpeed(Mathf.Lerp(TutorialSpeed, targetSpeed,
                Mathf.SmoothStep(0f, 1f, elapsed / 4f)));
        else
            driver.SetDifficultySpeed(targetSpeed);
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
        pickupEffects.RunState.BeginTutorialPursuit();
        pursuit.SetClosingSpeed(0f);
        driver.SetTutorialSpeed(TutorialSpeed);
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
        tutorial.Begin();
    }

    /// <summary>Starts counting the real run: from the title, or seamlessly after the tutorial.</summary>
    private void StartRealRun(bool afterTutorial)
    {
        StartedAfterTutorial = afterTutorial;
        distanceOrigin = driver.RoadDistanceTravelled;
        carriedDistance = 0f;
        elapsed = 0f;
        speedScheduleSeed = System.Guid.NewGuid().GetHashCode();
        pickupEffects.RunState.ResetPursuit();
        previousPursuitGap = pickupEffects.RunState.PursuitGap;
        fuel.SetFuel(fuel.MaximumFuel);
        fuel.DrainMode = FuelDrainMode.Normal;
        pickupEffects.RefillFuel(FuelState.MaximumFuel);
        shooting.SetAmmo(StartingAmmo);
        obstacles.BeginRealRun();
        fuelTanks.BeginRealRun();
        ammoSpawner.Suppressed = true;
        if (mysterySpawner != null) mysterySpawner.BeginRealRun();
        RunSession.ResetSwitchSchedule();

        runsThisSession++;
        State = OutlawGameState.Running;
        int totalRuns = OutlawProfile.RegisterRun();
        RunTracker.Begin(afterTutorial);
        WeeWooAnalytics.Track("run_start", "run_number_session", runsThisSession, "run_number_total", totalRuns,
            "after_tutorial", afterTutorial, "missions_active", RunAnalyticsReporter.MissionIds(RunTracker.ActiveMissions));

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
            WeeWooAnalytics.AddRow("tutorials", analytics.ExitTutorialRow("restarted"));
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

    private void RecordRunEnd(string reason) => analytics.RecordRunEnd(reason);

    private void OnMissionCompleted(MissionDefinition mission)
    {
        if (hud != null) hud.ShowMissionComplete(mission);
    }

}
