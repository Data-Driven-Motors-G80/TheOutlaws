namespace Outlaws.WorldShift
{
    public enum ShiftWorld
    {
        Day,
        Echo
    }

    public enum ShiftHazardKind
    {
        Fire,
        Police,
        Gap,
        Spikes,
        Barrier,
        Divider,
        Pedestrians,
        Oncoming,
        HalfBarrier
    }

    public sealed class ShiftHazard
    {
        public readonly ShiftWorld World;
        public readonly ShiftHazardKind Kind;
        public readonly float Start;
        public readonly float End;
        public readonly int LaneMask;
        public readonly float MotionSpeed;
        // -1: wall to centre on the left, +1: centre to wall on the right.
        public readonly int HalfSide;

        public ShiftHazard(ShiftWorld world, ShiftHazardKind kind, float start, float end, int laneMask, float motionSpeed = 0f, int halfSide = 0)
        {
            World = world;
            Kind = kind;
            Start = start;
            End = end;
            LaneMask = laneMask;
            MotionSpeed = motionSpeed;
            HalfSide = halfSide;
        }

        // Both collision and rendering sample the same deterministic road coordinates.
        // Traffic starts 150 m ahead; it keeps approaching even in the other world.
        public void BoundsAt(float playerDistance, out float start, out float end)
        {
            float travel = System.Math.Max(0f, playerDistance - (Start - 150f));
            start = Start - travel * MotionSpeed / WorldShiftLevel.Speed;
            end = start + End - Start;
        }
    }

    public sealed class ShiftCheckpoint
    {
        public readonly float Distance;
        public readonly ShiftWorld World;
        public readonly float Lateral;
        public readonly string Title;
        public readonly string Hint;

        public ShiftCheckpoint(float distance, ShiftWorld world, float lateral, string title, string hint)
        {
            Distance = distance;
            World = world;
            Lateral = lateral;
            Title = title;
            Hint = hint;
        }
    }

    public sealed class ShiftFuelPickup
    {
        public readonly ShiftWorld World;
        public readonly float Distance;
        public readonly int Lane;
        public ShiftFuelPickup(ShiftWorld world, float distance, int lane)
        { World = world; Distance = distance; Lane = lane; }
    }

    // Lane bits: 1 = left, 2 = middle, 4 = right. Coordinates are road metres.
    public static class WorldShiftLevel
    {
        public const float Length = 3360f;
        public const float Speed = 28f;
        public const float RoadWidth = 10f;
        public const float TutorialEnd = 560f;
        public const float FuelConsumption = 2f;
        public const float FuelRefill = 26f;

        // Only the final sector joins. Extend past extraction so the winning view stays joined.
        public const float MergeStart = 2800f;
        public static readonly float[] MergeBounds = { MergeStart, Length + 80f };
        public static bool IsMerged(float distance)
        {
            for (int i = 0; i < MergeBounds.Length; i += 2)
                if (distance >= MergeBounds[i] && distance < MergeBounds[i + 1]) return true;
            return false;
        }
        public static ShiftWorld WorldAt(float lateral) => lateral < 0f ? ShiftWorld.Day : ShiftWorld.Echo;
        public static bool IsVisible(ShiftWorld world, float at, float lateral, ShiftWorld current)
            => IsMerged(at) ? world == WorldAt(lateral) : world == current;

        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f,
                "CITY / FIRST SHIFT",
                "Space changes the world. Shift when the road is blocked."),
            new ShiftCheckpoint(560f, ShiftWorld.Day, 0f,
                "RELAY 1 / DAY TO NIGHT",
                "Take the arrow ramp. Once airborne, Space reveals the Night landing."),
            new ShiftCheckpoint(1120f, ShiftWorld.Echo, 0f,
                "THE MISSING ROAD",
                "A broken bridge has no safe lane. Shift before the edge and stay across."),
            new ShiftCheckpoint(1680f, ShiftWorld.Echo, 0f,
                "RELAY 2 / NIGHT TO DAY",
                "Night ramp, Day landing. Boost first; switch in the air."),
            new ShiftCheckpoint(2240f, ShiftWorld.Day, 0f,
                "RELAY 3 / THE LONG CROSSING",
                "Day ramp, Night landing. The final rift waits beyond the jump."),
            new ShiftCheckpoint(2800f, ShiftWorld.Day, -3.2f,
                "THE LAST CROSSING",
                "Rift crossed. Day left, Night right. Steer between the open halves; Space rests.")
        };

        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 100f, 116f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 200f, 216f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 320f, 326f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 440f, 452f, 7),
            // Fill ordinary driving stretches in BOTH worlds. Relay ramps,
            // landing recoveries and checkpoint entrances keep their clear space.
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 220f, 225.5f, 4, 12f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 300f, 305.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 360f, 372f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 395f, 400.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 400f, 404f, 5),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 520f, 525.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 540f, 545.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 885f, 890.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 990f, 995.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 1050f, 1062f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1100f, 1105.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1205f, 1210.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1210f, 1215.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1310f, 1315.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1380f, 1384f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1470f, 1475.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 1560f, 1572f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1650f, 1655.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2110f, 2115.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2120f, 2125.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2200f, 2205.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2630f, 2635.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2740f, 2745.5f, 2, 14f),
            // New rows are introduced gradually after the first-sector tutorial.
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 840f, 852f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Divider, 860f, 1000f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 900f, 912f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 900f, 904f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 960f, 965.5f, 1, 12f),
            // Night is a route with its own timing, not a permanent safe bypass.
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 980f, 986f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1060f, 1065.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1140f, 1145.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1020f, 1024f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 1020f, 1032f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1080f, 1084f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 1080f, 1084f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Gap, 1240f, 1288f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1320f, 1324f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 1340f, 1345.5f, 4, 12f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Gap, 1360f, 1408f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 1420f, 1426f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Gap, 1480f, 1528f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1520f, 1525.5f, 1, 12f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 1560f, 1572f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1580f, 1585.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 1640f, 1645.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 1600f, 1616f, 7),
            // Alternating closures follow the recovery zone, with room to read
            // the next world before its next closure. Launches remain clear.
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 2010f, 2022f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2014f, 2019.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Fire, 2070f, 2086f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 2100f, 2112f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 2128f, 2134f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2160f, 2165.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 2160f, 2176f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Police, 2160f, 2176f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2220f, 2225.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 2570f, 2574f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Gap, 2600f, 2656f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 2620f, 2632f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2690f, 2695.5f, 2, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 2670f, 2678f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 2680f, 2684f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 2680f, 2684f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 2720f, 2736f, 6),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Fire, 2720f, 2736f, 6),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2760f, 2765.5f, 1, 14f),
            // Ordinary-world introductions: exactly five metres wide, attached to one edge.
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 480f, 486f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 520f, 526f, 6, halfSide: 1),
            // Final rift: 100 m clear entry, then 70 m between alternating walls.
            // A car follows on the SAME closed half; do not cross back too early.
            // Their encounter positions are ~Start - 50 m, not their spawn points.
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 2900f, 2906f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 2970f, 2975.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 2970f, 2976f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 3040f, 3045.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 3040f, 3046f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 3110f, 3115.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 3110f, 3116f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 3180f, 3185.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 3180f, 3186f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Oncoming, 3250f, 3255.5f, 1, 14f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 3250f, 3256f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 3320f, 3325.5f, 4, 14f),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 3320f, 3326f, 3, halfSide: -1),
        };

        // Alternative-world fuel at 820 m offers a lane/world choice on a clear stretch.
        public static readonly ShiftFuelPickup[] FuelPickups =
        {
            new ShiftFuelPickup(ShiftWorld.Echo, 270f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 500f, 0),
            new ShiftFuelPickup(ShiftWorld.Echo, 820f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 820f, 1),
            new ShiftFuelPickup(ShiftWorld.Day, 1190f, 0),
            new ShiftFuelPickup(ShiftWorld.Echo, 1635f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 1968f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 2210f, 0),
            new ShiftFuelPickup(ShiftWorld.Echo, 2515f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 2770f, -1),
            new ShiftFuelPickup(ShiftWorld.Day, 2950f, -1),
            new ShiftFuelPickup(ShiftWorld.Day, 3228f, -1),
            new ShiftFuelPickup(ShiftWorld.Echo, 3320f, 1),
        };
    }
}
