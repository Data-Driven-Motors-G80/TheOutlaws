using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>One palette for every screen (menu, loading, pause, finish, crash card, story and HUD buttons).
    /// Training's green highway signs deliberately keep their own real-world road-sign style.</summary>
    public static class WorldShiftUiStyle
    {
        public static readonly Color Backdrop = new Color(.018f, .035f, .055f);
        public static readonly Color Scrim = new Color(.008f, .018f, .03f, .78f);
        public static readonly Color Face = new Color(.035f, .065f, .09f, .98f);
        public static readonly Color Rim = new Color(.22f, .34f, .36f);
        public static readonly Color Accent = new Color(.42f, .86f, .78f);
        public static readonly Color Muted = new Color(.55f, .66f, .71f);
        public static readonly Color Cream = new Color(1f, .95f, .84f);
        public static readonly Color Gold = new Color(1f, .75f, .32f);
        public static readonly Color Danger = new Color(1f, .52f, .39f);
        public static readonly Color Track = new Color(.12f, .21f, .25f);
        // Semantic menu colours. One accent (teal) for navigation; green only means "completed";
        // difficulty is ordered data, so it uses one warm heat ramp (amber, orange, red) instead of a rainbow.
        public static readonly Color Success = new Color(.40f, .86f, .52f);
        public static readonly Color HeatMild = new Color(1f, .76f, .30f);
        public static readonly Color HeatSpicy = new Color(1f, .54f, .24f);
        public static readonly Color HeatFiery = new Color(.98f, .33f, .26f);
    }
}
