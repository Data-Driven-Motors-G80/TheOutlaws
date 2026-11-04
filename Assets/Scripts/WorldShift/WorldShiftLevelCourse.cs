using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public enum LevelSignKind { Steer, Switch, Dashes, Freeze, Ramp }

    public readonly struct LevelSign
    {
        public readonly float Distance;
        public readonly LevelSignKind Kind;
        public LevelSign(float distance, LevelSignKind kind) { Distance = distance; Kind = kind; }
    }

    /// <summary>
    /// One authored level: sections of Day/Night obstacles, each ending in a big ramp
    /// (launch in one world, switch midair, land in the other). Landings are checkpoints.
    /// Still obstacles belong to one world; anything sliding or rolling exists in both
    /// worlds and freezes in Night.
    /// </summary>
    public sealed class LevelCourse
    {
        public ShiftLevel Environment;
        public bool Training;
        public float Length;
        public ShiftRelay[] Ramps;
        public ShiftHazard[] Hazards;
        public ShiftCheckpoint[] Checkpoints;
        // Tutorial sections end at a plain gate instead of a ramp.
        public float[] Gates;
        public LevelSign[] Signs;

        public ShiftHazard[] CreateHazards()
        {
            // Motion state lives on each hazard, so every attempt gets fresh copies.
            return System.Array.ConvertAll(Hazards, h => new ShiftHazard(h.World, h.Kind, h.Start, h.End, h.LaneMask,
                h.MotionSpeed, h.HalfSide, h.SharedAcrossWorlds, h.LateralMotion));
        }

        public float Heading(float d)
        {
            float amplitude = Training ? 0f : Environment == ShiftLevel.City ? 14f
                : Environment == ShiftLevel.Desert ? 22f : Environment == ShiftLevel.Mountain ? 30f : 24f;
            return amplitude * Mathf.Sin(d * Mathf.PI * 2f / 420f) * Straight(d);
        }

        public float TerrainSlope(float d)
        {
            float raw = Environment == ShiftLevel.Desert ? .045f * Mathf.Sin(d * Mathf.PI * 2f / 520f)
                : Environment == ShiftLevel.Ocean ? .025f * Mathf.Sin(d * Mathf.PI * 2f / 340f)
                : Environment == ShiftLevel.Snow ? -.04f : Environment == ShiftLevel.Mountain ? .06f * Mathf.Sin(d * Mathf.PI * 2f / 600f) : 0f;
            return raw * Straight(d) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 120f, d));
        }

        // Ramps get a flat, straight approach and landing so every jump reads clearly.
        private float Straight(float d)
        {
            float clear = 1f;
            foreach (var ramp in Ramps)
            {
                float into = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ramp.Ramp - 40f, ramp.Ramp - 100f, d));
                float outOf = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ramp.End, ramp.End + 60f, d));
                clear *= d < ramp.Ramp ? into : d > ramp.End ? outOf : 0f;
            }
            return clear * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Length - 80f, Length - 20f, d)));
        }
    }

    /// <summary>Level 0 is training; levels 1+ follow Lydia's environments.</summary>
    public static class WorldShiftLevelCourse
    {
        private const float RampToEnd = 200f;

        private sealed class Builder
        {
            private readonly LevelCourse course = new LevelCourse();
            private readonly List<ShiftHazard> hazards = new List<ShiftHazard>();
            private readonly List<ShiftRelay> ramps = new List<ShiftRelay>();
            private readonly List<ShiftCheckpoint> checkpoints = new List<ShiftCheckpoint>();
            private readonly List<float> gates = new List<float>();
            private readonly List<LevelSign> signs = new List<LevelSign>();
            private float start;

            public Builder(ShiftLevel environment, bool training = false)
            {
                course.Environment = environment;
                course.Training = training;
                checkpoints.Add(new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "", ""));
            }

            // Offsets are metres from the start of the current section.
            public Builder Day(float at, int lanes) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, start + at, start + at + 6f, lanes));
            public Builder Night(float at, int lanes) => Add(new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, start + at, start + at + 6f, lanes));
            public Builder DayHalf(float at, int side) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, start + at, start + at + 6f, side < 0 ? 3 : 6, halfSide: side));
            public Builder NightHalf(float at, int side) => Add(new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, start + at, start + at + 6f, side < 0 ? 3 : 6, halfSide: side));
            // Slides across the road in Day; freezes in place in Night.
            public Builder Slider(float at, float speed) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, start + at, start + at + 6f, 2, speed,
                sharedAcrossWorlds: true, lateralMotion: true));
            // Traffic in one world drives toward the player. "at" is roughly where they meet.
            public Builder DayCar(float at, int lane, float speed = 14f) => Add(Oncoming(ShiftWorld.Day, at, lane, speed, false));
            public Builder NightCar(float at, int lane, float speed = 14f) => Add(Oncoming(ShiftWorld.Echo, at, lane, speed, false));
            // A rolling rock exists in both worlds: it rolls in Day and stops in Night.
            public Builder Rock(float at, int lane, float speed = 10f) => Add(Oncoming(ShiftWorld.Day, at, lane, speed, true));
            public Builder Sign(float at, LevelSignKind kind) { signs.Add(new LevelSign(start + at, kind)); return this; }

            private ShiftHazard Oncoming(ShiftWorld world, float at, int lane, float speed, bool shared)
            {
                // Traffic waits 150 m ahead and closes at speed/28 of the player's progress.
                float k = speed / WorldShiftLevel.Speed;
                float spawn = start + at + 150f * k / (1f + k);
                return new ShiftHazard(world, ShiftHazardKind.Oncoming, spawn, spawn + 5.5f, lane, speed, sharedAcrossWorlds: shared);
            }

            private Builder Add(ShiftHazard hazard) { hazards.Add(hazard); return this; }

            /// <summary>Ends the section with a big ramp at "rampAt" launching from "from".</summary>
            public Builder Ramp(float rampAt, ShiftWorld from)
            {
                float ramp = start + rampAt;
                var relay = new ShiftRelay(ramps.Count + 1, ramp, ramp + 60f, ramp + 144f, from);
                ramps.Add(relay);
                checkpoints.Add(new ShiftCheckpoint(relay.End, relay.Destination, 0f, "", ""));
                start = ramp + RampToEnd;
                return this;
            }

            /// <summary>Ends a training section at a plain checkpoint gate.</summary>
            public Builder Gate(float at, ShiftWorld world)
            {
                gates.Add(start + at);
                checkpoints.Add(new ShiftCheckpoint(start + at, world, 0f, "", ""));
                start += at;
                return this;
            }

            public LevelCourse Build()
            {
                course.Ramps = ramps.ToArray();
                course.Hazards = hazards.ToArray();
                // The final landing is the finish line, not another checkpoint.
                if (ramps.Count > 0 && checkpoints[checkpoints.Count - 1].Distance == ramps[ramps.Count - 1].End)
                    checkpoints.RemoveAt(checkpoints.Count - 1);
                course.Checkpoints = checkpoints.ToArray();
                course.Gates = gates.ToArray();
                course.Signs = signs.ToArray();
                course.Length = ramps.Count > 0 ? ramps[ramps.Count - 1].Landing + 40f : start;
                return course;
            }
        }

        private const int Left = 1, Middle = 2, Right = 4, LeftMiddle = 3, MiddleRight = 6, Sides = 5, All = 7;
        private static readonly ShiftWorld D = ShiftWorld.Day, N = ShiftWorld.Echo;

        public static LevelCourse For(int level)
        {
            switch (level)
            {
                case 0: return Training();
                case 1: return City();
                case 2: return Desert();
                case 3: return Ocean();
                case 4: return Snow();
                default: return Mountain();
            }
        }

        // One idea per section, introduced by a highway sign.
        private static LevelCourse Training() => new Builder(ShiftLevel.City, true)
            .Sign(25f, LevelSignKind.Steer).Day(90f, Middle).Day(140f, LeftMiddle).Gate(180f, D)
            .Sign(25f, LevelSignKind.Switch).Day(110f, All).Gate(170f, N)
            .Sign(25f, LevelSignKind.Dashes).Day(70f, All).Night(120f, All).Gate(180f, D)
            .Sign(25f, LevelSignKind.Freeze).Slider(110f, 1.1f).Slider(150f, -1.3f).Gate(210f, N)
            .Sign(25f, LevelSignKind.Ramp).Night(70f, All).Ramp(120f, D)
            .Build();

        private static LevelCourse City() => new Builder(ShiftLevel.City)
            .Day(50f, Middle).Day(100f, All).Night(150f, LeftMiddle).Night(200f, All).Ramp(250f, D)
            .Night(50f, Right).Day(90f, All).Night(135f, All).Day(180f, LeftMiddle).Day(220f, All).Ramp(260f, N)
            .DayCar(60f, Middle).Night(60f, Sides).NightCar(130f, Left).Day(130f, All).Night(190f, All).Ramp(240f, D)
            .Slider(60f, 1f).Day(120f, All).Slider(170f, 1.25f).Day(220f, All).Ramp(260f, N)
            .Night(50f, All).Day(90f, LeftMiddle).Day(130f, MiddleRight).Day(170f, All).NightCar(210f, Middle).Night(250f, All).Ramp(290f, D)
            .Build();

        // Desert's sliding rocks are the featured hazard: freeze them in Night.
        private static LevelCourse Desert() => new Builder(ShiftLevel.Desert)
            .Slider(60f, .9f).Slider(110f, -1.1f).Night(160f, All).Slider(205f, 1.3f).Ramp(250f, D)
            .Rock(50f, Middle, 8f).Day(100f, All).Slider(140f, 1f).Slider(180f, -1.2f).Day(220f, All).Ramp(260f, N)
            .Slider(60f, 1f).Night(80f, Right).Slider(110f, -1.2f).Night(160f, All).Rock(205f, Middle).Ramp(250f, D)
            .Day(50f, All).NightHalf(90f, -1).Slider(130f, 1.1f).Slider(155f, -1.1f).Day(195f, All).Ramp(240f, N)
            .Slider(50f, 1.5f).Slider(72f, -1.5f).Slider(94f, 1.5f).Night(140f, All).Day(180f, LeftMiddle).Rock(195f, Left).Night(230f, All).Ramp(270f, D)
            .Build();

        // First-pass translations of the earlier Ocean/Snow/Mountain courses for the team to tune.
        private static LevelCourse Ocean() => new Builder(ShiftLevel.Ocean)
            .Slider(60f, .75f).Slider(110f, .95f).Night(160f, All).Slider(210f, -1.15f).Ramp(250f, D)
            .Day(50f, All).Night(100f, LeftMiddle).Day(150f, All).Night(190f, MiddleRight).Day(215f, All).Ramp(260f, N)
            .Slider(60f, 1.25f).Slider(110f, -1.4f).Night(160f, All).DayCar(200f, Middle).Ramp(250f, D)
            .Day(50f, LeftMiddle).Night(90f, MiddleRight).Day(130f, All).Night(180f, LeftMiddle).Day(220f, All).Ramp(260f, N)
            .Night(50f, All).Day(100f, LeftMiddle).Day(150f, MiddleRight).Slider(195f, 1.3f).Night(235f, All).Ramp(280f, D)
            .Build();

        private static LevelCourse Snow() => new Builder(ShiftLevel.Snow)
            .Day(60f, Left).Night(110f, Right).Slider(160f, .65f).Night(210f, All).Ramp(250f, D)
            .Day(50f, All).Night(100f, Left).Slider(150f, .75f).Day(220f, All).Ramp(260f, N)
            .Day(60f, LeftMiddle).Night(110f, MiddleRight).Day(160f, All).Night(210f, All).Ramp(250f, D)
            .Night(50f, Right).Day(90f, All).Slider(140f, -.8f).Day(200f, All).Ramp(240f, N)
            .Night(50f, All).Day(100f, LeftMiddle).Night(110f, Right).Day(150f, All).Night(200f, All).Ramp(240f, D)
            .Build();

        // Mountain keeps its half-walls and rolling rocks.
        private static LevelCourse Mountain() => new Builder(ShiftLevel.Mountain)
            .DayHalf(60f, -1).NightHalf(110f, 1).Rock(160f, Middle, 12f).Night(210f, All).Ramp(250f, D)
            .Day(50f, All).Rock(100f, Middle, 8f).NightHalf(150f, -1).Day(220f, All).Ramp(260f, N)
            .Rock(60f, Left, 12f).DayHalf(110f, 1).Night(160f, All).Rock(205f, Right, 10f).Ramp(250f, D)
            .Day(50f, All).NightHalf(100f, 1).Rock(150f, Middle, 11f).Day(220f, All).Ramp(260f, N)
            .DayHalf(50f, -1).Night(100f, All).DayHalf(150f, 1).Rock(190f, Middle, 11f).Night(230f, All).Ramp(270f, D)
            .Build();
    }
}
