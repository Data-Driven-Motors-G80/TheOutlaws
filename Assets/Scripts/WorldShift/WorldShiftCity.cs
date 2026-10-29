using UnityEngine;
using UnityEngine.InputSystem;

namespace Outlaws.WorldShift
{
    public enum ShiftRunState { Ready, Running, Paused, Retrying, Won }

    /// <summary>Isolated two-world prototype. Distance is the same coordinate in both worlds.</summary>
    public sealed class WorldShiftCity : MonoBehaviour, IWorldShiftView
    {
        // Every solid hazard costs half the initial 45 m gap. Capture still depends
        // on the remaining pursuit distance, never on a scripted collision count.
        private const float CollisionPursuitDrain = .5f;
        [SerializeField] private bool splitViewValidation;
        [SerializeField] private bool tutorialLevel;
        public bool TutorialLevel => tutorialLevel;
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
        public int RelaysCompleted => relay?.Completed ?? 0;
        public bool IsBoosting => Speed > WorldShiftLevel.Speed + .1f;
        private readonly FuelState fuel = new FuelState();
        private readonly RiskRunState pursuit = new RiskRunState();
        private readonly System.Collections.Generic.HashSet<ShiftHazard> hitHazards = new System.Collections.Generic.HashSet<ShiftHazard>();
        private bool[] fuelTaken;
        private float retryRemaining, hintRemaining, fallVelocity;
        private float checkpointFuel = FuelState.MaximumFuel;
        private float checkpointGap = RiskRunState.InitialPursuitGap;
        private int checkpointIndex;
        private string lastFailure;
        private AudioSource audioSource;
        private AudioClip shiftSound;

