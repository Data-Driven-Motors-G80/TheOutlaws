using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TutorialStep
{
    Steer,
    Dodge,
    Fuel,
    Ammo,
    Shoot,
    Done,
    Nitro, // Appended to preserve existing tutorial analytics indices.
    Reverse,
    Shield,
    Recovery,
    PursuitCollision
}

public enum TutorialHighlight
{
    None,
    Fuel,
    Ammo,
    RearView
}

/// <summary>
/// Show-don't-tell tutorial: one mechanic per step, and a step only ends once the
/// player has actually done it. Nothing here counts towards the real run.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialDirector : MonoBehaviour
{
    private const float SpawnAhead = 75f;
    private const float DodgeSpawnAhead = 55f;
    private const float ShootSpawnAhead = 60f;
    private const float FuelSpawnAhead = 35f;
    private const float SteerHoldSeconds = 0.2f;

    private InfiniteRoad road;
    private Transform player;
    private OutlawShooting shooting;
    private ObstacleSpawner obstacles;
    private FuelTankSpawner fuelTanks;
    private FuelMeter fuel;
    private PickupSpawner powerups;
    private PickupItem nitro;
    private bool fuelLessonStarted;
    private CarPickupEffects effects;
    private float previousReverseLateral;
    private float reversePracticeSeconds;
    private const float ReversePracticeMinimumSeconds = 4f;
    private const float ReverseDirectionHoldSeconds = 0.6f;
    private PickupItem lessonMystery;
    private bool reverseCollected;
    private bool lessonCollected;
    private bool shieldDemonstrated;
    private bool pursuitCollisionDemonstrated;

    private float stepStartedAt;
    private float tutorialStartedAt;
    private float waitRemaining;
    private float leftHeld;
    private float rightHeld;
    private int stepRetries;
    private int totalRetries;
    private int stepShots;
    private int stepDryFires;
    private readonly Dictionary<string, object> stepRecord = new Dictionary<string, object>();

    private GameObject tank;
    private bool tankCollected;
    private readonly List<GameObject> ammoBoxes = new List<GameObject>();
    private TrafficLightObstacle wall;
    private TrafficObstacleState shootWallState;
    private List<TrafficLightObstacle> dodgeRow = new List<TrafficLightObstacle>();
    private bool dodgeHit;
    private bool stepSucceeded;
    /// <summary>Shoot step: collect only the missing ammo before retrying the current wall.</summary>
    private bool reloading;

    public bool Active { get; private set; }
    public TutorialStep Step { get; private set; }
    public bool LeftDone { get; private set; }
    public bool RightDone { get; private set; }
    public string Feedback { get; private set; }
    public float FeedbackRemaining { get; private set; }
    public bool FeedbackIsGood { get; private set; }
    public float SecondsInTutorial => Active ? Time.time - tutorialStartedAt : 0f;

    public event Action Completed;

    public TutorialHighlight Highlight
    {
        get
        {
            switch (Step)
            {
                case TutorialStep.Fuel: return TutorialHighlight.Fuel;
                case TutorialStep.Ammo: return TutorialHighlight.Ammo;
                case TutorialStep.Shoot: return reloading ? TutorialHighlight.Ammo : TutorialHighlight.None;
                case TutorialStep.Dodge:
                case TutorialStep.Nitro:
                case TutorialStep.Recovery:
                case TutorialStep.PursuitCollision: return TutorialHighlight.RearView;
                default: return TutorialHighlight.None;
            }
        }
    }

    /// <summary>The one instruction shown until the player does it.</summary>
    public string Title
    {
        get
        {
            if (effects != null && (Step == TutorialStep.Shield || Step == TutorialStep.Recovery || Step == TutorialStep.Nitro)
                && effects.Backpack.Item != RandomPickupOutcome.None && !stepSucceeded)
                return "SPACE: USE " + effects.Backpack.Item.ToString().ToUpperInvariant();
            switch (Step)
            {
                case TutorialStep.Steer: return "LEFT AND RIGHT";
                case TutorialStep.Dodge: return "AVOID THIS BLOCK";
                case TutorialStep.Fuel: return fuelLessonStarted ? "GRAB FUEL" : "FUEL DRAINS OVER TIME";
                case TutorialStep.Reverse: return stepSucceeded ? "CONTROLS NORMAL"
                    : !reverseCollected ? "GRAB THE MYSTERY BOX"
                    : effects.ReverseCountdownRemaining > 0f ? "GET READY" : "TRY BOTH DIRECTIONS";
                case TutorialStep.Shield: return stepSucceeded ? "SHIELD SAVED YOU"
                    : lessonCollected ? "SHIELD: HIT THE BLOCK" : "GRAB THE MYSTERY BOX";
                case TutorialStep.Recovery: return lessonCollected ? "JAMMER: MORE SPACE" : "GRAB THE MYSTERY BOX";
                case TutorialStep.Nitro: return stepSucceeded ? "NITRO: PULL AWAY" : "GRAB THE MYSTERY BOX";
                case TutorialStep.PursuitCollision: return pursuitCollisionDemonstrated
                    ? "HITS BRING POLICE CLOSER" : "TRY HITTING THIS BLOCK";
                case TutorialStep.Ammo: return "MYSTERY BOX: GUN WITH 3 SHOTS";
                case TutorialStep.Shoot: return reloading ? "COLLECT A GUN BOX" : "SPACE: SHOOT";
                default: return null;
            }
        }
    }

    public void Configure(InfiniteRoad infiniteRoad, Transform playerTransform, OutlawShooting playerShooting,
        ObstacleSpawner obstacleSpawner, FuelTankSpawner fuelSpawner, FuelMeter fuelMeter)
    {
        road = infiniteRoad;
        player = playerTransform;
        shooting = playerShooting;
        obstacles = obstacleSpawner;
        fuelTanks = fuelSpawner;
        fuel = fuelMeter;
        powerups = FindFirstObjectByType<PickupSpawner>();
        effects = player.GetComponent<CarPickupEffects>();
    }

    public void Begin()
    {
        if (Active) return;
        Active = true;
        tutorialStartedAt = Time.time;
        GameEvents.PickupCollected += OnPickupCollected;
        effects.ItemActivated += OnItemActivated;
        GameEvents.ObstacleCollision += OnObstacleCollision;
        GameEvents.ShotFired += OnShotFired;
        GameEvents.DryFire += OnDryFire;
        if (fuel != null) fuel.DrainMode = FuelDrainMode.Frozen;
        // Ammo is introduced later; the squares fill up as the player collects A boxes.
        shooting.SetAmmo(0);
        WeeWooAnalytics.Track("tutorial_start");
        EnterStep(TutorialStep.Steer);
    }

    private void OnDestroy()
    {
        GameEvents.PickupCollected -= OnPickupCollected;
        if (effects != null) effects.ItemActivated -= OnItemActivated;
        GameEvents.ObstacleCollision -= OnObstacleCollision;
        GameEvents.ShotFired -= OnShotFired;
        GameEvents.DryFire -= OnDryFire;
    }

    /// <summary>Row for the analytics "tutorials" sheet if the tab closes mid-tutorial.</summary>
    public Dictionary<string, object> AbandonRow()
    {
        var row = new Dictionary<string, object>(stepRecord)
        {
            ["status"] = "abandoned",
            ["quit_step"] = Step.ToString(),
            ["quit_step_index"] = (int)Step,
            ["seconds"] = RunTracker.Round(SecondsInTutorial),
            ["retries"] = totalRetries
        };
        return row;
    }

    private void Update()
    {
        if (!Active || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;
        if (FeedbackRemaining > 0f) FeedbackRemaining = Mathf.Max(0f, FeedbackRemaining - dt);

        if (stepSucceeded)
        {
            waitRemaining -= dt;
            if (waitRemaining <= 0f) Advance();
            return;
        }

        switch (Step)
        {
            case TutorialStep.Steer: UpdateSteer(dt); break;
            case TutorialStep.Nitro: UpdateNitro(dt); break;
            case TutorialStep.Reverse: UpdateReverse(dt); break;
            case TutorialStep.Shield: UpdateShield(); break;
            case TutorialStep.Recovery: UpdateRecovery(); break;
            case TutorialStep.PursuitCollision: UpdatePursuitCollision(); break;
            case TutorialStep.Fuel: UpdateFuel(dt); break;
            case TutorialStep.Ammo: UpdateAmmo(dt); break;
            case TutorialStep.Shoot: UpdateShoot(dt); break;
            case TutorialStep.Dodge: UpdateDodge(dt); break;
        }
    }

    private void EnterStep(TutorialStep step)
    {
        Step = step;
        stepStartedAt = Time.time;
        stepRetries = 0;
        stepShots = 0;
        stepDryFires = 0;
        stepSucceeded = false;
        waitRemaining = 0f;
        WeeWooAnalytics.Track("tutorial_step_start", "step", step.ToString(), "step_index", (int)step);

        switch (step)
        {
            case TutorialStep.Fuel:
                // Separate the fuel lesson from the preceding collision/dodge feedback.
                fuel.DrainMode = FuelDrainMode.Frozen;
                fuelLessonStarted = false;
                waitRemaining = 0.8f;
                obstacles.ClearNear(player.position, SpawnAhead + 20f);
                tankCollected = false;
                break;
            case TutorialStep.Shield:
            case TutorialStep.Recovery:
                obstacles.ClearNear(player.position, SpawnAhead + 20f);
                lessonCollected = false;
                shieldDemonstrated = false;
                wall = null;
                lessonMystery = powerups.SpawnTutorialMystery(35f, PlayerLateral(),
                    step == TutorialStep.Shield ? PickupEffectType.Shield : PickupEffectType.ProximityRecovery);
                break;
            case TutorialStep.PursuitCollision:
                obstacles.ClearNear(player.position, SpawnAhead + 20f);
                pursuitCollisionDemonstrated = false;
                wall = obstacles.SpawnTutorialWall(45f, TrafficObstacleState.Yellow);
                break;
            case TutorialStep.Reverse:
                obstacles.ClearNear(player.position, SpawnAhead + 20f);
                ClearAmmoBoxes();
                fuel.DrainMode = FuelDrainMode.Frozen;
                LeftDone = RightDone = false;
                leftHeld = rightHeld = 0f;
                previousReverseLateral = PlayerLateral();
                reverseCollected = false;
                reversePracticeSeconds = 0f;
                lessonMystery = powerups.SpawnTutorialMystery(35f, PlayerLateral(), PickupEffectType.ReverseSteering);
                break;
            case TutorialStep.Nitro:
                nitro = powerups.SpawnTutorialNitro(35f, PlayerLateral());
                break;
            case TutorialStep.Ammo:
                shooting.SetAmmo(0);
                SpawnAmmoLine();
                break;
            case TutorialStep.Shoot:
                reloading = false;
                shootWallState = TrafficObstacleState.Green;
                wall = obstacles.SpawnTutorialWall(ShootSpawnAhead, shootWallState);
                break;
            case TutorialStep.Dodge:
                SpawnDodgeRow();
                break;
            case TutorialStep.Done:
                Finish();
                break;
        }
    }

    /// <summary>Moves on silently after a short breather; there are no success pop-ups.</summary>
    private void Succeed(float delay = 0.6f)
    {
        if (stepSucceeded) return;
        stepSucceeded = true;
        waitRemaining = delay;
        float seconds = Time.time - stepStartedAt;
        string name = Step.ToString().ToLowerInvariant();
        stepRecord[name + "_s"] = RunTracker.Round(seconds);
        stepRecord[name + "_retries"] = stepRetries;
        WeeWooAnalytics.Track("tutorial_step_complete", "step", Step.ToString(), "step_index", (int)Step,
            "seconds", RunTracker.Round(seconds), "retries", stepRetries, "shots", stepShots, "dry_fires", stepDryFires);
        WeeWooAnalytics.Flush();
    }

    private void Retry(string reason)
    {
        stepRetries++;
        totalRetries++;
        Flash("TRY AGAIN!", false);
        WeeWooAnalytics.Track("tutorial_retry", "step", Step.ToString(), "reason", reason, "attempt", stepRetries + 1);
    }

    private void Flash(string message, bool good)
    {
        Feedback = message;
        FeedbackIsGood = good;
        FeedbackRemaining = 1f;
    }

    private void Advance()
    {
        EnterStep(Step == TutorialStep.Shoot ? TutorialStep.Shield
            : Step == TutorialStep.Shield ? TutorialStep.Reverse
            : Step == TutorialStep.Reverse ? TutorialStep.Recovery
            : Step == TutorialStep.Recovery ? TutorialStep.PursuitCollision
            : Step == TutorialStep.PursuitCollision ? TutorialStep.Nitro
            : Step == TutorialStep.Nitro ? TutorialStep.Done : Step + 1);
    }

    private void UpdateSteer(float dt)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) leftHeld += dt;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) rightHeld += dt;
        LeftDone = leftHeld >= SteerHoldSeconds;
        RightDone = rightHeld >= SteerHoldSeconds;
        if (LeftDone && RightDone) Succeed(0.6f);
    }

    private void RespawnLessonBox(PickupEffectType outcome)
    {
        if (lessonMystery != null && !road.IsBehind(lessonMystery.transform.position, player.position, 3f)) return;
        if (lessonMystery != null) Destroy(lessonMystery.gameObject);
        lessonMystery = powerups.SpawnTutorialMystery(35f, PlayerLateral(), outcome);
    }

    private void UpdateShield()
    {
        if (!lessonCollected)
        {
            if (effects.Backpack.Item != RandomPickupOutcome.Shield) RespawnLessonBox(PickupEffectType.Shield);
            return;
        }
        if (shieldDemonstrated)
        {
            obstacles.ClearNear(player.position, SpawnAhead + 20f);
            Succeed(1.5f);
            return;
        }
        if (wall == null || !obstacles.IsActive(wall) || road.IsBehind(wall.transform.position, player.position, 3f))
        {
            obstacles.ClearNear(player.position, SpawnAhead + 20f);
            wall = obstacles.SpawnTutorialWall(45f, TrafficObstacleState.Green);
        }
    }

    private void UpdateRecovery()
    {
        if (!lessonCollected)
        {
            if (effects.Backpack.Item != RandomPickupOutcome.ProximityRecovery) RespawnLessonBox(PickupEffectType.ProximityRecovery);
            return;
        }
        // Leave room for the final nitro lesson to visibly open the gap further.
        effects.RunState.RecoveryCap = RiskRunState.MaximumPursuitGap;
        Succeed(1.5f);
    }

    private void UpdatePursuitCollision()
    {
        if (pursuitCollisionDemonstrated)
        {
            obstacles.ClearNear(player.position, SpawnAhead + 20f);
            Succeed(1.5f);
            return;
        }
        // Shooting or missing the demonstration must not leave the lesson stuck.
        if (wall == null || !obstacles.IsActive(wall) || road.IsBehind(wall.transform.position, player.position, 3f))
        {
            obstacles.ClearNear(player.position, SpawnAhead + 20f);
            wall = obstacles.SpawnTutorialWall(45f, TrafficObstacleState.Yellow);
        }
    }

    private void UpdateReverse(float dt)
    {
        if (!reverseCollected)
        {
            if (effects.ReverseCountdownRemaining > 0f || effects.ActiveEffect == PickupEffectType.ReverseSteering)
            {
                reverseCollected = true;
                previousReverseLateral = PlayerLateral();
            }
            else
            {
                if (lessonMystery == null || road.IsBehind(lessonMystery.transform.position, player.position, 3f))
                {
                    if (lessonMystery != null) Destroy(lessonMystery.gameObject);
                    lessonMystery = powerups.SpawnTutorialMystery(35f, PlayerLateral(), PickupEffectType.ReverseSteering);
                }
                return;
            }
        }
        float lateral = PlayerLateral();
        float movement = lateral - previousReverseLateral;
        previousReverseLateral = lateral;
        if (effects.ReverseCountdownRemaining > 0f) return;
        reversePracticeSeconds += dt;
        // Keep this safe practice active until the player has felt both directions.
        if (effects.RemainingSeconds < 1f)
            effects.RunState.Effects.Apply(PickupEffectType.ReverseSteering, 2f);
        Keyboard keys = Keyboard.current;
        if (keys == null) return;
        if ((keys.leftArrowKey.isPressed || keys.aKey.isPressed) && movement > 0f)
            leftHeld += dt;
        if ((keys.rightArrowKey.isPressed || keys.dKey.isPressed) && movement < 0f)
            rightHeld += dt;
        LeftDone = leftHeld >= ReverseDirectionHoldSeconds;
        RightDone = rightHeld >= ReverseDirectionHoldSeconds;
        if (!LeftDone || !RightDone || reversePracticeSeconds < ReversePracticeMinimumSeconds) return;
        effects.Clear();
        Succeed(0.8f);
    }

    private void UpdateNitro(float dt)
    {
        if (player.GetComponent<CarPickupEffects>().RunState.HasNitro)
        {
            // Allow time to see the gradual retreat, unlike the instant jammer bonus.
            Succeed(2.5f);
            return;
        }
        if (effects.Backpack.Item == RandomPickupOutcome.Nitro) return;
        if (nitro == null)
        {
            waitRemaining -= dt;
            if (waitRemaining <= 0f) nitro = powerups.SpawnTutorialNitro(35f, PlayerLateral());
        }
        else if (road.IsBehind(nitro.transform.position, player.position, 3f))
        {
            Destroy(nitro.gameObject);
            nitro = null;
            waitRemaining = 1f;
            Retry("missed_nitro");
        }
    }

    private void UpdateFuel(float dt)
    {
        if (!fuelLessonStarted)
        {
            waitRemaining -= dt;
            if (waitRemaining > 0f) return;
            fuelLessonStarted = true;
            fuel.DrainMode = FuelDrainMode.TutorialPlunge;
            tank = fuelTanks.SpawnAhead(FuelSpawnAhead, PlayerLateral());
        }
        if (tankCollected)
        {
            fuel.SetFuel(fuel.MaximumFuel);
            fuel.DrainMode = FuelDrainMode.Frozen;
            // Keep a brief break so fuel and ammo remain separate lessons.
            Succeed(0.8f);
            return;
        }

        if (tank == null)
        {
            waitRemaining -= dt;
            if (waitRemaining > 0f) return;
            tank = fuelTanks.SpawnAhead(FuelSpawnAhead, PlayerLateral());
            return;
        }

        if (road.IsBehind(tank.transform.position, player.position, 3f))
        {
            Destroy(tank);
            tank = null;
            waitRemaining = 1f;
            Retry("missed_fuel");
        }
    }

    private void SpawnAmmoLine(int targetAmmo = 3)
    {
        ClearAmmoBoxes();
        var box = powerups.SpawnTutorialMystery(35f, PlayerLateral(), PickupEffectType.Gun);
        if (box != null) ammoBoxes.Add(box.gameObject);
    }

    private void UpdateAmmo(float dt)
    {
        bool anyAhead = false;
        foreach (GameObject box in ammoBoxes)
            if (box != null && !road.IsBehind(box.transform.position, player.position, 2f))
                anyAhead = true;

        // The shooting lesson needs three rounds: one for the 1 block, two for the 2 block.
        if (shooting.CurrentAmmo >= 3)
        {
            ClearAmmoBoxes();
            Succeed();
            return;
        }

        if (!anyAhead)
        {
            ClearAmmoBoxes();
            Retry("missed_ammo");
            SpawnAmmoLine();
        }
    }

    private void ClearAmmoBoxes()
    {
        foreach (GameObject box in ammoBoxes)
            if (box != null) Destroy(box);
        ammoBoxes.Clear();
    }

    private void UpdateShoot(float dt)
    {
        int requiredAmmo = (int)shootWallState + 1;
        if (reloading)
        {
            // Keep earned rounds and refill only enough for this wall.
            bool anyAhead = false;
            foreach (GameObject box in ammoBoxes)
                if (box != null && !road.IsBehind(box.transform.position, player.position, 2f))
                    anyAhead = true;
            if (shooting.CurrentAmmo >= requiredAmmo)
            {
                ClearAmmoBoxes();
                reloading = false;
                waitRemaining = 0.4f;
            }
            else if (!anyAhead)
            {
                SpawnAmmoLine(requiredAmmo);
            }
            return;
        }

        if (wall == null)
        {
            waitRemaining -= dt;
            if (waitRemaining > 0f) return;
            // A player may fire while the next wall is being introduced.
            // Refill before placing it rather than making them hit it to retry.
            if (shooting.CurrentAmmo < requiredAmmo)
            {
                reloading = true;
                SpawnAmmoLine(requiredAmmo);
                return;
            }
            wall = obstacles.SpawnTutorialWall(ShootSpawnAhead, shootWallState);
            return;
        }

        if (!obstacles.IsActive(wall) && !wall.WasHit)
        {
            wall = null;
            if (shootWallState == TrafficObstacleState.Green)
            {
                shootWallState = TrafficObstacleState.Yellow;
                waitRemaining = 0.6f;
            }
            else
            {
                Succeed(0.4f);
            }
            return;
        }

        if (!obstacles.IsActive(wall) || road.IsBehind(wall.transform.position, player.position, 1f))
        {
            wall = null;
            Retry("hit_wall");
            obstacles.ClearNear(player.position, SpawnAhead + 20f);
            reloading = shooting.CurrentAmmo < requiredAmmo;
            if (reloading) SpawnAmmoLine(requiredAmmo);
            else waitRemaining = 0.8f;
        }
    }

    private void SpawnDodgeRow()
    {
        dodgeHit = false;
        dodgeRow = obstacles.SpawnTutorialDodgeRow(DodgeSpawnAhead);
    }

    private void UpdateDodge(float dt)
    {
        if (dodgeRow.Count == 0)
        {
            waitRemaining -= dt;
            if (waitRemaining <= 0f) SpawnDodgeRow();
            return;
        }

        TrafficLightObstacle block = dodgeRow[0];
        bool gone = !obstacles.IsActive(block);
        if (!gone && !road.IsBehind(block.transform.position, player.position, 1f)) return;

        dodgeRow.Clear();
        if (dodgeHit)
        {
            waitRemaining = 0.9f;
            Retry("hit_block");
            return;
        }

        Succeed();
    }

    private void Finish()
    {
        Active = false;
        float seconds = Time.time - tutorialStartedAt;
        OutlawProfile.MarkTutorialCompleted();
        var row = new Dictionary<string, object>(stepRecord)
        {
            ["status"] = "completed",
            ["seconds"] = RunTracker.Round(seconds),
            ["retries"] = totalRetries
        };
        WeeWooAnalytics.AddRow("tutorials", row);
        WeeWooAnalytics.Track("tutorial_complete", "seconds", RunTracker.Round(seconds), "retries", totalRetries);
        WeeWooAnalytics.Flush();
        OnDestroy();
        Completed?.Invoke();
    }

    private float PlayerLateral()
    {
        return road.TryGetPathPoint(player.position, out RoadPathPoint current)
            ? Vector3.Dot(player.position - current.Position, current.Right)
            : 0f;
    }

    private void OnPickupCollected(PickupKind kind, string outcome)
    {
        if (Active && Step == TutorialStep.Fuel && kind == PickupKind.Fuel) tankCollected = true;
        if (!Active || kind != PickupKind.Mystery) return;
    }

    private void OnItemActivated(RandomPickupOutcome item)
    {
        if (!Active) return;
        if ((Step == TutorialStep.Shield && item == RandomPickupOutcome.Shield)
            || (Step == TutorialStep.Recovery && item == RandomPickupOutcome.ProximityRecovery)) lessonCollected = true;
    }

    private void OnObstacleCollision(TrafficObstacleState colour, bool shielded)
    {
        if (Active && Step == TutorialStep.Dodge) dodgeHit = true;
        if (Active && Step == TutorialStep.Shield && shielded) shieldDemonstrated = true;
        if (Active && Step == TutorialStep.PursuitCollision && !shielded) pursuitCollisionDemonstrated = true;
    }

    private void OnShotFired()
    {
        if (Active) stepShots++;
    }

    private void OnDryFire()
    {
        if (Active) stepDryFires++;
    }
}
