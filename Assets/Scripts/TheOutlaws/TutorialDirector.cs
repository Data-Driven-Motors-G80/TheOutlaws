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
    Done
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
    private const float SteerHoldSeconds = 0.2f;

    private InfiniteRoad road;
    private Transform player;
    private OutlawShooting shooting;
    private ObstacleSpawner obstacles;
    private FuelTankSpawner fuelTanks;
    private FuelMeter fuel;

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
    /// <summary>Shoot step: the wall was hit, so the player collects ammo again before a new wall.</summary>
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
                case TutorialStep.Dodge: return TutorialHighlight.RearView;
                default: return TutorialHighlight.None;
            }
        }
    }

    /// <summary>The one instruction shown until the player does it.</summary>
    public string Title
    {
        get
        {
            switch (Step)
            {
                case TutorialStep.Steer: return "LEFT AND RIGHT";
                case TutorialStep.Dodge: return "AVOID OBSTACLES";
                case TutorialStep.Fuel: return "GRAB FUEL";
                case TutorialStep.Ammo: return "GRAB AMMO";
                case TutorialStep.Shoot: return reloading ? "GRAB AMMO" : "SHOOT";
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
    }

    public void Begin()
    {
        if (Active) return;
        Active = true;
        tutorialStartedAt = Time.time;
        GameEvents.PickupCollected += OnPickupCollected;
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
                fuel.DrainMode = FuelDrainMode.TutorialPlunge;
                // Let the player watch the gauge fall before the tank appears.
                waitRemaining = 1.4f;
                tank = null;
                tankCollected = false;
                break;
            case TutorialStep.Ammo:
                shooting.SetAmmo(0);
                SpawnAmmoLine();
                break;
            case TutorialStep.Shoot:
                reloading = false;
                shootWallState = TrafficObstacleState.Green;
                wall = obstacles.SpawnTutorialWall(SpawnAhead + 10f, shootWallState);
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
    private void Succeed(float delay = 1.2f)
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
        EnterStep(Step + 1);
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

    private void UpdateFuel(float dt)
    {
        if (tankCollected)
        {
            fuel.SetFuel(fuel.MaximumFuel);
            fuel.DrainMode = FuelDrainMode.Frozen;
            // A longer breather before ammo so the two lessons don't blur together.
            Succeed(2f);
            return;
        }

        if (tank == null)
        {
            waitRemaining -= dt;
            if (waitRemaining > 0f) return;
            // The F sits in the open lane just past a block: steer around it to choose the fuel.
            obstacles.SpawnTutorialDodgeRow(SpawnAhead, out float openLateral);
            tank = fuelTanks.SpawnAhead(SpawnAhead + 8f, openLateral);
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

    private void SpawnAmmoLine()
    {
        ammoBoxes.Clear();
        float lateral = PlayerLateral();
        for (int i = 0; i < 3; i++)
        {
            if (!road.TryGetPointAhead(player.position, 35f + i * 9f, out RoadPathPoint point)) continue;
            float clamped = Mathf.Clamp(lateral, -point.Width * 0.5f + 1.2f, point.Width * 0.5f - 1.2f);
            GameObject box = OutlawAmmoPickup.Create(
                point.Position + point.Right * clamped + point.Up * 0.32f, point.Up, shooting);
            box.transform.SetParent(transform, true);
            ammoBoxes.Add(box);
        }
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
        if (reloading)
        {
            // Hitting the wall resets the step: collect ammo again, then a new wall appears.
            bool anyAhead = false;
            foreach (GameObject box in ammoBoxes)
                if (box != null && !road.IsBehind(box.transform.position, player.position, 2f))
                    anyAhead = true;
            int requiredAmmo = shootWallState == TrafficObstacleState.Green ? 3 : 2;
            if (shooting.CurrentAmmo >= requiredAmmo && (!anyAhead || shooting.CurrentAmmo >= 3))
            {
                ClearAmmoBoxes();
                reloading = false;
                waitRemaining = 0.4f;
            }
            else if (!anyAhead)
            {
                ClearAmmoBoxes();
                SpawnAmmoLine();
            }
            return;
        }

        if (wall == null)
        {
            waitRemaining -= dt;
            if (waitRemaining > 0f) return;
            wall = obstacles.SpawnTutorialWall(SpawnAhead + 10f, shootWallState);
            return;
        }

        if (!obstacles.IsActive(wall) && !wall.WasHit)
        {
            wall = null;
            if (shootWallState == TrafficObstacleState.Green)
            {
                shootWallState = TrafficObstacleState.Yellow;
                waitRemaining = 1.2f;
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
            shooting.SetAmmo(0);
            reloading = true;
            SpawnAmmoLine();
        }
    }

    private void SpawnDodgeRow()
    {
        dodgeHit = false;
        dodgeRow = obstacles.SpawnTutorialDodgeRow(SpawnAhead + 5f);
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
    }

    private void OnObstacleCollision(TrafficObstacleState colour, bool shielded)
    {
        if (Active && Step == TutorialStep.Dodge) dodgeHit = true;
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