        public ShiftRunState State { get; private set; } = ShiftRunState.Ready;
        public ShiftWorld World { get; private set; } = ShiftWorld.Day;
        public float Distance { get; private set; }
        public float Lateral { get; private set; }
        public float Duration { get; private set; }
        public bool SplitViewValidation => splitViewValidation;
        public float RunLength => tutorialLevel ? WorldShiftTutorial.Length : splitViewValidation ? WorldShiftSplitView.Length : WorldShiftLevel.Length;
        public ShiftHazard[] Hazards => tutorialLevel ? WorldShiftTutorial.Hazards : splitViewValidation ? WorldShiftSplitView.Layout : WorldShiftLevel.Hazards;
        public ShiftFuelPickup[] Pickups => tutorialLevel ? System.Array.Empty<ShiftFuelPickup>() : splitViewValidation ? WorldShiftSplitView.Fuel : WorldShiftLevel.FuelPickups;
        public ShiftCheckpoint[] Checkpoints => tutorialLevel ? WorldShiftTutorial.Checkpoints : splitViewValidation ? WorldShiftSplitView.Checkpoints : WorldShiftLevel.Checkpoints;
        public bool IsJoinedDistance(float at) => splitViewValidation
            ? at >= WorldShiftSplitView.JoinStart && at < WorldShiftSplitView.JoinEnd : tutorialLevel ? at >= MergeStart : WorldShiftLevel.IsMerged(at);
        public bool IsWorldVisible(ShiftWorld world, float at, float lateral, ShiftWorld current)
            => IsJoinedDistance(at) ? world == WorldShiftLevel.WorldAt(lateral) : world == current;
        public float Progress => Mathf.Clamp01(Distance / RunLength);
        public int Deaths { get; private set; }
        public int Switches { get; private set; }
        public int FuelCollected { get; private set; }
        public float FuelFraction => fuel.CurrentFuel / FuelState.MaximumFuel;
        public bool IsMerged => IsJoinedDistance(Distance);
        private bool wasMerged;
        private ShiftWorld riftEntryWorld;
        public float MergeStart => tutorialLevel ? WorldShiftTutorial.MergeStart : splitViewValidation ? WorldShiftSplitView.JoinStart : WorldShiftLevel.MergeStart;
        // The safe entrance opens into a split over 16 metres, about half a second.
        public float JoinedViewSplit => splitViewValidation ? .5f : Mathf.Lerp(
            riftEntryWorld == ShiftWorld.Day ? 1f : 0f, .5f,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Distance - MergeStart) / 16f)));
        public float Speed => tutorialLevel ? 18f : relay?.SpeedAt(Distance) ?? WorldShiftLevel.Speed;
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
            : !splitViewValidation && Distance >= MergeStart - 64f && Distance < MergeStart
            ? "WORLD RIFT AHEAD - pass through, then steer between Day and Night."
            : hintRemaining > 0f ? Checkpoints[checkpointIndex].Hint : "";
        public WorldShiftRoute Route { get; private set; }
        public Transform Player { get; private set; }

        private void Awake()
        {
            if (roadTemplate == null || carPrefab == null || view == null || sun == null)
            { Debug.LogError("WorldShiftCity: missing scene references.", this); enabled = false; return; }
            var roadObject = new GameObject("Shared finite city road");
            roadObject.transform.SetParent(transform, false);
            Route = roadObject.AddComponent<WorldShiftRoute>();
            fuelTaken = new bool[Pickups.Length];
            Route.Build(roadTemplate, splitViewValidation || tutorialLevel, RunLength);
            if (!splitViewValidation && !tutorialLevel) relay = new WorldShiftRelay(Route);
            Player = new GameObject("Player - same position in both worlds").transform;
            Player.SetParent(transform, false);
            var visual = new GameObject("Original driving body").transform;
            visual.SetParent(Player, false);
            var body = Instantiate(carPrefab, visual);
            body.name = "Original prototype car";
            body.transform.localPosition = Vector3.up * 0.55f;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(1.6f, 0.85f, 3f);
            foreach (Collider collider in body.GetComponentsInChildren<Collider>()) collider.enabled = false;
            // Keep a consistently recognisable player in both palettes.
            body.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(new Color(1f, 0.76f, 0.18f));
            driver = Player.gameObject.AddComponent<AutoDriveCar>();
            driver.ConfigureExternalRoute(d => relay != null ? relay.SampleDriver(d) : Route.Sample(d), visual, WorldShiftLevel.Speed);
            presentation = gameObject.AddComponent<WorldShiftPresentation>();
            presentation.Build(this, Route, visual, view, sun, obstaclePrefab, policePrefab, streetlightPrefab, fuelPrefab);
            ConfigureSound();
            ApplyWorld(ShiftWorld.Day);
            Police = ChaseCar.CreateForFiniteRoute(policePrefab, transform, Player, d => relay != null ? relay.SamplePolice(d) : Route.Sample(d));
            UpdatePolice(0f);
            RearView = RearViewCamera.Create(Player, new[] { Police });
            RearView.transform.SetParent(transform, true);
            UpdateCamera(true);
            gameObject.AddComponent<WorldShiftHud>().Bind(this);
            gameObject.AddComponent<WorldShiftSplitView>().Initialize(this, view);
        }

        private void Start()
        {
            if (WorldShiftFlow.ConsumeAutoStart(tutorialLevel ? ShiftLevel.Tutorial : ShiftLevel.City)) BeginRun();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
                if (State == ShiftRunState.Ready && keyboard.enterKey.wasPressedThisFrame) BeginRun();
                else if (State == ShiftRunState.Won && keyboard.enterKey.wasPressedThisFrame) WorldShiftFlow.Next(tutorialLevel ? ShiftLevel.Tutorial : ShiftLevel.City);
                else if (State == ShiftRunState.Won && keyboard.rKey.wasPressedThisFrame) RestartRun();
                else if ((State == ShiftRunState.Running || State == ShiftRunState.Paused) && keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
            }
            float steer = keyboard == null ? 0f :
                ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f)
                - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);
            StepSimulation(Time.deltaTime, steer, keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
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
            hintRemaining = 11f;
        }

        public void RestartRun()
        {
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
            if (State == ShiftRunState.Running) State = ShiftRunState.Paused;
            else if (State == ShiftRunState.Paused) State = ShiftRunState.Running;
        }

        public void RetryCheckpoint()
        {
            if (State == ShiftRunState.Ready || State == ShiftRunState.Won) return;
            RestoreCheckpoint();
        }

        public bool TrySwitch()
        {
            if (State != ShiftRunState.Running) return false;
            if (IsMerged)
            {
                Notify("WORLDS JOINED - STEER LEFT / RIGHT TO CROSS", 1.5f);
                return false;
            }
            ShiftWorld next = World == ShiftWorld.Day ? ShiftWorld.Echo : ShiftWorld.Day;
            ApplyWorld(next);
            Switches++;
            // A switch always commits. Occupied destinations use the same impact
            // path as driving into a hazard; missing road still causes a fall.
            if (!IsRelayAirborne)
            {
                if (!Route.HasSurface(Distance, next)) Fail("NO ROAD");
                else ApplyImpact(FindCollision(next, Distance, Distance, Lateral, Lateral, true), Lateral);
            }
            if (IsRelayAirborne) Notify(relay.Hint(Distance, World), 1.8f);
            if (audioSource != null && shiftSound != null)
            { audioSource.pitch = next == ShiftWorld.Echo ? 1.15f : 0.85f; audioSource.PlayOneShot(shiftSound, 0.14f); }
            return true;
        }

        public void StepSimulation(float delta, float steer, bool switchPressed)
        {
            if (State == ShiftRunState.Paused || State == ShiftRunState.Ready || State == ShiftRunState.Won) return;
            delta = Mathf.Max(0f, delta);
            if (State == ShiftRunState.Retrying)
            {
                presentation.StepPedestrians(delta);
                retryRemaining -= delta;
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
            hintRemaining = Mathf.Max(0f, hintRemaining - delta);
            if (switchPressed) TrySwitch();
            float simulatedTime = 0f;
            // Small substeps make held steering and collision checks stable even after a slow frame.
            while (delta > 0.00001f && State == ShiftRunState.Running)
            {
                float dt = Mathf.Min(delta, 1f / 60f);
                delta -= dt;
                simulatedTime += dt;
                float previous = Distance, previousLateral = Lateral;
                float speed = Speed;
                if (relay != null)
                {
                    driver.ExternalVerticalControl = WorldShiftRelay.At(previous) != null;
                    if (relay.BeginStep(previous, previous + speed * dt, World))
                        Notify("AIRBORNE - PRESS SPACE FOR THE OTHER LANDING", 2.5f);
                }
                driver.StepExternal(dt, IsRelayAirborne ? steer * .4f : steer, speed);
                float nextLateral = driver.LateralOffset;
                float next = Mathf.Min(RunLength, driver.RoadPositionDistance);
                ShiftHazard hit = IsRelayAirborne ? null : FindCollision(World, previous, next, previousLateral, nextLateral, true);
                Duration += dt;
                fuel.Tick(dt, tutorialLevel ? 0f : WorldShiftLevel.FuelConsumption);
                pursuit.Tick(dt, speed, speed + (tutorialLevel ? 0f : Mathf.Lerp(.03f, .14f, Progress)));
                if (relay != null)
                {
                    bool safe = relay.FinishStep(next, World, out bool landed);
                    if (!safe || (!IsRelayAirborne && !Route.HasSurface(next, World)))
                    {
                        Distance = next;
                        Lateral = nextLateral;
                        Fail(!safe ? "MISSED RELAY LANDING" : "NO ROAD");
                        break;
                    }
                    if (landed) Notify("RELAY LANDED - KEEP DRIVING", 1.2f);
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
                UpdateMergeState();
                presentation.StepPedestrians(dt);
                CollectFuel(previous, next, previousLateral, nextLateral);
                if (checkpointIndex + 1 < Checkpoints.Length && Distance >= Checkpoints[checkpointIndex + 1].Distance)
                {
                    checkpointIndex++;
                    // Keep retries viable even if a player reached a checkpoint nearly empty.
                    checkpointFuel = Mathf.Max(35f, fuel.CurrentFuel);
                    checkpointGap = Mathf.Max(32f, pursuit.PursuitGap);
                    hintRemaining = 10f;
                    Notify(IsMerged ? "RIFT CROSSED - A / D TO CROSS WORLDS" : "CHECKPOINT " + (checkpointIndex + 1), IsMerged ? 3f : 1.8f);
                }
                if (pursuit.IsGameOver) Fail("CAUGHT");
                else if (Distance >= RunLength) State = ShiftRunState.Won;
                else if (fuel.IsEmpty) Fail("OUT OF FUEL");
            }
            UpdatePolice(simulatedTime);
            presentation.UpdateVisibility(Distance);
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
                if (merged && audioSource != null && shiftSound != null)
                { audioSource.pitch = .65f; audioSource.PlayOneShot(shiftSound, .22f); }
                Notify(merged ? "RIFT CROSSED - A / D TO CROSS WORLDS" : "WORLDS SEPARATED - SPACE TO SWITCH", 3f);
            }
        }

        private void ApplyImpact(ShiftHazard hit, float lateral)
        {
            if (hit == null) return;
            if (hit.Kind == ShiftHazardKind.Gap) { Fail("NO ROAD"); return; }
            hitHazards.Add(hit);
            if (!pursuit.TryCrash(CollisionPursuitDrain)) return;
            Hits++;
            presentation.OnHazardImpact(hit, lateral);
            Notify("HIT - POLICE 22.5 m CLOSER", 1.3f);
            if (pursuit.IsGameOver) Fail("CAUGHT");
        }

        private void UpdatePolice(float deltaTime)
        {
            if (Police == null) return;
            // Like the original chase, compress logical distance into a readable mirror gap.
            Police.StepFinite(deltaTime, Distance, Lateral, Mathf.Lerp(.45f, 6f, PursuitFraction));
        }

        private float ResolveDividers(float from, float to, float previousLateral, float lateral, float steer)
        {
            float clearance = WorldShiftLevel.RoadWidth / 6f + .8f + .03f;
            foreach (var hazard in Hazards)
            {
                if (hazard.World != World || hazard.Kind != ShiftHazardKind.Divider
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
            Deaths++;
            State = ShiftRunState.Retrying;
            lastFailure = reason;
            fallVelocity = -1f;
            retryRemaining = reason == "NO ROAD" || reason == "MISSED RELAY LANDING" ? 1.05f : 0.7f;
            Notify(reason + " - returning to checkpoint", 1.1f);
        }

        private void CollectFuel(float from, float to, float fromLateral, float toLateral)
        {
            for (int i = 0; i < fuelTaken.Length; i++)
            {
                ShiftFuelPickup pickup = Pickups[i];
                if (fuelTaken[i] || !IsWorldVisible(pickup.World, pickup.Distance, pickup.Lane * WorldShiftLevel.RoadWidth / 3f, World) || to + 1.3f < pickup.Distance || from - 1.3f > pickup.Distance) continue;
                float t = Mathf.InverseLerp(from, to, pickup.Distance);
                if (Mathf.Abs(Mathf.Lerp(fromLateral, toLateral, t) - pickup.Lane * WorldShiftLevel.RoadWidth / 3f) > 1.35f) continue;
                fuelTaken[i] = true;
                FuelCollected++;
                fuel.Refill(WorldShiftLevel.FuelRefill);
                presentation.RefreshFuel();
                Notify("FUEL +26", 1.2f);
            }
        }

        // Swept checks in the common road coordinate prevent tunnelling or switching inside a wall.
        private ShiftHazard FindCollision(ShiftWorld world, float from, float to, float fromLateral, float toLateral, bool skipHandled = false)
        {
            const float halfWidth = 0.8f;
            foreach (var hazard in Hazards)
            {
                bool merged = IsJoinedDistance(hazard.Start);
                if ((!merged && hazard.World != world) || (skipHandled && hitHazards.Contains(hazard))) continue;
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

        private void RestoreCheckpoint()
        {
            ShiftCheckpoint checkpoint = Checkpoints[checkpointIndex];
            Distance = checkpoint.Distance;
            Lateral = checkpoint.Lateral;
            retryRemaining = 0f;
            State = ShiftRunState.Running;
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
            driver.ExternalVerticalControl = false;
            driver.RestoreExternalPose(Distance, Lateral);
            wasMerged = IsMerged;
            riftEntryWorld = checkpoint.World;
            ApplyWorld(IsMerged ? WorldShiftLevel.WorldAt(Lateral) : checkpoint.World);
            UpdatePolice(0f);
            UpdateCamera(true);
            Notify("CHECKPOINT " + (checkpointIndex + 1) + (IsMerged ? " - STEER ACROSS THE SEAM" : " - SPACE TO SWITCH"), 2f);
        }

        private void ApplyWorld(ShiftWorld world)
        {
            World = world;
            Route.SetWorld(world);
            presentation.SetWorld(world);
            presentation.UpdateVisibility(Distance);
        }

        // Rendering can visit both visual states without advancing simulation or
        // changing the player's actual world, collision history or pursuit state.
        public void PresentWorldForCamera(ShiftWorld displayedWorld)
        {
            Route.SetWorld(displayedWorld);
            presentation.SetWorld(displayedWorld);
            presentation.UpdateVisibility(Distance);
        }

        private void UpdateCamera(bool immediate)
        {
            RoadPathPoint point = Route.Sample(Distance);
            float follow = splitViewValidation ? 0f : .28f * (1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(MergeStart - 56f, MergeStart - 16f, Distance)));
            Vector3 desired = point.Position - point.Forward * 12f + Vector3.up * 6.4f + point.Right * Lateral * follow;
            bool falling = State == ShiftRunState.Retrying && (lastFailure == "NO ROAD" || lastFailure == "MISSED RELAY LANDING");
            if (IsRelayAirborne && !falling) desired.y += Player.position.y - point.Position.y - .08f;
            if (falling) desired = point.Position - point.Forward * 2f + Vector3.up * 10f;
            float blend = immediate ? 1f : 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            view.transform.position = Vector3.Lerp(view.transform.position, desired, blend);
            if (IsMerged)
                view.transform.position -= point.Right * Vector3.Dot(view.transform.position - point.Position, point.Right);
            view.fieldOfView = Mathf.Lerp(70f, 76f, Mathf.InverseLerp(WorldShiftLevel.Speed, WorldShiftRelay.BoostSpeed, Speed));
            Vector3 target = Route.Sample(Distance + 22f).Position + Vector3.up * 0.6f;
            if (falling) target = Player.position + point.Forward * 4f;
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, Quaternion.LookRotation(target - view.transform.position, Vector3.up), IsMerged ? 1f : blend);
        }

        private void Notify(string value, float seconds) { Message = value; MessageRemaining = seconds; }

        private void ConfigureSound()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            const int rate = 22050, samples = 2646;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate, envelope = Mathf.Sin(Mathf.PI * i / samples);
                data[i] = Mathf.Sin(2f * Mathf.PI * (360f * t + 1000f * t * t)) * envelope * 0.2f;
            }
            shiftSound = AudioClip.Create("World switch cue", samples, 1, rate, false);
            shiftSound.SetData(data, 0);
        }

        private void OnDestroy()
        {
            if (shiftSound != null) Destroy(shiftSound);
            if (RearView != null) Destroy(RearView.gameObject);
        }
    }
}
