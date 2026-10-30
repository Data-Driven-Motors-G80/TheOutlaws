using System;
using UnityEngine;

namespace Outlaws.TransformStunt
{
    /// <summary>One deterministic simulation owns motion, oriented collisions and checkpoint state.</summary>
    public sealed class StuntRun
    {
        public static readonly string[] SizeNames = { "SMALL", "MEDIUM", "LARGE" };
        public const float SmallScale = .55f, LargeScale = 1.4f, NormalSpeed = 18, SmallSpeed = 10, LargeSpeed = 13;
        public readonly StuntCourse Course;
        public readonly FuelState Fuel = new FuelState();
        public Vector3 Position { get; private set; }
        public float Scale { get; private set; } = 1;
        public float Yaw => 0;
        public int SizeIndex { get; private set; } = 1;
        public bool Small => SizeIndex == 0;
        public bool Large => SizeIndex == 2;
        public bool Airborne { get; private set; }
        public bool Paused, Started;
        public bool Finished { get; private set; }
        public float Clock { get; private set; }
        public float Duration { get; private set; }
        public float Speed { get; private set; }
        public int Crashes { get; private set; }
        public int Collected { get; private set; }
        public int Lesson { get; private set; }
        public int Checkpoint { get; private set; }
        public readonly bool[] FuelTaken;
        public readonly bool[] Broken;
        public readonly bool[] Activated;
        public string Feedback { get; private set; } = "";
        public float FeedbackTime { get; private set; }
        public float RetryRemaining { get; private set; }
        private float lateralVelocity, windVelocity, verticalVelocity, launchSpeed, checkpointClock, checkpointFuel = 100;
        private bool[] checkpointTaken, checkpointBroken, checkpointActivated;
        private bool movedLeft, movedRight, shrunk, grown, brokeWall, pressedPlate, windSmall, windNormal, flightShrunk, landed;
        private int lessonFuel;

        public StuntRun(StuntCourse course)
        {
            Course = course;
            FuelTaken = new bool[course.Fuel.Count];
            checkpointTaken = new bool[FuelTaken.Length];
            Broken = new bool[course.Obstacles.Count]; checkpointBroken = new bool[Broken.Length];
            Activated = new bool[course.Plates.Count]; checkpointActivated = new bool[Activated.Length];
        }

        public string Instruction => !Course.Tutorial ? "A/D STEER    Q SHRINK    E GROW    ESC PAUSE    R RETRY" : new[] {
            "A / D or arrows: steer LEFT, then RIGHT. Forward driving is automatic.",
            "Q: shrink through the narrow gap. E: return to MEDIUM speed after it.",
            "E: grow to LARGE. Break the GOLD wall; solid orange walls cannot be broken.",
            "Collect the green F. Fuel drains during courses; small mode uses more fuel per metre.",
            "Grow LARGE to press the GOLD plate and open the door ahead.",
            "Try SMALL and MEDIUM in the wind. Small drifts more; grow to resist it.",
            "Approach the ramp at MEDIUM speed. Launch automatically; Q in flight to fit below the beam.",
            "Combine your skills: shrink through the gap, then pass the moving gate. Reach the finish!"
        }[Mathf.Clamp(Lesson, 0, 7)];

        public static Vector3 HalfSize(float scale) => new Vector3(.9f, .45f, 1.5f) * scale;
        public static bool Overlap(Vector3 a, Vector3 ah, float ay, Vector3 b, Vector3 bh, float by)
        {
            if (Mathf.Abs(a.y - b.y) >= ah.y + bh.y - .002f) return false;
            Vector3 ax = Quaternion.Euler(0, ay, 0) * Vector3.right;
            Vector3 az = Quaternion.Euler(0, ay, 0) * Vector3.forward;
            Vector3 bx = Quaternion.Euler(0, by, 0) * Vector3.right;
            Vector3 bz = Quaternion.Euler(0, by, 0) * Vector3.forward;
            Vector3 delta = b - a;
            return Axis(ax) && Axis(az) && Axis(bx) && Axis(bz);
            bool Axis(Vector3 n) => Mathf.Abs(Vector3.Dot(delta, n)) <
                ah.x * Mathf.Abs(Vector3.Dot(ax, n)) + ah.z * Mathf.Abs(Vector3.Dot(az, n)) +
                bh.x * Mathf.Abs(Vector3.Dot(bx, n)) + bh.z * Mathf.Abs(Vector3.Dot(bz, n)) - .002f;
        }

        public bool Clear(Vector3 bottom, float scale, float yaw, float clock)
        {
            Vector3 half = HalfSize(scale), centre = bottom + Vector3.up * half.y;
            for (int i = 0; i < Course.Obstacles.Count; i++)
            {
                var o = Course.Obstacles[i];
                if (Broken[i] || o.Plate >= 0 && Activated[o.Plate]) continue;
                if (Mathf.Abs(o.Center.z - bottom.z) > 20) continue;
                if (Overlap(centre, half, yaw, o.Position(clock), o.Size * .5f, o.Angle(clock))) return false;
            }
            return true;
        }

