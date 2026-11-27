using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public enum LevelSignKind { Steer, Switch, Freeze = 3, Ramp, AirFreeze, AirRelease, Oncoming }

    public readonly struct LevelSign
    {
        public readonly float Distance;
        public readonly LevelSignKind Kind;
        public readonly int CheckpointIndex;
        public LevelSign(float distance, LevelSignKind kind, int checkpointIndex = 0)
        { Distance = distance; Kind = kind; CheckpointIndex = checkpointIndex; }
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
        // Authored pickup positions; Ocean uses optional Day-only ammunition.
        public Vector2[] RequiredItems = System.Array.Empty<Vector2>();
        public float[] AirStrikeTargets = System.Array.Empty<float>();
        public float[] AirStrikeEnds = System.Array.Empty<float>();
        public bool[] AirStrikeCurtains = System.Array.Empty<bool>();
        public LevelSign[] Signs;
        public Vector2[] DustZones = System.Array.Empty<Vector2>();
        // Snow: road stretches of black ice (x = from, y = to). See WorldShiftCity.Ice.cs.
        public Vector2[] IceZones = System.Array.Empty<Vector2>();
        // Mountain: guardrail gaps on the valley side of bends (x = from, y = to, z = side: -1 left, +1 right).
        // Driving off the road edge inside one is a fall and a checkpoint retry. See WorldShiftCity.StepSimulation.
        public Vector3[] EdgeGaps = System.Array.Empty<Vector3>();

        public bool EdgeGapAt(float d, float side)
        {
            foreach (var gap in EdgeGaps) if (d >= gap.x && d < gap.y && side * gap.z > 0f) return true;
            return false;
        }

        public bool IceAt(float d)
        {
            foreach (var zone in IceZones) if (d >= zone.x && d < zone.y) return true;
            return false;
        }

        // Practice sections can keep their course/checkpoint without another instruction stop.
        public int SignIndexForCheckpoint(int checkpoint)
        {
            for (int i = 0; i < Signs.Length; i++)
                if (Signs[i].CheckpointIndex == checkpoint) return i;
            return -1;
        }

        public ShiftHazard[] CreateHazards()
        {
            // Motion state lives on each hazard, so every attempt gets fresh copies.
            return System.Array.ConvertAll(Hazards, h => new ShiftHazard(h.World, h.Kind, h.Start, h.End, h.LaneMask,
                h.MotionSpeed, h.HalfSide, h.SharedAcrossWorlds, h.LateralMotion, h.Rock?.Copy(), h.Gate?.Copy())
                { MovesWheneverDay = h.MovesWheneverDay });
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

        // Mountain climbs and descends more steeply than other levels, and the car feels it (GradeSpeedFactor).
        public const float MountainGrade = .085f, MountainGradeSpeed = 1.6f;
        /// <summary>Mountain cruise speed scale: slower climbing, faster descending. 1 elsewhere and on jump approaches.</summary>
        public float GradeSpeedFactor(float d) => Environment == ShiftLevel.Mountain && !Training ? 1f - MountainGradeSpeed * TerrainSlope(d) : 1f;
        public float TerrainSlope(float d)
        {
            float raw = Environment == ShiftLevel.Desert ? .045f * Mathf.Sin(d * Mathf.PI * 2f / 520f)
                : Environment == ShiftLevel.Ocean ? .025f * Mathf.Sin(d * Mathf.PI * 2f / 340f)
                : Environment == ShiftLevel.Snow ? -.04f : Environment == ShiftLevel.Mountain ? MountainGrade * Mathf.Sin(d * Mathf.PI * 2f / 600f) : 0f;
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
            private readonly List<float> airStrikes = new List<float>();
            private readonly List<float> airStrikeEnds = new List<float>();
            private readonly List<bool> airStrikeCurtains = new List<bool>();
            private readonly List<Vector2> dustZones = new List<Vector2>();
            private readonly List<Vector2> iceZones = new List<Vector2>();
            private readonly List<Vector3> edgeGaps = new List<Vector3>();
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
            public Builder Slider(float at, float speed) => Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, start + at + 2.2f, start + at + 3.8f, 2, speed,
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
            public Builder Sign(float at, LevelSignKind kind) { signs.Add(new LevelSign(start + at, kind, checkpoints.Count - 1)); return this; }
            public Builder Item(float at, int lane) { items.Add(new Vector2(start + at, lane * WorldShiftLevel.RoadWidth / 3f)); return this; }
            public Builder Dust(float from, float to) { dustZones.Add(new Vector2(start + from, start + to)); return this; }
            public Builder Ice(float from, float to) { iceZones.Add(new Vector2(start + from, start + to)); return this; }
            public Builder EdgeGap(float at, float length, int side) { edgeGaps.Add(new Vector3(start + at, start + at + length, side)); return this; }
            public Builder Checkpoint(float at, ShiftWorld world, float lateral = 0f)
            { checkpoints.Add(new ShiftCheckpoint(start + at, world, lateral, "", "")); return this; }

            private ShiftHazard Oncoming(ShiftWorld world, float at, int lane, float speed, bool shared)
            {
                // Traffic waits 150 m ahead and closes at speed/28 of the player's progress.
                float k = speed / WorldShiftLevel.Speed;
                float spawn = start + at + 150f * k / (1f + k);
                return new ShiftHazard(world, ShiftHazardKind.Oncoming, spawn, spawn + 5.5f, lane, speed, sharedAcrossWorlds: shared);
            }

            // Coastal attack corridors alternate with visible world-specific harbour closures.
            // All distances are authored once here; the boss consumes the same target list.
            public Builder OceanSection(int section, float rampAt, ShiftWorld launch)
            {
                Day(38f, Sides);
                // Let the first section introduce one hazard at a time; later sections keep the overlap.
                if(section>1) DayCar(62f, section % 2 == 0 ? Left : Right, 18f);
                Day(66f,section==1 ? Right : section%2==0 ? LeftMiddle : MiddleRight);
                float nightSpeed=section==1 ? 16f : 14f+section*6f;
                NightCar(100f, Middle, nightSpeed);
                if(section>1) NightCar(100f,section%2==0 ? Left : Right,nightSpeed);
                // Reuse the original teal police spike platform and its shared Day-move/Night-freeze rule.
                Slider(124f,section%2==0 ? -1.25f : 1.25f);
                Slider(347f,section%2==0 ? 1.4f : -1.4f);
                Item(95f, section % 2 == 0 ? -1 : 1);
                // A long point-bombing corridor replaces the second/third compact attacks.
                // Night weaves between opposite closures, then returns to Day through a cleared missile window.
                // The separate slow curtains still keep a completely clear Night route.
                int index=0;
                float lastDayRow=66f;
                for(float authoredAt=150f;authoredAt<=rampAt-80f;authoredAt+=72f,index++)
                {
                    if(index==2) continue; // consumed by the 108 m corridor starting at index 1
                    // Later checkpoints trade compact bombing pockets for a denser Night driving sequence.
                    // Routine Day missiles still run here, protecting the current Night opening.
                    if(section>=3 && index>=3) continue;
                    float at=authoredAt+(index>=3?8f:0f); // room for the second spike platform after the long field
                    bool spread=index==1;
                    airStrikes.Add(start+at);
                    airStrikeEnds.Add(start+at+(spread?108f:0f));
                    // One slow, almost full-width missile curtain per section, five at most.
                    airStrikeCurtains.Add(section==1 ? index==3 : index==0);
                    if(spread)
                    {
                        int blockedSide=section%2==0 ? Left : Right;
                        if(section==1) NightCar(at+30f,blockedSide,12f);
                        else
                        {
                            // Night is a short slalom, not a permanent shelter. The last gate forces Day.
                            Night(at+16f,blockedSide==Left ? LeftMiddle : MiddleRight);
                            NightCar(at+22f,blockedSide,nightSpeed);
                            Night(at+58f,blockedSide==Left ? MiddleRight : LeftMiddle);
                            NightCar(at+76f,blockedSide==Left ? Right : Left,nightSpeed);
                            Night(at+100f,All);
                        }
                        continue;
                    }
                    float gap=at+33f;
                    if(gap>rampAt-45f) continue;
                    bool ammo=index==0 || (section>=4 && index==3);
                    if(ammo) Item(gap,index==0?-1:1);
                    if(!ammo && rampAt-gap<74f)
                    {
                        // The final ordinary closure belongs to Day, with no nearby Night counterpart.
                        Day(gap,All);lastDayRow=gap;continue;
                    }
                    // These separated closures break permanent Night camping without occupying
                    // any missile footprint. The active bombing corridor itself remains fair.
                    Night(gap,All);
                }
                if(section>=3) OceanNightGauntlet(section,nightSpeed);
                if(rampAt-40f-lastDayRow>=28f)
                    Day(rampAt-40f,section%2==0 ? MiddleRight : LeftMiddle);
                // Save after the final barrier: a failed first jump need not replay the entire opening.
                if(section==1) Checkpoint(rampAt-28f,launch);
                // Keep the final 34 m clear so the player can line up and select the launch world.
                return Ramp(rampAt,launch,flightLength:132f);
            }

            // Snow-style centre openings, then fast paired traffic and offset exits. Distances are
            // encounters (not car spawns). Centre/side exits alternate by ONE lane every 30 m.
            // Never demand an immediate two-lane swerve after a wall or fast car.
            private void OceanNightGauntlet(int section,float speed)
            {
                Night(372f,Sides);
                NightCar(402f,Middle,speed);NightCar(402f,section%2==0 ? Left : Right,speed);
                Night(432f,Sides);
                NightCar(462f,section%2==0 ? Right : Left,speed);NightCar(462f,Middle,speed);
                if(section>=4)
                {
                    Item(407f,1); // Preserve the twelve optional Day rockets, including the late third pickup.
                    Night(492f,Sides);
                }
                if(section>=5)
                {
                    NightCar(522f,Middle,speed);NightCar(522f,Right,speed);
                    // Day is clear here, including a protected routine-missile window.
                    Night(534f,All);
                }
            }

            private Builder Add(ShiftHazard hazard)
            {
                // These levels' sideways movers never wait for the car (obstacle visual rule 4).
                hazard.MovesWheneverDay = !course.Training
                    && (course.Environment == ShiftLevel.Desert || course.Environment == ShiftLevel.Mountain || course.Environment == ShiftLevel.Ocean)
                    && hazard.SharedAcrossWorlds && hazard.LateralMotion;
                hazards.Add(hazard); return this;
            }

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

            /// <summary>
            /// Mountain: a big eagle gliding across the jump just authored, "afterTakeoff" metres into the flight. Like
            /// every sideways mover it glides in Day and freezes in Night, and it only meets a car that is in the air.
            /// </summary>
            public Builder Eagle(float afterTakeoff, float phase, float direction, float speed, float travelScale, float lane = 0f)
            {
                var relay = ramps[ramps.Count - 1];
                return AddRock(new WorldShiftLevelRock(relay.Takeoff + afterTakeoff, phase, direction, speed, flight: relay,
                    laneCentre: lane, travelScale: travelScale));
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
                course.IceZones = iceZones.ToArray();
                course.EdgeGaps = edgeGaps.ToArray();
                course.RequiredItems = items.ToArray();
                course.AirStrikeTargets = airStrikes.ToArray();
                course.AirStrikeEnds = airStrikeEnds.ToArray();
                course.AirStrikeCurtains = airStrikeCurtains.ToArray();
                course.Length = ramps.Count > 0 ? ramps[ramps.Count - 1].Landing + 40f : start;
                return course;
            }
        }

        private const int Left = 1, Middle = 2, Right = 4, LeftMiddle = 3, MiddleRight = 6, Sides = 5, All = 7;
        private static readonly ShiftWorld D = ShiftWorld.Day, N = ShiftWorld.Echo;

        public static LevelCourse For(int level)
        {
            switch (WorldShiftLevels.Environments[level])
            {
                case ShiftLevel.Tutorial: return Training();
                case ShiftLevel.City: return City();
                case ShiftLevel.Desert: return Desert();
                case ShiftLevel.Ocean: return Ocean();
                case ShiftLevel.Snow: return Snow();
                default: return Mountain();
            }
        }

        // Introduce each new idea once; the third section practices switching without another stop.
        private static LevelCourse Training() => new Builder(ShiftLevel.City, true)
            .Sign(25f, LevelSignKind.Steer).Permanent(90f, Middle).Permanent(140f, LeftMiddle).Gate(180f, D)
            .Sign(25f, LevelSignKind.Switch).SwitchWall(110f).Gate(170f, N)
            .Day(70f, All).Night(120f, All).Gate(180f, D)
            // Day traffic with the sun badge: steer round it, or switch to Night and it becomes passable dashes
            // (freezing is taught next, so this car never just stops).
            .Sign(25f, LevelSignKind.Oncoming).DayCar(110f, Middle, 10f).Gate(170f, D)
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

        // Mixed compact attacks and 108 m point-bombing corridors; five slow curtains. Preserve the authored
        // ramp/checkpoint coordinates while filling the formerly empty dry approaches.
        private static LevelCourse Ocean() => new Builder(ShiftLevel.Ocean)
            .OceanSection(1,488f,D)
            .OceanSection(2,528f,N)
            .OceanSection(3,538f,D)
            .OceanSection(4,568f,N)
            .OceanSection(5,608f,D)
            .Build();

        // Snow (Level 3): black ice. On ice the car keeps its sideways momentum, builds it slowly and only
        // countersteering brakes a slide, so a lane change on ice needs about twice the room of dry road.
        // Hard by design, with nothing that moves in Day (no sliders, gates or boulders): every obstacle is
        // something to steer around or a Space timing. Zippers pair white barricades with a one-world block in
        // the open lane, so the open lane flips world every 24 m. Rules (WorldShiftLevelTests.Snow): ice starts
        // 40 m after a landing and ends 70 m before a ramp; a Day obstacle and a Night obstacle never share a
        // spot (20 m apart or more); a keyboard-only driver clears every section.
        private static LevelCourse Snow() => new Builder(ShiftLevel.Snow)
            // 1 Frostbite: a four-gate zipper in the right lane, a wall-to-wall slide onto the ice while
            // switching, a full-width slalom, then a quick dry finish.
            .Permanent(50f, LeftMiddle).Night(50f, Right).Permanent(74f, LeftMiddle).Day(74f, Right)
            .Permanent(98f, LeftMiddle).Night(98f, Right).Permanent(122f, LeftMiddle).Day(122f, Right)
            .Ice(150f, 330f).Permanent(165f, MiddleRight).Night(195f, All).Day(230f, All)
            .Permanent(255f, LeftMiddle).Night(270f, All).Permanent(315f, MiddleRight)
            .Day(340f, All).Permanent(355f, Left).Night(375f, All).Day(395f, Middle).Ramp(440f, D)
            // 2 Rush hour on ice: thread between Day traffic, pick a side for Night traffic, a barricade
            // pocket that only Night can pass, then wall to wall.
            .Night(60f, All)
            .Ice(50f, 380f).DayCar(85f, Left).DayCar(110f, Right).Day(140f, All)
            .NightCar(165f, Middle).NightCar(190f, Left).Night(215f, All)
            .Permanent(240f, Sides).DayCar(240f, Middle).Day(275f, All).Permanent(300f, MiddleRight)
            .Night(320f, All).Permanent(362f, LeftMiddle).Day(385f, All).Ramp(450f, N)
            // 3 Shifting zipper: the open lane changes world every gate and slides one lane over, then a
            // half-second switch window between barricades on the ice.
            .Permanent(45f, LeftMiddle).Night(45f, Right).Permanent(69f, LeftMiddle).Day(69f, Right)
            .Permanent(110f, Sides).Night(110f, Middle).Permanent(134f, Sides).Day(134f, Middle)
            .Ice(150f, 370f).Permanent(185f, MiddleRight).Night(185f, Left).Day(220f, All)
            .Permanent(255f, Left).Night(275f, All).Permanent(300f, Sides).Day(300f, Middle)
            .Permanent(352f, MiddleRight).Night(385f, All).Day(410f, Left).Ramp(450f, D)
            // 4 Whiteout gauntlet: the longest ice, wall-to-wall slides with a Space press in the middle of
            // each, and Night traffic you cannot slide away from in time.
            .Ice(45f, 380f).NightHalf(70f, -1).Day(95f, All).Permanent(120f, LeftMiddle).Night(135f, All)
            .Day(170f, All).Permanent(180f, MiddleRight).NightCar(210f, Left)
            .Permanent(245f, MiddleRight).Day(245f, Left).Night(270f, All).Permanent(300f, LeftMiddle)
            .DayCar(330f, Right).Permanent(355f, Left).Night(355f, Middle)
            .Day(395f, All).Night(420f, LeftMiddle).Ramp(460f, N)
            // 5 Glacier run: everything at once. A zipper on ice, a slide while switching, traffic that
            // forces the centre, a barricade pocket, and two half-second switch windows before the jump.
            .Night(40f, All)
            .Ice(55f, 420f).Day(65f, Middle)
            .Permanent(90f, LeftMiddle).Night(90f, Right).Permanent(114f, LeftMiddle).Day(114f, Right)
            .Permanent(138f, LeftMiddle).Night(138f, Right)
            .Day(165f, All).Permanent(195f, MiddleRight).Night(220f, All).DayCar(245f, Left)
            .Permanent(270f, Sides).NightCar(270f, Middle).Day(300f, All).Permanent(325f, Middle)
            .Permanent(350f, Right).Night(350f, Left).Night(375f, All).Permanent(400f, Right).Day(400f, LeftMiddle)
            .Permanent(445f, LeftMiddle).Night(455f, All).Ramp(500f, D)
            .Build();

        // Mountain (Level 4), "Rockslide Pass": medium difficulty, between City and Snow. Boulders roll down the road
        // at you in Day and freeze in Night, but Night has its own rockfall walls, so switching to Day to pass one
        // restarts the slide. Rockfall boulders tumble across the road whenever it is Day; cliff ledges block half the
        // road. Guardrail gaps on the valley side of three bends: drive off the edge there and you fall to the last
        // checkpoint. No sliders, airborne rocks or traffic. Rules: WorldShiftLevelTests.Mountain (re-run after edits).
        // Medium-hard (playtest: the 35-obstacle version felt too easy). Faster boulders and goats; guardrail gaps sit
        // beside obstacles that force the right lane, so dodging means passing the drop; boulder slides follow forced
        // lane changes; Night walls come in quick pairs, forcing Day (and the boulders) back on. Every section ends with
        // a run of full walls alternating worlds 36 m apart (the shared minimum switch window), so you switch about
        // every second and a half; extra quick pairs mid-section and more boulders. Playtest: "a little harder than Ocean".
        // Section lengths are unchanged so bends, grade and guardrail gaps stay where they were.
        private static LevelCourse Mountain() => new Builder(ShiftLevel.Mountain)
            // 1 Scree: a ledge, a boulder slide, a squeeze into the right lane beside the drop, then a boulder down that lane.
            .DayHalf(50f, -1).Rock(100f, Right, 12f).Rock(135f, Middle, 12f).EdgeGap(153f, 24f, 1).Day(166f, LeftMiddle)
            .Rock(201f, Right, 12f).Night(195f, All).Crossing(240f, 2f, 1f, 4f).Rock(270f, Left, 13f)
            // Switch gauntlet: three full walls alternating worlds 36 m apart, a ledge and a boulder down the left lane.
            .Night(262f, All).Day(272f, LeftMiddle).Day(298f, All).Night(334f, All).Ramp(380f, D)
            // Every jump has an eagle soaring across it; freeze it in the air or steer around it.
            .Eagle(40f, 2f, 1f, 3.5f, .6f)
            // 2 Avalanche: squeezed right beside the drop in Night, then two Night walls 35 m apart force Day and restart four boulders.
            .Day(45f, All).Rock(80f, Left, 13f).EdgeGap(98f, 24f, 1).NightHalf(106f, -1).Rock(105f, Right, 13f)
            .Rock(130f, Middle, 13f).Rock(150f, Right, 13f).Night(150f, All).Night(185f, All)
            // Switch gauntlet: four full walls alternating every 36 m, with a boulder down the right lane.
            .Day(235f, All).Night(271f, All).NightHalf(285f, 1).Rock(300f, Right, 13f).Day(307f, All).Night(343f, All).Ramp(410f, N)
            .Eagle(52f, 1f, 1f, 3.5f, .8f)
            // 3 Goat gallery: two goats back to back walking opposite ways, two boulders between walls, a last goat.
            .Night(50f, All).Crossing(90f, 3f, 1f, 4.4f).Crossing(116f, 1f, -1f, 4.4f).Day(130f, All).Night(166f, All).Rock(165f, Middle, 14f)
            .Rock(182f, Left, 14f).Night(205f, All).DayHalf(240f, 1).Crossing(280f, 1.5f, -1f, 4.6f)
            .Night(320f, All).Day(358f, All).Ramp(410f, D)
            .Eagle(45f, .5f, -1f, 3.8f, .7f)
            // 4 Cliff ledges: a Night wall wakes the slide; the boulder you left behind keeps coming; a goat herd; a late squeeze left.
            .NightHalf(50f, -1).Rock(95f, Right, 13f).Night(120f, All).Rock(130f, Right, 13f).NightHalf(175f, 1).Night(189f, All).Rock(200f, Left, 13f)
            .Day(225f, All)
            // A goat herd in single file: three goats 5 m apart, each a step behind the last, so you pick a side.
            .Crossing(256f, 2.5f, 1f, 4.2f).Crossing(261f, 3.5f, 1f, 4.2f).Crossing(266f, 4.5f, 1f, 4.2f)
            .Night(302f, MiddleRight)
            .Day(325f, All).Night(362f, All).Ramp(410f, N)
            .Eagle(52f, 3f, -1f, 3.5f, .8f)
            // 5 Summit: three forced lanes (right, left between boulders, right again beside the drop), faster everything.
            .Night(55f, All).Rock(60f, Left, 14f).Rock(75f, Right, 14f).Rock(95f, Left, 14f).Rock(120f, Right, 14f).NightHalf(140f, -1)
            .Crossing(172f, 1.2f, 1f, 4.8f).Day(195f, All).Rock(225f, Middle, 14f).Day(238f, MiddleRight).Rock(242f, Right, 14f)
            .Night(260f, All).EdgeGap(290f, 24f, 1).DayHalf(300f, -1).Crossing(335f, .4f, 1f, 5f)
            .Night(368f, All).Day(406f, All).Ramp(450f, D)
            // The summit jump: two eagles, one early and one just before the finish.
            .Eagle(30f, 1f, 1f, 4f, .6f).Eagle(70f, 2.5f, -1f, 3f, .45f)
            .Build();
    }
}
