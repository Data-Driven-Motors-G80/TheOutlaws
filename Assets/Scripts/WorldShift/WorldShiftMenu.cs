using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Escape is the primary action; practice keeps the original checkpoint flow.
    public sealed class WorldShiftMenu : MonoBehaviour
    {
        private TMP_Text tutorialToggle;
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private void Awake()
        {
            Time.timeScale = 1f;
            var root = CreateCanvas(transform, "WorldShift menu");
            Panel(root, "Background", Vector2.zero, Vector2.one, Ink);
            Label(root, "WORLDSHIFT", .08f, .77f, .92f, .93f, 62f);
            Label(root, "GRAB LOOT. KNOW WHEN TO LEAVE.", .08f, .68f, .92f, .76f, 23f);
            Label(root, "BANK $" + WorldShiftFlow.Banked.ToString("N0") + "     BEST HAUL $" + WorldShiftFlow.BestHaul.ToString("N0"),
                .08f, .57f, .92f, .65f, 21f);
            Button(root, "START ESCAPE", new Vector2(.08f, .40f), new Vector2(.59f, .53f),
                () => WorldShiftFlow.StartGetaway(WorldShiftFlow.TutorialFirst));
            tutorialToggle = Button(root, "", new Vector2(.62f, .40f), new Vector2(.92f, .53f), () =>
            {
                WorldShiftFlow.TutorialFirst = !WorldShiftFlow.TutorialFirst;
                RefreshToggle();
            }).GetComponentInChildren<TMP_Text>();
            Label(root, "Extract to bank. Get caught, lose your haul.", .08f, .31f, .92f, .38f, 20f);
            Label(root, "PRACTICE  /  CHECKPOINT RETRIES", .08f, .22f, .92f, .28f, 16f);
            string[] names = { "CITY", "DESERT", "BASICS" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float x = .08f + i * .285f;
                Button(root, names[i], new Vector2(x, .11f), new Vector2(x + .265f, .21f),
                    () => WorldShiftFlow.Select((ShiftLevel)index, false));
            }
            Label(root, "A / D  STEER     SPACE  SWITCH     ESC  PAUSE", .08f, .025f, .92f, .08f, 16f);
            RefreshToggle();
        }
        private void RefreshToggle() => tutorialToggle.text = "TUTORIAL " + (WorldShiftFlow.TutorialFirst ? "ON" : "OFF");

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
