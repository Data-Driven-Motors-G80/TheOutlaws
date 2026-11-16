using TMPro;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>One type family: condensed display titles, readable UI text and genuine bold weights.</summary>
    internal static class WorldShiftTypography
    {
        private static TMP_FontAsset body, heading;
        public static TMP_FontAsset Body => body != null ? body : body = Resources.Load<TMP_FontAsset>("WorldShiftFonts/Barlow Medium") ?? TMP_Settings.defaultFontAsset;
        private static TMP_FontAsset Heading => heading != null ? heading : heading = Resources.Load<TMP_FontAsset>("WorldShiftFonts/Barlow Condensed Semibold") ?? Body;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFonts() { body = heading = null; }

        public static void Apply(TMP_Text label, FontStyles style, float size)
        {
            bool display = size >= 26f && (style & FontStyles.Bold) != 0;
            label.font = display ? Heading : Body;
            // The display file is already semibold; avoid synthetic bold on top of it.
            label.fontStyle = display ? style & ~FontStyles.Bold : style;
        }
    }
}
