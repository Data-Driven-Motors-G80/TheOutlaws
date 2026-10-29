using UnityEngine;
using UnityEngine.InputSystem;

namespace Outlaws.WorldShift
{
    // Normal/airborne rocks share one state across worlds. Finale rocks belong
    // to one half only, with their entire body kept away from the seam.
    public sealed class DesertTrialRock
    {
        public readonly float Distance, Phase, Direction;
        public readonly float MotionSpeed;
        public readonly bool Guarded;
        public readonly int MergeSide;
        public readonly bool Airborne;
        public readonly float Radius;
        public bool VisibleIn(ShiftWorld world) => MergeSide == 0 || (MergeSide < 0 ? world == ShiftWorld.Day : world == ShiftWorld.Echo);
        public float DaySeconds { get; private set; }
        public float NightSeconds { get; private set; }
        public float Lateral => AtTime(DaySeconds);
        private float AtTime(float seconds)
        {
            float x = Direction * (4.1f - Mathf.Abs(Mathf.Repeat(seconds * MotionSpeed + Phase + 4.1f, 16.4f) - 8.2f));
            return MergeSide == 0 ? x : MergeSide * (1.45f + Mathf.PingPong(seconds * MotionSpeed + Phase, 2.1f));
        }
        public float LateralIn(ShiftWorld world, bool joined) => AtTime(joined && world == ShiftWorld.Echo ? NightSeconds : DaySeconds);
        public DesertTrialRock(float distance, float phase, float direction, float motionSpeed = 1.6f, bool guarded = false, int mergeSide = 0, bool airborne = false, float radius = 1.3f)
        { Distance = distance; Phase = phase; Direction = direction; MotionSpeed = motionSpeed; Guarded = guarded; MergeSide = mergeSide; Airborne = airborne; Radius = mergeSide != 0 || airborne ? 1.1f : radius; }
        public void Advance(float delta, ShiftWorld world)
        { if (world == ShiftWorld.Day) DaySeconds += delta; }
        public void Restore(float daySeconds) => DaySeconds = daySeconds;
        public void CaptureNight() => NightSeconds = DaySeconds;
        public void RestoreNight(float nightSeconds) => NightSeconds = nightSeconds;
    }

    public readonly struct DesertTrialGate
    {
        public readonly float Start;
        public readonly ShiftWorld World;
        public readonly int HalfSide;
        public float End => Start + 8f;
        public DesertTrialGate(float start, ShiftWorld world, int halfSide = 0) { Start = start; World = world; HalfSide = halfSide; }
    }

