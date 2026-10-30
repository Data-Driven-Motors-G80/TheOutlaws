namespace Outlaws.WorldShift
{
    public static class WorldShiftTutorial
    {
        public const float Length = 720f, MergeStart = 520f;
        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 90f, 96f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 174f, 180f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 314f, 320f, 7),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 440f, 446f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 584f, 590f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 652f, 658f, 6, halfSide: 1)
        };
        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "STEER", "A / D  /  FOLLOW THE OPEN LANE"),
            new ShiftCheckpoint(120f, ShiftWorld.Day, 0f, "SWITCH", "SPACE  /  SWITCH TO NIGHT"),
            new ShiftCheckpoint(240f, ShiftWorld.Echo, 0f, "SWITCH BACK", "SPACE  /  SWITCH TO DAY"),
            new ShiftCheckpoint(380f, ShiftWorld.Day, 0f, "YOUR ROUTE", "STEER AROUND OR SWITCH"),
            new ShiftCheckpoint(520f, ShiftWorld.Day, 2.5f, "WORLDS JOIN", "A / D  /  CROSS BETWEEN WORLDS")
        };
    }
}
