namespace Outlaws.WorldShift
{
    /// <summary>Shared player-facing names for the two modes.</summary>
    public static class WorldShiftModeGuide
    {
        public static string Name(ShiftWorld world) => world == ShiftWorld.Day ? "Day" : "Nightshift";
    }
}
