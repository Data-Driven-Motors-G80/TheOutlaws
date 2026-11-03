using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Escape starts immediately; the optional tutorial teaches the controls separately.
    public sealed class WorldShiftMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private GameObject home;

        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);

        private void Awake()
        {
            Time.timeScale = 1f;
            var root = CreateCanvas(transform, "WorldShift menu");
            Panel(root, "Plain background", Vector2.zero, Vector2.one, Ink);
            // Keep every action inside the display's usable area.
            var safe = Panel(root, "Safe area", Vector2.zero, Vector2.one, Color.clear);
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            home = Panel(safe, "Choose a mode", Vector2.zero, Vector2.one, Color.clear).gameObject;
            var wordmark = Label(home.transform, "WorldShift", .075f, .78f, .8f, .94f, 60f);
            wordmark.fontStyle = FontStyles.Bold;
            wordmark.color = Cream;
            Label(home.transform, "Loot, fuel and powers in Day. None in Nightshift.", .079f, .71f, .8f, .78f, 23f).color = new Color(.70f, .77f, .82f);

            var escape = ModeCard(home.transform, "Endless escape", .075f, .49f);
            Label(escape, "Endless escape", .065f, .65f, .94f, .86f, 30f).fontStyle = FontStyles.Bold;
            Label(escape, "Grab loot. Cash out before you're caught.", .065f, .42f, .94f, .60f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(escape, "Play escape", new Vector2(.065f, .10f), new Vector2(.935f, .30f),
                () => WorldShiftFlow.StartGetaway(false), true);

            var training = ModeCard(home.transform, "Tutorial", .515f, .93f);
            Label(training, "Tutorial", .065f, .65f, .94f, .86f, 30f).fontStyle = FontStyles.Bold;
            Label(training, "A quick spin to learn the controls.", .065f, .42f, .94f, .60f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(training, "Start tutorial", new Vector2(.065f, .10f), new Vector2(.935f, .30f),
                () => WorldShiftFlow.StartGetaway(true), false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(escape.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
        }

        private RectTransform ModeCard(Transform parent, string name, float left, float right)
            => Panel(parent, name, new Vector2(left, .30f), new Vector2(right, .66f), new Color(.075f, .105f, .145f));

        private RectTransform MenuButton(Transform parent, string title, Vector2 min, Vector2 max, Action action, bool primary)
        {
            var rect = Button(parent, title, min, max, action);
            var image = rect.GetComponent<Image>();
            image.color = primary ? Gold : new Color(.14f, .23f, .30f);
            var label = rect.GetComponentInChildren<TMP_Text>();
            label.color = primary ? new Color(.12f, .10f, .07f) : Cream;
            label.fontStyle = FontStyles.Bold;
            label.fontSizeMin = label.fontSizeMax = 20f;
            var button = rect.GetComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            return rect;
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
