namespace Outlaws.WorldShift
{
    public static class WorldShiftTutorial
    {
        public const float Length = 720f, MergeStart = 520f;
        public static readonly ShiftHazard ShootingObstacle = new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 440f, 446f, 7);
        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 90f, 96f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 174f, 180f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 314f, 320f, 7),
            ShootingObstacle,
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 584f, 590f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 652f, 658f, 6, halfSide: 1)
        };
        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "STEER", "Use A / D or arrow keys to steer."),
            new ShiftCheckpoint(120f, ShiftWorld.Day, 0f, "SWITCH", "Tap Space to try night."),
            new ShiftCheckpoint(240f, ShiftWorld.Echo, 0f, "SWITCH BACK", "Tap Space to go back to day."),
            new ShiftCheckpoint(380f, ShiftWorld.Day, 0f, "SHOOT", "Click to shoot the block."),
            new ShiftCheckpoint(520f, ShiftWorld.Day, 2.5f, "YOUR CHOICE", "Steer around it or tap Space.")
        };
    }
}
