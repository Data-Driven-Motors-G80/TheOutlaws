using UnityEngine;
using UnityEngine.InputSystem;

namespace Outlaws.WorldShift
{
    public enum ShiftRunState { Ready, Running, Paused, Retrying, Won, Lost }

    /// <summary>Isolated two-world prototype. Distance is the same coordinate in both worlds.</summary>
    public sealed partial class WorldShiftCity : MonoBehaviour, IWorldShiftView
    {
        // Every solid hazard costs half the initial 45 m gap. Capture still depends
        // on the remaining pursuit distance, never on a scripted collision count.
        private const float CollisionPursuitDrain = .5f;
        [SerializeField] private ShiftLevel environment = ShiftLevel.City;
        public ShiftLevel Environment => environment;
        public float EnvironmentLength => GetawayMode ? WorldShiftEscapeProfile.Length(environment) : WorldShiftLevel.Length;
        // The retired standalone tutorial and split-view validation modes were removed; these stay false.
        public bool TutorialLevel => false;
        public bool GetawayMode { get; private set; }
        // Level play: authored Day/Night course, one-hit crashes, big-ramp checkpoints.
        private LevelCourse course;
        private ShiftHazard[] levelHazards;
        public WorldShiftLevelCollection Collection { get; private set; }
        public WorldShiftOceanHelicopter OceanBoss { get; private set; }
        public string FailureReason => State == ShiftRunState.Retrying ? lastFailure : "";
        private float RetryDuration => lastFailure == "DOLPHIN COLLISION" || lastFailure == "AIR STRIKE" ? 3f : Capturing ? 1.6f
            : GapFall ? GapFallWatchSeconds + 1.05f : 1.05f;
        /// <summary>Mountain: the car drove off through a guardrail gap. The camera watches it drop into the valley first.</summary>
        public bool GapFall { get; private set; }
        public const float GapFallWatchSeconds = 1.15f;
        /// <summary>True while the camera is still watching a gap fall, before the OFF THE ROAD card appears.</summary>
        public bool WatchingGapFall => State == ShiftRunState.Retrying && GapFall && RetryDuration - retryRemaining < GapFallWatchSeconds;
        public float CheckpointRetryProgress => Mathf.Clamp01(1f - retryRemaining / RetryDuration);
        public GameObject StreetlightPrefab => streetlightPrefab;
        public bool MissedRequiredItem => LevelMode && State == ShiftRunState.Retrying && lastFailure == "MISSED ITEM";
        public LevelCourse Course => course;
        public bool LevelMode => course != null;
        public int LevelIndex { get; private set; } = -1;
        public int CheckpointIndex => checkpointIndex;
        public bool TrainingLessonPaused { get; private set; }
        private bool trainingLessonRead;
        private readonly System.Collections.Generic.HashSet<int> completedTrainingSigns = new System.Collections.Generic.HashSet<int>();
        // Debounce the card fade-in; Enter acknowledges instructions.
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
        public ShiftRelay[] RelayLayout => LevelMode ? course.Ramps : GetawayMode ? escapeRelays : WorldShiftRelay.Sections;
        public string RelayHint => relay?.Hint(Distance, World) ?? "";
        public int RelaysCompleted => relay?.Completed ?? 0;
        public bool IsBoosting => Speed > WorldShiftLevel.Speed * WorldShiftLevel.CruiseSpeedMultiplier
            * (LevelMode ? course.GradeSpeedFactor(Distance) : 1f) + .1f;
        private readonly FuelState fuel = new FuelState();
        private readonly RiskRunState pursuit = new RiskRunState();
        private readonly System.Collections.Generic.HashSet<ShiftHazard> hitHazards = new System.Collections.Generic.HashSet<ShiftHazard>();
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
        public bool SplitViewValidation => false;
        public float RunLength => LevelMode ? course.Length : GetawayMode ? EnvironmentLength + WorldShiftGetaway.TailLength : WorldShiftLevel.Length;
        private ShiftHazard[] escapeHazards;
        private ShiftFuelPickup[] escapeFuel;
        public ShiftHazard[] Hazards => LevelMode ? levelHazards : GetawayMode ? escapeHazards : WorldShiftLevel.Hazards;
        public ShiftFuelPickup[] Pickups => LevelMode ? System.Array.Empty<ShiftFuelPickup>() : GetawayMode ? escapeFuel : WorldShiftLevel.FuelPickups;
        public ShiftCheckpoint[] Checkpoints => LevelMode ? course.Checkpoints : GetawayMode ? WorldShiftLevel.EscapeCheckpoints : WorldShiftLevel.Checkpoints;
        // Side-by-side worlds only existed in the removed split-view validation; roads never join.
        public bool IsJoinedDistance(float at) => false;
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
        public float MergeStart => LevelMode ? float.PositiveInfinity : WorldShiftLevel.MergeStart;
        // The safe entrance opens into a split over 16 metres, about half a second.
        public float JoinedViewSplit => Mathf.Lerp(
            riftEntryWorld == ShiftWorld.Day ? 1f : 0f, .5f,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Distance - MergeStart) / 16f)));
        private bool JumpSequence => relay?.SectionAt(Distance) != null || (Getaway?.Extras.IsJumpSequence(Distance) ?? false);
        private float BaseSpeed => TrainingFreezeWaiting ? 0f : JumpSequence ? relay?.SpeedAt(Distance) ?? WorldShiftLevel.Speed
            : LevelMode && course.Training ? TrainingSpeed
            : WorldShiftLevel.Speed * WorldShiftLevel.CruiseSpeedMultiplier * (LevelMode ? course.GradeSpeedFactor(Distance) : 1f);
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
        public string SectionHint => relay != null && relay.Hint(Distance, World).Length > 0 ? relay.Hint(Distance, World)
            : hintRemaining > 0f ? Checkpoints[checkpointIndex].Hint : "";
        public WorldShiftCombat Combat { get; private set; }
        public WorldShiftRoute Route { get; private set; }
        public Transform Player { get; private set; }
        public WorldShiftLevelEscape Escape { get; private set; }
        public bool StoryActive { get; private set; }
        internal WorldShiftImpactMoments Impact { get; private set; }
        private int storyFinishedFrame = -1;

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
            if (WorldShiftLevels.Active)
            {
                // Every level shares this scene; the course decides the environment.
                LevelIndex = WorldShiftLevels.Current;
                StoryActive = WorldShiftLevels.StartWithStory;
                course = WorldShiftLevelCourse.For(LevelIndex);
                environment = course.Environment;
                levelHazards = course.CreateHazards();
                foreach (var hazard in levelHazards)
                {
                    float at = hazard.Gate != null ? hazard.Gate.Airborne ? hazard.Gate.Flight.Ramp : hazard.Gate.Distance
                        : hazard.Rock != null && hazard.Rock.Airborne ? hazard.Rock.Flight.Ramp : hazard.Start;
                    foreach (var checkpoint in course.Checkpoints)
                        if (checkpoint.Distance <= at) hazard.WakeFloor = Mathf.Max(hazard.WakeFloor, checkpoint.Distance);
                }
                Collection = new WorldShiftLevelCollection(course.RequiredItems, environment == ShiftLevel.Ocean);
            }
            GetawayMode = !LevelMode && WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Running;
            if (GetawayMode) { fuel.SetCapacity(WorldShiftFlow.Garage.FuelCapacity); fuel.RefillToFull(); }
            if (GetawayMode)
            {
                escapeHazards = WorldShiftEscapeProfile.Hazards(environment, WorldShiftFlow.ObstacleMotionMultiplier);
                escapeFuel = WorldShiftEscapeProfile.Fuel(environment);
                escapeRelays = WorldShiftEscapeProfile.Relays(environment);
            }
            if (roadTemplate == null || carPrefab == null || view == null || sun == null)
            { Debug.LogError("WorldShiftCity: missing scene references.", this); enabled = false; return; }
            var roadObject = new GameObject("Shared finite city road");
            roadObject.transform.SetParent(transform, false);
            Route = roadObject.AddComponent<WorldShiftRoute>();
            fuelTaken = new bool[Pickups.Length];
            fuelPassed = new bool[Pickups.Length];
            Route.Environment = environment;
            Route.SharedWorldRules = GetawayMode || LevelMode;
            Route.Course = course;
            Route.Build(roadTemplate, false, RunLength, relayLayout: RelayLayout, extraLength: GetawayMode ? WorldShiftGetaway.TailLength : 0f);
            relay = new WorldShiftRelay(Route, RelayLayout, LevelMode && course.Training ? TrainingSpeed : WorldShiftLevel.Speed);
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
            if (LevelMode && course.IceZones.Length > 0) WorldShiftIcePatches.Create(this, Route, course);
            ApplyWorld(ShiftWorld.Day);
            Police = ChaseCar.CreateForFiniteRoute(policePrefab, transform, Player, d => Route.SampleDriver(d, relay != null ? relay.SamplePolice(d) : Route.Sample(d), Lateral));
            if (LevelMode)
            {
                var policeArt=Police.GetComponentInChildren<WorldShiftPoliceCarArt>();
                policeArt.gameObject.AddComponent<WorldShiftPoliceEmergencyEffects>().Bind(this);
            }
            UpdatePolice(0f);
            if (!GetawayMode && !LevelMode)
            {
                RearView = RearViewCamera.Create(Player, new[] { Police });
                RearView.transform.SetParent(transform, true);
            }
            UpdateCamera(true);
            if (!LevelMode) { Combat = gameObject.AddComponent<WorldShiftCombat>(); Combat.Bind(this); }
            if (LevelMode)
            {
                gameObject.AddComponent<WorldShiftLocalLighting>().Bind(this);
                Impact = gameObject.AddComponent<WorldShiftImpactMoments>(); Impact.Bind(this);
                gameObject.AddComponent<WorldShiftLevelAudio>().Bind(this);
                Escape=gameObject.AddComponent<WorldShiftLevelEscape>();Escape.Bind(this,view);
                if (environment == ShiftLevel.Ocean)
                { OceanBoss = gameObject.AddComponent<WorldShiftOceanHelicopter>(); OceanBoss.Bind(this); }
                gameObject.AddComponent<WorldShiftLevelHud>().Bind(this);
            }
            else if (GetawayMode) { Getaway = gameObject.AddComponent<WorldShiftGetaway>(); Getaway.Bind(this); }
            else gameObject.AddComponent<WorldShiftHud>().Bind(this);
        }

        private void Start()
        {
            if (LevelMode)
            {
                BeginRun();
                return;
            }
            if (WorldShiftFlow.ConsumeAutoStart(environment)) BeginRun();
        }

        private void Update()
        {
            if (WorldShiftSceneLoading.Active || StoryActive || storyFinishedFrame == Time.frameCount) return;
            var keyboard = Keyboard.current;
            bool switchPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (State == ShiftRunState.Running && TrainingLessonPaused)
                {
                    // Enter dismisses instructions; Space is reserved for world switching.
                    bool continuePressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                    if (continuePressed && Time.realtimeSinceStartup - trainingSignShownAt >= TrainingSignInputDelay) ContinueTrainingLesson();
                    else if (keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
                    switchPressed = false;
                }
                else if (State == ShiftRunState.Ready && keyboard.enterKey.wasPressedThisFrame) BeginRun();
                else if (!GetawayMode && !LevelMode && State == ShiftRunState.Won && keyboard.enterKey.wasPressedThisFrame) WorldShiftFlow.Next(environment);
                else if (!GetawayMode && !LevelMode && State == ShiftRunState.Won && keyboard.rKey.wasPressedThisFrame) RestartRun();
                // R restarts the checkpoint while driving; while paused, the pause card owns restarts.
                else if (State == ShiftRunState.Running && keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
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
            if (State != ShiftRunState.Ready || StoryActive) return;
            if (LevelMode) WorldShiftLevelAnalytics.BeginAttempt(LevelIndex, Checkpoints.Length, () => Distance, () => Duration, () => World);
            State = ShiftRunState.Running;
            TrainingLessonPaused = false;
            trainingLessonRead = false;
            hintRemaining = 11f;
        }

        public void FinishStory()
        {
            if (!StoryActive) return;
            StoryActive = false;
            storyFinishedFrame = Time.frameCount;
            if(OceanBoss!=null) UpdateCamera(true);
            BeginRun();
        }

        public void ContinueTrainingLesson()
        {
            if (State == ShiftRunState.Running && TrainingLessonPaused)
            {
                TrainingLessonPaused = false;
                trainingLessonRead = true;
                if (LevelMode && course.Training) completedTrainingSigns.Add(checkpointIndex);
                if (LevelMode) WorldShiftLevelAnalytics.SignRead();
            }
        }

        public void RestartRun()
        {
            if (GetawayMode) { WorldShiftFlow.StartGetaway(false); return; }
            completedTrainingSigns.Clear();
            TrainingFreezeStep = 0;
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

        // Esc during the crash card is remembered and pauses once the car respawns.
        public bool PauseQueued { get; private set; }

        public void TogglePause()
        {
            if (State == ShiftRunState.Retrying) { PauseQueued = !PauseQueued; return; }
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
            if (GetawayMode && (relay?.HoldingLanding(Distance, World) ?? false)) return false;
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
            if (StoryActive || TrainingLessonPaused || State == ShiftRunState.Paused || State == ShiftRunState.Ready || State == ShiftRunState.Won || State == ShiftRunState.Lost) return;
            delta = Mathf.Max(0f, delta);
            if (State == ShiftRunState.Retrying)
            {
                presentation.StepPedestrians(delta);
                retryRemaining -= delta;
                if (Capturing) UpdatePolice(delta);
                if (GapFall) StepGapFall(delta);
                else if (lastFailure == "NO ROAD" || lastFailure == "MISSED RELAY LANDING")
                {
                    fallVelocity -= 28f * delta;
                    float overEdge = Mathf.Abs(Lateral) > WorldShiftLevel.RoadWidth * .5f ? Mathf.Sign(Lateral) * 5f : 0f;
                    Player.position += (Player.forward * WorldShiftLevel.Speed * 0.25f + Player.right * overEdge
                        + Vector3.up * fallVelocity) * delta;
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
                foreach (var hazard in Hazards) hazard.Advance(dt, Distance, World);
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
                ApplySurfaceGrip(previous);
                float airSteering = LevelMode && environment == ShiftLevel.Desert ? .65f
                    : LevelMode && environment == ShiftLevel.Ocean ? .6f : .4f;
                // Mountain guardrail gaps open the road edge; everywhere else the car stays on the carriageway.
                bool edgeGaps = LevelMode && course.EdgeGaps.Length > 0 && !IsRelayAirborne;
                driver.OpenLeftEdge = edgeGaps && course.EdgeGapAt(previous, -1f);
                driver.OpenRightEdge = edgeGaps && course.EdgeGapAt(previous, 1f);
                driver.StepExternal(dt, (IsRelayAirborne ? steer * airSteering : steer) * (JumpSequence ? 1f : Getaway?.Extras.SteeringMultiplier ?? 1f), speed);
                Getaway?.FinishStep(driver.RoadPositionDistance, driver);
                float nextLateral = driver.LateralOffset;
                float next = Mathf.Min(RunLength, driver.RoadPositionDistance);
                // Desert's vultures and Mountain's eagles can hit a car in the air (a crash sends it back to the checkpoint).
                ShiftHazard hit = IsRelayAirborne && !(LevelMode && (environment == ShiftLevel.Desert || environment == ShiftLevel.Mountain)) ? null
                    : FindCollision(World, previous, next, previousLateral, nextLateral, true, previousHeight, Player.position.y);
                Duration += dt;
                if (!LevelMode && (!GetawayMode || !Getaway.InRecovery))
                {
                    fuel.Tick(dt, WorldShiftLevel.FuelConsumption);
                    pursuit.Tick(dt, speed, BaseSpeed + Mathf.Lerp(.03f, .14f, Progress) + (GetawayMode ? WorldShiftFlow.Escape.ExtraPursuitSpeed : 0f));
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
                        Impact?.Land(Player.position, Player.forward, environment, World);
                        Notify("LANDED", 1.2f);
                    }
                }
                // Driving through a guardrail gap: once the car's centre leaves the road it falls (checkpoint retry).
                if (LevelMode && !IsRelayAirborne && Mathf.Abs(nextLateral) > WorldShiftLevel.RoadWidth * .5f
                    && course.EdgeGapAt(next, nextLateral))
                {
                    Distance = next;
                    Lateral = nextLateral;
                    Fail("NO ROAD");
                    break;
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
                int trainingSign = LevelMode && course.Training ? course.SignIndexForCheckpoint(checkpointIndex) : -1;
                if (trainingSign >= 0 && !trainingLessonRead
                    && Distance >= course.Signs[trainingSign].Distance - 5f && !TrainingLessonPaused)
                {
                    TrainingLessonPaused = true;
                    trainingSignShownAt = Time.realtimeSinceStartup;
                    WorldShiftLevelAnalytics.SignShown(checkpointIndex);
                }
                if (Collection != null && !Collection.Step(previous, next, previousLateral, nextLateral, OceanBoss == null || (!OceanBoss.Defeated && World == ShiftWorld.Day)))
                { Fail("MISSED ITEM"); break; }
                if (OceanBoss != null && OceanBoss.Step(dt, previous, next, previousLateral, nextLateral))
                { Fail(OceanBoss.CollisionReason); break; }
                UpdateMergeState();
                presentation.StepPedestrians(dt);
                CollectFuel(previous, next, previousLateral, nextLateral);
                Getaway?.Collect(previous, next, previousLateral, nextLateral);
                Combat?.Step(previous, next, previousLateral, nextLateral);
                if (checkpointIndex + 1 < Checkpoints.Length && Distance >= Checkpoints[checkpointIndex + 1].Distance)
                {
                    checkpointIndex++;
                    Collection?.SaveCheckpoint();
                    OceanBoss?.SaveCheckpoint();
                    trainingLessonRead = completedTrainingSigns.Contains(checkpointIndex);
                    if (LevelMode)
                    {
                        WorldShiftLevelAnalytics.Checkpoint(checkpointIndex);
                        CheckpointNoticeRemaining = 2.2f;
                    }
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
                    if (LevelMode) { WorldShiftLevels.Complete(LevelIndex); WorldShiftLevelAnalytics.Completed(); }
                    Getaway?.Finish(Lateral);
                }
            }
            UpdatePolice(simulatedTime);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(World);
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
            if (Getaway != null && Getaway.Extras.BlocksImpact(pursuit.CollisionProtectionRemaining > 0f))
            { presentation.OnHazardImpact(hit, lateral); return; }
            if (!pursuit.TryCrash(CollisionPursuitDrain * (GetawayMode ? WorldShiftFlow.Garage.CollisionMultiplier : 1f))) return;
            Hits++;
            WorldShiftAnalytics.Impact(hit.Kind.ToString());
            presentation.OnHazardImpact(hit, lateral);
            Notify("POLICE CLOSER", 1.3f);
            if (pursuit.IsGameOver) Fail("CAUGHT");
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
                GapFall = reason == "NO ROAD" && course != null && course.EdgeGaps.Length > 0
                    && Mathf.Abs(Lateral) > WorldShiftLevel.RoadWidth * .5f;
                if (GapFall) BeginGapFall();
                retryRemaining = RetryDuration;
                if (Impact != null)
                {
                    if (reason == "NO ROAD" || reason == "MISSED RELAY LANDING") Impact.Fall();
                    else if (reason != "MISSED ITEM") Impact.Crash(Player.position, Player.forward);
                }
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

        private void RestoreCheckpoint()
        {
            // Preserve finished demonstrations for this run; incomplete sequences start fresh
            // because retry restores the checkpoint world and mover positions.
            if (!trainingFreezeDemonstrated) TrainingFreezeStep = 0;
            if (LevelMode)
            {
                WorldShiftLevelAnalytics.Respawned(Checkpoints[checkpointIndex].World);
                // Every retry replays the section exactly: movers return to their start.
                foreach (var hazard in levelHazards) hazard.ResetMotion();
            }
            Combat?.ResetPractice();
            if (LevelMode) WorldShiftLevelAnalytics.RampEnded(false, "retry");
            ShiftCheckpoint checkpoint = Checkpoints[checkpointIndex];
            CheckpointNoticeRemaining = 0f;
            Collection?.Restore(checkpoint.Distance);
            OceanBoss?.Restore(checkpoint.Distance);
            Distance = checkpoint.Distance;
            Lateral = checkpoint.Lateral;
            retryRemaining = 0f;
            State = ShiftRunState.Running;
            if (LevelMode && TrainingLessonPaused) WorldShiftLevelAnalytics.SignRead("restart");
            TrainingLessonPaused = false;
            trainingLessonRead = completedTrainingSigns.Contains(checkpointIndex);
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
            policeReady=false; policeCapturing=false;
            UpdatePolice(0f);
            UpdateCamera(true);
            Notify("CHECKPOINT " + (checkpointIndex + 1), 2f);
            if (PauseQueued) { PauseQueued = false; TogglePause(); }
            Impact?.Clear();
        }

        private void ApplyWorld(ShiftWorld world)
        {
            World = world;
            Route.SetWorld(world);
            presentation.SetWorld(world);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(world);
        }

        // Rendering can visit both visual states without advancing simulation or
        // changing the player's actual world, collision history or pursuit state.
        public void PresentWorldForCamera(ShiftWorld displayedWorld)
        {
            Route.SetWorld(displayedWorld);
            presentation.SetWorld(displayedWorld);
            presentation.UpdateVisibility(Distance);
            Getaway?.Present(displayedWorld);
        }

        private void Notify(string value, float seconds) { Message = value; MessageRemaining = seconds; }

        private void OnDestroy()
        {
            if (RearView != null) Destroy(RearView.gameObject);
        }
    }
}
