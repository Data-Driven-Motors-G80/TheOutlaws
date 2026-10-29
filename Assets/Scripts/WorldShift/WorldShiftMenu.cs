using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Reuses the original start-screen structure: level choice, tutorial switch,
    // then one explicit Start button. All visuals are UI rectangles and text.
    public sealed class WorldShiftMenu : MonoBehaviour
    {
        private ShiftLevel selected;
        private TMP_Text selection, toggleLabel;
        private Image[] cards;
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private void Awake()
        {
            Time.timeScale = 1f;
            var root = CreateCanvas(transform, "World Shift start menu");
            Panel(root, "Background", Vector2.zero, Vector2.one, Ink);
            Label(root, "THE OUTLAWS", .08f, .80f, .92f, .94f, 54f);
            Label(root, "ONE ROAD. TWO WORLDS. FIND THE WAY THROUGH.", .08f, .73f, .92f, .80f, 21f);
            cards = new Image[3];
            string[] names = { "01  CITY", "02  DESERT", "TUTORIAL" };
            string[] descriptions = { "Switch routes. Jump between worlds.\nEscape through the final rift.\n\nAbout 2 minutes",
                "Day rolls. Night freezes.\nDense rock fields and a final rift.\n\nAbout 2 minutes",
                "Steer, switch, then cross the seam.\nShort checkpoints. No fuel pressure.\n\nAbout 40 seconds" };
            for (int i = 0; i < 3; i++)
            {
                int index = i; float x = .08f + i * .285f;
                cards[i] = Button(root, names[i], new Vector2(x, .56f), new Vector2(x + .265f, .68f), () => Choose((ShiftLevel)index)).GetComponent<Image>();
                Label(root, descriptions[i], x + .015f, .32f, x + .25f, .55f, 20f);
            }
            toggleLabel = Button(root, "", new Vector2(.08f, .22f), new Vector2(.62f, .29f), () =>
            { WorldShiftFlow.TutorialFirst = !WorldShiftFlow.TutorialFirst; UpdateSelection(); }).GetComponentInChildren<TMP_Text>();
            selection = Label(root, "", .08f, .13f, .62f, .20f, 21f);
            Button(root, "START", new Vector2(.68f, .12f), new Vector2(.915f, .25f), () => WorldShiftFlow.Select(selected, WorldShiftFlow.TutorialFirst));
            Label(root, "A / D or arrows: steer      SPACE: switch worlds      ESC: pause", .08f, .035f, .92f, .095f, 17f);
            Choose(ShiftLevel.City);
        }
        private void Choose(ShiftLevel level) { selected = level; UpdateSelection(); }
        private void UpdateSelection()
        {
            for (int i = 0; i < cards.Length; i++) cards[i].color = i == (int)selected ? new Color(.15f, .38f, .47f) : new Color(.09f, .13f, .18f);
            toggleLabel.text = selected == ShiftLevel.Tutorial ? "TUTORIAL SELECTED - continue to City when ready"
                : (WorldShiftFlow.TutorialFirst ? "[ON] " : "[OFF] ") + "PLAY TUTORIAL BEFORE THIS LEVEL";
            selection.text = selected == ShiftLevel.Tutorial ? "Start with the basics."
                : "Selected: " + (selected == ShiftLevel.City ? "CITY" : "DESERT") + (WorldShiftFlow.TutorialFirst ? "  /  Tutorial first" : "  /  Skip tutorial");
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
