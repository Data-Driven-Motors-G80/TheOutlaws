using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>One shared status layout and units for City, Desert, Escape and Practice.</summary>
    public sealed class WorldShiftStatusStrip
    {
        private static readonly Color Gold = new Color(1f, .77f, .25f);
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .96f);
        private static readonly Color Danger = new Color(1f, .3f, .2f);
        public TMP_Text World { get; private set; }
        public TMP_Text Loot { get; private set; }
        public TMP_Text Fuel { get; private set; }
        public TMP_Text Speed { get; private set; }
        public GameObject MirrorPanel { get; private set; }
        private Image mirrorBorder;
        private RawImage mirror;

        public static WorldShiftStatusStrip Create(Transform parent, bool showLoot = false)
        {
            var strip = new WorldShiftStatusStrip();
            var row = WorldShiftMenu.Panel(parent, "Driving status", new Vector2(.02f, .88f), new Vector2(.98f, .98f), Ink);
            strip.World = Label(row, "World", .02f, .13f, 22f);
            strip.Loot = Label(row, "Loot total", .15f, .35f, 30f);
            strip.Loot.gameObject.SetActive(showLoot);
            strip.Loot.color = Gold;
            strip.Fuel = Label(row, "Fuel percentage", .37f, .54f, 22f);
            strip.Speed = Label(row, "Speed km/h", .56f, .75f, 22f);
            var frame = WorldShiftMenu.Panel(parent, "Police mirror", new Vector2(.78f, .88f), new Vector2(.98f, .98f), Ink);
            strip.MirrorPanel = frame.gameObject;
            strip.mirrorBorder = frame.GetComponent<Image>();
            strip.mirror = new GameObject("Rear view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            strip.mirror.transform.SetParent(frame, false);
            strip.mirror.raycastTarget = false;
            strip.mirror.rectTransform.anchorMin = new Vector2(.02f, .05f);
            strip.mirror.rectTransform.anchorMax = new Vector2(.98f, .95f);
            strip.mirror.rectTransform.offsetMin = strip.mirror.rectTransform.offsetMax = Vector2.zero;
            return strip;
        }

        private static TMP_Text Label(Transform row, string name, float left, float right, float size)
        {
            var label = WorldShiftMenu.Label(row, "", left, .10f, right, .90f, size);
            label.name = name;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSizeMin = size - 2f;
            return label;
        }

        public void Refresh(ShiftWorld world, float fuelFraction, float metresPerSecond, Texture rearView, float pursuitGap,
            bool showFuel = true, bool joined = false)
        {
            World.text = joined ? "Day + night" : world == ShiftWorld.Day ? "Day" : "Night";
            World.color = world == ShiftWorld.Day ? Gold : new Color(.3f, .85f, 1f);
            Fuel.gameObject.SetActive(showFuel);
            Fuel.text = "Fuel " + Mathf.Clamp(Mathf.CeilToInt(fuelFraction * 100f), 0, 100) + "%";
            Fuel.color = fuelFraction < .25f ? Danger : Color.white;
            Speed.text = Mathf.RoundToInt(Mathf.Max(0f, metresPerSecond) * 3.6f) + " km/h";
            MirrorPanel.SetActive(rearView != null);
            mirrorBorder.color = pursuitGap < 23f ? Danger : Ink;
            mirror.texture = rearView;
            // Crop the centre instead of stretching the police car to fit the mirror.
            if (rearView != null)
            {
                var rect = mirror.rectTransform.rect;
                float aspect = rect.height > 0f ? rect.width / rect.height : 4f;
                float source = (float)rearView.width / rearView.height;
                float width = Mathf.Min(1f, aspect / source), height = Mathf.Min(1f, source / aspect);
                mirror.uvRect = new Rect((1f - width) * .5f, (1f - height) * .5f, width, height);
            }
        }
    }
}
