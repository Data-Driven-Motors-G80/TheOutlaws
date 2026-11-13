using UnityEngine;
using UnityEngine.InputSystem;

namespace Outlaws.WorldShift
{
    public enum ShiftRunState { Ready, Running, Paused, Retrying, Won, Lost }

    /// <summary>Isolated two-world prototype. Distance is the same coordinate in both worlds.</summary>
    public sealed class WorldShiftCity : MonoBehaviour, IWorldShiftView
    {
        // Every solid hazard costs half the initial 45 m gap. Capture still depends
        // on the remaining pursuit distance, never on a scripted collision count.
        private const float CollisionPursuitDrain = .5f;
        [SerializeField] private ShiftLevel environment = ShiftLevel.City;
        public ShiftLevel Environment => environment;
        public float EnvironmentLength => GetawayMode ? WorldShiftEscapeProfile.Length(environment) : WorldShiftLevel.Length;
        [SerializeField] private bool splitViewValidation;
        [SerializeField] private bool tutorialLevel;
        public bool TutorialLevel => tutorialLevel;
        public bool GetawayMode { get; private set; }
        // Level play: authored Day/Night course, one-hit crashes, big-ramp checkpoints.
        private LevelCourse course;
        private ShiftHazard[] levelHazards;
        public WorldShiftLevelCollection Collection { get; private set; }
        public bool MissedRequiredItem => LevelMode && State == ShiftRunState.Retrying && lastFailure == "MISSED ITEM";
        public LevelCourse Course => course;
        public bool LevelMode => course != null;
        public int LevelIndex { get; private set; } = -1;
        public int CheckpointIndex => checkpointIndex;
        public bool TrainingLessonPaused { get; private set; }
        private bool trainingLessonRead;
        // Debounce only (the card's fade-in): a Space press already in flight is not a decision to continue.
        private const float TrainingSignInputDelay = .35f;
        private float trainingSignShownAt = float.NegativeInfinity;
        public int TrainingFreezeStep { get; private set; }
        private bool trainingFreezeDemonstrated => TrainingFreezeStep >= 3;
        private bool InFreezeLesson => LevelMode && course.Training && checkpointIndex == 4;
        private float TrainingFreezeStop => Checkpoints[checkpointIndex].Distance + 70f;
        public bool TrainingFreezeWaiting => InFreezeLesson && !trainingFreezeDemonstrated
            && Distance >= TrainingFreezeStop - .001f;
        // Police pull up to the crashed car before the checkpoint restart.
        public bool Capturing => LevelMode && State == ShiftRunState.Retrying && lastFailure != "NO ROAD" && lastFailure != "MISSED RELAY LANDING" && lastFailure != "MISSED ITEM";
        public const float LevelPoliceGap = 6f;
        // First-timers get a little more reading time in Training.
        public const float TrainingSpeed = 20f;
        private bool switchingIn;
        private ShiftHazard crashHazard;
        public WorldShiftGetaway Getaway { get; private set; }
        [SerializeField] private RoadSegment roadTemplate;
        [SerializeField] private GameObject carPrefab;
        [SerializeField] private Camera view;
        [SerializeField] private Light sun;
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private GameObject policePrefab;
        [SerializeField] private GameObject streetlightPrefab;
        [SerializeField] private GameObject fuelPrefab;
        private WorldShiftPresentation presentation;
        private AutoDriveCar driver;
        private WorldShiftRelay relay;
        public bool IsRelayAirborne => relay != null && relay.IsAirborne;
        private ShiftRelay[] escapeRelays;
        public ShiftRelay[] RelayLayout => LevelMode ? course.Ramps : tutorialLevel ? WorldShiftTutorial.Relays : GetawayMode ? escapeRelays : WorldShiftRelay.Sections;
        public string RelayHint => relay?.Hint(Distance, World) ?? "";
        public int RelaysCompleted => relay?.Completed ?? 0;
        public bool IsBoosting => Speed > WorldShiftLevel.Speed * WorldShiftLevel.CruiseSpeedMultiplier + .1f;
        private readonly FuelState fuel = new FuelState();
        private readonly RiskRunState pursuit = new RiskRunState();
        private readonly System.Collections.Generic.HashSet<ShiftHazard> hitHazards = new System.Collections.Generic.HashSet<ShiftHazard>();
        private bool tutorialHintCompleted;
        public string TutorialControlKey => State == ShiftRunState.Retrying
            ? checkpointIndex == 5 ? "A / D" : WorldShiftTutorial.LessonKeys[checkpointIndex]
            : checkpointIndex == 3 && TutorialShotComplete ? "A / D"
            : checkpointIndex == 1 ? World == ShiftWorld.Day ? "SPACE" : "A / D"
            : checkpointIndex == 2 ? World == ShiftWorld.Echo ? "SPACE" : "A / D"
            : checkpointIndex == 5 ? IsRelayAirborne && World == ShiftWorld.Day ? "SPACE" : "A / D"
            : WorldShiftTutorial.LessonKeys[checkpointIndex];
        public int TutorialLessonIndex => checkpointIndex;
        public bool TutorialLessonComplete => checkpointIndex == 3
            ? TutorialShotComplete : tutorialHintCompleted;
        public string TutorialPrompt => !tutorialLevel ? ""
            : State == ShiftRunState.Retrying ? checkpointIndex == 2
                ? WorldShiftTutorial.NightSuppliesMessage : WorldShiftTutorial.LessonMessages[checkpointIndex]
            : checkpointIndex == 3 && TutorialShotComplete && Distance < WorldShiftTutorial.FinalLessonStart
                ? WorldShiftTutorial.LessonMessages[4]
            : TutorialLessonComplete ? ""
            : checkpointIndex == 1 ? World == ShiftWorld.Day
                ? "Enter Nightshift to escape the roadblock."
                : "The Day barrier is gone.\nSteer past the stopped traffic."
            : checkpointIndex == 2 ? World == ShiftWorld.Echo
                ? WorldShiftTutorial.NightSuppliesMessage
                : "Collect cash in Day to fund your escape."
            : checkpointIndex == 5 ? World == ShiftWorld.Echo
                ? IsRelayAirborne || RelaysCompleted > 0 ? WorldShiftTutorial.NightLandingMessage
                    : "Return to Day to reach the ramp."
                : WorldShiftTutorial.LessonMessages[5]
            : WorldShiftTutorial.LessonMessages[checkpointIndex];
        private bool TutorialWaitingForLesson
        {
            get
            {
                if (!tutorialLevel || checkpointIndex == 1 || checkpointIndex == 2) return false;
                float end = checkpointIndex + 1 < Checkpoints.Length ? Checkpoints[checkpointIndex + 1].Distance : RunLength;
                if (Distance < end - 2f) return false;
                bool completed = checkpointIndex == 3 ? TutorialShotComplete : tutorialHintCompleted;
                return !completed;
            }
        }
        private int tutorialCheckpointSwitches;
        private float tutorialCheckpointLateral;
        private bool[] fuelTaken, fuelPassed;
        private float retryRemaining, hintRemaining, fallVelocity;
        private float checkpointFuel = FuelState.MaximumFuel;
        private float checkpointGap = RiskRunState.InitialPursuitGap;
        private int checkpointIndex;
        public float CheckpointNoticeRemaining { get; private set; }
        private string lastFailure;

