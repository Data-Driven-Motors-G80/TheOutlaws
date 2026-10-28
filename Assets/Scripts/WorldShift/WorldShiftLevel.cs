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
        Oncoming
    }

    public sealed class ShiftHazard
    {
        public readonly ShiftWorld World;
        public readonly ShiftHazardKind Kind;
        public readonly float Start;
        public readonly float End;
        public readonly int LaneMask;
        public readonly float MotionSpeed;

        public ShiftHazard(ShiftWorld world, ShiftHazardKind kind, float start, float end, int laneMask, float motionSpeed = 0f)
        {
            World = world;
            Kind = kind;
            Start = start;
            End = end;
            LaneMask = laneMask;
            MotionSpeed = motionSpeed;
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

        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f,
                "CITY / FIRST SHIFT",
                "Space changes the world. Shift when the road is blocked."),
            new ShiftCheckpoint(560f, ShiftWorld.Day, 0f,
                "STAY AND STEER",
                "Day medians split the road. Echo clears them. Watch the oncoming lane."),
            new ShiftCheckpoint(1120f, ShiftWorld.Echo, 0f,
                "THE MISSING ROAD",
                "A broken bridge has no safe lane. Shift before the edge and stay across."),
            new ShiftCheckpoint(1680f, ShiftWorld.Echo, 0f,
                "READ BOTH WORLDS",
                "Crossings crowd Echo. Long Day medians force an early lane choice."),
            new ShiftCheckpoint(2240f, ShiftWorld.Day, 0f,
                "ONE ROAD, TWO ROUTES",
                "Full blocks arrive faster. Switch early and watch for oncoming police."),
            new ShiftCheckpoint(2800f, ShiftWorld.Day, -3.2f,
                "THE LAST CROSSING",
                "Shift past the fire, move right, then shift across the final gap.")
        };

        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 100f, 116f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 200f, 216f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 320f, 326f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 440f, 452f, 7),
            // New rows are introduced gradually after the first-sector tutorial.
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 600f, 606f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 660f, 676f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Divider, 700f, 770f, 2),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 740f, 744f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 780f, 796f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 840f, 852f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Divider, 860f, 1000f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 900f, 912f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 900f, 904f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 960f, 965.5f, 1, 12f),
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
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 1600f, 1616f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Divider, 1720f, 1900f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1740f, 1744f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 1740f, 1744f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 1800f, 1816f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 1800f, 1812f, 3),
            // The Night crossing overlaps Day's long median: choose a side before shifting.
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 1830f, 1836f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1860f, 1864f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 1860f, 1864f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 1920f, 1924f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Police, 1920f, 1936f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 1948f, 1964f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 1980f, 1984f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Fire, 2040f, 2056f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 2100f, 2112f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 2128f, 2134f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 2160f, 2176f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Police, 2160f, 2176f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 2300f, 2304f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Gap, 2360f, 2416f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Oncoming, 2380f, 2385.5f, 4, 12f),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 2440f, 2452f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 2480f, 2496f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 2540f, 2544f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Gap, 2600f, 2656f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 2670f, 2678f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 2680f, 2684f, 4),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 2680f, 2684f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 2720f, 2736f, 6),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Fire, 2720f, 2736f, 6),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 2860f, 2864f, 6),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 2860f, 2864f, 6),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Divider, 2880f, 3080f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Fire, 2920f, 2936f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 2980f, 2992f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 3040f, 3052f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Police, 3040f, 3056f, 3),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Spikes, 3100f, 3104f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Pedestrians, 3120f, 3126f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Gap, 3160f, 3216f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Spikes, 3240f, 3244f, 3),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Police, 3280f, 3296f, 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Fire, 3280f, 3296f, 1),
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
            new ShiftFuelPickup(ShiftWorld.Echo, 1968f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 2185f, 0),
            new ShiftFuelPickup(ShiftWorld.Echo, 2515f, 0),
            new ShiftFuelPickup(ShiftWorld.Day, 2770f, -1),
            new ShiftFuelPickup(ShiftWorld.Echo, 3070f, 1),
            new ShiftFuelPickup(ShiftWorld.Day, 3228f, 1),
            new ShiftFuelPickup(ShiftWorld.Day, 3320f, 1),
        };
    }
}
