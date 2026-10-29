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
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "1 / STEER", "A / D or arrows. Move away from the centre block."),
            new ShiftCheckpoint(120f, ShiftWorld.Day, 0f, "2 / SWITCH", "SPACE: change the whole world. The orange wall exists only in Day."),
            new ShiftCheckpoint(240f, ShiftWorld.Echo, 0f, "3 / SWITCH BACK", "The blue wall belongs to Night. SPACE returns to Day. Switching inside a wall counts as a hit."),
            new ShiftCheckpoint(380f, ShiftWorld.Day, 0f, "4 / CHOOSE A ROUTE", "Steer around a half-width wall, or switch worlds. Solids hurt; gaps make you fall."),
            new ShiftCheckpoint(520f, ShiftWorld.Day, 2.5f, "5 / WORLDS JOIN", "A / D crosses the seam: left is Day, right is Night. SPACE rests here.")
        };
    }
}
