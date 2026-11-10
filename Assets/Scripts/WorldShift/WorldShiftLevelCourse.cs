using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public enum LevelSignKind { Steer, Switch, Dashes, Freeze, Ramp, AirFreeze, AirRelease, Oncoming }

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
        // Required Ocean items exist in both worlds and never act as hazards.
        public Vector2[] RequiredItems = System.Array.Empty<Vector2>();
        public LevelSign[] Signs;
        public Vector2[] DustZones = System.Array.Empty<Vector2>();

        public ShiftHazard[] CreateHazards()
        {
            // Motion state lives on each hazard, so every attempt gets fresh copies.
            return System.Array.ConvertAll(Hazards, h => new ShiftHazard(h.World, h.Kind, h.Start, h.End, h.LaneMask,
                h.MotionSpeed, h.HalfSide, h.SharedAcrossWorlds, h.LateralMotion, h.Rock?.Copy(), h.Gate?.Copy()));
        }

        public float Heading(float d)
        {
            if (!Training && Environment == ShiftLevel.Desert) return DesertHeading(d);
            float amplitude = Training ? 0f : Environment == ShiftLevel.City ? 14f
                : Environment == ShiftLevel.Mountain ? 30f : 24f;
            return amplitude * Mathf.Sin(d * Mathf.PI * 2f / 420f) * Straight(d);
        }

        private float DesertHeading(float d)
        {
            float from = 40f;
            for (int i = 0; i < Ramps.Length; i++)
            {
                float to = Ramps[i].Ramp - 80f;
                if (d >= from && d <= to)
                {
                    float Smooth(float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, d));
                    // The longer storm stretch gets a gentle two-sided canyon S-bend.
                    if (i == 4)
                        return 20f * Smooth(from, from + 70f) - 34f * Smooth(from + 110f, to - 110f)
                            + 14f * Smooth(to - 70f, to);
                    // Reuse the old desert's roughly 80 m bends, with alternating directions.
                    float angle = i == 0 ? 28f : i == 1 ? -30f : i == 2 ? 32f : i == 3 ? -28f : -32f;
                    return angle * (Smooth(from, from + 80f) - Smooth(to - 80f, to));
                }
                from = Ramps[i].End + 45f;
            }
            return 0f;
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
        // Non-monotonic phases scatter neighbouring spheres without rerolling a retry.
        private static readonly float[] FinalRockPhases = { -1.6f, 1.4f, -.8f, 1.6f, -.3f };

        private sealed class Builder
        {
            private readonly LevelCourse course = new LevelCourse();
            private readonly List<ShiftHazard> hazards = new List<ShiftHazard>();
            private readonly List<ShiftRelay> ramps = new List<ShiftRelay>();
            private readonly List<ShiftCheckpoint> checkpoints = new List<ShiftCheckpoint>();
            private readonly List<float> gates = new List<float>();
            private readonly List<LevelSign> signs = new List<LevelSign>();
            private readonly List<Vector2> items = new List<Vector2>();
            private readonly List<Vector2> dustZones = new List<Vector2>();
            private readonly bool landingCheckpoints;
            private float start;

            public Builder(ShiftLevel environment, bool training = false, bool landingCheckpoints = true)
            {
                course.Environment = environment;
                course.Training = training;
                this.landingCheckpoints = landingCheckpoints;
                checkpoints.Add(new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "", ""));
            }

            // Offsets are metres from the start of the current section.
            public Builder Day(float at, int lanes) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, start + at, start + at + 6f, lanes));
            public Builder Permanent(float at, int lanes) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier,
                start + at, start + at + 6f, lanes, sharedAcrossWorlds: true));
            public Builder SwitchWall(float at) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.SwitchWall,
                start + at, start + at + 6f, 7));
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
            public Builder Crossing(float at, float phase, float direction, float speed)
                => AddRock(new WorldShiftLevelRock(start + at, phase, direction, speed));
            public Builder SlidingGate(float at, float direction) => AddGate(new WorldShiftSlidingGate(start + at, direction));
            private Builder AddGate(WorldShiftSlidingGate gate) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.SlidingGate,
                gate.Distance - gate.Size.z * .5f, gate.Distance + gate.Size.z * .5f, 7, gate.Speed,
                sharedAcrossWorlds: true, lateralMotion: true, gate: gate));
            private Builder AddRock(WorldShiftLevelRock rock) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.RollingRock,
                rock.Distance - rock.Radius, rock.Distance + rock.Radius, 2, rock.Motion.MotionSpeed,
                sharedAcrossWorlds: true, lateralMotion: true, rock: rock));
            public Builder Sign(float at, LevelSignKind kind) { signs.Add(new LevelSign(start + at, kind)); return this; }
            public Builder Item(float at, int lane) { items.Add(new Vector2(start + at, lane * WorldShiftLevel.RoadWidth / 3f)); return this; }
            public Builder Dust(float from, float to) { dustZones.Add(new Vector2(start + from, start + to)); return this; }
            public Builder Checkpoint(float at, ShiftWorld world, float lateral = 0f)
            { checkpoints.Add(new ShiftCheckpoint(start + at, world, lateral, "", "")); return this; }

            private ShiftHazard Oncoming(ShiftWorld world, float at, int lane, float speed, bool shared)
            {
                // Traffic waits 150 m ahead and closes at speed/28 of the player's progress.
                float k = speed / WorldShiftLevel.Speed;
                float spawn = start + at + 150f * k / (1f + k);
                return new ShiftHazard(world, ShiftHazardKind.Oncoming, spawn, spawn + 5.5f, lane, speed, sharedAcrossWorlds: shared);
            }

            private Builder Add(ShiftHazard hazard) { hazards.Add(hazard); return this; }

            /// <summary>Ends the section with a big ramp at "rampAt" launching from "from".</summary>
            public Builder Ramp(float rampAt, ShiftWorld from, int airRocks = 0, float flightLength = 84f, bool releaseGate = false)
            {
                float ramp = start + rampAt;
                var relay = new ShiftRelay(ramps.Count + 1, ramp, ramp + 60f, ramp + 60f + flightLength, from);
                ramps.Add(relay);
                int count = airRocks;
                if (releaseGate)
                    AddGate(new WorldShiftSlidingGate(relay.Takeoff + 56f, ramps.Count == 4 ? -1f : 1f, relay));
                for (int i = 0; i < count; i++)
                {
                    float phase = i % 4 == 0 ? -2.6f : i % 4 == 1 ? 1.1f : i % 4 == 2 ? 2.3f : -1.8f;
                    if (releaseGate)
                    {
                        // Early balls are on the side opposite this gate's right-hand exit.
                        AddRock(new WorldShiftLevelRock(relay.Takeoff + 24f + i * 14f, phase,
                            i % 2 == 0 ? 1f : -1f, 18f, flight: relay,
                            laneCentre: i == 0 ? -3f : -1.4f, travelScale: .12f));
                    }
                    else if (count >= 7)
                    {
                        // Each wave sweeps all three lanes. Shuffled, larger phase offsets
                        // break the smooth column while retaining an outside frozen bypass.
                        int withinWave = i % 5;
                        float at = i < 5 ? 24f + withinWave * 10f : 130f + withinWave * 10f;
                        int phaseIndex = (withinWave + (i < 5 ? 0 : 2)) % FinalRockPhases.Length;
                        float wavePhase = FinalRockPhases[phaseIndex] + (i < 5 ? 0f : .25f);
                        AddRock(new WorldShiftLevelRock(relay.Takeoff + at, wavePhase, i < 5 ? 1f : -1f,
                            11.5f, flight: relay, travelScale: .9f));
                    }
                    else
                    {
                        // Different depths keep every pair apart even when their x paths cross.
                        float at = count == 2 ? (i == 0 ? 26f : 58f)
                            : count == 3 ? 22f + i * 20f : i == 0 ? 22f : i == 1 ? 28f : i == 2 ? 58f : 64f;
                        float lane = count == 2 ? (i == 0 ? -2.8f : .4f)
                            : i == 0 ? -3.1f : i == 1 ? -1.5f : i == 2 ? 3.1f : 1.5f;
                        AddRock(new WorldShiftLevelRock(relay.Takeoff + at, phase, i % 2 == 0 ? 1f : -1f,
                            18f, flight: relay, laneCentre: lane, travelScale: .18f));
                    }
                }
                if (landingCheckpoints) checkpoints.Add(new ShiftCheckpoint(relay.End, relay.Destination, 0f, "", ""));
                start = relay.End;
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
                course.DustZones = dustZones.ToArray();
                course.RequiredItems = items.ToArray();
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
            .Sign(25f, LevelSignKind.Steer).Permanent(90f, Middle).Permanent(140f, LeftMiddle).Gate(180f, D)
            .Sign(25f, LevelSignKind.Switch).SwitchWall(110f).Gate(170f, N)
            .Sign(25f, LevelSignKind.Dashes).Day(70f, All).Night(120f, All).Gate(180f, D)
            .Sign(25f, LevelSignKind.Oncoming).Rock(110f, Middle, 10f).Gate(170f, D)
            .Sign(25f, LevelSignKind.Freeze).Slider(110f, 1.1f).Slider(160f, -1.3f).Slider(210f, 1.1f).Gate(270f, N)
            .Sign(25f, LevelSignKind.Ramp).Night(70f, All).Ramp(120f, D)
            .Build();

        private static LevelCourse City() => new Builder(ShiftLevel.City)
            .Permanent(50f, Middle).Day(100f, All).Night(150f, LeftMiddle).Night(200f, All).Ramp(250f, D)
            .Night(50f, Right).Day(90f, All).Night(135f, All).Day(180f, LeftMiddle).Day(220f, All).Ramp(260f, N)
            .DayCar(60f, Middle).Night(60f, Sides).NightCar(130f, Left).Day(130f, All).Night(190f, All).Ramp(240f, D)
            .Slider(60f, 1f).Day(120f, All).Slider(170f, 1.25f).Day(220f, All).Ramp(260f, N)
            .Night(50f, All).Day(90f, LeftMiddle).Day(130f, MiddleRight).Day(170f, All).NightCar(210f, Middle).Night(250f, All).Ramp(290f, D)
            .Build();

        // Six freeze/release puzzles, including a separate dust-field checkpoint section.
        private static LevelCourse Desert() => new Builder(ShiftLevel.Desert, landingCheckpoints: false)
            // 1: crossing rocks, alternate worlds, then freeze the first aerial pair.
            .Crossing(60f, 4.1f, 1f, 3.4f).Day(105f, LeftMiddle).Night(150f, MiddleRight)
            .Crossing(195f, 4.1f, -1f, 3.8f).Day(240f, All).Night(285f, All).Slider(300f, 1.4f)
            .Ramp(340f, D, 2)
            // 2: release a badly frozen rock, then freeze the central Night opening.
            .Crossing(55f, 4.1f, -1f, 4f).Day(100f, All)
            .Day(145f, All).Crossing(175f, 0f, 1f, 4.2f).Night(175f, Sides)
            .Day(225f, LeftMiddle).SlidingGate(240f, -1f).Day(295f, All)
            .Checkpoint(270f, N).Ramp(340f, N, releaseGate: true)
            // 3: staggered opposing motion followed by another freeze window.
            .Day(55f, All).Crossing(105f, 4.1f, 1f, 4.5f).Night(105f, Sides)
            .Crossing(150f, 1.5f, -1f, 4.8f).Day(195f, All).SlidingGate(235f, 1f)
            .Night(280f, All).Crossing(300f, 6.5f, 1f, 4.5f).Checkpoint(270f, D).Ramp(340f, D, 3)
            // 4: shared rocks and narrow world-specific routes cannot be skipped by staying in Night.
            .Night(55f, All).SlidingGate(95f, 1f).Day(135f, MiddleRight)
            .Crossing(175f, 4.1f, 1f, 5f).Night(175f, LeftMiddle).Day(215f, All)
            .Slider(255f, -1.8f).Crossing(235f, 2.8f, -1f, 4.8f).Checkpoint(270f, N).Ramp(340f, N, 2, releaseGate: true)
            // 5: nineteen rocks, two shuttling blocks and clear windows to wake motion again.
            .Checkpoint(0f, D).Dust(40f, 435f).Day(55f, All)
            .NightHalf(72f, 1)
            .Crossing(85f, 3.5f, 1f, 4.4f).Crossing(96f, 3.2f, 1f, 4.5f).Crossing(108f, 2.2f, -1f, 4.5f)
            .Crossing(120f, 2.6f, -1f, 4.6f).Crossing(131f, .4f, 1f, 4.6f).NightHalf(140f, -1)
            .Crossing(154f, -2.5f, 1f, 4.5f).Crossing(166f, -2.8f, 1f, 4.5f).Crossing(177f, 3.1f, -1f, 4.4f)
            .Night(190f, All).SlidingGate(225f, -1f).Day(250f, All)
            .Crossing(258f, 2.6f, 1f, 4.7f).Crossing(270f, 2.9f, 1f, 4.7f).Crossing(281f, -.7f, 1f, 4.8f).NightHalf(286f, 1)
            .Crossing(293f, -1f, 1f, 4.8f).Crossing(304f, -2.8f, 1f, 4.7f)
            .Crossing(316f, 2.4f, -1f, 4.7f).Crossing(327f, 2.1f, -1f, 4.6f)
            .Night(338f, All).SlidingGate(370f, 1f)
            .Crossing(390f, 3.4f, -1f, 4.8f).Crossing(402f, .3f, -1f, 5f)
            .Crossing(414f, 2.8f, 1f, 4.8f).Crossing(426f, 3.1f, 1f, 4.8f)
            .Checkpoint(430f, D).Ramp(500f, D, 4)
            // 6: alternating closures and the longer ten-ball Day launch finale.
            .Crossing(55f, 3.1f, -1f, 5f).Crossing(82f, .6f, 1f, 5.2f).Night(115f, All)
            .Crossing(150f, 4.1f, -1f, 5.2f).Day(195f, All)
            .Crossing(240f, 4.1f, 1f, 5.3f).Night(240f, Sides)
            .SlidingGate(270f, -1f).Night(320f, All).Checkpoint(290f, D).Ramp(360f, D, 10, 196f)
            .Build();

        // Each landing banks a completed collection section. Items stay clear of jumps
        // and give at least 35 m to read a hazard or steer toward the next target.
        private static LevelCourse Ocean() => new Builder(ShiftLevel.Ocean)
            // 1: learn the diamond on a clear approach, then reuse switching and freezing.
            .Item(100f, 0)
            .Day(155f, All).Slider(205f, .95f).Night(255f, All).Ramp(310f, D)
            // 2: steer to both sides; the diamonds remain collectible in either world.
            .Item(85f, -1).Day(135f, All)
            .Item(190f, 1).Night(240f, LeftMiddle).Day(285f, All).Ramp(340f, N)
            // 3: freeze the movers, then line up for each required item.
            .Slider(55f, 1.25f).Item(110f, 1).Night(160f, All)
            .Slider(210f, -1.4f).Item(265f, -1).Ramp(330f, D)
            // 4: collect through alternating world closures with room to change lane.
            .Item(65f, -1).Day(110f, All).Item(160f, 0)
            .Night(210f, All).Item(260f, 1).Day(305f, All).Ramp(360f, N)
            // 5: combine collection, world choice and freezing before the last jump.
            .Night(50f, All).Item(100f, 1).Day(145f, LeftMiddle)
            .Item(195f, -1).Slider(245f, 1.3f).Item(300f, 0)
            .Night(350f, All).Ramp(405f, D)
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
