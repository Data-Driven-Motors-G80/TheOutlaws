using System;
using UnityEngine;

namespace Outlaws.TransformStunt
{
    /// <summary>One deterministic simulation owns motion, oriented collisions and checkpoint state.</summary>
    public sealed class StuntRun
    {
        public static readonly float[] Angles = { 0, -45, 90, 45 };
        public static readonly string[] AngleNames = { "STRAIGHT", "LEFT 45", "SIDEWAYS", "RIGHT 45" };
        public const float SmallScale = .55f, NormalSpeed = 18, SmallSpeed = 10;
        public readonly StuntCourse Course;
        public readonly FuelState Fuel = new FuelState();
        public Vector3 Position { get; private set; }
        public float Scale { get; private set; } = 1;
        public float Yaw { get; private set; }
        public int AngleIndex { get; private set; }
        public bool Small { get; private set; }
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
        public string Feedback { get; private set; } = "";
        public float FeedbackTime { get; private set; }
        public float RetryRemaining { get; private set; }
        private float lateralVelocity, windVelocity, verticalVelocity, launchSpeed, checkpointClock, checkpointFuel = 100;
        private bool[] checkpointTaken;
        private bool movedLeft, movedRight, shrunk, grown, rotated, windSmall, windNormal, flightShrunk, landed;
        private int lessonFuel;

        public StuntRun(StuntCourse course)
        {
            Course = course;
            FuelTaken = new bool[course.Fuel.Count];
            checkpointTaken = new bool[FuelTaken.Length];
        }

        public string Instruction => !Course.Tutorial ? "A/D STEER    SPACE SIZE    E ROTATE    ESC PAUSE    R RETRY" : new[] {
            "A / D or arrows: steer LEFT, then RIGHT. Forward driving is automatic.",
            "SPACE: shrink through the narrow gap. SPACE again after it to return to normal speed.",
            "SHRINK, then E for LEFT 45. Steer diagonally LEFT through the angled slot. Next angle is on the HUD.",
            "Collect the green F. Fuel drains during courses; small mode uses more fuel per metre.",
            "Steer through the moving opening. Shrinking gives you more room and a slower approach.",
            "Try BOTH sizes in the wind. Small cars drift more; normal cars resist it. Steer against the arrows.",
            "Approach the ramp NORMAL and STRAIGHT. Launch automatically; SPACE in flight to fit below the beam.",
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
            foreach (var o in Course.Obstacles)
            {
                if (Mathf.Abs(o.Center.z - bottom.z) > 20) continue;
                if (Overlap(centre, half, yaw, o.Position(clock), o.Size * .5f, o.Angle(clock))) return false;
            }
            return true;
        }

        public void Transform(bool size, bool rotate)
        {
            if (!Started || Paused || Finished || RetryRemaining > 0) return;
            if (size)
            {
                float target = Small ? 1 : SmallScale;
                if (CanTransform(target, Angles[AngleIndex]))
                {
                    Small = !Small;
                    shrunk |= Small;
                    grown |= !Small && shrunk && Position.z > 208;
                    flightShrunk |= Small && Airborne;
                }
                else Notify("NOT ENOUGH ROOM TO CHANGE SIZE");
            }
            if (rotate)
            {
                int next = (AngleIndex + 1) % Angles.Length;
                if (CanTransform(Small ? SmallScale : 1, Angles[next])) { AngleIndex = next; rotated = true; }
                else Notify("ROTATION BLOCKED - MOVE CLEAR FIRST");
            }
        }

        private bool CanTransform(float scale, float yaw)
        {
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f;
                if (!Clear(Position, Mathf.Lerp(Scale, scale, t), Mathf.LerpAngle(Yaw, yaw, t), Clock)) return false;
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
            float scale = Mathf.MoveTowards(Scale, Small ? SmallScale : 1, dt * 2.25f);
            float yaw = Mathf.MoveTowardsAngle(Yaw, Angles[AngleIndex], dt * 675);
            if (Clear(Position, scale, yaw, Clock)) { Scale = scale; Yaw = yaw; }
            else { Small = Scale < .775f; AngleIndex = NearestAngle(Yaw); Notify("TRANSFORMATION BLOCKED"); }
            Speed = Airborne ? launchSpeed : Mathf.Lerp(SmallSpeed, NormalSpeed, Mathf.InverseLerp(SmallScale, 1, Scale));
            if (Course.Tutorial && !Airborne && Lesson != 6) Speed *= .7f;
            // At small speed, holding a direction traces a 45-degree path through angled slots.
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, steer * Mathf.Min(14, Speed), dt * 65);
            Vector3 wind = Vector3.zero;
            foreach (var w in Course.Winds) if (w.Contains(Position))
            {
                wind += w.Acceleration * Mathf.Lerp(1, 4, Mathf.InverseLerp(1, SmallScale, Scale));
                windSmall |= Small; windNormal |= !Small;
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
            if (!Clear(next, Scale, Yaw, Clock)) { Fail("HIT - CHECK YOUR SIZE AND ANGLE"); return; }
            Position = next;
            Fuel.Tick(dt, Course.Tutorial ? 0 : 2);
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
            Lesson == 2 ? rotated : Lesson == 3 ? Collected > lessonFuel : Lesson == 5 ? windSmall && windNormal :
            Lesson == 6 ? flightShrunk && landed : true;
        private static int NearestAngle(float yaw)
        { int best = 0; for (int i = 1; i < 4; i++) if (Mathf.Abs(Mathf.DeltaAngle(yaw, Angles[i])) < Mathf.Abs(Mathf.DeltaAngle(yaw, Angles[best]))) best = i; return best; }
        public static float HalfWidth(float scale, float yaw) => scale * (.9f * Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)) + 1.5f * Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)));
        private void Snapshot() { checkpointClock = Clock; checkpointFuel = Fuel.CurrentFuel; Array.Copy(FuelTaken, checkpointTaken, FuelTaken.Length); }
        private void Finish() { Finished = true; Notify("COURSE COMPLETE"); }
        public void Notify(string message) { Feedback = message; FeedbackTime = 3; }
        public void Fail(string reason) { if (RetryRemaining > 0 || Finished) return; Crashes++; RetryRemaining = .45f; Notify(reason); }
        public void Retry() { if (!Started || Finished) return; Paused = false; Fail("RETRY CHECKPOINT"); }
        private void Restore()
        {
            Position = new Vector3(0, 0, Course.Checkpoints[Checkpoint]);
            Small = Airborne = false; Scale = 1; Yaw = 0; AngleIndex = 0;
            lateralVelocity = windVelocity = verticalVelocity = launchSpeed = 0;
            Clock = checkpointClock; Fuel.Reset(Mathf.Max(60, checkpointFuel));
            Array.Copy(checkpointTaken, FuelTaken, FuelTaken.Length);
            movedLeft = movedRight = shrunk = grown = rotated = windSmall = windNormal = flightShrunk = landed = false;
            lessonFuel = Collected;
        }
    }
}
