namespace Outlaws.WorldShift
{
    /// <summary>Licensed full-length songs. Attribution is also available from the pause menu.</summary>
    public static class WorldShiftMusicLibrary
    {
        public const string Artist = "Kevin MacLeod (incompetech.com)";
        public const string LicenseUrl = "https://creativecommons.org/licenses/by/4.0/";
        public static string Title(ShiftLevel level)
        {
            switch (level)
            {
                case ShiftLevel.City: return "Funkorama";
                case ShiftLevel.Desert: return "Whiskey on the Mississippi";
                case ShiftLevel.Ocean: return "Surf Shimmy";
                case ShiftLevel.Snow: return "Cold Funk";
                case ShiftLevel.Mountain: return "Mountain Emperor";
                default: return "";
            }
        }
        // Match representative music loudness without modifying the original recordings.
        public static float MixGain(ShiftLevel level) => level == ShiftLevel.Desert ? 1.65f
            : level == ShiftLevel.Snow ? 1.18f : level == ShiftLevel.City ? 1.09f : 1f;
        public static string SourceUrl(ShiftLevel level)
        {
            string isrc;
            switch (level)
            {
                case ShiftLevel.City: isrc = "USUAN1100474"; break;
                case ShiftLevel.Desert: isrc = "USUAN1100709"; break;
                case ShiftLevel.Ocean: isrc = "USUAN1700018"; break;
                case ShiftLevel.Snow: isrc = "USUAN1100499"; break;
                case ShiftLevel.Mountain: isrc = "USUAN1700012"; break;
                default: return "https://incompetech.com/";
            }
            return "https://incompetech.com/music/royalty-free/index.html?isrc=" + isrc;
        }
    }
}