        public void Transform(bool shrink, bool grow)
        {
            if (!Started || Paused || Finished || RetryRemaining > 0) return;
            int next = Mathf.Clamp(SizeIndex + (grow ? 1 : 0) - (shrink ? 1 : 0), 0, 2);
            if (next == SizeIndex) return;
            float target = next == 0 ? SmallScale : next == 2 ? LargeScale : 1;
            if (CanTransform(target))
            {
                SizeIndex = next;
                shrunk |= next == 0;
                grown |= next >= 1 && shrunk && Position.z > 208;
                flightShrunk |= next == 0 && Airborne;
            }
            else Notify("NOT ENOUGH ROOM TO GROW");
        }

        private bool CanTransform(float scale)
        {
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f;
                if (!Clear(Position, Mathf.Lerp(Scale, scale, t), 0, Clock)) return false;
            }
            return true;
        }

        public void Advance(float delta, float steer)
        {
            if (!Started || Paused || Finished || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            delta = Mathf.Min(delta, .25f);
            Duration += delta;
            FeedbackTime = Mathf.Max(0, FeedbackTime - delta);
            if (RetryRemaining > 0)
            {
                RetryRemaining -= delta;
                if (RetryRemaining <= 0) Restore();
                return;
            }
            while (delta > .000001f && RetryRemaining <= 0 && !Finished)
            {
                float dt = Mathf.Min(delta, 1f / 240f);
                Step(dt, Mathf.Clamp(steer, -1, 1));
                delta -= dt;
            }
        }

        private void Step(float dt, float steer)
        {
            Clock += dt;
            movedLeft |= steer < -.1f; movedRight |= steer > .1f;
            float goalScale = SizeIndex == 0 ? SmallScale : SizeIndex == 2 ? LargeScale : 1;
            float scale = Mathf.MoveTowards(Scale, goalScale, dt * 2.25f);
            if (Clear(Position, scale, 0, Clock)) Scale = scale;
            else { SizeIndex = Scale < .8f ? 0 : Scale > 1.2f ? 2 : 1; Notify("TRANSFORMATION BLOCKED"); }
            float goalSpeed = SizeIndex == 0 ? SmallSpeed : SizeIndex == 2 ? LargeSpeed :
                Course.Environment == StuntEnvironment.City && Position.z > 445 && Position.z < 505 && Position.x > 1.5f ? 21 : NormalSpeed;
            if (Course.Tutorial && Lesson != 6) goalSpeed *= .7f;
            if (!Airborne) Speed = Mathf.MoveTowards(Speed <= 0 ? NormalSpeed : Speed, goalSpeed, dt * (goalSpeed < Speed ? 5 : 6));
            else Speed = launchSpeed;
            // At small speed, holding a direction traces a 45-degree path through angled slots.
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, steer * Mathf.Min(14, Speed), dt * 65);
            Vector3 wind = Vector3.zero;
            foreach (var w in Course.Winds) if (w.Contains(Position))
            {
                wind += w.Acceleration * (Small ? 4 : Large ? .25f : 1);
                windSmall |= Small; windNormal |= SizeIndex == 1;
            }
            windVelocity += wind.x * dt;
            // Drag gives each size a controllable terminal drift rather than unbounded wind speed.
            windVelocity *= Mathf.Exp(-dt * (Airborne ? 1.2f : 2f));
            Vector3 next = Position + new Vector3((lateralVelocity + windVelocity) * dt, 0, Speed * dt);
            foreach (var jump in Course.Jumps)
                if (!Airborne && Position.z < jump.Takeoff && next.z >= jump.Takeoff)
                { Airborne = true; launchSpeed = Speed; verticalVelocity = jump.LaunchUp; next.y = 1.4f; }
            if (Airborne)
            {
                verticalVelocity += (-9.81f + wind.y) * dt;
                next.y += verticalVelocity * dt;
                if (next.y <= Course.Surface(next.z) && Course.HasRoad(next.z) && verticalVelocity < 0)
                {
                    next.y = Course.Surface(next.z); Airborne = false; landed = true;
                    if (Mathf.Abs(next.x) + HalfWidth(Scale, Yaw) > 5) { Fail("MISSED THE LANDING"); return; }
                }
                if (next.y < -4) { Fail("MISSED THE LANDING - LAUNCH AT NORMAL SPEED"); return; }
            }
            else
            {
                next.y = Course.Surface(next.z);
                if (!Course.HasRoad(next.z)) { Fail("NO ROAD"); return; }
            }
            if (Mathf.Abs(next.x) + HalfWidth(Scale, Yaw) > 5 && !Airborne) { Fail("OFF THE TRACK - COUNTERSTEER OR GROW"); return; }
            for (int i = 0; i < Broken.Length; i++)
            {
                var obstacle = Course.Obstacles[i];
                if (obstacle.Weak && !Broken[i] && Large && Scale > 1.32f &&
                    Overlap(next + Vector3.up * HalfSize(Scale).y, HalfSize(Scale), 0,
                        obstacle.Position(Clock), obstacle.Size * .5f, obstacle.Angle(Clock)))
                { Broken[i] = true; brokeWall = true; Notify("SUPPORT BROKEN - TWO ROUTES AHEAD"); }
            }
            if (!Clear(next, Scale, 0, Clock)) { Fail("HIT - CHOOSE A CLEAR OPENING"); return; }
            Position = next;
            for (int i = 0; i < Activated.Length; i++)
                if (!Activated[i] && Large && Scale > 1.32f && Mathf.Abs(next.z - Course.Plates[i]) < 1.5f && Mathf.Abs(next.x) < 3)
                { Activated[i] = true; pressedPlate = true; Notify("HEAVY PLATE ACTIVATED - DOOR OPEN"); }
            Fuel.Tick(dt, Course.Tutorial ? 0 : Large ? 3.5f : 2);
            if (Course.Tutorial && Lesson == 3 && Collected == lessonFuel) Fuel.Consume(Mathf.Min(dt * 10, Mathf.Max(0, Fuel.CurrentFuel - 25)));
            for (int i = 0; i < FuelTaken.Length; i++)
                if (!FuelTaken[i] && Mathf.Abs(next.z - Course.Fuel[i].z) < 1.8f && Mathf.Abs(next.x - Course.Fuel[i].x) < 1.25f && next.y < 1.5f)
                { FuelTaken[i] = true; Collected++; Fuel.Refill(30); Notify("FUEL +30"); }
            if (Fuel.IsEmpty) { Fail("OUT OF FUEL - COLLECT GREEN F PICKUPS"); return; }
            if (Course.Tutorial)
            {
                float end = (Lesson + 1) * 140;
                if (Position.z >= end - 8)
                {
                    if (!LessonComplete()) { Fail("TRY THIS LESSON AGAIN: " + Instruction); return; }
                    if (Lesson == 7) { Finish(); return; }
                    Lesson++; Checkpoint = Lesson;
                    Snapshot(); Restore(); Notify("LESSON " + (Lesson + 1) + " / 8");
                }
            }
            else
            {
                if (Checkpoint + 1 < Course.Checkpoints.Count && Position.z >= Course.Checkpoints[Checkpoint + 1])
                { Checkpoint++; Snapshot(); Notify("CHECKPOINT"); }
                if (Position.z >= Course.Length) Finish();
            }
        }

