using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Runtime presentation for the two-world city run. Gameplay owns all input and state.</summary>
    public sealed class WorldShiftHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .044f, .075f, .94f);
        private static readonly Color Surface = new Color(.066f, .092f, .133f, .98f);
        private static readonly Color White = new Color(.95f, .97f, 1f);
        private static readonly Color Muted = new Color(.65f, .73f, .82f);
        private static readonly Color Day = new Color(1f, .74f, .35f);
        private static readonly Color Echo = new Color(.35f, .88f, 1f);
        private static readonly Color Success = new Color(.52f, .94f, .74f);

        private WorldShiftCity game;
        private TMP_FontAsset font;
        private RectTransform canvasRoot;
        private RectTransform safeRoot;
        private CanvasGroup hud;
        private CanvasGroup hintGroup;
        private WorldShiftStatusStrip statusStrip;
        private TMP_Text sectionHint;
        private TMP_Text retryMessage;
        private GameObject modal;
        private GameObject readyPanel;
        private GameObject pausePanel;
        private GameObject wonPanel;
        private GameObject retryPanel;
        private ShiftRunState previousState;
        private bool hasState;
        private string previousHint;
        private float hintAge;
        private Rect previousSafeArea;
        private int previousWidth;
        private int previousHeight;

        private TMP_Text lessonHint;
        private GameObject lessonReady, lessonPause, lessonDone, lessonDriving;

        private void BuildLesson()
        {
            canvasRoot = WorldShiftMenu.CreateCanvas(transform, "Learn to play");
            lessonDriving = WorldShiftMenu.Panel(canvasRoot, "One lesson at a time", Vector2.zero, Vector2.one, Color.clear).gameObject;
            lessonHint = WorldShiftMenu.Label(lessonDriving.transform, "", .16f, .77f, .78f, .85f, 26f);
            lessonHint.alignment = TextAlignmentOptions.Center;
            lessonHint.outlineWidth = .18f;
            lessonHint.outlineColor = Color.black;
            WorldShiftMenu.Button(lessonDriving.transform, "Pause", new Vector2(.85f, .89f), new Vector2(.97f, .96f), game.TogglePause);

            lessonReady = LessonPanel("Let's take a quick spin.", "You'll pick it up as we go.");
            WorldShiftMenu.Button(lessonReady.transform, "Let's drive", new Vector2(.26f, .25f), new Vector2(.74f, .36f), game.BeginRun);
            WorldShiftMenu.Button(lessonReady.transform, "Back to menu", new Vector2(.36f, .11f), new Vector2(.64f, .20f), WorldShiftFlow.Menu);
            lessonPause = LessonPanel("Paused", "Take your time.");
            WorldShiftMenu.Button(lessonPause.transform, "Keep going", new Vector2(.26f, .32f), new Vector2(.74f, .43f), game.TogglePause);
            WorldShiftMenu.Button(lessonPause.transform, "Skip the lesson", new Vector2(.26f, .18f), new Vector2(.74f, .29f), () => WorldShiftFlow.Next(ShiftLevel.Tutorial));
            WorldShiftMenu.Button(lessonPause.transform, "Back to menu", new Vector2(.36f, .06f), new Vector2(.64f, .14f), WorldShiftFlow.Menu);
            lessonDone = LessonPanel("You've got it!", "Ready for a drive?");
            string next = WorldShiftFlow.TutorialStartsEscape ? "Start your escape" : "Try the " + WorldShiftFlow.TutorialDestination.ToLowerInvariant();
            WorldShiftMenu.Button(lessonDone.transform, next, new Vector2(.26f, .25f), new Vector2(.74f, .36f), () => WorldShiftFlow.Next(ShiftLevel.Tutorial));
            WorldShiftMenu.Button(lessonDone.transform, "Back to menu", new Vector2(.36f, .11f), new Vector2(.64f, .20f), WorldShiftFlow.Menu);
        }

        private GameObject LessonPanel(string title, string subtitle)
        {
            var panel = WorldShiftMenu.Panel(canvasRoot, title, Vector2.zero, Vector2.one, Ink).gameObject;
            WorldShiftMenu.Label(panel.transform, title, .14f, .62f, .86f, .78f, 44f).alignment = TextAlignmentOptions.Center;
            WorldShiftMenu.Label(panel.transform, subtitle, .14f, .48f, .86f, .58f, 23f).alignment = TextAlignmentOptions.Center;
            return panel;
        }

        private void RefreshLesson()
        {
            lessonReady.SetActive(game.State == ShiftRunState.Ready);
            lessonPause.SetActive(game.State == ShiftRunState.Paused);
            lessonDone.SetActive(game.State == ShiftRunState.Won);
            bool driving = game.State == ShiftRunState.Running || game.State == ShiftRunState.Retrying;
            lessonDriving.SetActive(driving);
            lessonHint.text = game.State == ShiftRunState.Retrying ? "Let's try that bit again." : game.SectionHint;
        }

        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            if (canvasRoot == null) { if (game.TutorialLevel) BuildLesson(); else Build(); }
            hasState = false;
            previousHint = null;
            Refresh();
        }

        private void Update()
        {
            if (game != null && canvasRoot != null) Refresh();
        }

        private void Refresh()
        {
            if (game.TutorialLevel) { RefreshLesson(); return; }
            RefreshSafeArea();
            bool running = game.State == ShiftRunState.Running;
            bool retrying = game.State == ShiftRunState.Retrying;
            bool menu = game.State == ShiftRunState.Ready || game.State == ShiftRunState.Paused
                || game.State == ShiftRunState.Won;
            if (!hasState || previousState != game.State)
            {
                modal.SetActive(menu);
                readyPanel.SetActive(game.State == ShiftRunState.Ready);
                pausePanel.SetActive(game.State == ShiftRunState.Paused);
                wonPanel.SetActive(game.State == ShiftRunState.Won);
                retryPanel.SetActive(retrying);
                hud.alpha = menu ? 0f : 1f;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                previousState = game.State;
                hasState = true;
            }

            statusStrip.SetAmmo(game.Combat != null ? game.Combat.Ammo : 0, game.Combat != null);
            statusStrip.Refresh(game.World, game.FuelFraction, game.RearView != null ? game.RearView.Texture : null,
                game.PursuitGap, joined: game.IsMerged);

            string currentHint = !game.IsRelayAirborne && game.Combat != null && game.Combat.Hint.Length > 0 ? game.Combat.Hint : game.SectionHint;
            if (previousHint != currentHint)
            {
                previousHint = currentHint;
                sectionHint.text = previousHint ?? string.Empty;
                hintAge = 0f;
            }
            if (running) hintAge += Time.unscaledDeltaTime;
            hintGroup.alpha = running && !string.IsNullOrEmpty(previousHint)
                ? game.TutorialLevel ? 1f : 1f - Mathf.Clamp01((hintAge - 3f) / .5f) : 0f;

            if (retrying) retryMessage.text = game.Message == "NO ROAD" ? "Try switching before the gap."
                : game.Message == "MISSED RELAY LANDING" ? "Switch worlds before you land."
                : game.Message == "OUT OF FUEL" ? "Grab a fuel can on the way." : "Let's try that bit again.";
        }

        private void Build()
        {
            font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            canvasRoot = Rect("WorldShiftCanvas", transform);
            Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            safeRoot = Rect("SafeArea", canvasRoot);
            Stretch(safeRoot);

            RectTransform hudRoot = Rect("RunHUD", safeRoot);
            Stretch(hudRoot);
            hud = hudRoot.gameObject.AddComponent<CanvasGroup>();
            hud.interactable = false;
            hud.blocksRaycasts = false;
            statusStrip = WorldShiftStatusStrip.Create(hudRoot);

            RectTransform hintRoot = Rect("SectionHint", hudRoot);
            Place(hintRoot, new Vector2(.5f, 1f), new Vector2(-70f, -137f), new Vector2(700f, 52f));
            hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();
            sectionHint = Label("Hint", hintRoot, string.Empty, 26f, White, TextAlignmentOptions.Center);
            Stretch(sectionHint.rectTransform);
            sectionHint.fontStyle = FontStyles.Bold;
            sectionHint.outlineWidth = .16f;
            sectionHint.outlineColor = new Color(0f, 0f, 0f, .8f);

            RectTransform retry = Panel("RetryBanner", safeRoot, Ink);
            Place(retry, new Vector2(.5f, 1f), new Vector2(0f, -220f), new Vector2(760f, 100f));
            retryPanel = retry.gameObject;
            AddAccent(retry, Day);
            FixedLabel(retry, "Let's try that again.", 17f, Day, 28f, 18f, 704f, 23f, true);
            retryMessage = FixedLabel(retry, string.Empty, 21f, White, 28f, 48f, 704f, 30f);

            RectTransform backdrop = Panel("MenuBackdrop", canvasRoot, new Color(.015f, .029f, .052f, .76f));
            Stretch(backdrop);
            backdrop.GetComponent<Image>().raycastTarget = true;
            modal = backdrop.gameObject;
            readyPanel = BuildReady(backdrop);
            pausePanel = BuildPause(backdrop);
            wonPanel = BuildWon(backdrop);
            EnsureEventSystem();
        }

        private GameObject BuildReady(RectTransform parent)
        {
            RectTransform card = MenuCard(parent, "Ready", 780f, 370f, Echo);
            FixedLabel(card, "City practice", 42f, White, 42f, 35f, 696f, 65f, true);
            FixedLabel(card, game.Combat != null ? "A / D to steer. Space to switch. F to shoot." : "A / D to steer. Space to switch worlds.", 24f, Echo, 42f, 130f, 696f, 42f);
            FixedLabel(card, "Slip up? We'll restart nearby.", 20f, Muted, 42f, 192f, 696f, 40f);
            Button(card, "Let's drive", 42f, 270f, 338f, Echo, () => game.BeginRun());
            Button(card, "Back to menu", 400f, 270f, 338f, Surface, WorldShiftFlow.Menu, White);
            return card.gameObject;
        }

        private GameObject BuildPause(RectTransform parent)
        {
            RectTransform card = MenuCard(parent, "Paused", 780f, 370f, Echo);
            FixedLabel(card, "Paused", 44f, White, 42f, 35f, 696f, 65f, true);
            FixedLabel(card, "Take your time.", 21f, Muted, 42f, 120f, 696f, 40f);
            Button(card, "Keep driving", 42f, 200f, 338f, Echo, () => game.TogglePause());
            Button(card, "Back to menu", 400f, 200f, 338f, Surface, WorldShiftFlow.Menu, White);
            Button(card, "Start over", 42f, 278f, 338f, Surface, game.RestartRun, White);
            return card.gameObject;
        }

        private GameObject BuildWon(RectTransform parent)
        {
            RectTransform card = MenuCard(parent, "Complete", 780f, 440f, Success);
            FixedLabel(card, "Nice driving!", 42f, White, 42f, 35f, 696f, 65f, true);
            FixedLabel(card, "Want to try the desert?", 24f, Muted, 42f, 140f, 696f, 45f);
            Button(card, "Try the desert", 42f, 300f, 338f, Success, () => WorldShiftFlow.Next(ShiftLevel.City));
            Button(card, "Back to menu", 400f, 300f, 338f, Surface, WorldShiftFlow.Menu, White);
            return card.gameObject;
        }

        private RectTransform MenuCard(RectTransform parent, string name, float width, float height, Color accent)
        {
            RectTransform card = Panel(name, parent, Ink);
            Place(card, new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, height));
            AddAccent(card, accent);
            return card;
        }

        private void Button(RectTransform parent, string title, float x, float y, float width, Color color,
            Action action, Color? foreground = null)
        {
            RectTransform root = Panel(title, parent, color);
            TopLeft(root, x, y, width, 56f);
            Image background = root.GetComponent<Image>();
            background.raycastTarget = true;
            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.86f, .93f, 1f);
            colors.pressedColor = new Color(.67f, .78f, .9f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            button.onClick.AddListener(() => { if (game != null) action(); });
            TMP_Text label = Label("Label", root, title, 18f, foreground ?? Ink, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 1f;
            Stretch(label.rectTransform, 12f, 4f, 12f, 4f);
        }

        private void Key(RectTransform parent, string title, float x, float width)
        {
            RectTransform key = Panel(title, parent, Surface);
            TopLeft(key, x, 11f, width, 34f);
            TMP_Text label = Label("Key", key, title, 16f, White, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            Stretch(label.rectTransform);
        }

        private TMP_Text FixedLabel(RectTransform parent, string text, float size, Color color,
            float x, float y, float width, float height, bool bold = false)
        {
            TMP_Text label = Label("Text", parent, text, size, color, TextAlignmentOptions.Left);
            TopLeft(label.rectTransform, x, y, width, height);
            if (bold) label.fontStyle = FontStyles.Bold;
            return label;
        }

        private TMP_Text Label(string name, RectTransform parent, string value, float size, Color color,
            TextAlignmentOptions alignment)
        {
            RectTransform root = Rect(name, parent);
            TextMeshProUGUI text = root.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * .82f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void AddAccent(RectTransform parent, Color color)
        {
            RectTransform accent = Panel("Accent", parent, color);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = Vector2.one;
            accent.pivot = new Vector2(.5f, 1f);
            accent.offsetMin = new Vector2(0f, -3f);
            accent.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void RefreshSafeArea()
        {
            Rect area = Screen.safeArea;
            if (area == previousSafeArea && Screen.width == previousWidth && Screen.height == previousHeight) return;
            previousSafeArea = area;
            previousWidth = Screen.width;
            previousHeight = Screen.height;
            if (Screen.width < 1 || Screen.height < 1) return;
            safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject input = new GameObject("WorldShiftEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(transform, false);
            InputSystemUIInputModule module = input.GetComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        private static string FormatTime(float elapsed)
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(elapsed));
            return $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
