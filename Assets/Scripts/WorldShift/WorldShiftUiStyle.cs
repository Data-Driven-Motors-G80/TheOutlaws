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
    }
}
