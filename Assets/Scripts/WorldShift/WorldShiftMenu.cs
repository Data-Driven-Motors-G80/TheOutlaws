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
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private GameObject home, practice;

        private void Awake()
        {
            Time.timeScale = 1f;
            var root = CreateCanvas(transform, "WorldShift menu");
            Panel(root, "Background", Vector2.zero, Vector2.one, Ink);
            home = Panel(root, "Choose a mode", Vector2.zero, Vector2.one, Color.clear).gameObject;
            Label(home.transform, "WorldShift", .08f, .78f, .92f, .93f, 58f);
            Label(home.transform, "How do you want to play?", .08f, .69f, .92f, .77f, 25f);

            var escape = Panel(home.transform, "Endless escape", new Vector2(.08f, .25f), new Vector2(.49f, .63f), new Color(.07f, .15f, .19f));
            Label(escape, "Endless escape", .06f, .72f, .94f, .94f, 31f);
            Label(escape, "Grab loot. Cash out or keep going.\nGet caught and this run's loot is gone.", .06f, .35f, .94f, .68f, 21f);
            Button(escape, "Play escape", new Vector2(.06f, .07f), new Vector2(.94f, .28f),
                () => WorldShiftFlow.StartGetaway(WorldShiftFlow.TutorialFirst));

            var training = Panel(home.transform, "Practice", new Vector2(.52f, .25f), new Vector2(.93f, .63f), new Color(.07f, .10f, .14f));
            Label(training, "Practice", .06f, .72f, .94f, .94f, 31f);
            Label(training, "Get to know the roads.\nMake a mistake? Just try that bit again.", .06f, .35f, .94f, .68f, 21f);
            Button(training, "Choose a route", new Vector2(.06f, .07f), new Vector2(.94f, .28f), () => ShowPractice(true));
            var learn = Button(home.transform, "New here? Learn to play", new Vector2(.30f, .09f), new Vector2(.71f, .18f),
                () => WorldShiftFlow.Select(ShiftLevel.Tutorial, false));
            learn.GetComponent<Image>().color = Color.clear;
            var lessonLink = learn.GetComponentInChildren<TMP_Text>();
            lessonLink.fontSizeMin = lessonLink.fontSizeMax = 19f;

            practice = Panel(root, "Practice routes", Vector2.zero, Vector2.one, Ink).gameObject;
            Label(practice.transform, "Practice", .12f, .76f, .88f, .91f, 48f);
            Label(practice.transform, "Pick a route. We'll restart nearby if you slip up.", .12f, .64f, .88f, .74f, 23f);
            Button(practice.transform, "City", new Vector2(.12f, .42f), new Vector2(.48f, .58f), () => WorldShiftFlow.Select(ShiftLevel.City, false));
            Button(practice.transform, "Desert", new Vector2(.52f, .42f), new Vector2(.88f, .58f), () => WorldShiftFlow.Select(ShiftLevel.Desert, false));
            Label(practice.transform, "No loot to lose here. Just driving.", .12f, .29f, .88f, .39f, 21f);
            Button(practice.transform, "Back", new Vector2(.12f, .12f), new Vector2(.36f, .23f), () => ShowPractice(false));
            ShowPractice(false);
        }

        private void ShowPractice(bool show)
        {
            practice.SetActive(show);
            home.SetActive(!show);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
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
