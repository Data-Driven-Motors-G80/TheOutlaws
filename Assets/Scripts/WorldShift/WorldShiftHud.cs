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
        private RectTransform progressFill;
        private CanvasGroup hud;
        private CanvasGroup hintGroup;
        private CanvasGroup messageGroup;
        private Image worldMark;
        private Image worldBackground;
        private Image progressImage;
        private TMP_Text worldLabel;
        private TMP_Text steerControl, switchControl;
        private TMP_Text worldSubLabel;
        private TMP_Text sectionLabel;
        private TMP_Text progressLabel;
        private TMP_Text clockLabel;
        private TMP_Text sectionHint;
        private TMP_Text message;
        private TMP_Text retryMessage;
        private GameObject controlsRoot;
        private RectTransform fuelFill;
        private Image fuelImage;
        private Image fuelBorder;
        private TMP_Text fuelLabel;
        private TMP_Text speedLabel;
        private Image[] fuelWarningEdges;
        private RectTransform mirrorRoot;
        private RawImage mirrorImage;
        private Image mirrorBorder;
        private TMP_Text pursuitLabel;
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

            Color accent = game.World == ShiftWorld.Day ? Day : Echo;
            worldMark.color = accent;
            worldBackground.color = new Color(accent.r, accent.g, accent.b, .11f);
            worldLabel.color = accent;
            worldLabel.text = game.IsMerged ? "Day + night" : game.World == ShiftWorld.Day ? "Day" : "Night";
            worldLabel.fontSize = game.IsMerged ? 17f : 26f;
            worldSubLabel.text = game.IsMerged ? "A / D  CROSS" : game.IsRelayAirborne ? "AIRBORNE" : "";
            steerControl.text = game.IsMerged ? "CROSS" : "STEER";
            switchControl.text = game.IsMerged ? "RESTING" : game.IsRelayAirborne ? "LAND" : "SWITCH";
            switchControl.color = game.IsMerged ? Muted : White;
            sectionLabel.text = "City practice";
            clockLabel.gameObject.SetActive(false);
            progressLabel.gameObject.SetActive(false);
            progressImage.color = accent;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(game.Progress), 1f);
            progressLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(game.Progress) * 100f)}%";
            clockLabel.text = FormatTime(game.Duration);
            bool lowFuel = game.FuelFraction < .25f;
            Color fuelColor = lowFuel ? new Color(1f, .24f, .19f) : new Color(1f, .69f, .28f);
            fuelFill.anchorMax = new Vector2(Mathf.Clamp01(game.FuelFraction), 1f);
            fuelImage.color = fuelColor;
            fuelBorder.color = lowFuel ? new Color(.34f, .06f, .05f, .94f) : Ink;
            fuelLabel.text = lowFuel ? "Fuel's low" : "Fuel";
            speedLabel.gameObject.SetActive(false);
            fuelLabel.color = fuelColor;
            speedLabel.text = Mathf.RoundToInt(game.Speed * 3.6f) + " KM/H";
            float edgeAlpha = running && lowFuel ? .05f + .09f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f)) : 0f;
            edgeAlpha = Mathf.Max(edgeAlpha, running ? Mathf.Clamp01(game.HitWarningRemaining) * .16f : 0f);
            foreach (Image edge in fuelWarningEdges) edge.color = new Color(.9f, .025f, .03f, edgeAlpha);
            speedLabel.color = game.IsBoosting ? Echo : Muted;
            bool hasMirror = !game.TutorialLevel && game.RearView != null && game.RearView.Texture != null;
            mirrorRoot.gameObject.SetActive(hasMirror);
            if (hasMirror)
            {
                if (mirrorImage.texture != game.RearView.Texture) mirrorImage.texture = game.RearView.Texture;
                float danger = 1f - Mathf.Clamp01((game.PursuitFraction - .15f) / .65f);
                Color pursuitColor = Color.Lerp(White, new Color(1f, .23f, .19f), danger);
                mirrorBorder.color = pursuitColor;
                pursuitLabel.color = pursuitColor;
                pursuitLabel.text = game.PursuitGap < 23f ? "They're close!" : "";
            }

            if (previousHint != game.SectionHint)
            {
                previousHint = game.SectionHint;
                sectionHint.text = previousHint ?? string.Empty;
                hintAge = 0f;
            }
            if (running) hintAge += Time.unscaledDeltaTime;
            hintGroup.alpha = running && !string.IsNullOrEmpty(previousHint)
                ? game.TutorialLevel ? 1f : 1f - Mathf.Clamp01((hintAge - 3f) / .5f) : 0f;
            controlsRoot.SetActive(!running || game.Duration < 8f);

            bool usefulMessage = game.Message == "FUEL +26" || game.Message == "POLICE CLOSER" || game.Message == "LANDED";
            bool showMessage = running && game.MessageRemaining > 0f && usefulMessage;
            messageGroup.alpha = showMessage ? Mathf.Clamp01(game.MessageRemaining * 3f) : 0f;
            if (showMessage) message.text = game.Message == "FUEL +26" ? "Fuel topped up." : game.Message == "LANDED" ? "Nice landing!" : "They're getting close.";
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
            scaler.referenceResolution = new Vector2(1600f, 900f);
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
            BuildHeader(hudRoot);
            BuildControls(hudRoot);
            BuildFuel(hudRoot);
            BuildMirror(hudRoot);

            RectTransform hintRoot = Rect("SectionHint", hudRoot);
            Place(hintRoot, new Vector2(.5f, 1f), new Vector2(-60f, -155f), new Vector2(720f, 60f));
            hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();
            sectionHint = Label("Hint", hintRoot, string.Empty, 20f, White, TextAlignmentOptions.Center);
            Stretch(sectionHint.rectTransform);
            sectionHint.fontStyle = FontStyles.Bold;
            sectionHint.outlineWidth = .16f;
            sectionHint.outlineColor = new Color(0f, 0f, 0f, .8f);

            RectTransform messageRoot = Panel("Message", safeRoot, Ink);
            Place(messageRoot, new Vector2(.5f, 0f), new Vector2(0f, 132f), new Vector2(760f, 54f));
            messageGroup = messageRoot.gameObject.AddComponent<CanvasGroup>();
            messageGroup.interactable = false;
            messageGroup.blocksRaycasts = false;
            message = Label("MessageText", messageRoot, string.Empty, 20f, Day, TextAlignmentOptions.Center);
            Stretch(message.rectTransform, 24f, 6f, 24f, 6f);

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

        private void BuildHeader(RectTransform parent)
        {
            RectTransform header = Panel("Header", parent, Ink);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1f);
            header.offsetMin = new Vector2(30f, -118f);
            header.offsetMax = new Vector2(-30f, -26f);
            RectTransform world = Panel("WorldCapsule", header, new Color(Day.r, Day.g, Day.b, .11f));
            Place(world, new Vector2(0f, .5f), new Vector2(104f, 0f), new Vector2(168f, 56f));
            worldBackground = world.GetComponent<Image>();
            RectTransform mark = Panel("WorldIndicator", world, Day);
            Place(mark, new Vector2(0f, .5f), new Vector2(17f, 0f), new Vector2(4f, 32f));
            worldMark = mark.GetComponent<Image>();
            worldLabel = FixedLabel(world, "DAY", 26f, Day, 30f, 4f, 126f, 31f, true);
            worldLabel.enableAutoSizing = false; // Refresh sets the compact joined-world title explicitly.
            worldSubLabel = FixedLabel(world, "CURRENT WORLD", 10f, Muted, 31f, 36f, 130f, 14f);
            worldSubLabel.characterSpacing = 1.4f;

            sectionLabel = Label("Section", header, "CITY RUN", 23f, White, TextAlignmentOptions.Left);
            sectionLabel.fontStyle = FontStyles.Bold;
            sectionLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            sectionLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            sectionLabel.rectTransform.pivot = new Vector2(.5f, 1f);
            sectionLabel.rectTransform.offsetMin = new Vector2(212f, -50f);
            sectionLabel.rectTransform.offsetMax = new Vector2(-370f, -15f);

            progressLabel = Label("Extraction", header, "00%  /  EXTRACTION", 16f, Muted, TextAlignmentOptions.Right);
            Place(progressLabel.rectTransform, Vector2.one, new Vector2(-270f, -32f), new Vector2(250f, 30f));
            clockLabel = Label("ActiveTime", header, "0:00", 27f, White, TextAlignmentOptions.Right);
            Place(clockLabel.rectTransform, Vector2.one, new Vector2(-75f, -44f), new Vector2(100f, 45f));

            RectTransform track = Panel("RouteProgress", header, new Color(.22f, .28f, .35f, .7f));
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(.5f, 0f);
            track.offsetMin = new Vector2(214f, 23f);
            track.offsetMax = new Vector2(-145f, 27f);
            progressFill = Panel("ProgressFill", track, Day);
            Stretch(progressFill);
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressImage = progressFill.GetComponent<Image>();
        }

        private void BuildControls(RectTransform parent)
        {
            RectTransform panel = Panel("Controls", parent, Ink);
            controlsRoot = panel.gameObject;
            Place(panel, new Vector2(.5f, 0f), new Vector2(0f, 55f), new Vector2(810f, 56f));
            Key(panel, "A / D", 20f, 76f);
            FixedLabel(panel, "OR ARROWS", 14f, Muted, 108f, 17f, 110f, 23f);
            steerControl = FixedLabel(panel, "STEER", 15f, White, 225f, 16f, 76f, 25f, true);
            Key(panel, "SPACE", 334f, 94f);
            switchControl = FixedLabel(panel, "SWITCH", 15f, White, 443f, 16f, 103f, 25f, true);
            Key(panel, "ESC", 606f, 60f);
            FixedLabel(panel, "PAUSE", 15f, Muted, 681f, 16f, 95f, 25f);
        }

        private void BuildFuel(RectTransform parent)
        {
            RectTransform panel = Panel("Fuel", parent, Ink);
            TopLeft(panel, 30f, 136f, 235f, 64f);
            fuelBorder = panel.GetComponent<Image>();
            panel.gameObject.SetActive(!game.TutorialLevel);
            fuelLabel = FixedLabel(panel, "FUEL  100%", 16f, Day, 16f, 10f, 205f, 22f, true);
            RectTransform track = Panel("FuelTrack", panel, Surface);
            TopLeft(track, 16f, 39f, 203f, 7f);
            fuelFill = Panel("FuelRemaining", track, Day);
            Stretch(fuelFill);
            fuelImage = fuelFill.GetComponent<Image>();
            speedLabel = FixedLabel(panel, "101 KM/H", 14f, Muted, 16f, 58f, 203f, 20f);

            fuelWarningEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform edge = Panel("LowFuelEdge", parent, Color.clear);
                Stretch(edge);
                if (i < 2)
                {
                    edge.anchorMin = new Vector2(i, 0f); edge.anchorMax = new Vector2(i, 1f);
                    edge.pivot = new Vector2(i, .5f); edge.sizeDelta = new Vector2(20f, 0f);
                }
                else
                {
                    edge.anchorMin = new Vector2(0f, i - 2); edge.anchorMax = new Vector2(1f, i - 2);
                    edge.pivot = new Vector2(.5f, i - 2); edge.sizeDelta = new Vector2(0f, 16f);
                }
                fuelWarningEdges[i] = edge.GetComponent<Image>();
            }
        }

        private void BuildMirror(RectTransform parent)
        {
            mirrorRoot = Panel("RearViewMirror", parent, White);
            mirrorRoot.anchorMin = mirrorRoot.anchorMax = Vector2.one;
            mirrorRoot.pivot = Vector2.one;
            mirrorRoot.anchoredPosition = new Vector2(-30f, -136f);
            mirrorRoot.sizeDelta = new Vector2(320f, 110f);
            mirrorBorder = mirrorRoot.GetComponent<Image>();
            RectTransform inside = Panel("MirrorBacking", mirrorRoot, Ink);
            Stretch(inside, 2f, 2f, 2f, 2f);
            RectTransform imageRoot = Rect("PoliceRearView", mirrorRoot);
            TopLeft(imageRoot, 4f, 4f, 312f, 78f);
            mirrorImage = imageRoot.gameObject.AddComponent<RawImage>();
            mirrorImage.color = Color.white;
            mirrorImage.raycastTarget = false;
            pursuitLabel = FixedLabel(mirrorRoot, "POLICE", 13f, White, 10f, 85f, 300f, 19f, true);
            pursuitLabel.characterSpacing = 1.1f;
        }

        private GameObject BuildReady(RectTransform parent)
        {
            RectTransform card = MenuCard(parent, "Ready", 780f, 370f, Echo);
            FixedLabel(card, "City practice", 42f, White, 42f, 35f, 696f, 65f, true);
            FixedLabel(card, "A / D to steer. Space to switch worlds.", 24f, Echo, 42f, 130f, 696f, 42f);
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
