using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Small authored playtest courses sharing the same driving, freezing and shooting rules.</summary>
    public static class WorldShiftEscapeProfile
    {
        public static float Length(ShiftLevel level) => level == ShiftLevel.Ocean ? 1520f
            : level == ShiftLevel.Snow ? 1600f : level == ShiftLevel.Mountain ? 1680f : WorldShiftLevel.EscapeLength;
        public static float Fork(float length) => length == 1520f ? 480f : length == 1600f ? 520f
            : length == 1680f ? 560f : length == WorldShiftLevel.EscapeLength ? 400f : length == WorldShiftLevel.Length ? 1120f : 720f;
        public static ShiftRelay[] Relays(ShiftLevel level) => level == ShiftLevel.City ? WorldShiftRelay.EscapeSections
            : new[] { level == ShiftLevel.Ocean ? new ShiftRelay(1, 1040f, 1100f, 1184f, ShiftWorld.Day)
                : level == ShiftLevel.Snow ? new ShiftRelay(1, 1100f, 1160f, 1244f, ShiftWorld.Day)
                : new ShiftRelay(1, 1140f, 1200f, 1284f, ShiftWorld.Day) };
        public static ShiftFuelPickup[] Fuel(ShiftLevel level)
        {
            if (level == ShiftLevel.City) return WorldShiftLevel.CreateEscapeFuel();
            float[] distances = level == ShiftLevel.Ocean ? new[] { 430f, 990f, 1280f, 1480f }
                : level == ShiftLevel.Snow ? new[] { 490f, 1060f, 1340f, 1560f } : new[] { 480f, 1030f, 1380f, 1640f };
            return System.Array.ConvertAll(distances, d => new ShiftFuelPickup(ShiftWorld.Day, d, 0));
        }
        private static ShiftHazard Block(float at, int lane, float motion = 0f) =>
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, at, at + 6f, lane,
                motion, sharedAcrossWorlds: true, lateralMotion: motion != 0f);
        private static ShiftHazard Wall(float at, int side) =>
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, at, at + 6f,
                side < 0 ? 3 : 6, halfSide: side, sharedAcrossWorlds: true);
        private static ShiftHazard Rock(float at, int lane, float speed) =>
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, at, at + 6f, lane, speed, sharedAcrossWorlds: true);
        private static float ExtraSwitchWall(ShiftLevel level) => level == ShiftLevel.City ? 180f
            : level == ShiftLevel.Desert ? 1400f : level == ShiftLevel.Ocean ? 1440f
            : level == ShiftLevel.Snow ? 1420f : 120f;
        public static float[] SwitchWalls(ShiftLevel level) => level == ShiftLevel.City ? new[] { 300f, 860f, ExtraSwitchWall(level) }
            : level == ShiftLevel.Desert ? new[] { 420f, 2320f, ExtraSwitchWall(level) } : level == ShiftLevel.Ocean ? new[] { 390f, 950f, ExtraSwitchWall(level) }
            : level == ShiftLevel.Snow ? new[] { 450f, 990f, ExtraSwitchWall(level) } : new[] { 420f, 980f, ExtraSwitchWall(level) };
        // Reward the return to Day after the extra barrier; leave time to clear its whole footprint first.
        public static Vector2[] SwitchRewards(ShiftLevel level)
        {
            float wall = ExtraSwitchWall(level), lane = level == ShiftLevel.Snow ? -3.2f : level == ShiftLevel.Ocean ? 0f : 3.2f;
            float first = level == ShiftLevel.Ocean ? 60f : 50f;
            return new[] { new Vector2(wall + first, lane), new Vector2(wall + first + 20f, lane) };
        }
        // Short Day reward windows connect encounters; City is the approved pacing reference.
        public static Vector2[] PacingRewards(ShiftLevel level) => level == ShiftLevel.City
            ? new[] { new Vector2(760f, 3.2f), new Vector2(930f, 3.2f), new Vector2(1300f, 3.2f) }
            : level == ShiftLevel.Desert ? new[] { new Vector2(1540f, 3.2f), new Vector2(1920f, -3.2f) }
            : level == ShiftLevel.Ocean ? new[] { new Vector2(230f, 3.2f), new Vector2(340f, 3.2f), new Vector2(880f, 3.2f), new Vector2(1370f, 3.2f) }
            : level == ShiftLevel.Snow ? new[] { new Vector2(890f, 3.2f) }
            : new[] { new Vector2(510f, 3.2f), new Vector2(950f, 0f), new Vector2(1080f, -3.2f) };

        public static float SteeringGrip(ShiftLevel level) => level == ShiftLevel.Snow ? .85f : 1f;

        public static bool SnowSlide(ShiftLevel level, float distance, ShiftRelay[] relays)
        {
            if (level != ShiftLevel.Snow || distance < 80f || distance >= Length(level) + WorldShiftBonusJump.ShortStartOffset - 40f) return false;
            // Keep choice points and both ends of every jump predictable, including the optional ramp.
            if (WorldShiftForkRoad.InMainFork(Length(level), distance, 50f)) return false;
            foreach (var jump in relays)
                if (distance >= jump.Ramp - 80f && distance < jump.End + 60f) return false;
            return true;
        }

        // Profiles express road character, while forks and relay approaches get flat, straight sight lines.
        public static float Heading(ShiftLevel level, float d, float fork, ShiftRelay jump, float length)
        {
            float Bend(float a, float b, float c, float e) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, d))
                - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, e, d));
            float between = fork + 300f, after = jump.End + 60f;
            if (level == ShiftLevel.Mountain)
                return 46f * Bend(80f, 180f, 220f, 340f) - 34f * Bend(340f, 400f, 440f, fork - 80f)
                    - 44f * Bend(between, between + 70f, between + 120f, jump.Ramp - 60f)
                    + 40f * Bend(after, after + 70f, after + 110f, after + 170f)
                    - 30f * Bend(after + 170f, after + 210f, after + 230f, length - 10f);
            if (level == ShiftLevel.Ocean)
                return 32f * Bend(100f, 190f, 260f, fork - 100f)
                    - 36f * Bend(between, between + 70f, between + 120f, jump.Ramp - 60f)
                    + 28f * Bend(after, after + 70f, after + 110f, length - 10f);
            if (level == ShiftLevel.Snow)
                return 24f * Bend(100f, 220f, 320f, fork - 80f)
                    - 28f * Bend(between, between + 90f, between + 130f, jump.Ramp - 80f)
                    + 18f * Bend(after, after + 90f, after + 140f, length - 10f);
            return 18f * Bend(100f, 160f, 180f, 240f) - 14f * Bend(240f, 290f, 310f, fork - 40f)
                - 24f * Bend(between, between + 70f, between + 110f, jump.Ramp - 60f)
                + 20f * Bend(after, after + 60f, after + 100f, length - 10f);
        }
        public static float TerrainSlope(ShiftLevel level, float d, float length, ShiftRelay[] relays)
        {
            if (level == ShiftLevel.City || d < 60f || d >= length) return 0f;
            float raw = level == ShiftLevel.Desert ? .045f * Mathf.Sin((d - 60f) * Mathf.PI * 2f / 620f)
                : level == ShiftLevel.Ocean ? .025f * Mathf.Sin((d - 60f) * Mathf.PI * 2f / 340f)
                : level == ShiftLevel.Snow ? -.055f
                : relays.Length > 0 && d >= relays[0].End ? -.12f : .10f;
            float Pulse(float a, float b, float c, float e) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, d))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c, e, d)));
            float fork = Fork(length);
            float clear = 1f - Pulse(fork - 80f, fork - 40f, fork + 320f, fork + 360f);
            foreach (var jump in relays) clear *= 1f - Pulse(jump.Ramp - 80f, jump.Ramp - 20f, jump.End, jump.End + 60f);
            return raw * clear * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 140f, d))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length - 80f, length, d)));
        }
        public static ShiftHazard[] Hazards(ShiftLevel level, float motionMultiplier)
        {
            var course = level == ShiftLevel.City ? WorldShiftLevel.CreateEscapeHazards()
                : level == ShiftLevel.Ocean ? new[] { Block(120f, 2, .75f), Block(180f, 2, .95f), Block(280f, 2, -1.15f),
                    Block(830f, 2, 1.25f), Block(920f, 2, -1.4f), Block(1320f, 3), Block(1420f, 6) }
                : level == ShiftLevel.Snow ? new[] { Block(180f, 1), Block(290f, 4), Block(390f, 2, .65f),
                    Block(850f, 1), Block(940f, 2, .75f), Block(1390f, 3), Block(1480f, 6) }
                : new[] { Wall(190f, -1), Wall(280f, 1), Rock(370f, 2, 12f),
                    Rock(900f, 2, 8f), Rock(970f, 1, 12f), Rock(1030f, 4, 10f), Wall(1420f, -1), Rock(1450f, 2, 11f), Wall(1560f, 1) };
            var encounters = new System.Collections.Generic.List<ShiftHazard>(course);
            foreach (float at in SwitchWalls(level))
                encounters.Add(new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.SwitchWall, at, at + 6f, 7));
            return System.Array.ConvertAll(encounters.ToArray(), h => new ShiftHazard(h.World, h.Kind, h.Start, h.End, h.LaneMask,
                h.MotionSpeed * motionMultiplier, h.HalfSide, h.SharedAcrossWorlds, h.LateralMotion));
        }
        public static Color Ground(ShiftLevel level, bool night) => level == ShiftLevel.Desert
            ? night ? new Color(.17f, .24f, .31f) : new Color(.52f, .32f, .16f)
            : level == ShiftLevel.Ocean
            ? night ? new Color(.025f, .12f, .18f) : new Color(.12f, .5f, .63f)
            : level == ShiftLevel.Snow ? night ? new Color(.36f, .46f, .57f) : new Color(.82f, .89f, .93f)
            : night ? new Color(.13f, .15f, .18f) : new Color(.38f, .39f, .36f);
    }
}