    /// <summary>Shared simulation for the short timing trial and full desert level.</summary>
    public sealed class DesertShiftTrial : MonoBehaviour, IWorldShiftView
    {
        public const float Length = 960f, Speed = 24f;
        [SerializeField] private RoadSegment roadTemplate;
        [SerializeField] private GameObject carPrefab;
        [SerializeField] private Camera view;
        [SerializeField] private Light sun;
        [SerializeField] private Material roadMaterial, sandMaterial;
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private bool fullLevel;
        [SerializeField] private GameObject policePrefab;
        public bool FullLevel => fullLevel;
        public bool SplitViewValidation => false;
        public float RunLength => fullLevel ? DesertShiftLevel.Length : Length;
        public float MergeStart => fullLevel ? DesertShiftLevel.MergeStart : float.PositiveInfinity;
        public bool IsMerged => fullLevel && Distance >= MergeStart;
        public float JoinedViewSplit => Mathf.Lerp(entryWorld == ShiftWorld.Day ? 1f : 0f, .5f,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Distance - MergeStart) / 16f)));
        public DesertTrialRock[] Rocks =
        {
            new DesertTrialRock(180f, 0f, 1f),
            new DesertTrialRock(480f, 4.4f, -1f),
            new DesertTrialRock(506f, 4.4f, 1f),
            new DesertTrialRock(720f, 12.8f, 1f),
            new DesertTrialRock(840f, 9.2f, -1f)
        };
        public static readonly DesertTrialGate[] Gates =
        {
            new DesertTrialGate(158f, ShiftWorld.Day), new DesertTrialGate(270f, ShiftWorld.Echo),
            new DesertTrialGate(458f, ShiftWorld.Day), new DesertTrialGate(570f, ShiftWorld.Echo),
            new DesertTrialGate(698f, ShiftWorld.Day), new DesertTrialGate(762f, ShiftWorld.Echo),
            new DesertTrialGate(818f, ShiftWorld.Day)
        };
        // Original desert-style single/two-lane rows, placed in the clear links
        // between freeze puzzles. Coordinates are distance and lane (-1/0/+1).
        public static readonly Vector2[] OrdinaryObstacles =
        {
            new Vector2(230f, 1f), new Vector2(340f, -1f), new Vector2(540f, 1f),
            new Vector2(580f, -1f), new Vector2(900f, -1f), new Vector2(900f, 1f)
        };
        public DesertTrialGate[] ActiveGates { get; private set; }
        public DesertShiftTraffic[] Traffic { get; private set; } = System.Array.Empty<DesertShiftTraffic>();
        public GameObject PolicePrefab => policePrefab;
        private float[] checkpointTraffic, checkpointNightTraffic;
        private float checkpointNightSeconds;
        public float RockLateral(DesertTrialRock rock, ShiftWorld world) => rock.LateralIn(world, IsMerged);
        public bool FrozenView(ShiftWorld world) => IsMerged ? world == ShiftWorld.Echo : World == ShiftWorld.Echo;
        public Vector2[] ActiveObstacles => fullLevel ? System.Array.Empty<Vector2>() : OrdinaryObstacles;
        public float CheckpointSpacing => fullLevel ? DesertShiftLevel.CheckpointSpacing : 300f;
        public bool HasGuards(DesertTrialRock rock) => !fullLevel || rock.Distance == 180f || rock.Guarded;
        private readonly RiskRunState pursuit = new RiskRunState();
        private readonly FuelState fuel = new FuelState();
        private readonly System.Collections.Generic.HashSet<string> contacts = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<int> collectedFuel = new System.Collections.Generic.HashSet<int>();
        private float checkpointFuel = 100f, checkpointGap = 45f;
        private ShiftWorld entryWorld;
        public float FuelFraction => fuel.CurrentFuel / 100f;
        public float PursuitGap => pursuit.PursuitGap;
        public ChaseCar Police { get; private set; }
        public RearViewCamera RearView { get; private set; }
        public WorldShiftRelay Relay { get; private set; }
        public bool IsAirborne => Relay != null && Relay.IsAirborne;
        public RoadPathPoint DriverPoint(float distance) => Relay != null ? Relay.SampleDriver(distance) : Route.Sample(distance);
        public RoadPathPoint RockPoint(DesertTrialRock rock) => rock.Airborne
            ? Relay.SampleFlight(DesertShiftLevel.Relays[0], rock.Distance) : Route.Sample(rock.Distance);
        public bool IsFuelTaken(int index) => collectedFuel.Contains(index);
        public int FuelCount => fullLevel ? 12 : 0;
        public float FuelDistance(int index) => index / 2 * 480f + (index % 2 == 0 ? 24f : 460f);
        private AutoDriveCar driver;
        private DesertShiftTrialView presentation;
        private float retryTime, messageTime;
        private float checkpointDaySeconds;
        private static readonly float[] CrossingGuardOffsets = { -4.5f, 4.5f };
        public WorldShiftRoute Route { get; private set; }
        public Transform Player { get; private set; }
        public ShiftWorld World { get; private set; } = ShiftWorld.Day;
        public ShiftRunState State { get; private set; } = ShiftRunState.Ready;
        public float Distance { get; private set; }
        public float Lateral => driver != null ? driver.LateralOffset : 0f;
        public float Duration { get; private set; }
        public int Stage { get; private set; }
        public int Retries { get; private set; }
        public int Switches { get; private set; }
        public string Message { get; private set; } = "";
        public string Hint => messageTime > 0f ? Message : IsMerged
            ? "LEFT KEEPS MOVING / RIGHT STAYS FROZEN. A / D crosses worlds. SPACE rests."
            : Relay != null && Relay.SectionAt(Distance) != null ? IsAirborne
                ? World == ShiftWorld.Day ? "AIRBORNE - SPACE FREEZES THE ROCKS. LAND IN NIGHT."
                    : "FROZEN - STEER THROUGH THE AIRBORNE OPENING. STAY IN NIGHT TO LAND."
                : Relay.Hint(Distance, World)
            : fullLevel && Distance > MergeStart - 120f ? "RIFT AHEAD - left is Day, right is Night. Steer to cross."
            : fullLevel && DesertShiftLevel.InRockField(Distance) ? "FREEZE EACH GROUP. Blue walls require DAY, then freeze the next opening."
            : World == ShiftWorld.Echo
            ? "ROCKS + TRAFFIC FROZEN. Still solid. Return to Day at a blue wall."
            : Stage == 0 ? "Let the rock leave the centre, then SPACE to freeze the opening."
            : Stage == 1 ? "Watch both rocks. Freeze when the centre route is clear."
            : "Freeze, pass, then return to Day to release the next rock. Freeze again.";

        private void Awake()
        {
            if (roadTemplate == null || carPrefab == null || view == null || sun == null)
            { Debug.LogError("DesertShiftTrial: scene references missing."); enabled = false; return; }
            ActiveGates = fullLevel ? DesertShiftLevel.CreateGates() : Gates;
            if (fullLevel) { Rocks = DesertShiftLevel.CreateRocks(); Traffic = DesertShiftLevel.CreateTraffic(); }
            checkpointTraffic = new float[Traffic.Length]; checkpointNightTraffic = new float[Traffic.Length];
            var road = new GameObject("Shared desert test road");
            road.transform.SetParent(transform, false);
            Route = road.AddComponent<WorldShiftRoute>();
            Route.Build(roadTemplate, true, RunLength, true, fullLevel ? DesertShiftLevel.Relays : null);
            if (fullLevel) Relay = new WorldShiftRelay(Route, DesertShiftLevel.Relays, Speed);
            Player = new GameObject("Shared player - continuous between worlds").transform;
            Player.SetParent(transform, false);
            var visual = new GameObject("Original driving body").transform;
            visual.SetParent(Player, false);
            GameObject body = Instantiate(carPrefab, visual);
            body.transform.localPosition = Vector3.up * .55f;
            body.transform.localScale = new Vector3(1.6f, .85f, 3f);
            foreach (Collider collider in body.GetComponentsInChildren<Collider>()) collider.enabled = false;
            body.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(new Color(1f, .76f, .18f));
            driver = Player.gameObject.AddComponent<AutoDriveCar>();
            driver.ConfigureExternalRoute(DriverPoint, visual, Speed);
            driver.ExternalVerticalControl = fullLevel;
            presentation = gameObject.AddComponent<DesertShiftTrialView>();
            presentation.Build(this, view, sun, roadMaterial, sandMaterial, obstaclePrefab);
            if (fullLevel)
            {
                Police = ChaseCar.CreateForFiniteRoute(policePrefab, transform, Player, Relay.SamplePolice);
                Police.StepFinite(0f, 0f, 0f, 6f);
                RearView = RearViewCamera.Create(Player, new[] { Police });
                RearView.transform.SetParent(transform, true);
                var rift = gameObject.AddComponent<WorldShiftRift>(); rift.Build(this, Route);
                gameObject.AddComponent<WorldShiftSplitView>().Initialize(this, view);
            }
            presentation.Refresh();
        }

        private void Start() { if (fullLevel && WorldShiftFlow.ConsumeAutoStart(ShiftLevel.Desert)) Begin(); }
        public void PresentWorldForCamera(ShiftWorld world) => presentation.Present(world);

        private void Update()
        {
            var key = Keyboard.current;
            if (key != null)
            {
                if (key.enterKey.wasPressedThisFrame)
                { if (State == ShiftRunState.Won && fullLevel) WorldShiftFlow.Next(ShiftLevel.Desert); else if (State == ShiftRunState.Won) Restart(); else Begin(); }
                if (key.escapeKey.wasPressedThisFrame) TogglePause();
                if (key.rKey.wasPressedThisFrame) Retry();
            }
            float steer = key == null ? 0f : ((key.dKey.isPressed || key.rightArrowKey.isPressed) ? 1f : 0f)
                - ((key.aKey.isPressed || key.leftArrowKey.isPressed) ? 1f : 0f);
            StepSimulation(Time.deltaTime, steer, key != null && key.spaceKey.wasPressedThisFrame);
        }
        private void LateUpdate() { if (presentation != null) presentation.Refresh(); }
        public void Begin() { if (State == ShiftRunState.Ready) State = ShiftRunState.Running; }
        public void TogglePause()
        {
            if (State == ShiftRunState.Running) State = ShiftRunState.Paused;
            else if (State == ShiftRunState.Paused) State = ShiftRunState.Running;
        }
        public void Restart()
        {
            Stage = Retries = Switches = 0; Duration = checkpointDaySeconds = checkpointNightSeconds = 0f;
            checkpointFuel = 100f; checkpointGap = 45f; collectedFuel.Clear();
            System.Array.Clear(checkpointTraffic, 0, checkpointTraffic.Length);
            System.Array.Clear(checkpointNightTraffic, 0, checkpointNightTraffic.Length);
            Restore();
        }
        public void Retry()
        { if (State != ShiftRunState.Ready) { Retries++; Restore(); } }
        public bool TrySwitch()
        {
            if (State != ShiftRunState.Running) return false;
            if (IsMerged) { Notify("A / D CROSSES WORLDS HERE", 1f); return false; }
            ShiftWorld next = World == ShiftWorld.Day ? ShiftWorld.Echo : ShiftWorld.Day;
            World = next;
            Switches++;
            Notify(next == ShiftWorld.Echo ? "FROZEN - ROCKS + TRAFFIC ARE STILL SOLID" : "RELEASED - ROCKS + TRAFFIC MOVE AGAIN", 1.3f);
            if (!IsAirborne && !Route.HasSurface(Distance, World)) Fail("NO ROAD - USE THE DAY BOOST RAMP");
            else if (!IsAirborne) CheckGates(Distance, Lateral);
            CheckRockContacts(Distance, Lateral, 0f);
            presentation.Refresh();
            return true;
        }

        public void StepSimulation(float delta, float steer, bool shift)
        {
            if (State == ShiftRunState.Paused || State == ShiftRunState.Ready || State == ShiftRunState.Won) return;
            if (State == ShiftRunState.Retrying)
            { retryTime -= Mathf.Max(0f, delta); if (retryTime <= 0f) Restore(); return; }
            if (shift) TrySwitch();
            float elapsed = 0f;
            while (delta > .00001f && State == ShiftRunState.Running)
            {
                float dt = Mathf.Min(delta, 1f / 60f);
                delta -= dt;
                elapsed += dt;
                float before = Distance, previousX = Lateral;
                float speed = Relay != null ? Relay.SpeedAt(Distance) : Speed;
                if (Relay != null) Relay.BeginStep(Distance, Distance + speed * dt, World);
                driver.StepExternal(dt, IsAirborne ? steer * .65f : steer, speed);
                Distance = Mathf.Min(RunLength, driver.RoadPositionDistance);
                if (Relay != null)
                {
                    if (!Relay.FinishStep(Distance, World, out bool landed)) { Fail("MISSED LANDING - SWITCH TO NIGHT IN THE AIR"); break; }
                    if (landed) Notify("NIGHT LANDING - BACK ON THE ROAD", 1.5f);
                }
                if (!IsAirborne && !Route.HasSurface(Distance, World)) { Fail("NO ROAD - USE THE DAY BOOST RAMP"); break; }
                if (IsMerged)
                {
                    if (before < MergeStart)
                    {
                        entryWorld = World;
                        foreach (var rock in Rocks) rock.CaptureNight();
                        foreach (var car in Traffic) car.CaptureNight();
                        Notify("RIFT - LEFT MOVES / RIGHT STAYS FROZEN", 3f);
                    }
                    World = WorldShiftLevel.WorldAt(Lateral);
                }
                Duration += dt;
                messageTime -= dt;
                if (fullLevel)
                {
                    fuel.Tick(dt, 1.5f);
                    pursuit.Tick(dt, speed, speed + .04f);
                    for (int i = 0; i < FuelCount; i++)
                        if (!collectedFuel.Contains(i) && Distance + 1.3f >= FuelDistance(i) && before - 1.3f <= FuelDistance(i) && Mathf.Abs(Lateral) < 1.6f)
                        { collectedFuel.Add(i); fuel.Refill(26f); Notify("FUEL +26", 1f); }
                }
                CheckRockContacts(before, previousX, dt);
                StepTraffic(dt, before, previousX);
                if (State != ShiftRunState.Running) break;
                foreach (var rock in Rocks)
                    if (!IsAirborne && HasGuards(rock))
                    foreach (float offset in CrossingGuardOffsets)
                        if (Distance + 1.5f >= rock.Distance + offset - 1.25f && before - 1.5f <= rock.Distance + offset + 1.25f
                            && Mathf.Max(Mathf.Abs(previousX), Mathf.Abs(Lateral)) + .8f > 2.25f)
                            Impact("guard" + rock.Distance, "SIDE BARRIER - use the centre opening");
                if (State != ShiftRunState.Running) break;
                foreach (var block in ActiveObstacles)
                    if (Distance + 1.5f >= block.x - 1.25f && before - 1.5f <= block.x + 1.25f
                        && Mathf.Max(previousX, Lateral) >= block.y * WorldShiftLevel.RoadWidth / 3f - 1.7f
                        && Mathf.Min(previousX, Lateral) <= block.y * WorldShiftLevel.RoadWidth / 3f + 1.7f)
                        Impact("block" + block, "ROADBLOCK - use the open lane");
                if (State != ShiftRunState.Running) break;
                CheckGates(before, previousX);
                if (State != ShiftRunState.Running) break;
                int previousStage = Stage;
                Stage = Mathf.Min(fullLevel ? 5 : 2, Mathf.FloorToInt(Distance / CheckpointSpacing));
                if (Stage != previousStage)
                {
                    checkpointDaySeconds = Rocks[0].DaySeconds; checkpointNightSeconds = Rocks[0].NightSeconds;
                    checkpointFuel = Mathf.Max(50f, fuel.CurrentFuel); checkpointGap = Mathf.Max(32f, pursuit.PursuitGap);
                    for (int i = 0; i < Traffic.Length; i++)
                    { checkpointTraffic[i] = Traffic[i].Travel; checkpointNightTraffic[i] = Traffic[i].NightTravel; }
                }
                if (fullLevel && fuel.IsEmpty) Fail("OUT OF FUEL");
                else if (Distance >= RunLength) State = ShiftRunState.Won;
            }
            if (Police != null) Police.StepFinite(elapsed, Distance, Lateral, Mathf.Lerp(.45f, 6f, Mathf.Clamp01(PursuitGap / 45f)));
            GetComponent<WorldShiftRift>()?.Refresh(Distance);
            presentation.Refresh();
        }

        private void CheckGates(float before, float previousX)
        {
            if (IsAirborne) return;
            foreach (var gate in ActiveGates)
            {
                bool visible = gate.Start >= MergeStart ? gate.HalfSide != 0 && gate.World == WorldShiftLevel.WorldAt(gate.HalfSide) : gate.World == World;
                if (!visible || Distance + 1.5f < gate.Start || before - 1.5f > gate.End) continue;
                float min = gate.HalfSide > 0 ? 0f : -5f, max = gate.HalfSide < 0 ? 0f : 5f;
                if (Mathf.Max(previousX, Lateral) + .8f > min && Mathf.Min(previousX, Lateral) - .8f < max)
                    Impact("gate" + gate.Start, "WORLD WALL - switch or cross before reaching it");
            }
        }

        private void CheckRockContacts(float before, float previousX, float dt)
        {
            foreach (var rock in Rocks)
            {
                ShiftWorld owner = rock.MergeSide > 0 ? ShiftWorld.Echo : ShiftWorld.Day;
                float oldX = RockLateral(rock, IsMerged ? owner : World);
                rock.Advance(dt, IsMerged ? ShiftWorld.Day : World);
                if (rock.Airborne != IsAirborne || (!IsMerged && !rock.VisibleIn(World))) continue;
                if (Distance + 1.5f + rock.Radius < rock.Distance || before - 1.5f - rock.Radius > rock.Distance) continue;
                float x = RockLateral(rock, IsMerged ? owner : World);
                float radius = rock.Radius + .8f;
                bool hit = Mathf.Min(previousX - oldX, Lateral - x) < radius && Mathf.Max(previousX - oldX, Lateral - x) > -radius;
                if (hit) Impact("rock" + rock.Distance, "ROCK CONTACT - freeze, then steer through the opening");
            }
        }

        // Clip collisions to the same world half that the compositor actually
        // displays. A Day rock on the hidden right half cannot hit the player.
        private static bool TouchesHalf(float carA, float carB, float objectA, float objectB, float radius, ShiftWorld world)
        {
            float min = Mathf.Max(world == ShiftWorld.Day ? -5f : 0f, Mathf.Min(objectA, objectB) - radius);
            float max = Mathf.Min(world == ShiftWorld.Day ? 0f : 5f, Mathf.Max(objectA, objectB) + radius);
            return min < max && Mathf.Max(carA, carB) + .8f > min && Mathf.Min(carA, carB) - .8f < max;
        }

        private void StepTraffic(float dt, float before, float previousX)
        {
            foreach (var car in Traffic)
            {
                float oldDay = car.Position(ShiftWorld.Day, IsMerged);
                car.Advance(dt, IsMerged ? ShiftWorld.Day : World, Distance);
                if (IsAirborne) continue;
                float x = car.Lane * WorldShiftLevel.RoadWidth / 3f;
                for (int i = 0; i < 2; i++)
                {
                    ShiftWorld world = (ShiftWorld)i;
                    if (!IsMerged && world != World) continue;
                    float oldPosition = IsMerged && world == ShiftWorld.Echo ? car.Position(world, true) : oldDay;
                    float position = car.Position(world, IsMerged);
                    if (Distance + 1.5f < position - DesertShiftTraffic.HalfLength || before - 1.5f > oldPosition + DesertShiftTraffic.HalfLength) continue;
                    bool hit = IsMerged ? TouchesHalf(previousX, Lateral, x, x, DesertShiftTraffic.HalfWidth, world)
                        : Mathf.Max(previousX, Lateral) + .8f > x - DesertShiftTraffic.HalfWidth && Mathf.Min(previousX, Lateral) - .8f < x + DesertShiftTraffic.HalfWidth;
                    if (hit) Impact("traffic" + car.Start, "ONCOMING POLICE - freeze, then steer around");
                }
            }
        }
        private void Impact(string id, string reason)
        {
            if (State != ShiftRunState.Running || !contacts.Add(id)) return;
            if (!fullLevel) { Fail(reason); return; }
            if (!pursuit.TryCrash(.5f)) return;
            Notify("HIT - POLICE 22.5 m CLOSER", 1.3f);
            if (pursuit.IsGameOver) Fail("CAUGHT");
        }

        private void Fail(string reason)
        { Retries++; State = ShiftRunState.Retrying; retryTime = .7f; Notify(reason, 2f); }
        private void Notify(string text, float time) { Message = text; messageTime = time; }
        private void Restore()
        {
            Distance = Stage * CheckpointSpacing;
            World = ShiftWorld.Day;
            State = ShiftRunState.Running;
            fuel.Reset(checkpointFuel);
            pursuit.Restore(new RiskRunSnapshot(checkpointGap, false, 0, false, 0, 0, PickupEffectType.None, 0f, 0f, 0f));
            pursuit.RecoveryCap = 45f;
            contacts.Clear();
            collectedFuel.RemoveWhere(i => FuelDistance(i) >= Distance);
            foreach (var rock in Rocks) { rock.Restore(checkpointDaySeconds); rock.RestoreNight(checkpointNightSeconds); }
            for (int i = 0; i < Traffic.Length; i++) Traffic[i].Restore(checkpointTraffic[i], checkpointNightTraffic[i]);
            Relay?.Reset(Distance);
            driver.RestoreExternalPose(Distance, 0f);
            if (IsMerged) { World = ShiftWorld.Echo; entryWorld = ShiftWorld.Echo; }
            if (Police != null) Police.StepFinite(0f, Distance, Lateral, Mathf.Lerp(.45f, 6f, Mathf.Clamp01(PursuitGap / 45f)));
            Notify("CHECKPOINT - DAY ROLLS / NIGHT FREEZES", 1.6f);
            presentation.Refresh();
        }
    }
}
