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
        public GameObject MirrorPanel { get; private set; }
        private Image mirrorBorder;
        private RawImage mirror;
        private GameObject ammoRoot;
        private Image[] rounds;
        private TMP_Text switchHint;

        public static WorldShiftStatusStrip Create(Transform parent, bool showLoot = false)
        {
            var strip = new WorldShiftStatusStrip();
            var row = WorldShiftMenu.Panel(parent, "Driving status", new Vector2(.20f, .88f), new Vector2(.80f, .98f), Ink);
            strip.World = Label(row, "World", .02f, .13f, 22f);
            strip.World.rectTransform.anchorMin = new Vector2(.02f, .38f);
            strip.switchHint = WorldShiftMenu.Label(row, "SPACE: switch", .02f, .07f, .13f, .37f, 14f);
            strip.switchHint.alignment = TextAlignmentOptions.Center;
            strip.Loot = Label(row, "Loot total", .02f, .30f, 24f);
            strip.Loot.gameObject.SetActive(showLoot);
            strip.Loot.color = Gold;
            strip.Fuel = Label(row, "Fuel percentage", .34f, .62f, 22f);
            var ammo = WorldShiftMenu.Panel(row, "Ammo", new Vector2(.67f, .1f), new Vector2(.98f, .9f), Color.clear);
            strip.ammoRoot = ammo.gameObject;
            var key = WorldShiftMenu.Label(ammo, "Ammo\n<size=12>F / click</size>", .02f, .1f, .29f, .9f, 18f);
            key.alignment = TextAlignmentOptions.Center;
            strip.rounds = new Image[3];
            for (int i = 0; i < 3; i++)
                strip.rounds[i] = WorldShiftMenu.Panel(ammo, "Round " + (i + 1), new Vector2(.32f + i * .21f, .28f), new Vector2(.42f + i * .21f, .72f), Color.white).GetComponent<Image>();
            strip.ammoRoot.SetActive(false);
            var frame = WorldShiftMenu.Panel(parent, "Police mirror", new Vector2(.78f, .88f), new Vector2(.98f, .98f), Ink);
            strip.MirrorPanel = frame.gameObject;
            strip.mirrorBorder = frame.GetComponent<Image>();
            strip.mirror = new GameObject("Rear view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            strip.mirror.transform.SetParent(frame, false);
            strip.mirror.raycastTarget = false;
            strip.mirror.rectTransform.anchorMin = new Vector2(.02f, .05f);
            strip.mirror.rectTransform.anchorMax = new Vector2(.98f, .95f);
            strip.mirror.rectTransform.offsetMin = strip.mirror.rectTransform.offsetMax = Vector2.zero;
            var caption = WorldShiftMenu.Panel(frame, "Police label", new Vector2(.02f, .69f), new Vector2(.35f, .95f), Ink);
            WorldShiftMenu.Label(caption, "Police", .03f, 0f, .97f, 1f, 14f).alignment = TextAlignmentOptions.Center;
            strip.World.gameObject.SetActive(false);
            strip.switchHint.gameObject.SetActive(false);
            strip.MirrorPanel.SetActive(false);
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

        public void SetAmmo(int ammo, bool visible)
        {
            ammoRoot.SetActive(visible);
            for (int i = 0; i < rounds.Length; i++) rounds[i].color = i < ammo ? Color.white : new Color(.25f, .3f, .35f);
        }

        public void Refresh(ShiftWorld world, float fuelFraction, Texture rearView, float pursuitGap,
            bool showFuel = true, bool joined = false)
        {
            switchHint.gameObject.SetActive(false);
            World.text = joined ? "Day + Nightshift" : WorldShiftModeGuide.Name(world);
            World.color = world == ShiftWorld.Day ? Gold : new Color(.3f, .85f, 1f);
            Fuel.gameObject.SetActive(showFuel);
            Fuel.text = "Fuel " + Mathf.Clamp(Mathf.CeilToInt(fuelFraction * 100f), 0, 100) + "%";
            Fuel.color = fuelFraction < .25f ? Danger : Color.white;
            MirrorPanel.SetActive(false);
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
