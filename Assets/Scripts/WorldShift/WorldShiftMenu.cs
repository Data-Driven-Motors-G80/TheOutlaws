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
        private GameObject home, garage;
        private TMP_Text savings, homeSavings, garageMessage;
        private readonly TMP_Text[] upgradeDetails = new TMP_Text[3];
        private readonly UnityEngine.UI.Button[] upgradeButtons = new UnityEngine.UI.Button[3];
        private float nextPurchaseTime;

        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);

        private void Awake()
        {
            Time.timeScale = 1f;
            WorldShiftAnalytics.MenuShown();
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
            Label(home.transform, "Collect cash. Escape. Upgrade your car.", .079f, .71f, .8f, .78f, 23f).color = new Color(.70f, .77f, .82f);

            var escape = ModeCard(home.transform, "Endless escape", .075f, .49f);
            Label(escape, "Endless escape", .065f, .65f, .94f, .86f, 30f).fontStyle = FontStyles.Bold;
            Label(escape, "Grab loot. Cash out before you're caught.", .065f, .42f, .94f, .60f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(escape, "Play escape", new Vector2(.065f, .10f), new Vector2(.935f, .30f),
                () => WorldShiftFlow.StartGetaway(false), true);

            var training = ModeCard(home.transform, "Tutorial", .515f, .93f);
            Label(training, "Tutorial", .065f, .65f, .94f, .86f, 30f).fontStyle = FontStyles.Bold;
            Label(training, "First time? Learn by driving.", .065f, .42f, .94f, .60f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(training, "Start tutorial", new Vector2(.065f, .10f), new Vector2(.935f, .30f),
                () => WorldShiftFlow.StartGetaway(true), false);
            homeSavings = Label(home.transform, "", .075f, .19f, .49f, .26f, 24f);
            MenuButton(home.transform, "Garage", new Vector2(.515f, .16f), new Vector2(.93f, .25f),
                () => ShowGarage(true), false);
            Label(home.transform, "Steer: ← / →     Switch: SPACE     Shoot: F / click", .075f, .065f, .93f, .125f, 20f);
            BuildGarage(safe);
            RefreshGarage();
            if (WorldShiftFlow.ConsumeGarageRequest()) { ShowGarage(true); return; }
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(escape.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
        }

        private void BuildGarage(Transform parent)
        {
            garage = Panel(parent, "Garage screen", Vector2.zero, Vector2.one, Ink).gameObject;
            Label(garage.transform, "Garage", .075f, .83f, .6f, .94f, 48f).fontStyle = FontStyles.Bold;
            savings = Label(garage.transform, "", .65f, .85f, .93f, .93f, 26f);
            savings.alignment = TextAlignmentOptions.Right;
            Label(garage.transform, "Permanent upgrades for every escape. Spend only cash you have banked.",
                .075f, .74f, .93f, .81f, 22f);
            string[] names = { "Fuel tank", "Armor", "Headlights" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float left = .075f + i * .29f;
                var card = Panel(garage.transform, names[i], new Vector2(left, .30f),
                    new Vector2(left + .27f, .70f), new Color(.075f, .105f, .145f));
                Label(card, names[i], .07f, .77f, .93f, .94f, 28f).fontStyle = FontStyles.Bold;
                upgradeDetails[i] = Label(card, "", .07f, .29f, .93f, .73f, 22f);
                var button = MenuButton(card, "Upgrade " + names[i], new Vector2(.07f, .06f),
                    new Vector2(.93f, .23f), () => Purchase(index), true);
                upgradeButtons[i] = button.GetComponent<Button>();
            }
            garageMessage = Label(garage.transform, "Upgrades stay with you after a lost run.", .075f, .21f, .93f, .28f, 21f);
            MenuButton(garage.transform, "Back", new Vector2(.075f, .07f), new Vector2(.32f, .17f),
                () => ShowGarage(false), false);
            MenuButton(garage.transform, "Start escape", new Vector2(.65f, .07f), new Vector2(.93f, .17f),
                () => WorldShiftFlow.StartGetaway(false), true);
            garage.SetActive(false);
        }

        private void Purchase(int index)
        {
            if (Time.unscaledTime < nextPurchaseTime) return;
            nextPurchaseTime = Time.unscaledTime + .35f;
            bool bought = WorldShiftFlow.BuyUpgrade((VehicleUpgrades.Part)index);
            garageMessage.text = bought ? "Upgrade installed and saved. Ready for your next escape."
                : "Not enough saved cash, or this upgrade is already maxed.";
            RefreshGarage();
        }

        private void RefreshGarage()
        {
            var progress = WorldShiftFlow.Garage;
            homeSavings.text = savings.text = "Saved: $" + progress.Banked.ToString("N0");
            for (int i = 0; i < 3; i++)
            {
                var part = (VehicleUpgrades.Part)i;
                int level = progress.Level(part);
                string current = i == 0 ? (100 + level * 10) + " fuel capacity"
                    : i == 1 ? (level * 5) + "% less collision penalty"
                    : (74 + level * 15) + " m headlight range";
                string next = i == 0 ? "+10 fuel capacity" : i == 1 ? "+5% collision protection" : "+15 m light range";
                upgradeDetails[i].text = "Level " + level + " / 3\n" + current + "\n\n"
                    + (level == 3 ? "Fully upgraded" : "Next: " + next);
                bool affordable = level < 3 && progress.Banked >= progress.Price(part);
                upgradeButtons[i].interactable = affordable;
                upgradeButtons[i].GetComponentInChildren<TMP_Text>().text = level == 3 ? "Max level"
                    : affordable ? "Upgrade: $" + progress.Price(part).ToString("N0")
                    : "Need $" + progress.Price(part).ToString("N0");
            }
        }

        private void ShowGarage(bool show)
        {
            RefreshGarage(); home.SetActive(!show); garage.SetActive(show);
            if (show) WorldShiftAnalytics.GarageShown();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject((show ? garage : home)
                    .GetComponentsInChildren<Button>()[show ? 3 : 0].gameObject);
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
