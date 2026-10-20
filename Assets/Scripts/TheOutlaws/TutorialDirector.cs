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
    Reverse, // Retired lessons, kept so older analytics rows still decode.
    Shield, // The shield mystery box, followed by a wall to ram.
    Recovery,
    PursuitCollision,
    Intro,
    ShootTwice,
    MysteryBoost, // Retired.
    MysteryDizzy
}

public enum TutorialHighlight
{
    None,
    Fuel,
    Ammo,
    RearView
}

/// <summary>
/// Short show-don't-tell tutorial: one instruction at a time, and a step only ends once the
/// player has done it. Each lesson's objects are laid out on the road well ahead (never popping in
/// near the car), and the car speeds up while the next lesson is still far away so there is no
/// waiting. Blocks in lessons guide the player towards the pickups. A hit stops the car, the police
/// drive into it ("TRY AGAIN!") and the lesson is laid out again; a missed pickup just flashes
/// "TRY AGAIN!" and lays the lesson out again without stopping. Nothing here counts towards the real run.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialDirector : MonoBehaviour
{
    private static readonly TutorialStep[] Order =
    {
        TutorialStep.Intro, TutorialStep.Steer, TutorialStep.Dodge, TutorialStep.Fuel, TutorialStep.Ammo,
        TutorialStep.Shoot, TutorialStep.ShootTwice, TutorialStep.Shield, TutorialStep.MysteryDizzy,
        TutorialStep.Nitro, TutorialStep.Done
    };

    /// <summary>Lessons are placed at least this far ahead, like the real run's spawns.</summary>
    private const float PrespawnAhead = PickupLookAhead.Distance;
    /// <summary>While the current lesson is farther than this, the car drives faster to reach it.</summary>
    private const float CatchUpAhead = 50f;
    private const float CatchUpMultiplier = 1.5f;
    private const float ShootingSpeedMultiplier = 0.8f;
    private const float DodgeRowGap = 45f;
    private const float GuideBlockGap = 10f;
    private const float SteerHoldSeconds = 0.2f;
    private const float IntroSeconds = 2.5f;
    private const float ShieldWallGap = 25f;
    private const int StartingAmmo = 2;
    private const int AmmoBoxes = 3;
    private const float SpareAmmoAhead = 3f;
    private const float SpareAmmoDelay = 0.1f;
    private const float FuelTankGap = 6f;
    private const float NitroMessageSeconds = 1.6f;
    private const float CompleteMessageSeconds = 1.6f;
    /// <summary>A quick crash: the car stops, the police bump it, and the lesson is laid out again.</summary>
    private const float CatchSeconds = 0.7f;
    /// <summary>"HIT BLOCKED!" stays up this long after ramming the wall with the shield.</summary>
    private const float HitBlockedSeconds = 2.3f;
    /// <summary>A failed attempt lays its lesson out a little closer than a new lesson, so retries come quickly.</summary>
    private const float RetryAhead = 70f;
    /// <summary>The shooting walls appear this far ahead (the first one right after the practice shot).</summary>
    private const float ShootAhead = 70f;

    /// <summary>One lesson's objects, with the road distances they span.</summary>
    private sealed class Section
    {
        public float Start;
        public float End;
        public readonly List<GameObject> Pickups = new List<GameObject>();
        public readonly List<TrafficLightObstacle> Blocks = new List<TrafficLightObstacle>();
        /// <summary>Analytics name of each block (which obstacle a player crashed into).</summary>
        public readonly List<string> BlockNames = new List<string>();
        /// <summary>Road distance of each block, to tell when the car has got past it.</summary>
        public readonly List<float> BlockAt = new List<float>();
        public readonly List<bool> BlockDone = new List<bool>();
        public readonly List<string> PickupNames = new List<string>();
        public readonly List<bool> PickupDone = new List<bool>();
    }

    /// <summary>Analytics for one lesson, kept across retries (and across the dizzy/nitro loop).</summary>
    private sealed class StepStats
    {
        public float Seconds;
        public int Entries;
        public int Retries;
        public int Hits;
        public int Misses;
        public int Shots;
        public int DryFires;
        public int SpareAmmo;
        public int Completions;
    }

    private InfiniteRoad road;
    private Transform player;
    private AutoDriveCar driver;
    private OutlawShooting shooting;
    private ObstacleSpawner obstacles;
    private FuelTankSpawner fuelTanks;
    private FuelMeter fuel;
    private PickupSpawner powerups;
    private CarPickupEffects effects;

    private float tutorialStartedAt;
    private float waitRemaining;
    private float leftHeld;
    private float rightHeld;
    private int totalRetries;
    private int totalHits;
    private int totalMisses;
    private int totalShots;
    private int totalDryFires;
    private string failLog = "";
    /// <summary>Everything passed, shot, collected or missed: "Step:what=result;...".</summary>
    private string passLog = "";
    /// <summary>What the player got past in the current attempt (reported with a failure).</summary>
    private string attemptProgress = "";
    private int stepsCompleted;
    private string lastStepCompleted = "";
    private bool relaid;
    private readonly Dictionary<string, object> stepRecord = new Dictionary<string, object>();
    private readonly Dictionary<TutorialStep, StepStats> stats = new Dictionary<TutorialStep, StepStats>();
    private readonly Dictionary<TutorialStep, Section> sections = new Dictionary<TutorialStep, Section>();

    private int tanksCollected;
    private int fuelTanksApplied;
    private bool shootPracticed;
    private bool shieldCollected;
    private bool shieldDemonstrated;
    private bool dizzyCollected;
    private int ammoBoxesCollected;
    private float lastShotAt = -100f;
    private GameObject spareAmmo;
    private float catchRemaining;
    private bool stepSucceeded;

    public bool Active { get; private set; }
    public TutorialStep Step { get; private set; }
    public bool LeftDone { get; private set; }
    public bool RightDone { get; private set; }
    public string Feedback { get; private set; }
    public float FeedbackRemaining { get; private set; }
    public bool FeedbackIsGood { get; private set; }
    public float SecondsInTutorial => Active ? Time.time - tutorialStartedAt : 0f;
    /// <summary>After a failure: the car stops and the police drive into it before the lesson restarts.</summary>
    public bool Catching { get; private set; }
    /// <summary>Scales the tutorial driving speed: zero while caught, faster while the next lesson is far.</summary>
    public float SpeedMultiplier { get; private set; } = 1f;
    public bool TitleFlashing => Step == TutorialStep.Fuel && tanksCollected == 0 && !Catching;
    /// <summary>The first shooting lesson starts with a practice shot at an empty road; the first wall follows it.</summary>
    public bool ShootPracticePending => Step == TutorialStep.Shoot && !shootPracticed && !Catching;

    public event Action Completed;

    private float CarDistance => driver != null ? driver.RoadPositionDistance : 0f;
    private StepStats Stats
    {
        get
        {
            if (!stats.TryGetValue(Step, out StepStats current)) stats[Step] = current = new StepStats();
            return current;
        }
    }
    private bool NitroCollected => Step == TutorialStep.Nitro && effects.RunState.HasNitro;
    /// <summary>After "NITRO! SPEED BOOST", "TUTORIAL COMPLETE" shows on its own before the run starts.</summary>
    private bool CompletePhase => NitroCollected && stepSucceeded && waitRemaining <= CompleteMessageSeconds;

    public TutorialHighlight Highlight
    {
        get
        {
            switch (Step)
            {
                case TutorialStep.Fuel: return TutorialHighlight.Fuel;
                case TutorialStep.Ammo: return TutorialHighlight.Ammo;
                case TutorialStep.Intro: return TutorialHighlight.RearView;
                default: return TutorialHighlight.None;
            }
        }
    }

    /// <summary>The one instruction shown until the player does it.</summary>
    public string Title
    {
        get
        {
            if (Catching) return "TRY AGAIN!";
            switch (Step)
            {
                case TutorialStep.Intro: return "ESCAPE THE COPS!";
                case TutorialStep.Steer: return "STEER";
                case TutorialStep.Dodge: return "AVOID OBSTACLES";
                case TutorialStep.Fuel: return "FUEL LOW";
                case TutorialStep.Ammo: return "GRAB 3 AMMO";
                case TutorialStep.Shoot: return ShootPracticePending ? "SHOOT A BULLET" : "DESTROY THE OBSTACLE";
                case TutorialStep.ShootTwice: return "DESTROY THE OBSTACLE";
                case TutorialStep.Shield:
                    return shieldDemonstrated ? "HIT BLOCKED!" : shieldCollected ? "SHIELD!" : "MYSTERY BOX";
                case TutorialStep.MysteryDizzy: return dizzyCollected ? "DIZZY!" : "MYSTERY BOX";
                case TutorialStep.Nitro: return CompletePhase ? "TUTORIAL COMPLETE" : NitroCollected ? "NITRO!" : "DIZZY!";
                default: return null;
            }
        }
    }

    /// <summary>A few words under the title, or null.</summary>
    public string Subtitle
    {
        get
        {
            if (Catching) return null;
            switch (Step)
            {
                case TutorialStep.MysteryDizzy:
                    return dizzyCollected ? "CONTROLS REVERSED" : null;
                case TutorialStep.Shield: return shieldCollected && !shieldDemonstrated ? "PROTECTS FROM A HIT" : null;
                case TutorialStep.Nitro: return CompletePhase ? null : NitroCollected ? "SPEED BOOST" : "CONTROLS REVERSED";
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
        driver = player.GetComponent<AutoDriveCar>();
    }

    public void Begin()
    {
        if (Active) return;
        Active = true;
        tutorialStartedAt = Time.time;
        totalRetries = totalHits = totalMisses = totalShots = totalDryFires = 0;
        failLog = passLog = attemptProgress = lastStepCompleted = "";
        stepsCompleted = 0;
        stepRecord.Clear();
        stats.Clear();
        Catching = false;
        SpeedMultiplier = 1f;
        sections.Clear();
        GameEvents.PickupCollected += OnPickupCollected;
        GameEvents.ObstacleCollision += OnObstacleCollision;
        GameEvents.ShotFired += OnShotFired;
        GameEvents.DryFire += OnDryFire;
        GameEvents.AmmoPickupWastedFull += OnAmmoWastedFull;
        // Fuel drains for the whole tutorial but never runs out.
        if (fuel != null) fuel.DrainMode = FuelDrainMode.TutorialSteady;
        // Empty squares until the ammo lesson, which loads the two starting rounds and asks for three more.
        shooting.SetAmmo(0);
        WeeWooAnalytics.Track("tutorial_start");
        EnterStep(TutorialStep.Intro);
    }

    private void OnDestroy()
    {
        GameEvents.PickupCollected -= OnPickupCollected;
        GameEvents.ObstacleCollision -= OnObstacleCollision;
        GameEvents.ShotFired -= OnShotFired;
        GameEvents.DryFire -= OnDryFire;
        GameEvents.AmmoPickupWastedFull -= OnAmmoWastedFull;
    }

    /// <summary>Row for the analytics "tutorials" sheet if the tab closes mid-tutorial.</summary>
    public Dictionary<string, object> AbandonRow()
    {
        StepStats current = Stats;
        var row = new Dictionary<string, object>(stepRecord)
        {
            ["status"] = "abandoned",
            ["quit_step"] = Step.ToString(),
            ["quit_step_index"] = (int)Step,
            // The lesson they left on: how long and how many tries, and how far the last try got.
            ["quit_step_seconds"] = RunTracker.Round(current.Seconds),
            ["quit_step_attempt"] = current.Retries + 1,
            ["quit_step_retries"] = current.Retries,
            ["quit_step_progress"] = attemptProgress,
            ["seconds"] = RunTracker.Round(SecondsInTutorial),
            ["retries"] = totalRetries
        };
        AddTotals(row);
        return row;
    }

    /// <summary>Whole-tutorial totals for the tutorials sheet.</summary>
    private void AddTotals(Dictionary<string, object> row)
    {
        row["hits_total"] = totalHits;
        row["misses_total"] = totalMisses;
        row["shots_total"] = totalShots;
        row["dry_fires_total"] = totalDryFires;
        row["attempts_total"] = stepsCompleted + totalRetries;
        row["steps_completed"] = stepsCompleted;
        row["last_step_completed"] = lastStepCompleted;
        int firstTry = 0;
        foreach (KeyValuePair<TutorialStep, StepStats> entry in stats)
            if (entry.Value.Completions > 0 && entry.Value.Retries == 0) firstTry++;
        row["steps_first_try"] = firstTry;
        row["fail_log"] = failLog;
        row["pass_log"] = passLog;
    }

    private void Update()
    {
        if (!Active || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;
        if (FeedbackRemaining > 0f) FeedbackRemaining = Mathf.Max(0f, FeedbackRemaining - dt);
        // Lesson time includes police catches but not pauses or the breather after succeeding.
        if (!stepSucceeded) Stats.Seconds += dt;
        SpeedMultiplier = Mathf.MoveTowards(SpeedMultiplier, TargetSpeedMultiplier(), (Catching ? 5f : 1.5f) * dt);

        if (Catching)
        {
            catchRemaining -= dt;
            if (catchRemaining <= 0f) EndCatch();
            return;
        }

        EnsureSections();
        TrackSectionObjects();
        if (stepSucceeded)
        {
            waitRemaining -= dt;
            if (waitRemaining <= 0f) Advance();
            return;
        }

        switch (Step)
        {
            case TutorialStep.Intro: UpdateIntro(dt); break;
            case TutorialStep.Steer: UpdateSteer(dt); break;
            case TutorialStep.Dodge: UpdateDodge(); break;
            case TutorialStep.Fuel: UpdateFuel(); break;
            case TutorialStep.Ammo: UpdateAmmo(); break;
            case TutorialStep.Shoot:
            case TutorialStep.ShootTwice: UpdateShoot(); break;
            case TutorialStep.Shield: UpdateShield(); break;
            case TutorialStep.MysteryDizzy: UpdateDizzy(); break;
            case TutorialStep.Nitro: UpdateNitro(); break;
        }
    }

    private float TargetSpeedMultiplier()
    {
        if (Catching) return 0f;
        // The shooting lessons drive a steady 20% slower, to give time to aim. No catch-up there: shots
        // reach the wall from any distance, and easing off the boost while shooting (just as the spare
        // A box pops up) felt like the car was being slowed down.
        if (Step == TutorialStep.Shoot || Step == TutorialStep.ShootTwice) return ShootingSpeedMultiplier;
        // Never during the fuel warning: that stretch should feel calm.
        if (stepSucceeded || Step == TutorialStep.Fuel || !sections.TryGetValue(Step, out Section current)) return 1f;
        return current.Start - CarDistance > CatchUpAhead ? CatchUpMultiplier : 1f;
    }

    private void EnterStep(TutorialStep step, bool restart = false)
    {
        Step = step;
        stepSucceeded = false;
        waitRemaining = 0f;
        relaid = restart;
        attemptProgress = "";
        if (!restart && step != TutorialStep.Done)
        {
            // Entries > 1 only for the nitro lesson after a failure sent the player back to the dizzy box.
            Stats.Entries++;
            WeeWooAnalytics.Track("tutorial_step_start", "step", step.ToString(), "step_index", (int)step,
                "entry", Stats.Entries, "tutorial_s", RunTracker.Round(SecondsInTutorial));
        }

        switch (step)
        {
            case TutorialStep.Intro:
                waitRemaining = IntroSeconds;
                break;
            case TutorialStep.Steer:
                LeftDone = RightDone = false;
                leftHeld = rightHeld = 0f;
                break;
            case TutorialStep.Fuel:
                // Fuel has been draining all along; now it drops straight into the red and keeps falling.
                tanksCollected = 0;
                fuelTanksApplied = 0;
                fuel.SetFuel(Mathf.Min(fuel.CurrentFuel, fuel.MaximumFuel * 0.22f));
                fuel.DrainMode = FuelDrainMode.TutorialPlunge;
                break;
            case TutorialStep.Ammo:
                // The two starting rounds appear as "GRAB 3 AMMO" begins.
                shooting.SetAmmo(StartingAmmo);
                ammoBoxesCollected = 0;
                break;
            case TutorialStep.Shoot:
                // A retry after hitting the wall skips the practice shot.
                shootPracticed = restart;
                if (shooting.CurrentAmmo < (restart ? 1 : 2)) shooting.SetAmmo(restart ? 1 : 2);
                break;
            case TutorialStep.ShootTwice:
                if (restart && shooting.CurrentAmmo < 2) shooting.SetAmmo(2);
                break;
            case TutorialStep.Shield:
                shieldCollected = false;
                shieldDemonstrated = false;
                // No rounds, so the wall after the shield can only be rammed.
                shooting.SetAmmo(0);
                break;
            case TutorialStep.MysteryDizzy:
                dizzyCollected = false;
                break;
            case TutorialStep.Done:
                Finish();
                return;
        }
        EnsureSections();
    }

    /// <summary>Moves on after a short breather; there are no success pop-ups.</summary>
    private void Succeed(float delay)
    {
        if (stepSucceeded) return;
        stepSucceeded = true;
        waitRemaining = delay;
        StepStats current = Stats;
        if (current.Completions++ == 0)
        {
            stepsCompleted++;
            lastStepCompleted = Step.ToString();
        }
        string name = Step.ToString().ToLowerInvariant();
        stepRecord[name + "_s"] = RunTracker.Round(current.Seconds);
        stepRecord[name + "_attempts"] = current.Retries + 1;
        stepRecord[name + "_first_try"] = current.Retries == 0;
        stepRecord[name + "_retries"] = current.Retries;
        stepRecord[name + "_hits"] = current.Hits;
        stepRecord[name + "_misses"] = current.Misses;
        stepRecord[name + "_shots"] = current.Shots;
        stepRecord[name + "_dry_fires"] = current.DryFires;
        // Lesson-specific results.
        if (Step == TutorialStep.Fuel) stepRecord["fuel_tanks_collected"] = tanksCollected;
        if (Step == TutorialStep.Ammo) stepRecord["ammo_boxes_collected"] = ammoBoxesCollected;
        if (Step == TutorialStep.Shoot || Step == TutorialStep.ShootTwice) stepRecord[name + "_spare_ammo"] = current.SpareAmmo;
        if (Step == TutorialStep.Shield) stepRecord["shield_blocked_hit"] = shieldDemonstrated;
        WeeWooAnalytics.Track("tutorial_step_complete", "step", Step.ToString(), "step_index", (int)Step,
            "seconds", RunTracker.Round(current.Seconds), "attempts", current.Retries + 1, "first_try", current.Retries == 0,
            "retries", current.Retries, "hits", current.Hits, "misses", current.Misses,
            "shots", current.Shots, "dry_fires", current.DryFires, "completion", current.Completions,
            "tutorial_s", RunTracker.Round(SecondsInTutorial));
        WeeWooAnalytics.Flush();
    }

    private void Advance()
    {
        int index = Array.IndexOf(Order, Step);
        EnterStep(index >= 0 && index + 1 < Order.Length ? Order[index + 1] : TutorialStep.Done);
    }

    // ---------------------------------------------------------------- lesson layout

    private static bool HasSection(TutorialStep step)
    {
        return step != TutorialStep.Intro && step != TutorialStep.Steer && step != TutorialStep.Done;
    }

    private static TutorialStep NextSectionStep(TutorialStep step)
    {
        for (int i = Array.IndexOf(Order, step) + 1; i < Order.Length; i++)
            if (HasSection(Order[i])) return Order[i];
        return TutorialStep.Done;
    }

    private static float SectionLength(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Dodge: return DodgeRowGap;
            case TutorialStep.Fuel: return FuelTankGap + GuideBlockGap;
            case TutorialStep.Shield: return ShieldWallGap;
            case TutorialStep.MysteryDizzy: return GuideBlockGap;
            case TutorialStep.Ammo: return 26f;
            default: return 0f;
        }
    }

    /// <summary>Road left after a lesson for its own timing (fuel warning, shield wall, controls flip).</summary>
    private static float GapAfter(TutorialStep step)
    {
        switch (step)
        {
            // Room for "FUEL LOW" to sink in before the tanks (driven at normal speed).
            case TutorialStep.Dodge: return 75f;
            // Between the 1 wall and the 2 wall.
            case TutorialStep.Shoot: return 25f;
            case TutorialStep.ShootTwice: return 30f;
            // Room after the shield wall before the dizzy box.
            case TutorialStep.Shield: return 50f;
            // A stretch of reversed steering before the gap with the nitro box.
            case TutorialStep.MysteryDizzy: return 45f;
            default: return 35f;
        }
    }

    /// <summary>Keeps the current lesson and the one after it laid out ahead of the car.</summary>
    private void EnsureSections()
    {
        // Nothing appears until the player has steered both ways, and no wall before the practice shot.
        if (!HasSection(Step) || ShootPracticePending) return;
        if (!sections.ContainsKey(Step))
            SpawnSection(Step, relaid ? RetryAhead : Step == TutorialStep.Shoot ? ShootAhead : PrespawnAhead);
        if (!sections.TryGetValue(Step, out Section placed)) return;
        TutorialStep next = NextSectionStep(Step);
        if (next == TutorialStep.Done || next == TutorialStep.Shoot || sections.ContainsKey(next)) return;
        float minimumAhead = next == TutorialStep.ShootTwice ? ShootAhead : PrespawnAhead;
        SpawnSection(next, Mathf.Max(minimumAhead, placed.End + GapAfter(Step) - CarDistance));
    }

    /// <summary>Places a lesson's objects <paramref name="ahead"/> metres up the road (retried until the road exists).</summary>
    private void SpawnSection(TutorialStep step, float ahead)
    {
        if (!road.TryGetPointAhead(player.position, ahead + SectionLength(step) + 1f, out _)) return;
        var section = new Section { Start = CarDistance + ahead, End = CarDistance + ahead + SectionLength(step) };
        float at = CarDistance + ahead;
        switch (step)
        {
            case TutorialStep.Dodge:
                // Two rows: go left for the first, then right for the second.
                AddBlocks(section, "dodge_row_1", at, obstacles.SpawnTutorialDodgeRow(ahead, -1f, out _, TrafficObstacleState.Green));
                AddBlocks(section, "dodge_row_2", at + DodgeRowGap, obstacles.SpawnTutorialDodgeRow(ahead + DodgeRowGap, 1f, out _, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.Fuel:
                // Two tanks one behind the other in the right lane (either one will do), with a block
                // behind them steering the player there.
                float second = ahead + FuelTankGap;
                AddPickup(section, "fuel_tank_1", fuelTanks.SpawnAhead(ahead, Lane(ahead, 1), expires: false));
                AddPickup(section, "fuel_tank_2", fuelTanks.SpawnAhead(second, Lane(second, 1), expires: false));
                AddBlocks(section, "fuel_guide_block", at + FuelTankGap + GuideBlockGap,
                    obstacles.SpawnTutorialDodgeRow(second + GuideBlockGap, 1f, out _, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.Ammo:
                // Three boxes one behind the other in the middle lane (following the road's curve), with a
                // small "2" block in the right lane at the end.
                for (int i = 0; i < AmmoBoxes; i++) AddPickup(section, "ammo_box_" + (i + 1), SpawnAmmoBox(ahead + i * 10f, 0));
                AddBlock(section, "ammo_2_block", at + 26f, obstacles.SpawnTutorialLaneBlock(ahead + 26f, 1, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.Shoot:
                AddBlock(section, "wall_1", at, obstacles.SpawnTutorialWall(ahead, TrafficObstacleState.Green));
                break;
            case TutorialStep.ShootTwice:
                AddBlock(section, "wall_2", at, obstacles.SpawnTutorialWall(ahead, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.Shield:
                // Shield box in the middle lane, then a full-width wall to ram with it.
                AddPickup(section, "shield_box", powerups.SpawnTutorialMystery(ahead, 0f, PickupEffectType.Shield)?.gameObject);
                AddBlock(section, "shield_wall", at + ShieldWallGap, obstacles.SpawnTutorialWall(ahead + ShieldWallGap, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.MysteryDizzy:
                // A narrow block on the far left edge nudges the player over; the reverse-steering box sits
                // in the right lane level with the second obstacle (a row leaving only that lane open).
                float box = ahead + GuideBlockGap;
                AddBlock(section, "dizzy_edge_block", at, obstacles.SpawnTutorialEdgeBlock(ahead, -1, TrafficObstacleState.Green));
                AddPickup(section, "dizzy_box", powerups.SpawnTutorialMystery(box, Lane(box, 1), PickupEffectType.ReverseSteering)?.gameObject);
                AddBlocks(section, "dizzy_guide_block", at + GuideBlockGap, obstacles.SpawnTutorialDodgeRow(box, 1f, out _, TrafficObstacleState.Yellow));
                break;
            case TutorialStep.Nitro:
                // With controls reversed: narrow blocks hugging both edges, the nitro box in the wide middle gap.
                AddPickup(section, "nitro_box", powerups.SpawnTutorialNitro(ahead, 0f)?.gameObject);
                AddBlock(section, "nitro_left_edge", at, obstacles.SpawnTutorialEdgeBlock(ahead, -1, TrafficObstacleState.Green));
                AddBlock(section, "nitro_right_edge", at, obstacles.SpawnTutorialEdgeBlock(ahead, 1, TrafficObstacleState.Yellow));
                break;
        }
        sections[step] = section;
    }

    private static void AddBlock(Section section, string name, float at, TrafficLightObstacle block)
    {
        if (block == null) return;
        section.Blocks.Add(block);
        section.BlockNames.Add(name);
        section.BlockAt.Add(at);
        section.BlockDone.Add(false);
    }

    private static void AddBlocks(Section section, string name, float at, List<TrafficLightObstacle> blocks)
    {
        foreach (TrafficLightObstacle block in blocks) AddBlock(section, name, at, block);
    }

    private static void AddPickup(Section section, string name, GameObject pickup)
    {
        if (pickup == null) return;
        section.Pickups.Add(pickup);
        section.PickupNames.Add(name);
        section.PickupDone.Add(false);
    }

    /// <summary>
    /// Logs how the player got past each object of the current lesson: a block dodged, shot or rammed
    /// with the shield, a pickup collected or missed. A crash is logged as a failure instead.
    /// </summary>
    private void TrackSectionObjects()
    {
        if (!sections.TryGetValue(Step, out Section section)) return;
        for (int i = 0; i < section.Blocks.Count; i++)
        {
            if (section.BlockDone[i]) continue;
            TrafficLightObstacle block = section.Blocks[i];
            string result = null;
            if (block != null && block.WasHit) result = CarDistance > section.BlockAt[i] + 1f ? "shielded" : null;
            else if (block == null || !obstacles.IsActive(block)) result = "shot";
            else if (CarDistance > section.BlockAt[i] + 1f) result = "dodged";
            if (result == null) continue;
            section.BlockDone[i] = true;
            RecordObject(section.BlockNames[i], result);
        }
        for (int i = 0; i < section.Pickups.Count; i++)
        {
            if (section.PickupDone[i]) continue;
            GameObject pickup = section.Pickups[i];
            string result = pickup == null || !pickup.activeInHierarchy ? "collected"
                : IsPassed(pickup, 3f) ? "missed" : null;
            if (result == null) continue;
            section.PickupDone[i] = true;
            RecordObject(section.PickupNames[i], result);
        }
    }

    private void RecordObject(string what, string result)
    {
        // Several blocks can share a name (a row): log the row once.
        if (attemptProgress.Contains(what + "=")) return;
        attemptProgress += (attemptProgress.Length > 0 ? "," : "") + what + "=" + result;
        if (passLog.Length < 600) passLog += (passLog.Length > 0 ? ";" : "") + Step + ":" + what + "=" + result;
        StepStats current = Stats;
        WeeWooAnalytics.Track("tutorial_object", "step", Step.ToString(), "what", what, "result", result,
            "attempt", current.Retries + 1, "step_s", RunTracker.Round(current.Seconds),
            "ammo", shooting.CurrentAmmo, "reversed", effects.ActiveEffect == PickupEffectType.ReverseSteering);
    }

    private GameObject SpawnAmmoBox(float ahead, int lane)
    {
        return SpawnAmmoBoxAt(ahead, Lane(ahead, lane));
    }

    private GameObject SpawnAmmoBoxAt(float ahead, float lateral)
    {
        if (!road.TryGetPointAhead(player.position, ahead, out RoadPathPoint point)) return null;
        GameObject box = OutlawAmmoPickup.Create(
            point.Position + point.Right * lateral + point.Up * 0.32f, point.Up, shooting);
        box.transform.SetParent(transform, true);
        return box;
    }

    /// <summary>Lateral offset of the left (-1), middle (0) or right (+1) lane at a distance ahead.</summary>
    private float Lane(float ahead, int lane)
    {
        if (!road.TryGetPointAhead(player.position, ahead, out RoadPathPoint point)) return 0f;
        float limit = Mathf.Max(0f, point.Width * 0.5f - 1.2f);
        return Mathf.Clamp(lane * point.Width / 3f, -limit, limit);
    }

    private void ClearSections()
    {
        if (spareAmmo != null) Destroy(spareAmmo);
        spareAmmo = null;
        foreach (Section section in sections.Values)
            foreach (GameObject pickup in section.Pickups)
                if (pickup != null) Destroy(pickup);
        sections.Clear();
        obstacles.ClearNear(player.position, 300f);
    }

    private float PlayerLateral()
    {
        return road.TryGetPathPoint(player.position, out RoadPathPoint current)
            ? Vector3.Dot(player.position - current.Position, current.Right)
            : 0f;
    }

    private bool IsPassed(GameObject item, float margin = 2f)
    {
        return item == null || road.IsBehind(item.transform.position, player.position, margin);
    }

    private bool AllPickupsPassed(TutorialStep step)
    {
        return sections.TryGetValue(step, out Section section) && section.Pickups.TrueForAll(item => IsPassed(item, 3f));
    }

    // ---------------------------------------------------------------- police catch

    /// <summary>A crash: <paramref name="obstacle"/> names the block that was hit.</summary>
    private void StartCatch(string obstacle)
    {
        Catching = true;
        catchRemaining = CatchSeconds;
        RecordFail("hit", "hit_block", obstacle);
    }

    /// <summary>
    /// Logs a failed attempt: kind "hit" (crash, police catch) or "miss" (pickup not collected), what it
    /// was, and the situation at that moment. Also kept in the run's fail log for the tutorials sheet.
    /// </summary>
    private void RecordFail(string kind, string reason, string what)
    {
        StepStats current = Stats;
        current.Retries++;
        totalRetries++;
        if (kind == "hit") { current.Hits++; totalHits++; }
        else { current.Misses++; totalMisses++; }
        if (failLog.Length < 400) failLog += (failLog.Length > 0 ? ";" : "") + Step + ":" + what;
        // "attempt" is the try that just failed; "progress" is what it got past first.
        WeeWooAnalytics.Track("tutorial_retry", "step", Step.ToString(), "reason", reason, "kind", kind, "what", what,
            "attempt", current.Retries, "next_attempt", current.Retries + 1, "progress", attemptProgress,
            "step_s", RunTracker.Round(current.Seconds),
            "tutorial_s", RunTracker.Round(SecondsInTutorial), "ammo", shooting.CurrentAmmo,
            "fuel_pct", Mathf.RoundToInt(fuel.NormalizedFuel * 100f),
            "reversed", effects.ActiveEffect == PickupEffectType.ReverseSteering, "shield", effects.ShieldReady);
    }

    /// <summary>The tutorial block closest to the car (the one it just crashed into).</summary>
    private string NearestBlockName()
    {
        string nearest = "unknown";
        float best = 15f * 15f;
        foreach (Section section in sections.Values)
            for (int i = 0; i < section.Blocks.Count; i++)
            {
                TrafficLightObstacle block = section.Blocks[i];
                if (block == null || !block.gameObject.activeInHierarchy) continue;
                float distance = (block.transform.position - player.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = section.BlockNames[i];
            }
        return nearest;
    }

    /// <summary>Lays the failed lesson out again far ahead; the catch-up speed brings it back quickly.</summary>
    private void EndCatch()
    {
        Catching = false;
        // Back to full speed straight away rather than slowly accelerating from a stop.
        SpeedMultiplier = 1f;
        ClearSections();
        effects.Clear();
        effects.RunState.BeginTutorialPursuit();
        EnterStep(RetryStep(), restart: true);
    }

    /// <summary>Failing the reversed-steering stretch starts again from the dizzy box.</summary>
    private TutorialStep RetryStep()
    {
        return Step == TutorialStep.Nitro ? TutorialStep.MysteryDizzy : Step;
    }

    /// <summary>A missed pickup is not punished: flash "TRY AGAIN!" and lay the lesson out again ahead.</summary>
    private void RelayLesson(string reason, string what)
    {
        Feedback = "TRY AGAIN!";
        FeedbackIsGood = false;
        FeedbackRemaining = 1.2f;
        RecordFail("miss", reason, what);
        ClearSections();
        TutorialStep retry = RetryStep();
        if (retry != Step) effects.Clear();
        EnterStep(retry, restart: true);
    }

    // ---------------------------------------------------------------- steps

    private void UpdateIntro(float dt)
    {
        waitRemaining -= dt;
        if (waitRemaining <= 0f) Succeed(0f);
    }

    private void UpdateSteer(float dt)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) leftHeld += dt;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) rightHeld += dt;
        LeftDone = leftHeld >= SteerHoldSeconds;
        RightDone = rightHeld >= SteerHoldSeconds;
        if (LeftDone && RightDone) Succeed(0.2f);
    }

    private void UpdateDodge()
    {
        // Measured by road distance (blocks are pooled and reused). Right after the second row the fuel
        // drops into the red (bar and screen border) and "FUEL LOW" appears at the same moment.
        if (sections.TryGetValue(TutorialStep.Dodge, out Section section) && section.End - CarDistance < -1f)
            Succeed(0f);
    }

    private void UpdateFuel()
    {
        if (!sections.TryGetValue(TutorialStep.Fuel, out Section section) || section.Pickups.Count == 0) return;
        // Each tank fills half the bar, so both are worth grabbing.
        if (tanksCollected > fuelTanksApplied)
        {
            fuelTanksApplied = tanksCollected;
            fuel.SetFuel(fuel.MaximumFuel * Mathf.Min(1f, 0.5f * fuelTanksApplied));
            fuel.DrainMode = FuelDrainMode.TutorialSteady;
        }
        if (!section.Pickups.TrueForAll(tank => IsPassed(tank, 3f))) return;
        // Past both tanks: one is enough, but the lesson only ends past the block behind them,
        // so crashing into that block retries this lesson rather than the next one.
        if (tanksCollected == 0) RelayLesson("missed_fuel", "fuel_tanks");
        else if (section.End - CarDistance < -1f) Succeed(0.3f);
    }

    private void UpdateAmmo()
    {
        if (!sections.TryGetValue(TutorialStep.Ammo, out Section section)) return;
        bool boxesPassed = section.Pickups.TrueForAll(box => IsPassed(box));
        // Collecting all three boxes is what counts (shooting some rounds meanwhile is fine).
        // Finish once past the "2" block too.
        bool blocksPassed = section.End - CarDistance < -1f;
        if (ammoBoxesCollected >= AmmoBoxes && blocksPassed) Succeed(0.2f);
        else if (boxesPassed && ammoBoxesCollected < AmmoBoxes) RelayLesson("missed_ammo", "ammo_boxes_" + ammoBoxesCollected + "_of_" + AmmoBoxes);
    }

    private void UpdateShoot()
    {
        if (!sections.TryGetValue(Step, out Section section)) return;
        if (section.Blocks.Count == 0)
        {
            Succeed(0.2f);
            return;
        }
        TrafficLightObstacle wall = section.Blocks[0];
        // Destroyed by shots (driving into it starts a police catch instead).
        if (!obstacles.IsActive(wall) && !wall.WasHit)
        {
            Succeed(0.2f);
            return;
        }
        // Shoot as much as you like: once the squares hit zero, an ammo box appears right in front of
        // the car, so the player is only out of rounds for a split second. It waits a tenth of a second
        // so the last bullet is already well past the spot and never flies through the box.
        bool spareWaiting = spareAmmo != null && !IsPassed(spareAmmo, 0.5f);
        bool lastBulletClear = Time.time - lastShotAt > SpareAmmoDelay;
        if (shooting.CurrentAmmo == 0 && !spareWaiting && lastBulletClear)
        {
            spareAmmo = SpawnAmmoBoxAt(SpareAmmoAhead, PlayerLateral());
            if (spareAmmo != null) Stats.SpareAmmo++;
        }
    }

    private void UpdateShield()
    {
        if (!sections.TryGetValue(TutorialStep.Shield, out Section section)) return;
        if (!shieldCollected)
        {
            if (effects.ShieldReady) shieldCollected = true;
            else if (AllPickupsPassed(TutorialStep.Shield)) RelayLesson("missed_box", "shield_box");
            return;
        }
        // Once past the wall (normally rammed with the shield), move on.
        if (section.End - CarDistance < -1f) Succeed(shieldDemonstrated ? HitBlockedSeconds : 0f);
    }

    private void UpdateDizzy()
    {
        if (!dizzyCollected)
        {
            if (effects.ReverseCountdownRemaining > 0f || effects.ActiveEffect == PickupEffectType.ReverseSteering)
                dizzyCollected = true;
            else if (AllPickupsPassed(TutorialStep.MysteryDizzy))
                RelayLesson("missed_box", "dizzy_box");
            return;
        }
        // Controls are reversed straight away for the nitro lesson.
        Succeed(0f);
    }

    private void UpdateNitro()
    {
        if (effects.RunState.HasNitro)
        {
            // Controls back to normal for the real run.
            effects.Clear();
            Succeed(NitroMessageSeconds + CompleteMessageSeconds);
            return;
        }
        // Keep the controls reversed until the nitro box (also after a retry).
        if (effects.ReverseCountdownRemaining <= 0f
            && (effects.ActiveEffect != PickupEffectType.ReverseSteering || effects.RemainingSeconds < 1f))
            effects.RunState.Effects.Apply(PickupEffectType.ReverseSteering, 2f);
        if (AllPickupsPassed(TutorialStep.Nitro)) RelayLesson("missed_nitro", "nitro_box");
    }

    private void Finish()
    {
        Active = false;
        Catching = false;
        SpeedMultiplier = 1f;
        ClearSections();
        float seconds = Time.time - tutorialStartedAt;
        OutlawProfile.MarkTutorialCompleted();
        var row = new Dictionary<string, object>(stepRecord)
        {
            ["status"] = "completed",
            ["seconds"] = RunTracker.Round(seconds),
            ["retries"] = totalRetries
        };
        AddTotals(row);
        WeeWooAnalytics.AddRow("tutorials", row);
        WeeWooAnalytics.Track("tutorial_complete", "seconds", RunTracker.Round(seconds), "retries", totalRetries,
            "hits", totalHits, "misses", totalMisses, "shots", totalShots);
        WeeWooAnalytics.Flush();
        OnDestroy();
        Completed?.Invoke();
    }

    private void OnPickupCollected(PickupKind kind, string outcome)
    {
        if (!Active || Catching) return;
        if (Step == TutorialStep.Fuel && kind == PickupKind.Fuel) tanksCollected++;
        if (Step == TutorialStep.Ammo && kind == PickupKind.Ammo) ammoBoxesCollected++;
    }

    private void OnObstacleCollision(TrafficObstacleState colour, bool shielded)
    {
        if (!Active || Catching) return;
        // A shield absorbs the hit: that is the lesson, not a failure.
        if (shielded)
        {
            if (Step == TutorialStep.Shield) shieldDemonstrated = true;
            return;
        }
        StartCatch(NearestBlockName());
    }

    private void OnShotFired()
    {
        if (!Active) return;
        Stats.Shots++;
        totalShots++;
        lastShotAt = Time.time;
        if (ShootPracticePending) shootPracticed = true;
    }

    /// <summary>An A box touched with all five squares full still counts as collected in the lesson.</summary>
    private void OnAmmoWastedFull()
    {
        if (Active && !Catching && Step == TutorialStep.Ammo) ammoBoxesCollected++;
    }

    private void OnDryFire()
    {
        if (!Active) return;
        Stats.DryFires++;
        totalDryFires++;
    }
}