        public ShiftRunState State { get; private set; } = ShiftRunState.Ready;
        public ShiftWorld World { get; private set; } = ShiftWorld.Day;
        public float Distance { get; private set; }
        public float Lateral { get; private set; }
        public float Duration { get; private set; }
        public bool SplitViewValidation => splitViewValidation;
        public float RunLength => LevelMode ? course.Length : tutorialLevel ? WorldShiftTutorial.Length : splitViewValidation ? WorldShiftSplitView.Length : GetawayMode ? EnvironmentLength + WorldShiftGetaway.TailLength : WorldShiftLevel.Length;
        private ShiftHazard[] escapeHazards, tutorialHazards;
        private ShiftFuelPickup[] escapeFuel;
        public ShiftHazard[] Hazards => LevelMode ? levelHazards : tutorialLevel ? tutorialHazards : splitViewValidation ? WorldShiftSplitView.Layout : GetawayMode ? escapeHazards : WorldShiftLevel.Hazards;
        public ShiftFuelPickup[] Pickups => tutorialLevel || LevelMode ? System.Array.Empty<ShiftFuelPickup>() : splitViewValidation ? WorldShiftSplitView.Fuel : GetawayMode ? escapeFuel : WorldShiftLevel.FuelPickups;
        public ShiftCheckpoint[] Checkpoints => LevelMode ? course.Checkpoints : tutorialLevel ? WorldShiftTutorial.Checkpoints : splitViewValidation ? WorldShiftSplitView.Checkpoints : GetawayMode ? WorldShiftLevel.EscapeCheckpoints : WorldShiftLevel.Checkpoints;
        // Side-by-side worlds are retained only in the standalone rendering validation scene.
        public bool IsJoinedDistance(float at) => splitViewValidation
            && at >= WorldShiftSplitView.JoinStart && at < WorldShiftSplitView.JoinEnd;
        public bool IsWorldVisible(ShiftWorld world, float at, float lateral, ShiftWorld current)
            => IsJoinedDistance(at) ? world == WorldShiftLevel.WorldAt(lateral) : world == current;
        public float Progress => Mathf.Clamp01(Distance / RunLength);
        public int Deaths { get; private set; }
        public int Switches { get; private set; }
        public int FuelCollected { get; private set; }
        public float FuelFraction => fuel.CurrentFuel / fuel.Capacity;
        public bool IsMerged => IsJoinedDistance(Distance);
        private bool wasMerged;
        private ShiftWorld riftEntryWorld;
        public float MergeStart => LevelMode ? float.PositiveInfinity : tutorialLevel ? WorldShiftTutorial.MergeStart : splitViewValidation ? WorldShiftSplitView.JoinStart : WorldShiftLevel.MergeStart;
        // The safe entrance opens into a split over 16 metres, about half a second.
        public float JoinedViewSplit => splitViewValidation ? .5f : Mathf.Lerp(
            riftEntryWorld == ShiftWorld.Day ? 1f : 0f, .5f,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Distance - MergeStart) / 16f)));
        private bool JumpSequence => relay?.SectionAt(Distance) != null || (Getaway?.Extras.IsJumpSequence(Distance) ?? false);
        public ShiftHazard TutorialShootingObstacle => tutorialLevel ? tutorialHazards[3] : null;
        public bool IsTutorialShotTarget(ShiftHazard hazard) => tutorialLevel && (hazard == tutorialHazards[3] || hazard == tutorialHazards[6] || hazard == tutorialHazards[7]);
        public bool TutorialShotComplete => tutorialLevel && ((Combat?.IsDestroyed(tutorialHazards[3]) ?? false)
            || (Combat?.IsDestroyed(tutorialHazards[6]) ?? false) || (Combat?.IsDestroyed(tutorialHazards[7]) ?? false));
        public bool TutorialShootingActive => tutorialLevel && checkpointIndex == 3 && Distance >= 330f && Distance < WorldShiftTutorial.FinalLessonStart;
        private bool WaitingForTutorialShot => TutorialShootingActive && !TutorialShotComplete;
        private float BaseSpeed => TrainingFreezeWaiting ? 0f : tutorialLevel ? TutorialWaitingForLesson || WaitingForTutorialShot && Distance >= 410f ? 0f : JumpSequence ? relay.SpeedAt(Distance) : 18f : JumpSequence ? relay?.SpeedAt(Distance) ?? WorldShiftLevel.Speed
            : LevelMode && course.Training ? TrainingSpeed
            : WorldShiftLevel.Speed * (splitViewValidation ? 1f : WorldShiftLevel.CruiseSpeedMultiplier);
        public float Speed => BaseSpeed * (JumpSequence ? 1f : Getaway?.Extras.SpeedMultiplier ?? 1f);
        public ChaseCar Police { get; private set; }
        public RearViewCamera RearView { get; private set; }
        public float PursuitGap => pursuit.PursuitGap;
        public float PursuitFraction => Mathf.Clamp01(PursuitGap / RiskRunState.InitialPursuitGap);
        public float HitWarningRemaining => pursuit.CollisionProtectionRemaining;
        public int Hits { get; private set; }
        public bool IsFuelCollected(int index) => fuelTaken[index];
        public string Message { get; private set; } = "";
        public float MessageRemaining { get; private set; }
        public string SectionTitle => Checkpoints[checkpointIndex].Title;
        public string SectionHint => TutorialShootingActive ? WaitingForTutorialShot
            ? "F / click: shoot the block."
            : Duration - Combat.LastShotAt < 1f ? "Nice shot!" : ""
            : tutorialLevel ? tutorialHintCompleted ? "" : Checkpoints[checkpointIndex].Hint
            : relay != null && relay.Hint(Distance, World).Length > 0 ? relay.Hint(Distance, World)
            : hintRemaining > 0f ? Checkpoints[checkpointIndex].Hint : "";
        public WorldShiftCombat Combat { get; private set; }
        public WorldShiftTutorialRewards TutorialRewards { get; private set; }
        public WorldShiftRoute Route { get; private set; }
        public Transform Player { get; private set; }

        public Vector3 LevelRockPosition(WorldShiftLevelRock rock)
        {
            RoadPathPoint point = rock.Airborne ? relay.SampleFlight(rock.Flight, rock.Distance) : Route.Sample(rock.Distance);
            return point.Position + point.Right * rock.Lateral + (rock.Airborne ? Vector3.up * .63f : point.Up * rock.Radius);
        }

        public Vector3 LevelGatePosition(WorldShiftSlidingGate gate)
        {
            RoadPathPoint point = gate.Airborne ? relay.SampleFlight(gate.Flight, gate.Distance) : Route.Sample(gate.Distance);
            return point.Position + point.Right * gate.Lateral
                + (gate.Airborne ? Vector3.up * .63f : point.Up * (gate.Size.y * .5f));
        }

        private void Awake()
        {
            if (WorldShiftLevels.Active && !splitViewValidation)
            {
                // Every level shares this scene; the course decides the environment.
                LevelIndex = WorldShiftLevels.Current;
                course = WorldShiftLevelCourse.For(LevelIndex);
                environment = course.Environment;
                tutorialLevel = false;
                levelHazards = course.CreateHazards();
                Collection = new WorldShiftLevelCollection(course.RequiredItems);
            }
            GetawayMode = !LevelMode && !tutorialLevel && !splitViewValidation && WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Running;
            if (GetawayMode) { fuel.SetCapacity(WorldShiftFlow.Garage.FuelCapacity); fuel.RefillToFull(); }
            if (GetawayMode)
            {
                escapeHazards = WorldShiftEscapeProfile.Hazards(environment, WorldShiftFlow.ObstacleMotionMultiplier);
                escapeFuel = WorldShiftEscapeProfile.Fuel(environment);
                escapeRelays = WorldShiftEscapeProfile.Relays(environment);
            }
            if (tutorialLevel) tutorialHazards = WorldShiftTutorial.CreateHazards();
            if (roadTemplate == null || carPrefab == null || view == null || sun == null)
            { Debug.LogError("WorldShiftCity: missing scene references.", this); enabled = false; return; }
            var roadObject = new GameObject("Shared finite city road");
            roadObject.transform.SetParent(transform, false);
            Route = roadObject.AddComponent<WorldShiftRoute>();
            fuelTaken = new bool[Pickups.Length];
            fuelPassed = new bool[Pickups.Length];
            Route.Environment = environment;
            Route.SharedWorldRules = GetawayMode || tutorialLevel || LevelMode;
            Route.Course = course;
            Route.Build(roadTemplate, splitViewValidation || tutorialLevel, RunLength, relayLayout: splitViewValidation ? null : RelayLayout, extraLength: GetawayMode ? WorldShiftGetaway.TailLength : 0f);
            if (!splitViewValidation) relay = new WorldShiftRelay(Route, RelayLayout, tutorialLevel ? 18f : LevelMode && course.Training ? TrainingSpeed : WorldShiftLevel.Speed);
            Player = new GameObject("Player - same position in both worlds").transform;
            Player.SetParent(transform, false);
            var visual = new GameObject("Original driving body").transform;
            visual.SetParent(Player, false);
            WorldShiftBlockVisuals.Vehicle(visual, false);
            driver = Player.gameObject.AddComponent<AutoDriveCar>();
            driver.EnvironmentSteeringGrip = GetawayMode || LevelMode ? WorldShiftEscapeProfile.SteeringGrip(environment) : 1f;
            driver.ConfigureExternalRoute(d =>
            {
                var ground = relay != null ? relay.SampleDriver(d) : Route.Sample(d);
                ground = Route.SampleDriver(d, ground, Lateral);
                return Getaway?.Extras != null ? Getaway.Extras.Sample(d, ground) : ground;
            }, visual, WorldShiftLevel.Speed);
            presentation = gameObject.AddComponent<WorldShiftPresentation>();
            presentation.Build(this, Route, visual, view, sun);
            ApplyWorld(ShiftWorld.Day);
            Police = ChaseCar.CreateForFiniteRoute(policePrefab, transform, Player, d => Route.SampleDriver(d, relay != null ? relay.SamplePolice(d) : Route.Sample(d), Lateral));
            UpdatePolice(0f);
            if (!GetawayMode && !tutorialLevel && !LevelMode)
            {
                RearView = RearViewCamera.Create(Player, new[] { Police });
                RearView.transform.SetParent(transform, true);
            }
            UpdateCamera(true);
            if (!splitViewValidation && !LevelMode) { Combat = gameObject.AddComponent<WorldShiftCombat>(); Combat.Bind(this); }
            if (tutorialLevel)
            {
                TutorialRewards = gameObject.AddComponent<WorldShiftTutorialRewards>(); TutorialRewards.Bind(this);
                WorldShiftAnalytics.BeginTutorial(Checkpoints.Length, () => Distance, () => Duration, () => World);
            }
            if (LevelMode) gameObject.AddComponent<WorldShiftLevelHud>().Bind(this);
            else if (GetawayMode) { Getaway = gameObject.AddComponent<WorldShiftGetaway>(); Getaway.Bind(this); }
            else gameObject.AddComponent<WorldShiftHud>().Bind(this);
            if (splitViewValidation) gameObject.AddComponent<WorldShiftSplitView>().Initialize(this, view);
        }

        private void Start()
        {
            if (LevelMode)
            {
                WorldShiftLevelAnalytics.BeginAttempt(LevelIndex, Checkpoints.Length, () => Distance, () => Duration, () => World);
                BeginRun();
                return;
            }
            if (WorldShiftFlow.ConsumeAutoStart(tutorialLevel ? ShiftLevel.Tutorial : environment)) BeginRun();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            bool switchPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (State == ShiftRunState.Running && TrainingLessonPaused)
                {
                    // Space continues (no extra key to learn). A press already in flight as the card
                    // appears is ignored, and the continuing press never also switches worlds.
                    bool continuePressed = switchPressed || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                    if (continuePressed && Time.realtimeSinceStartup - trainingSignShownAt >= TrainingSignInputDelay) ContinueTrainingLesson();
                    else if (keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
                    switchPressed = false;
                }
                else if (State == ShiftRunState.Ready && keyboard.enterKey.wasPressedThisFrame) BeginRun();
                else if (!GetawayMode && !LevelMode && State == ShiftRunState.Won && keyboard.enterKey.wasPressedThisFrame) WorldShiftFlow.Next(tutorialLevel ? ShiftLevel.Tutorial : environment);
                else if (!GetawayMode && !LevelMode && State == ShiftRunState.Won && keyboard.rKey.wasPressedThisFrame) RestartRun();
                else if ((State == ShiftRunState.Running || State == ShiftRunState.Paused) && keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
            }
            float steer = keyboard == null ? 0f :
                ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f)
                - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);
            StepSimulation(Time.deltaTime, steer, switchPressed);
            if ((keyboard != null && keyboard.fKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))) Combat?.TryShoot();
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            UpdateCamera(false);
        }

        public void BeginRun()
        {
            if (State != ShiftRunState.Ready) return;
            State = ShiftRunState.Running;
            TrainingLessonPaused = false;
            trainingLessonRead = false;
            hintRemaining = 11f;
        }

        public void ContinueTrainingLesson()
        {
            if (State == ShiftRunState.Running && TrainingLessonPaused)
            {
                TrainingLessonPaused = false;
                trainingLessonRead = true;
                if (LevelMode) WorldShiftLevelAnalytics.SignRead();
            }
        }

        public void RestartRun()
        {
            if (GetawayMode) { WorldShiftFlow.StartGetaway(false); return; }
            checkpointIndex = 0;
            Deaths = Switches = 0;
            Hits = 0;
            FuelCollected = 0;
            System.Array.Clear(fuelTaken, 0, fuelTaken.Length);
            checkpointFuel = FuelState.MaximumFuel;
            checkpointGap = RiskRunState.InitialPursuitGap;
            Duration = 0f;
            RestoreCheckpoint();
        }

        public void TogglePause()
        {
            if (State == ShiftRunState.Running) { if (LevelMode) WorldShiftLevelAnalytics.Paused(); else WorldShiftAnalytics.Paused(); }
            if (State == ShiftRunState.Running) State = ShiftRunState.Paused;
            else if (State == ShiftRunState.Paused)
            {
                State = ShiftRunState.Running;
                if (LevelMode) WorldShiftLevelAnalytics.Resumed();
            }
        }

        public void RetryCheckpoint()
        {
            if (GetawayMode || State == ShiftRunState.Ready || State == ShiftRunState.Won || State == ShiftRunState.Lost) return;
            // A restart during the crash animation is not a separate try.
            if (LevelMode && State != ShiftRunState.Retrying) WorldShiftLevelAnalytics.CheckpointRetried();
            RestoreCheckpoint();
        }

        public bool TrySwitch()
        {
            if (State != ShiftRunState.Running || TrainingLessonPaused) return false;
            // The retired escape/tutorial retain their one-world landing protection.
            // Current levels share a recovery deck after a valid landing and never swallow Space there.
            if ((GetawayMode || tutorialLevel) && (relay?.HoldingLanding(Distance, World) ?? false)) return false;
            if (IsMerged)
            {
                Notify("A / D  CROSS WORLDS", 1.5f);
                return false;
            }
            ShiftWorld next = World == ShiftWorld.Day ? ShiftWorld.Echo : ShiftWorld.Day;
            ApplyWorld(next);
            Getaway?.OnWorldChanged();
            Switches++;
            if (LevelMode) WorldShiftLevelAnalytics.Switched(next, IsRelayAirborne);
            else WorldShiftAnalytics.Switched(next, IsRelayAirborne || (Getaway?.Extras.Jump.Traversing ?? false));
            CompleteTutorialHint();
            // A switch always commits. Occupied destinations use the same impact
            // path as driving into a hazard; missing road still causes a fall.
            if (!IsRelayAirborne)
            {
                if (!Route.HasSurface(Distance, next)) Fail("NO ROAD");
                else
                {
                    // Switching into the other world's obstacle (its dashed outline) is a crash.
                    switchingIn = true;
                    ApplyImpact(FindCollision(next, Distance, Distance, Lateral, Lateral, true), Lateral);
                    switchingIn = false;
                }
            }
            else if (LevelMode && environment == ShiftLevel.Desert)
                ApplyImpact(FindCollision(next, Distance, Distance, Lateral, Lateral, true), Lateral);
            // Demonstrate both directions before driving on: Night stops motion,
            // Day restores it, then Night freezes the practice route again.
            if (InFreezeLesson && trainingLessonRead && State == ShiftRunState.Running && TrainingFreezeStep < 3
                && next == (TrainingFreezeStep == 1 ? ShiftWorld.Day : ShiftWorld.Echo))
            {
                TrainingFreezeStep++;
                if (TrainingFreezeStep >= 3) WorldShiftLevelAnalytics.FreezeDemonstrated();
            }
            if (IsRelayAirborne) Notify(relay.Hint(Distance, World), 1.8f);
            return true;
        }

        public void StepSimulation(float delta, float steer, bool switchPressed)
        {
            if (TrainingLessonPaused || State == ShiftRunState.Paused || State == ShiftRunState.Ready || State == ShiftRunState.Won || State == ShiftRunState.Lost) return;
            delta = Mathf.Max(0f, delta);
            if (State == ShiftRunState.Retrying)
            {
                presentation.StepPedestrians(delta);
                retryRemaining -= delta;
                if (Capturing) UpdatePolice(delta);
                if (lastFailure == "NO ROAD" || lastFailure == "MISSED RELAY LANDING")
                {
                    fallVelocity -= 28f * delta;
                    Player.position += (Player.forward * WorldShiftLevel.Speed * 0.25f + Vector3.up * fallVelocity) * delta;
                    Player.Rotate(35f * delta, 0f, 0f, Space.Self);
                }
                if (retryRemaining <= 0f) RestoreCheckpoint();
                return;
            }
            MessageRemaining = Mathf.Max(0f, MessageRemaining - delta);
            CheckpointNoticeRemaining = Mathf.Max(0f, CheckpointNoticeRemaining - delta);
            hintRemaining = Mathf.Max(0f, hintRemaining - delta);
            if (switchPressed) TrySwitch();
            float simulatedTime = 0f;
            // Small substeps make held steering and collision checks stable even after a slow frame.
            while (delta > 0.00001f && State == ShiftRunState.Running && !TrainingLessonPaused)
            {
                float dt = Mathf.Min(delta, 1f / 60f);
                delta -= dt;
                float previous = Distance, previousLateral = Lateral, previousHeight = Player.position.y;
                // Lesson waits are safe reading time; traffic must not approach a stopped learner.
                if (!TutorialWaitingForLesson)
                    foreach (var hazard in Hazards)
                    {
                        if (tutorialLevel && hazard.MotionSpeed > 0f &&
                            hazard != tutorialHazards[checkpointIndex == 2 ? 2 : 1]) continue;
                        if (tutorialLevel && checkpointIndex == 0) continue;
                        hazard.Advance(dt, Distance, World);
                    }
                // Keep the demonstration alive while the car waits safely. This is
                // not driving time, and neither the car nor police advances.
                if (TrainingFreezeWaiting) { WorldShiftLevelAnalytics.FreezeDemoWaiting(); continue; }
                float speed = Speed;
                if (InFreezeLesson && !trainingFreezeDemonstrated)
                {
                    float drivingDelta = Mathf.Min(dt, Mathf.Max(0f, TrainingFreezeStop - Distance) / speed);
                    dt = drivingDelta;
                }
                simulatedTime += dt;
                Getaway?.BeginStep(previous, previous + speed * dt, driver);
                if (relay != null)
                {
                    driver.ExternalVerticalControl = relay.SectionAt(previous) != null || (Getaway?.Extras.Jump.Traversing ?? false);
                    if (relay.BeginStep(previous, previous + speed * dt, World))
                    {
                        if (LevelMode) WorldShiftLevelAnalytics.RampStarted(relay.Flight.Number);
                        Notify("SPACE  /  SWITCH TO LAND", 2.5f);
                    }
                }
                bool snowSlide = GetawayMode && WorldShiftEscapeProfile.SnowSlide(environment, previous, RelayLayout);
                driver.EnvironmentReleaseGrip = snowSlide ? .6f : 1f;
                driver.EnvironmentCountersteerGrip = snowSlide ? 1.25f : 1f;
                float airSteering = LevelMode && environment == ShiftLevel.Desert ? .65f : .4f;
                driver.StepExternal(dt, (IsRelayAirborne ? steer * airSteering : steer) * (JumpSequence ? 1f : Getaway?.Extras.SteeringMultiplier ?? 1f), speed);
                Getaway?.FinishStep(driver.RoadPositionDistance, driver);
                float nextLateral = driver.LateralOffset;
                float next = Mathf.Min(RunLength, driver.RoadPositionDistance);
                ShiftHazard hit = IsRelayAirborne && !(LevelMode && environment == ShiftLevel.Desert) ? null
                    : FindCollision(World, previous, next, previousLateral, nextLateral, true, previousHeight, Player.position.y);
                Duration += dt;
                if (!LevelMode && (!GetawayMode || !Getaway.InRecovery))
                {
                    fuel.Tick(dt, tutorialLevel ? 0f : WorldShiftLevel.FuelConsumption);
                    pursuit.Tick(dt, speed, BaseSpeed + (tutorialLevel ? 0f : Mathf.Lerp(.03f, .14f, Progress)) + (GetawayMode ? WorldShiftFlow.Escape.ExtraPursuitSpeed : 0f));
                }
                if (relay != null)
                {
                    bool safe = relay.FinishStep(next, World, out bool landed);
                    if (landed && LevelMode) Route.CompleteLanding(relay.LastLanding);
                    if (!safe || (!IsRelayAirborne && !Route.HasSurface(next, World)))
                    {
                        Distance = next;
                        Lateral = nextLateral;
                        Fail(!safe ? "MISSED RELAY LANDING" : "NO ROAD");
                        break;
                    }
                    if (landed)
                    {
                        if (LevelMode) WorldShiftLevelAnalytics.RampEnded(true, "landed");
                        Notify("LANDED", 1.2f);
                    }
                }
                if (hit != null)
                {
                    if (hit.Kind == ShiftHazardKind.Gap)
                    {
                        Distance = next;
                        Lateral = nextLateral;
                        Fail("NO ROAD");
                        break;
                    }
                    ApplyImpact(hit, nextLateral);
                    if (State != ShiftRunState.Running) break;
                }
                nextLateral = ResolveDividers(previous, next, previousLateral, nextLateral, steer);
                Distance = next;
                Lateral = nextLateral;
                if (LevelMode && course.Training && !trainingLessonRead && checkpointIndex < course.Signs.Length
                    && Distance >= course.Signs[checkpointIndex].Distance - 5f && !TrainingLessonPaused)
                {
                    TrainingLessonPaused = true;
                    trainingSignShownAt = Time.realtimeSinceStartup;
                    WorldShiftLevelAnalytics.SignShown(checkpointIndex);
                }
                if (Collection != null && !Collection.Step(previous, next, previousLateral, nextLateral))
                { Fail("MISSED ITEM"); break; }
                CompleteTutorialHint();
                UpdateMergeState();
                presentation.StepPedestrians(dt);
                CollectFuel(previous, next, previousLateral, nextLateral);
                Getaway?.Collect(previous, next, previousLateral, nextLateral);
                Combat?.Step(previous, next, previousLateral, nextLateral);
                TutorialRewards?.Step(previous, next, previousLateral, nextLateral);
                CompleteTutorialHint();
                if (tutorialLevel && (checkpointIndex == 1 || checkpointIndex == 2) && !TutorialLessonComplete
                    && Distance >= Checkpoints[checkpointIndex + 1].Distance - 2f)
                { Fail("TRY THIS STEP AGAIN"); break; }
                if (checkpointIndex + 1 < Checkpoints.Length && Distance >= Checkpoints[checkpointIndex + 1].Distance)
                {
                    checkpointIndex++;
                    trainingLessonRead = false;
                    if (tutorialLevel) WorldShiftAnalytics.TutorialStep(checkpointIndex, Checkpoints[checkpointIndex].Title);
                    if (LevelMode)
                    {
                        WorldShiftLevelAnalytics.Checkpoint(checkpointIndex);
                        CheckpointNoticeRemaining = 2.2f;
                    }
                    tutorialHintCompleted = false;
                    tutorialCheckpointSwitches = Switches;
                    tutorialCheckpointLateral = Lateral;
                    CompleteTutorialHint();
                    // Keep retries viable even if a player reached a checkpoint nearly empty.
                    checkpointFuel = Mathf.Max(35f, fuel.CurrentFuel);
                    checkpointGap = Mathf.Max(32f, pursuit.PursuitGap);
                    hintRemaining = 10f;
                    Notify(IsMerged ? "A / D  CROSS WORLDS" : "CHECKPOINT " + (checkpointIndex + 1), IsMerged ? 3f : 1.8f);
                }
                if (pursuit.IsGameOver) Fail("CAUGHT");
                else if (fuel.IsEmpty) Fail("OUT OF FUEL");
                else if (Distance >= RunLength)
                {
                    State = ShiftRunState.Won;
                    if (tutorialLevel) WorldShiftAnalytics.EndTutorial("completed");
                    if (LevelMode) { WorldShiftLevels.Complete(LevelIndex); WorldShiftLevelAnalytics.Completed(); }
                    Getaway?.Finish(Lateral);
                }
            }
            UpdatePolice(simulatedTime);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(World);
            TutorialRewards?.Present(World);
        }

        private void UpdateMergeState()
        {
            bool merged = IsMerged;
            // Inside an overlap the two layouts coexist. Crossing the centre updates
            // the player's world identity, never their pose, speed or momentum.
            if (merged && !wasMerged) riftEntryWorld = World;
            if (merged) World = WorldShiftLevel.WorldAt(Lateral);
            if (merged != wasMerged)
            {
                wasMerged = merged;
                ApplyWorld(World);
                Notify(merged ? "A / D  CROSS WORLDS" : "SPACE  SWITCH WORLDS", 3f);
            }
        }

        internal void HitForkObstacle(float at) => ApplyImpact(new ShiftHazard(World, ShiftHazardKind.Barrier, at, at + 2f, 4), Lateral);

        private void ApplyImpact(ShiftHazard hit, float lateral)
        {
            if (hit == null) return;
            if (hit.Kind == ShiftHazardKind.Gap) { Fail("NO ROAD"); return; }
            hitHazards.Add(hit);
            if (LevelMode)
            {
                // One hit ends the attempt at this checkpoint.
                presentation.OnHazardImpact(hit, lateral);
                crashHazard = hit;
                Fail(switchingIn ? "switched_into" : "crash");
                return;
            }
            if (tutorialLevel && (checkpointIndex == 1 || checkpointIndex == 2))
            { Fail("TRY THIS STEP AGAIN"); return; }
            if (Getaway != null && Getaway.Extras.BlocksImpact(pursuit.CollisionProtectionRemaining > 0f))
            { presentation.OnHazardImpact(hit, lateral); return; }
            if (!pursuit.TryCrash(CollisionPursuitDrain * (GetawayMode ? WorldShiftFlow.Garage.CollisionMultiplier : 1f))) return;
            Hits++;
            WorldShiftAnalytics.Impact(hit.Kind.ToString());
            presentation.OnHazardImpact(hit, lateral);
            Notify("POLICE CLOSER", 1.3f);
            if (pursuit.IsGameOver) Fail("CAUGHT");
        }

        private void UpdatePolice(float deltaTime)
        {
            if (Police == null) return;
            if (LevelMode)
            {
                // The cruiser trails at a fixed distance and only closes in after a crash.
                bool capture = Capturing;
                Police.StepFinite(deltaTime, Distance, capture ? Lateral + (Lateral > 0f ? -2.6f : 2.6f) : Lateral, capture ? 0f : LevelPoliceGap);
                return;
            }
            // Like the original chase, compress logical distance into a readable mirror gap.
            Police.StepFinite(deltaTime, Distance, Lateral, Mathf.Lerp(.45f, 6f, PursuitFraction));
        }

        private float ResolveDividers(float from, float to, float previousLateral, float lateral, float steer)
        {
            float clearance = WorldShiftLevel.RoadWidth / 6f + .8f + .03f;
            foreach (var hazard in Hazards)
            {
                if (!WorldShiftCombat.IsActive(Combat, hazard, hazard.VisibleIn(World)) || hazard.Kind != ShiftHazardKind.Divider
                    || to + 1.5f < hazard.Start || from - 1.5f > hazard.End || Mathf.Abs(lateral) >= clearance) continue;
                float side = Mathf.Abs(previousLateral) > .1f ? Mathf.Sign(previousLateral)
                    : Mathf.Abs(steer) > .1f ? Mathf.Sign(steer) : 1f;
                lateral = side * clearance;
                driver.ConstrainExternalLateral(lateral);
            }
            return lateral;
        }

        private void Fail(string reason)
        {
            if (LevelMode)
            {
                WorldShiftLevelAnalytics.Died(reason == "NO ROAD" ? "fell" : reason == "MISSED RELAY LANDING" ? "missed_landing"
                    : reason == "MISSED ITEM" ? "missed_item" : reason, crashHazard, IsRelayAirborne, Lateral);
                crashHazard = null;
                Deaths++;
                State = ShiftRunState.Retrying;
                lastFailure = reason;
                fallVelocity = -1f;
                retryRemaining = Capturing ? 1.6f : 1.05f;
                return;
            }
            WorldShiftAnalytics.Died(reason);
            if (GetawayMode)
            {
                State = ShiftRunState.Lost;
                Getaway.Lose(reason == "CAUGHT" ? "CAUGHT" : reason == "OUT OF FUEL" ? "OUT OF FUEL" : reason == "MISSED RELAY LANDING" ? "MISSED LANDING" : "FELL OFF ROAD");
                return;
            }
            Deaths++;
            State = ShiftRunState.Retrying;
            lastFailure = reason;
            fallVelocity = -1f;
            retryRemaining = reason == "NO ROAD" || reason == "MISSED RELAY LANDING" ? 1.05f : 0.7f;
            Notify(reason, 1.1f);
        }

        private void CollectFuel(float from, float to, float fromLateral, float toLateral)
        {
            if (GetawayMode)
                for (int i = 0; i < fuelPassed.Length; i++)
                    if (!fuelTaken[i] && !fuelPassed[i] && from > Pickups[i].Distance + 1.3f) { fuelPassed[i] = true; WorldShiftAnalytics.Missed("fuel"); }
            if (GetawayMode && World != ShiftWorld.Day) return;
            for (int i = 0; i < fuelTaken.Length; i++)
            {
                ShiftFuelPickup pickup = Pickups[i];
                if (fuelTaken[i] || (!GetawayMode && !IsWorldVisible(pickup.World, pickup.Distance, pickup.Lane * WorldShiftLevel.RoadWidth / 3f, World)) || to + 1.3f < pickup.Distance || from - 1.3f > pickup.Distance) continue;
                float t = Mathf.InverseLerp(from, to, pickup.Distance);
                if (Mathf.Abs(Mathf.Lerp(fromLateral, toLateral, t) - pickup.Lane * WorldShiftLevel.RoadWidth / 3f) > 1.35f) continue;
                fuelTaken[i] = true;
                FuelCollected++;
                WorldShiftAnalytics.Fuel();
                fuel.Refill(WorldShiftLevel.FuelRefill);
                presentation.RefreshFuel();
                Notify("FUEL +26", 1.2f);
            }
        }

        // Swept checks in the common road coordinate prevent tunnelling or switching inside a wall.
        private ShiftHazard FindCollision(ShiftWorld world, float from, float to, float fromLateral, float toLateral, bool skipHandled = false,
            float fromHeight = float.NaN, float toHeight = float.NaN)
        {
            const float halfWidth = 0.8f;
            foreach (var hazard in Hazards)
            {
                bool merged = IsJoinedDistance(hazard.Start);
                if (!WorldShiftCombat.IsActive(Combat, hazard)) continue;
                if ((!merged && !hazard.VisibleIn(world)) || (skipHandled && hitHazards.Contains(hazard))) continue;
                if (hazard.Gate != null)
                {
                    if (hazard.Gate.Airborne != IsRelayAirborne) continue;
                    float aHeight = float.IsNaN(fromHeight) ? Player.position.y : fromHeight;
                    float bHeight = float.IsNaN(toHeight) ? Player.position.y : toHeight;
                    if (hazard.Gate.Touches(from, to, fromLateral, toLateral, aHeight, bHeight,
                        Mathf.Abs(to - from) < .00001f ? hazard.Gate.Lateral : hazard.PreviousLateralOffset,
                        LevelGatePosition(hazard.Gate).y)) return hazard;
                    continue;
                }
                if (hazard.Rock != null)
                {
                    if (hazard.Rock.Airborne != IsRelayAirborne) continue;
                    float aHeight = float.IsNaN(fromHeight) ? Player.position.y : fromHeight;
                    float bHeight = float.IsNaN(toHeight) ? Player.position.y : toHeight;
                    if (hazard.Rock.Touches(from, to, fromLateral, toLateral, aHeight, bHeight,
                        Mathf.Abs(to - from) < .00001f ? hazard.Rock.Lateral : hazard.PreviousLateralOffset,
                        LevelRockPosition(hazard.Rock).y)) return hazard;
                    continue;
                }
                if (IsRelayAirborne) continue;
                // A missing deck drops the car when its centre crosses the lip.
                float halfLength = hazard.Kind == ShiftHazardKind.Gap ? 0f : 1.5f;
                hazard.BoundsAt(from, out float start, out float end);
                hazard.BoundsAt(to, out float nextStart, out float nextEnd);
                if (hazard.Kind == ShiftHazardKind.Pedestrians)
                {
                    // The authored six-metre crossing is road paint, not a solid
                    // wall. Contact belongs to the figures standing near its centre.
                    float centre = (start + end) * .5f;
                    float nextCentre = (nextStart + nextEnd) * .5f;
                    start = centre - .65f;
                    end = centre + .65f;
                    nextStart = nextCentre - .65f;
                }
                float relativeFrom = from - start, relativeTo = to - nextStart;
                float depth = end - start;
                if (relativeTo + halfLength < 0f || relativeFrom - halfLength > depth) continue;
                float entry = relativeTo > relativeFrom ? Mathf.Clamp01((-halfLength - relativeFrom) / (relativeTo - relativeFrom)) : 0f;
                float exit = relativeTo > relativeFrom ? Mathf.Clamp01((depth + halfLength - relativeFrom) / (relativeTo - relativeFrom)) : 1f;
                float a = Mathf.Lerp(fromLateral, toLateral, entry), b = Mathf.Lerp(fromLateral, toLateral, exit);
                if (hazard.LateralMotion)
                {
                    float left = Mathf.Min(hazard.PreviousLateralOffset, hazard.LateralOffset) - .8f - halfWidth;
                    float right = Mathf.Max(hazard.PreviousLateralOffset, hazard.LateralOffset) + .8f + halfWidth;
                    if (Mathf.Max(a, b) >= left && Mathf.Min(a, b) <= right) return hazard;
                    continue;
                }
                if (hazard.Kind == ShiftHazardKind.HalfBarrier)
                {
                    float min = hazard.HalfSide < 0 ? -WorldShiftLevel.RoadWidth * .5f : 0f;
                    float max = hazard.HalfSide < 0 ? 0f : WorldShiftLevel.RoadWidth * .5f;
                    if (merged && hazard.World != WorldShiftLevel.WorldAt(hazard.HalfSide)) continue;
                    if (Mathf.Max(a, b) + halfWidth > min && Mathf.Min(a, b) - halfWidth < max) return hazard;
                    continue;
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((hazard.LaneMask & (1 << lane)) == 0) continue;
                    float centre = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                    float extent = WorldShiftLevel.RoadWidth / 6f + halfWidth;
                    if (Mathf.Max(a, b) >= centre - extent && Mathf.Min(a, b) <= centre + extent) return hazard;
                }
            }
            return null;
        }

        private void CompleteTutorialHint()
        {
            if (!tutorialLevel || tutorialHintCompleted) return;
            switch (checkpointIndex)
            {
                case 0: tutorialHintCompleted = Mathf.Abs(Lateral - Checkpoints[0].Lateral) >= .8f; break;
                case 1:
                    tutorialHazards[1].BoundsAt(Distance, out _, out float end);
                    tutorialHintCompleted = World == ShiftWorld.Echo && Distance > end + 5f;
                    break;
                case 2: tutorialHintCompleted = World == ShiftWorld.Day && TutorialRewards != null && TutorialRewards.Collected > 0; break;
                case 4: tutorialHintCompleted = Distance >= WorldShiftTutorial.LastFinalObstacle + 12f; break;
                case 5: tutorialHintCompleted = RelaysCompleted > 0 && Distance >= WorldShiftTutorial.Relays[0].End; break;
            }
        }

        private void RestoreCheckpoint()
        {
            TrainingFreezeStep = 0;
            if (LevelMode)
            {
                WorldShiftLevelAnalytics.Respawned(Checkpoints[checkpointIndex].World);
                // Every retry replays the section exactly: movers return to their start.
                foreach (var hazard in levelHazards) hazard.ResetMotion();
            }
            Combat?.ResetPractice();
            if (tutorialLevel) foreach (var hazard in Hazards) hazard.ResetMotion();
            tutorialHintCompleted = false;
            tutorialCheckpointSwitches = Switches;
            if (LevelMode) WorldShiftLevelAnalytics.RampEnded(false, "retry");
            ShiftCheckpoint checkpoint = Checkpoints[checkpointIndex];
            CheckpointNoticeRemaining = 0f;
            tutorialCheckpointLateral = checkpoint.Lateral;
            TutorialRewards?.Restore(checkpoint.Distance);
            Collection?.Restore(checkpoint.Distance);
            Distance = checkpoint.Distance;
            Lateral = checkpoint.Lateral;
            retryRemaining = 0f;
            State = ShiftRunState.Running;
            if (LevelMode && TrainingLessonPaused) WorldShiftLevelAnalytics.SignRead("restart");
            TrainingLessonPaused = false;
            trainingLessonRead = false;
            hintRemaining = 10f;
            fuel.Reset(checkpointFuel);
            pursuit.Restore(new RiskRunSnapshot(checkpointGap, false, 0, false, 0, 0, PickupEffectType.None, 0f, 0f, 0f));
            pursuit.RecoveryCap = RiskRunState.InitialPursuitGap;
            hitHazards.Clear();
            presentation.ResetPedestrians();
            FuelCollected = 0;
            for (int i = 0; i < fuelTaken.Length; i++)
            {
                if (Pickups[i].Distance >= checkpoint.Distance) fuelTaken[i] = false;
                if (fuelTaken[i]) FuelCollected++;
            }
            relay?.Reset(Distance);
            Route.RestoreLandingRecoveries(Distance);
            driver.ExternalVerticalControl = false;
            driver.RestoreExternalPose(Distance, Lateral);
            wasMerged = IsMerged;
            riftEntryWorld = checkpoint.World;
            ApplyWorld(IsMerged ? WorldShiftLevel.WorldAt(Lateral) : checkpoint.World);
            UpdatePolice(0f);
            UpdateCamera(true);
            Notify("CHECKPOINT " + (checkpointIndex + 1), 2f);
        }

        private void ApplyWorld(ShiftWorld world)
        {
            World = world;
            Route.SetWorld(world);
            presentation.SetWorld(world);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(world);
            TutorialRewards?.Present(world);
        }

        // Rendering can visit both visual states without advancing simulation or
        // changing the player's actual world, collision history or pursuit state.
        public void PresentWorldForCamera(ShiftWorld displayedWorld)
        {
            Route.SetWorld(displayedWorld);
            presentation.SetWorld(displayedWorld);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(displayedWorld);
            TutorialRewards?.Present(displayedWorld);
        }

        private void UpdateCamera(bool immediate)
        {
            RoadPathPoint point = Route.SampleDriver(Distance, Route.Sample(Distance), Lateral);
            float follow = splitViewValidation ? 0f : .28f;
            Vector3 desired = point.Position - point.Forward * 12f + Vector3.up * 6.4f + point.Right * Lateral * follow;
            bool falling = State == ShiftRunState.Retrying && (lastFailure == "NO ROAD" || lastFailure == "MISSED RELAY LANDING");
            if ((IsRelayAirborne || (Getaway?.Extras.Jump.Traversing ?? false)) && !falling) desired.y += Player.position.y - point.Position.y - .08f;
            if (falling) desired = point.Position - point.Forward * 2f + Vector3.up * 10f;
            float blend = immediate ? 1f : 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            view.transform.position = Vector3.Lerp(view.transform.position, desired, blend);
            if (IsMerged)
                view.transform.position -= point.Right * Vector3.Dot(view.transform.position - point.Position, point.Right);
            view.fieldOfView = Mathf.Lerp(70f, 76f, Mathf.InverseLerp(WorldShiftLevel.Speed * WorldShiftLevel.CruiseSpeedMultiplier, WorldShiftRelay.BoostSpeed, Speed));
            Vector3 target = Route.SampleDriver(Distance + 22f, Route.Sample(Distance + 22f), Lateral).Position + Vector3.up * 0.6f;
            if (falling) target = Player.position + point.Forward * 4f;
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, Quaternion.LookRotation(target - view.transform.position, Vector3.up), IsMerged ? 1f : blend);
        }

        private void Notify(string value, float seconds) { Message = value; MessageRemaining = seconds; }

        private void OnDestroy()
        {
            if (RearView != null) Destroy(RearView.gameObject);
        }
    }
}
