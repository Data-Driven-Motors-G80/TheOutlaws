namespace Outlaws.WorldShift
{
    /// <summary>Shared player-facing names and explanations for the two modes.</summary>
    public static class WorldShiftModeGuide
    {
        public const string DayCash = "Collect cash in Day.";
        public const string DaySupplies = "Find fuel and power-ups in Day.";
        public const string NightFrozen = "Nightshift stops moving obstacles.";
        public const string NightSolid = "Frozen obstacles can still hit you.";
        public const string NightSupplies = "No cash, fuel or power-ups in Nightshift.";
        public const string NightChase = "Fuel still drains. The police keep chasing.";
        public static string Name(ShiftWorld world) => world == ShiftWorld.Day ? "Day" : "Nightshift";
        public static string NightExplanation(float elapsed)
            => elapsed < 2f ? NightFrozen : elapsed < 4f ? NightSupplies : NightChase;
    }
}
