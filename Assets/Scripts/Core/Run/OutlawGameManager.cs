// Coordinates the run lifecycle, difficulty and encounter scheduling.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RunEncounterKind { Mystery, Ramp, Roadblock, Fog, Desert, Portal }

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

    private Object encounterOwner;
    private RunEncounterKind encounterKind;
    private float encounterStart, encounterEnd, nextMajorAt;
    private float lastJumpOfferDistance = -200f;
    public float LastJumpOfferDistance => lastJumpOfferDistance;
    public bool JumpRampOverdue => UsesCoordinatedPacing && DistanceTravelled - lastJumpOfferDistance >= 300f;
    public void RecordJumpRampOffer(float roadDistance)
    {
        lastJumpOfferDistance = DistanceTravelled + Mathf.Max(0f, roadDistance - driver.RoadPositionDistance);
    }
    private readonly Dictionary<Object, Vector2> encounterRequests = new Dictionary<Object, Vector2>();
    public bool UsesCoordinatedPacing => environment != null && !environment.IsOcean;
    public bool IsReliefPhase => UsesCoordinatedPacing && RunDifficultyState.Evaluate(elapsed).Relief > 0.85f;
    public float ActualDrivingSpeed => Mathf.Max(1f, driver.ForwardSpeed * driver.EnvironmentSpeedMultiplier
        * pickupEffects.RunState.ForwardSpeedMultiplier);

    public bool IsSharpRoadSection(float distance, float length = 40f)
    {
        if (!road.TrySample(distance, out RoadPathPoint first)) return true;
        Vector3 initial = Vector3.ProjectOnPlane(first.Forward, Vector3.up);
        Vector3 previous = initial;
        for (float offset = 5f; offset <= length; offset += 5f)
        {
            if (!road.TrySample(distance + offset, out RoadPathPoint point)) return true;
            Vector3 heading = Vector3.ProjectOnPlane(point.Forward, Vector3.up);
            if (Vector3.Angle(initial, heading) > 18f || Vector3.Angle(previous, heading) > 4f) return true;
            previous = heading;
        }
        return false;
    }

    private void RefreshEncounter()
    {
        if (encounterOwner is DesertCrossingController desert && desert.CrossingActive) return;
        if (encounterOwner != null && driver.RoadPositionDistance <= encounterEnd) return;
        if (encounterOwner != null)
            nextMajorAt = elapsed + (encounterKind == RunEncounterKind.Mystery ? 0f : 0.5f);
        encounterOwner = null;
    }

    // Resolve encounter priority before a spawner commits its placement.
    public bool CanScheduleEncounter(Object owner, RunEncounterKind kind, float from, float to)
    {
        if (!UsesCoordinatedPacing) return true;
        if (State != OutlawGameState.Running || IsTutorial || pickupEffects.RunState.IsRoadRecovering) return false;
        RefreshEncounter();
        bool portalDue = RunSession.NextPortalAt >= 0f && RunSession.NextPortalAt <= elapsed + 3f;
        if (portalDue && kind != RunEncounterKind.Portal) return false;
        bool overlap = encounterOwner != null && from <= encounterEnd && to >= encounterStart;

        if (kind == RunEncounterKind.Fog)
            return !driver.IsAirborne && !(overlap && encounterKind == RunEncounterKind.Ramp);
        if (JumpRampOverdue && kind != RunEncounterKind.Ramp && kind != RunEncounterKind.Portal) return false;
        if (kind == RunEncounterKind.Mystery)
            return !(overlap && encounterKind != RunEncounterKind.Fog);
        if (kind != RunEncounterKind.Portal && kind != RunEncounterKind.Mystery)
        {
            float fuelFloor = kind == RunEncounterKind.Ramp && JumpRampOverdue ? 8f : 10f;
            if (fuel.CurrentFuel < fuelFloor || (driver.IsAirborne && kind != RunEncounterKind.Roadblock)) return false;
            if (fuelTanks.ConflictsWithFuelRoute(from, to)) return false;
        }
        if (kind != RunEncounterKind.Portal && IsSharpRoadSection(from)) return false;
        Vector2 request = encounterRequests.TryGetValue(owner, out Vector2 prior)
            && elapsed - prior.y < 2f ? new Vector2(prior.x, elapsed) : new Vector2(elapsed, elapsed);
        encounterRequests[owner] = request;
        if (encounterOwner == owner) return true;
        if (encounterOwner != null || elapsed < nextMajorAt) return false;

        if (!portalDue && !(kind == RunEncounterKind.Ramp && JumpRampOverdue))
            foreach (var pair in encounterRequests)
                if (pair.Key != owner && pair.Key != null && elapsed - pair.Value.y < 1f
                    && elapsed - pair.Value.x >= 8f && pair.Value.x < request.x) return false;
        return true;
    }

    public void BeginEncounter(Object owner, RunEncounterKind kind, float from, float to)
    {
        if (!UsesCoordinatedPacing || kind == RunEncounterKind.Fog || kind == RunEncounterKind.Mystery) return;
        encounterOwner = owner;
        encounterKind = kind;
        encounterStart = from;
        encounterEnd = to;
        encounterRequests.Remove(owner);
    }

    public void EndEncounter(Object owner)
    {
        if (encounterOwner != owner) return;
        nextMajorAt = elapsed + (encounterKind == RunEncounterKind.Mystery ? 0f : 0.5f);
        encounterOwner = null;
    }

    // Allow normal obstacle pressure through ramp flights.
    public bool CanPlaceNormalObstacle(float distance)
    {
        if (!UsesCoordinatedPacing) return true;
        RefreshEncounter();
        return encounterOwner == null || encounterKind == RunEncounterKind.Mystery || encounterKind == RunEncounterKind.Fog || encounterKind == RunEncounterKind.Ramp
            || distance < encounterStart - ActualDrivingSpeed || distance > encounterEnd;
    }

    public float RoadPressureMultiplier(float distance)
    {
        if (!UsesCoordinatedPacing) return 1f;
        RefreshEncounter();

        return 1f;
    }

    public bool ConflictsWithFuel(float from, float to) => fuelTanks.ConflictsWithFuelRoute(from, to);

    public bool IsFuelRouteClear(float distance)
    {
        if (!UsesCoordinatedPacing) return true;
        RefreshEncounter();
        if (encounterOwner is DesertCrossingController desert)
            return desert.IsFuelPositionSafe(distance);
        if (encounterOwner != null && encounterKind == RunEncounterKind.Ramp)
            return distance < encounterStart - ActualDrivingSpeed || distance > encounterStart + 20f;
        return encounterOwner == null || encounterKind == RunEncounterKind.Mystery
            || distance < encounterStart - ActualDrivingSpeed || distance > encounterEnd + ActualDrivingSpeed;
    }

    public OutlawGameState State { get; private set; } = OutlawGameState.Ready;

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

        road.ConfigureBalancedLayout(!environment.IsOcean);
        obstacles.Pacing = this;
        fuelTanks.ConfigurePacing(this);
        mysterySpawner.ConfigurePacing(this);
        pickupEffects.EnableGunGuarantee = !environment.IsOcean;

        NitroRampSpawner ramps = GetComponent<NitroRampSpawner>();
        if (ramps == null) ramps = gameObject.AddComponent<NitroRampSpawner>();
        ramps.Configure(this, driver, road, obstacles);

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
        pickupEffects.RestoreLootPacing(carried.EmptyGunBoxStreak);
        lastJumpOfferDistance = carried.LastJumpOfferDistance;
        mysterySpawner.RestoreSchedule(carried.NextMysteryRowAt);
        pickupEffects.RestoreTransientEffects(carried.SunnyRemaining, carried.ReverseCountdown, carried.PendingReverse);
        pickupEffects.RestoreRun(carried.Risk, carried.StateFuel);
        previousPursuitGap = pickupEffects.RunState.PursuitGap;
        obstacles.BeginRealRun();

        UpdateDifficulty();
        State = OutlawGameState.Running;
        Time.timeScale = 1f;
        RunTracker.EnvironmentSwitches++;
        WeeWooAnalytics.Track("env_switch", "to", SceneManager.GetActiveScene().name);
    }

    // Capture gameplay and spawn pacing together for environment transfers.
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
            EmptyGunBoxStreak = pickupEffects.EmptyGunBoxStreak,
            LastJumpOfferDistance = lastJumpOfferDistance,
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
            Nitro = pickupEffects.RunState.HasNitro || pickupEffects.RunState.HasRampBoost,
            ShieldReady = pickupEffects.ShieldReady,
            Ammo = shooting.CurrentAmmo,
            MaximumAmmo = shooting.MaximumAmmo
        };
    }

    // Apply the current environment profile to the shared run systems.
    private void UpdateDifficulty()
    {

        pickupEffects.ConfigureHarderDifficulty(UsesCoordinatedPacing);
        fuel.HarderDifficultyActive = UsesCoordinatedPacing;
        driver.HarderDrivingConditions = UsesCoordinatedPacing;
        float targetSpeed = RunDifficultyState.DrivingSpeed(elapsed, speedScheduleSeed, UsesCoordinatedPacing);
        if (IsTutorialHandoff)
            driver.SetTutorialSpeed(Mathf.Lerp(TutorialSpeed, targetSpeed,
                Mathf.SmoothStep(0f, 1f, elapsed / 4f)));
        else
            driver.SetDifficultySpeed(targetSpeed);
        if (UsesCoordinatedPacing) road.SetDrivingPace(targetSpeed * driver.EnvironmentSpeedMultiplier);
        obstacles.ConfigureDifficulty(
            RunDifficultyState.ObstacleDensity(elapsed),
            UsesCoordinatedPacing ? Mathf.Min(0.95f, Mathf.Max(0.4f, RunDifficultyState.DoubleRowChance(elapsed) + 0.15f))
                : RunDifficultyState.DoubleRowChance(elapsed),
            elapsed);
        pursuit.SetClosingSpeed(RunDifficultyState.PoliceClosingSpeed(elapsed, UsesCoordinatedPacing));

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

    // Start regular play after optional tutorial setup.
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

        if (!focused) Pause("focus_lost");
    }
#endif

    public void Restart()
    {

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