        private bool LessonComplete() => Lesson == 0 ? movedLeft && movedRight : Lesson == 1 ? shrunk && grown :
            Lesson == 2 ? brokeWall : Lesson == 3 ? Collected > lessonFuel : Lesson == 4 ? pressedPlate : Lesson == 5 ? windSmall && windNormal :
            Lesson == 6 ? flightShrunk && landed : true;
        public static float HalfWidth(float scale, float yaw) => scale * (.9f * Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)) + 1.5f * Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)));
        private void Snapshot() { checkpointClock = Clock; checkpointFuel = Fuel.CurrentFuel; Array.Copy(FuelTaken, checkpointTaken, FuelTaken.Length); Array.Copy(Broken, checkpointBroken, Broken.Length); Array.Copy(Activated, checkpointActivated, Activated.Length); }
        private void Finish() { Finished = true; Notify("COURSE COMPLETE"); }
        public void Notify(string message) { Feedback = message; FeedbackTime = 3; }
        public void Fail(string reason) { if (RetryRemaining > 0 || Finished) return; Crashes++; RetryRemaining = .45f; Notify(reason); }
        public void Retry() { if (!Started || Finished) return; Paused = false; Fail("RETRY CHECKPOINT"); }
        private void Restore()
        {
            Position = new Vector3(0, 0, Course.Checkpoints[Checkpoint]);
            SizeIndex = 1; Airborne = false; Scale = 1; Speed = NormalSpeed;
            lateralVelocity = windVelocity = verticalVelocity = launchSpeed = 0;
            Clock = checkpointClock; Fuel.Reset(Mathf.Max(60, checkpointFuel));
            Array.Copy(checkpointTaken, FuelTaken, FuelTaken.Length);
            Array.Copy(checkpointBroken, Broken, Broken.Length); Array.Copy(checkpointActivated, Activated, Activated.Length);
            movedLeft = movedRight = shrunk = grown = brokeWall = pressedPlate = windSmall = windNormal = flightShrunk = landed = false;
            lessonFuel = Collected;
        }
    }
}
