using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Home is the level select. Level 0 is training; each level unlocks the next.
    public sealed class WorldShiftMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);
        private static readonly Color Tile = new Color(.14f, .23f, .30f);
        private static readonly Color LockedTile = new Color(.08f, .11f, .14f);
        private RectTransform levels;
        private GameObject testBadge;

        private void Awake()
        {
            Time.timeScale = 1f;
            WorldShiftLevelAnalytics.MenuShown();
            var root = CreateCanvas(transform, "WorldShift menu");
            Panel(root, "Plain background", Vector2.zero, Vector2.one, Ink);
            // Keep every action inside the display's usable area.
            var safe = Panel(root, "Safe area", Vector2.zero, Vector2.one, Color.clear);
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            var wordmark = Label(safe, "WorldShift", .20f, .74f, .80f, .88f, 52f);
            wordmark.fontStyle = FontStyles.Bold;
            wordmark.color = Cream;
            wordmark.alignment = TextAlignmentOptions.Center;
            levels = Panel(safe, "Levels", new Vector2(.04f, .30f), new Vector2(.96f, .64f), Color.clear);
            // Team testing: Shift+U opens every level. A small open lock shows it is on.
            testBadge = Panel(safe, "All levels open (Shift+U)", new Vector2(.95f, .05f), new Vector2(.95f, .05f), Gold).gameObject;
            ((RectTransform)testBadge.transform).sizeDelta = new Vector2(48f, 48f);
            testBadge.GetComponent<Image>().sprite = WorldShiftIcons.Get(ShiftIcon.Unlocked);
            BuildLevels();
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.uKey.wasPressedThisFrame && keyboard.shiftKey.isPressed)
            {
                WorldShiftLevels.UnlockAll = !WorldShiftLevels.UnlockAll;
                WorldShiftLevelAnalytics.UnlockAllToggled(WorldShiftLevels.UnlockAll);
                BuildLevels();
            }
        }

        private void BuildLevels()
        {
            for (int i = levels.childCount - 1; i >= 0; i--) Destroy(levels.GetChild(i).gameObject);
            testBadge.SetActive(WorldShiftLevels.UnlockAll);
            int count = WorldShiftLevels.Count;
            GameObject first = null;
            bool firstUnbeaten = false;
            for (int i = 0; i < count; i++)
            {
                int level = i;
                bool open = WorldShiftLevels.IsUnlocked(level);
                float width = 1f / count;
                var tile = Panel(levels, "Level " + level, new Vector2(i * width + .01f, 0f), new Vector2((i + 1) * width - .01f, 1f), open ? Tile : LockedTile);
                var image = tile.GetComponent<Image>();
                image.raycastTarget = true;
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() =>
                {
                    WorldShiftLevelAnalytics.LevelPicked(level, WorldShiftLevels.IsUnlocked(level));
                    WorldShiftLevels.Play(level);
                });
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                // A locked tile shows its lock in place of the number.
                var number = Label(tile, open ? level.ToString() : "", .05f, .38f, .95f, .92f, 96f);
                number.fontStyle = FontStyles.Bold;
                number.alignment = TextAlignmentOptions.Center;
                number.color = open ? Cream : new Color(1f, 1f, 1f, .25f);
                var name = Label(tile, WorldShiftLevels.Name(level), .05f, .10f, .95f, .30f, 24f);
                name.alignment = TextAlignmentOptions.Center;
                name.color = open ? Accent : new Color(1f, 1f, 1f, .25f);
                if (!open) Badge(tile, ShiftIcon.Lock, new Color(1f, 1f, 1f, .7f), 84f, new Vector2(.5f, .64f));
                else if (WorldShiftLevels.IsBeaten(level)) Badge(tile, ShiftIcon.Check, new Color(.4f, 1f, .6f), 40f, new Vector2(.84f, .88f));
                // Focus the first open level not yet beaten: the one to play next.
                if (open && (first == null || WorldShiftLevels.IsBeaten(level - 1) && !firstUnbeaten)) { first = tile.gameObject; firstUnbeaten = !WorldShiftLevels.IsBeaten(level); }
            }
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first);
        }

        private static void Badge(RectTransform parent, ShiftIcon icon, Color colour, float size, Vector2 anchor)
        {
            var badge = Panel(parent, icon + " badge", anchor, anchor, colour);
            badge.sizeDelta = new Vector2(size, size);
            badge.GetComponent<Image>().sprite = WorldShiftIcons.Get(icon);
        }

        public static RectTransform CreateCanvas(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 150;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(parent, false);
            }
            return (RectTransform)root.transform;
        }
        public static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        public static TMP_Text Label(Transform parent, string text, float x0, float y0, float x1, float y1, float size)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); root.transform.SetParent(parent, false);
            var label = root.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.color = Color.white; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size;
            label.rectTransform.anchorMin = new Vector2(x0, y0); label.rectTransform.anchorMax = new Vector2(x1, y1);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; return label;
        }
        public static RectTransform Button(Transform parent, string title, Vector2 min, Vector2 max, Action action)
        {
            var rect = Panel(parent, title, min, max, new Color(.12f, .28f, .36f)); var image = rect.GetComponent<Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var label = Label(rect, title, .03f, .08f, .97f, .92f, 23f); label.color = Accent; label.alignment = TextAlignmentOptions.Center;
            return rect;
        }
    }

}
