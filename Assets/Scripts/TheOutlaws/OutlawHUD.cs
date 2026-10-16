using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OutlawHUD : MonoBehaviour
{
    private const float RearViewSize = 230f;
    private const float Margin = 20f;
    private const float MissionRowHeight = 50f;
    private const float LowFuelFraction = 0.25f;

    private static readonly Color AmmoBlue = new Color(0.12f, 0.55f, 1f);
    private static readonly Color PanelColour = new Color(0.03f, 0.04f, 0.06f, 0.96f);
    private static readonly Color Gold = new Color(1f, 0.86f, 0.18f);
    private static readonly Color Good = new Color(0.3f, 0.95f, 0.45f);
    private static readonly Color Bad = new Color(1f, 0.45f, 0.2f);
    private static readonly Color PoliceRed = new Color(1f, 0.23f, 0.19f);
    private static readonly Color FuelOrange = new Color(1f, 0.45f, 0.05f);
    private static readonly Color LowFuelRed = new Color(0.85f, 0.02f, 0.05f);
    private static readonly Color PoliceBlue = new Color(0.18f, 0.48f, 1f);
    private static readonly Color ShieldCyan = new Color(0.3f, 0.9f, 1f);
    private static readonly Color ReversePurple = new Color(0.78f, 0.35f, 1f);
    private static readonly Color NitroOrange = new Color(1f, 0.55f, 0.1f);

    private OutlawGameManager game;
    [SerializeField] private Image ammoBar;
    [SerializeField] private TMP_Text ammoLabel;
    [SerializeField] private TMP_Text extractionLabel;
    private RearViewCamera rearView;
    private float missionToastRemaining;
    private string missionToastTitle;
    private string missionToastHeading;
    private string messageText;
    private Color messageColour;
    private float messageRemaining;
    private bool wasPoliceAlert;
    private bool wasBanner;
    private int shownReverseActivations;
    private int shownReverseCompletions;
    private int shownPickupCount;
    private int shownShieldUses;
    private bool countersSynced;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;
    private GUIStyle bigStyle;
    private GUIStyle keyStyle;
    private GUIStyle leftStyle;
    private GUIStyle rightStyle;
    private float uiScale = 1f;

    public void Configure(OutlawGameManager manager)
    {
        game = manager;
        // Arriving through an environment switch.
        if (RunSession.HasPending)
            ShowMessage(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Env_Snow" ? "PORTAL! YOU ENTERED THE MOUNTAINS" : "PORTAL! BACK TO THE CITY", ShieldCyan, 3.5f);
        // Bind saved UI without rebuilding or repositioning it during Play.
        if (ammoBar == null) ammoBar = FindImage("AmmoBar");
        if (ammoLabel == null)
            ammoLabel = GameObject.Find("AMMO Label")?.GetComponent<TMP_Text>();
        if (extractionLabel == null)
            extractionLabel = GameObject.Find("EXTRACTION Label")?.GetComponent<TMP_Text>();
        if (ammoBar == null || ammoLabel == null)
            Debug.LogError("The Outlaws HUD is missing saved UI references. Assign them in the Inspector.", this);

        // Ammo is drawn as one blue square per bullet; the bar itself is hidden.
        if (ammoBar != null) ammoBar.enabled = false;
        // The HUD draws the fuel/ammo column itself so it lines up exactly.
        if (ammoLabel != null) ammoLabel.gameObject.SetActive(false);
        Image fuelBar = FindImage("FuelBar");
        if (fuelBar != null) fuelBar.enabled = false;
        GameObject fuelLabel = GameObject.Find("FUEL Label");
        if (fuelLabel != null) fuelLabel.SetActive(false);

        // Centre the distance readout (the scene places it off-centre).
        GameObject score = GameObject.Find("Score");
        if (score != null)
        {
            RectTransform scoreRect = score.GetComponent<RectTransform>();
            scoreRect.anchoredPosition = new Vector2(0f, -18f);
            TMP_Text scoreText = score.GetComponent<TMP_Text>();
            if (scoreText != null) scoreText.alignment = TextAlignmentOptions.Center;
        }

        // The rear-view camera replaces the police distance bar.
        GameObject proximity = GameObject.Find("Proximity");
        if (proximity != null) proximity.SetActive(false);
        if (game.Player != null) rearView = RearViewCamera.Create(game.Player, game.PoliceCars);

        // Power-up feedback is shown in one place (under the rear view), not as pop-ups.
        PickupHUD popups = Object.FindFirstObjectByType<PickupHUD>();
        if (popups != null) popups.enabled = false;
    }

    private void OnDestroy()
    {
        if (rearView != null) Destroy(rearView.gameObject);
    }

    public void ShowMissionComplete(MissionDefinition mission)
    {
        missionToastHeading = $"MISSION {mission.Id} COMPLETE!";
        missionToastTitle = mission.Title;
        missionToastRemaining = 3.2f;
    }

    private void Update()
    {
        if (game == null) return;
        float dt = Time.unscaledDeltaTime;
        bool running = game.State == OutlawGameState.Running;
        missionToastRemaining = Mathf.Max(0f, missionToastRemaining - (running ? dt : 0f));
        messageRemaining = Mathf.Max(0f, messageRemaining - (running ? dt : 0f));
        if (!countersSynced && game.PickupEffects != null)
        {
            // A run carried over from the other scene restores its counters in Start;
            // adopt them so they don't read as fresh events.
            shownPickupCount = game.PickupEffects.PickupCount;
            shownShieldUses = game.PickupEffects.ShieldUseCount;
            shownReverseActivations = game.PickupEffects.ReverseActivationCount;
            shownReverseCompletions = game.PickupEffects.ReverseCompletionCount;
            countersSynced = true;
        }
        if (game.PickupEffects != null && game.PickupEffects.PickupCount != shownPickupCount)
        {
            shownPickupCount = game.PickupEffects.PickupCount;
            RandomPickupOutcome outcome = game.PickupEffects.LastOutcome;
            if (outcome == RandomPickupOutcome.Nitro) ShowMessage("NITRO ACTIVATED!", NitroOrange);
            else if (outcome == RandomPickupOutcome.Shield) ShowMessage("SHIELD ACTIVATED!", ShieldCyan);
            else if (outcome == RandomPickupOutcome.ProximityRecovery) ShowMessage("POLICE JAMMED!", Good);
        }

        if (game.PickupEffects != null && game.PickupEffects.ShieldUseCount != shownShieldUses)
        {
            shownShieldUses = game.PickupEffects.ShieldUseCount;
            ShowMessage("HIT BLOCKED!", ShieldCyan, 1.5f);
        }

        // Events that replace the centred message (most recent wins).
        if (game.PickupEffects != null)
        {
            if (game.PickupEffects.ReverseActivationCount != shownReverseActivations)
            {
                shownReverseActivations = game.PickupEffects.ReverseActivationCount;
                ShowMessage("CONTROLS SWAPPED!", ReversePurple);
            }
            if (game.PickupEffects.ReverseCompletionCount != shownReverseCompletions)
            {
                shownReverseCompletions = game.PickupEffects.ReverseCompletionCount;
                ShowMessage("CONTROLS NORMAL", Good, 1.5f);
            }
        }
        bool banner = game.RunStartBannerRemaining > 0f && !game.IsTutorial;
        if (banner && !wasBanner) ShowMessage("GO! ESCAPE THE COPS!", Gold, 2f);
        wasBanner = banner;
        bool alert = game.PoliceAlertActive && !game.IsTutorial;
        if (alert && !wasPoliceAlert) ShowMessage("POLICE CLOSING IN!", PoliceRed, 2f);
        wasPoliceAlert = alert;

        Keyboard keyboard = Keyboard.current;
        if (game.State == OutlawGameState.Ready && keyboard != null && keyboard.tKey.wasPressedThisFrame)
            game.SetTutorialSelected(!game.TutorialSelected);
    }

    public void BuildLabeledBars()
    {
        // This layout builder is used only by the explicit editor setup command.
        if (Application.isPlaying) return;
        Image fuelBar = FindImage("FuelBar");
        Image policeBar = FindImage("ProximityFill");

        if (fuelBar != null)
        {
            RectTransform fuelRect = fuelBar.rectTransform;
            ammoBar = FindImage("AmmoBar");
            if (ammoBar == null) fuelRect.anchoredPosition += Vector2.down * 30f;
            CreateLabel(fuelRect, "FUEL");

            if (ammoBar == null)
            {
                GameObject ammoObject = Instantiate(fuelBar.gameObject, fuelRect.parent);
                ammoObject.name = "AmmoBar";
                ammoBar = ammoObject.GetComponent<Image>();
            }
            ammoBar.color = AmmoBlue;
            ammoBar.fillAmount = 2f / 5f;
            RectTransform ammoRect = ammoBar.rectTransform;
            ammoRect.anchoredPosition = fuelRect.anchoredPosition + Vector2.down * 104f;
            ammoLabel = CreateLabel(ammoRect, "AMMO");
            ammoLabel.text = "AMMO";
        }

        if (policeBar != null)
        {
            policeBar.fillAmount = RiskRunState.InitialPursuitGap / RiskRunState.MaximumPursuitGap;
            CreateLabel(policeBar.rectTransform, "POLICE DISTANCE");
            extractionLabel = CreateLabel(policeBar.rectTransform, "EXTRACTION");
            extractionLabel.fontSize = 18f;
            extractionLabel.text = "DISTANCE: 0 m";
            extractionLabel.rectTransform.anchoredPosition = policeBar.rectTransform.anchoredPosition
                + Vector2.down * (policeBar.rectTransform.rect.height
                    * Mathf.Abs(policeBar.rectTransform.localScale.y) * 0.5f + 22f);
        }
    }

    private static Image FindImage(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform bar, string text)
    {
        Transform existing = bar.parent.Find(text + " Label");
        if (existing != null) return existing.GetComponent<TextMeshProUGUI>();
        GameObject labelObject = new GameObject(text + " Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.layer = bar.gameObject.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(bar.parent, false);
        labelRect.anchorMin = bar.anchorMin;
        labelRect.anchorMax = bar.anchorMax;
        labelRect.pivot = bar.pivot;
        labelRect.sizeDelta = new Vector2(bar.sizeDelta.x, 28f);
        labelRect.anchoredPosition = bar.anchoredPosition + Vector2.up
            * (bar.rect.height * Mathf.Abs(bar.localScale.y) * 0.5f + 18f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 60,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        titleStyle.normal.textColor = Color.white;

        bigStyle = new GUIStyle(titleStyle) { fontSize = 40 };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true
        };
        bodyStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        smallStyle.normal.textColor = Color.white;

        leftStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = 19 };
        rightStyle = new GUIStyle(leftStyle) { alignment = TextAnchor.MiddleRight, fontSize = 16 };

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        keyStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };

    }

    private void OnGUI()
    {
        if (game == null)
        {
            return;
        }

        EnsureStyles();
        uiScale = Mathf.Clamp(Screen.width / 1280f, 0.65f, 1.5f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        float width = Screen.width / uiScale;
        float height = Screen.height / uiScale;

        if (game.State == OutlawGameState.Ready)
        {
            DrawStartMenu(width, height);
        }
        else if (game.State == OutlawGameState.Won || game.State == OutlawGameState.Lost)
        {
            DrawRunningHUD(width, height);
            DrawResult(width, height);
        }
        else
        {
            DrawRunningHUD(width, height);
            if (game.State == OutlawGameState.Paused) DrawPauseMenu(width, height);
        }

        GUI.matrix = previous;
    }

    // ---------- Title ----------

    private void DrawStartMenu(float width, float height)
    {
        const float panelWidth = 560f, padding = 44f;
        int missionRows = Mathf.Max(1, OutlawProfile.VisibleMissions().Count);
        float panelHeight = 384f + missionRows * MissionRowHeight;
        Rect panel = new Rect((width - panelWidth) * 0.5f, Mathf.Max(10f, (height - panelHeight) * 0.5f), panelWidth, panelHeight);
        DrawPanel(panel);
        float inner = panel.width - padding * 2f;
        float x = panel.x + padding;
        float y = panel.y + 26f;

        // Title and best distance.
        GUIStyle title = new GUIStyle(titleStyle) { fontSize = 52 };
        GUI.Label(new Rect(panel.x, y, panel.width, 62f),
            $"<color={Hex(PoliceRed)}>WEE</color> <color={Hex(PoliceBlue)}>WOO</color> RUN", title);
        y += 62f;
        GUIStyle best = new GUIStyle(smallStyle) { fontSize = 16 };
        GUI.Label(new Rect(panel.x, y, panel.width, 24f), OutlawProfile.BestDistance > 0
            ? $"<color={Hex(Gold)}>BEST  {OutlawProfile.BestDistance:N0} m</color>"
            : $"<color={Hex(Gold)}>ESCAPE THE COPS</color>", best);
        y += 40f;

        // Tutorial switch.
        DrawRect(new Rect(x, y, inner, 1f), new Color(1f, 1f, 1f, 0.15f));
        y += 16f;
        GUIStyle rowLabel = new GUIStyle(leftStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(x, y, 200f, 36f), "TUTORIAL", rowLabel);
        if (DrawSwitch(new Rect(x + inner - 120f, y, 120f, 36f), game.TutorialSelected))
            game.SetTutorialSelected(!game.TutorialSelected);
        y += 52f;
        DrawRect(new Rect(x, y, inner, 1f), new Color(1f, 1f, 1f, 0.15f));
        y += 18f;

        // Missions.
        y = DrawMissionList(new Rect(x, y, inner, 0f), false);

        // Start.
        if (GUI.Button(new Rect(panel.x + (panel.width - 260f) * 0.5f, panel.y + panelHeight - 86f, 260f, 58f), "START", buttonStyle))
        {
            game.BeginGame();
        }
    }

    // ---------- Running ----------

    private void DrawRunningHUD(float width, float height)
    {
        DrawAmmoSquares();
        DrawRearView(width);
        DrawPowerUps(width);
        DrawMessage(width);
        DrawPortalCountdown(width);

        if (game.IsTutorial) DrawTutorial(width, height);

        DrawLowFuelWarning(width, height);

        // Mission toasts sit in the top-left corner, under the ammo, away from the road.
        if (missionToastRemaining > 0f && game.State == OutlawGameState.Running)
        {
            GUIStyle line = new GUIStyle(leftStyle) { fontSize = 17 };
            // Wide enough for both lines (the bold heading is usually the longer one).
            GUIStyle bold = new GUIStyle(line) { fontStyle = FontStyle.Bold };
            float textWidth = Mathf.Max(line.CalcSize(new GUIContent(missionToastTitle)).x,
                bold.CalcSize(new GUIContent(missionToastHeading)).x) + 12f;
            Rect toast = new Rect(Margin, 180f, textWidth + 28f, 60f);
            DrawPanel(toast);
            DrawBorder(toast, 2f, Good);
            GUI.Label(new Rect(toast.x + 14f, toast.y + 4f, toast.width, 26f),
                $"<b><color={Hex(Good)}>{missionToastHeading}</color></b>", line);
            GUI.Label(new Rect(toast.x + 14f, toast.y + 30f, toast.width, 26f), missionToastTitle, line);
        }
    }

    private void DrawPortalCountdown(float width)
    {
        if (game.IsTutorial ||
            (game.State != OutlawGameState.Running && game.State != OutlawGameState.Paused))
            return;

        int seconds = Mathf.CeilToInt(RunSession.SecondsUntilPortal(game.ElapsedSeconds));
        string label = seconds > 0 ? $"PORTAL IN {seconds} s" : "PORTAL AHEAD";
        Rect panel = new Rect((width - 280f) * 0.5f, 112f, 280f, 34f);
        DrawPanel(panel);
        GUI.Label(panel, $"<color={Hex(ShieldCyan)}>{label}</color>", bodyStyle);
    }

    /// <summary>One centred message under the distance; the most recent event replaces the last.</summary>
    private void DrawMessage(float width)
    {
        if (game.State != OutlawGameState.Running && game.State != OutlawGameState.Paused) return;
        string text = messageText;
        Color colour = messageColour;
        CarPickupEffects effects = game.PickupEffects;
        // The control-swap countdown is live, so it always shows its current number.
        if (effects != null && effects.ReverseCountdownRemaining > 0f)
        {
            text = $"CONTROLS SWAP IN {Mathf.CeilToInt(effects.ReverseCountdownRemaining)}";
            colour = ReversePurple;
        }
        else if (messageRemaining <= 0f) return;
        if (string.IsNullOrEmpty(text)) return;

        GUIStyle style = new GUIStyle(bodyStyle) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = false };
        float textWidth = style.CalcSize(new GUIContent(text)).x + 32f;
        Rect rect = new Rect((width - textWidth) * 0.5f, 66f, textWidth, 38f);
        DrawRect(rect, new Color(colour.r * 0.2f, colour.g * 0.2f, colour.b * 0.2f, 0.92f));
        DrawBorder(rect, 2f, colour);
        style.normal.textColor = colour;
        GUI.Label(rect, text, style);
    }

    private void ShowMessage(string text, Color colour, float seconds = 2f)
    {
        messageText = text;
        messageColour = colour;
        messageRemaining = seconds;
    }

    private bool FuelLow => game.Fuel != null && game.Fuel.NormalizedFuel < LowFuelFraction;

    /// <summary>A pulsing edge vignette that communicates danger without covering the road.</summary>
    private void DrawLowFuelWarning(float width, float height)
    {
        if (!FuelLow || game.State != OutlawGameState.Running) return;

        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        float edge = Mathf.Lerp(28f, 46f, pulse);
        Color vignette = new Color(LowFuelRed.r, LowFuelRed.g, LowFuelRed.b,
            Mathf.Lerp(0.08f, 0.22f, pulse));

        DrawRect(new Rect(0f, 0f, width, edge), vignette);
        DrawRect(new Rect(0f, height - edge, width, edge), vignette);
        DrawRect(new Rect(0f, edge, edge, Mathf.Max(0f, height - edge * 2f)), vignette);
        DrawRect(new Rect(width - edge, edge, edge, Mathf.Max(0f, height - edge * 2f)), vignette);

        GUIStyle warning = new GUIStyle(leftStyle)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        warning.normal.textColor = new Color(1f, 0.22f, 0.18f,
            Mathf.Lerp(0.72f, 1f, pulse));
        GUI.Label(new Rect(304f, 42f, 180f, 30f), "LOW FUEL", warning);
    }

    /// <summary>Top-left column, all left-aligned: FUEL label, fuel bar, AMMO label, one square per bullet.</summary>
    private void DrawAmmoSquares()
    {
        if (game.Shooting == null || game.Fuel == null) return;
        const float x = 24f, columnWidth = 270f, gap = 10f;
        int maximum = game.Shooting.MaximumAmmo;
        float size = (columnWidth - gap * (maximum - 1)) / maximum;
        GUIStyle label = new GUIStyle(leftStyle) { fontSize = 17, fontStyle = FontStyle.Bold };

        // Fuel.
        GUI.Label(new Rect(x, 18f, columnWidth, 24f), "FUEL", label);
        Rect bar = new Rect(x, 44f, columnWidth, 26f);
        DrawRect(bar, new Color(0f, 0f, 0f, 0.45f));
        DrawRect(new Rect(bar.x, bar.y, bar.width * game.Fuel.NormalizedFuel, bar.height), FuelLow ? LowFuelRed : FuelOrange);
        DrawBorder(bar, 2f, FuelLow ? PoliceRed : new Color(1f, 1f, 1f, 0.5f));
        // Ammo.
        GUI.Label(new Rect(x, 80f, columnWidth, 24f), "AMMO", label);
        float y = 106f;
        int ammo = game.Shooting.CurrentAmmo;
        for (int i = 0; i < maximum; i++)
        {
            Rect square = new Rect(x + i * (size + gap), y, size, size);
            bool filled = i < ammo;
            DrawRect(square, filled ? AmmoBlue : new Color(0f, 0f, 0f, 0.35f));
            DrawBorder(square, 2f, filled ? new Color(0.75f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.35f));
        }
        if (game.IsTutorial && game.Tutorial.Highlight == TutorialHighlight.Ammo)
            DrawHighlight(new Rect(x - 10f, y - 10f, columnWidth + 20f, size + 20f));
        if (game.IsTutorial && game.Tutorial.Highlight == TutorialHighlight.Fuel)
            DrawHighlight(new Rect(x - 10f, bar.y - 10f, columnWidth + 20f, bar.height + 20f));
    }

    private void DrawRearView(float width)
    {
        if (rearView == null || rearView.Texture == null) return;
        Rect view = new Rect(width - RearViewSize - Margin, Margin, RearViewSize, RearViewSize);
        float gap = game.PursuitGap;
        // The border turns red as the police get closer (no blinking).
        float danger = Mathf.InverseLerp(30f, 8f, gap);
        Color border = Color.Lerp(new Color(0.85f, 0.9f, 1f), PoliceRed, danger);
        const float thickness = 5f;

        DrawRect(new Rect(view.x - thickness, view.y - thickness, view.width + thickness * 2f, view.height + thickness * 2f), border);
        GUI.DrawTexture(view, rearView.Texture, ScaleMode.ScaleAndCrop, false);
        Rect tag = new Rect(view.x, view.y, view.width, 28f);
        DrawRect(tag, new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(tag, "<b>BEHIND YOU</b>", smallStyle);

        if (game.IsTutorial && game.Tutorial.Highlight == TutorialHighlight.RearView)
            DrawHighlight(new Rect(view.x - 16f, view.y - 16f, view.width + 32f, view.height + 32f));
    }

    /// <summary>All power-up state lives in one small stack of tiles under the rear view.</summary>
    private void DrawPowerUps(float width)
    {
        CarPickupEffects effects = game.PickupEffects;
        if (effects == null) return;
        // Only one power-up can be active at a time; show just that one.
        string label = null;
        string seconds = null;
        Color colour = Color.white;
        if (effects.ActiveEffect == PickupEffectType.ReverseSteering)
        {
            label = "CONTROLS SWAPPED";
            seconds = Mathf.CeilToInt(effects.RemainingSeconds) + "s";
            colour = ReversePurple;
        }
        else if (effects.RunState.HasNitro)
        {
            label = "NITRO";
            colour = NitroOrange;
        }
        else if (effects.ShieldReady)
        {
            label = "SHIELD";
            colour = ShieldCyan;
        }
        if (label == null) return;

        GUIStyle text = new GUIStyle(leftStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
        float x = width - RearViewSize - Margin;
        Rect rect = new Rect(x, Margin + RearViewSize + 14f, RearViewSize, 30f);
        DrawPanel(rect);
        DrawRect(new Rect(rect.x, rect.y, 6f, rect.height), colour);
        text.normal.textColor = colour;
        GUI.Label(new Rect(rect.x + 14f, rect.y, rect.width - 60f, rect.height), label, text);
        if (seconds != null)
        {
            GUIStyle right = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 10f, rect.height), seconds, right);
        }
    }

    private void DrawTutorial(float width, float height)
    {
        TutorialDirector tutorial = game.Tutorial;
        if (tutorial.Title == null) return;
        Keyboard keys = Keyboard.current;
        bool steering = tutorial.Step == TutorialStep.Steer || tutorial.Step == TutorialStep.Dodge;
        bool shooting = tutorial.Step == TutorialStep.Shoot && tutorial.Highlight != TutorialHighlight.Ammo;
        bool showKeys = steering || shooting;

        // One box with one instruction; "TRY AGAIN!" briefly takes its place after a miss.
        float panelHeight = shooting ? 220f : showKeys ? 160f : 80f;
        Rect panel = new Rect((width - 520f) * 0.5f, height - panelHeight - 60f, 520f, panelHeight);
        DrawPanel(panel);
        bool retry = tutorial.FeedbackRemaining > 0f && !string.IsNullOrEmpty(tutorial.Feedback);
        string heading = retry
            ? $"<color={Hex(Bad)}>{tutorial.Feedback}</color>"
            : $"<color={Hex(Gold)}>{tutorial.Title}</color>";
        GUI.Label(new Rect(panel.x, panel.y + 12f, panel.width, 56f), heading, bigStyle);

        if (steering)
        {
            float keyY = panel.y + 80f;
            bool left = tutorial.Step == TutorialStep.Steer ? tutorial.LeftDone
                : keys != null && (keys.aKey.isPressed || keys.leftArrowKey.isPressed);
            bool right = tutorial.Step == TutorialStep.Steer ? tutorial.RightDone
                : keys != null && (keys.dKey.isPressed || keys.rightArrowKey.isPressed);
            DrawKey(new Rect(panel.center.x - 230f, keyY, 220f, 60f), "A / ← LEFT", left);
            DrawKey(new Rect(panel.center.x + 10f, keyY, 220f, 60f), "RIGHT D / →", right);
        }
        else if (shooting)
        {
            GUI.Label(new Rect(panel.x + 20f, panel.y + 70f, panel.width - 40f, 58f),
                "Number on block = shots to destroy\n1 = 1 shot    2 = 2 shots", bodyStyle);
            bool held = keys != null && keys.spaceKey.isPressed;
            DrawKey(new Rect(panel.center.x - 150f, panel.y + 140f, 300f, 60f), "SPACE", held);
        }

        GUI.Label(new Rect(0f, height - 42f, width, 30f), "TUTORIAL   (Esc: pause)", smallStyle);
    }

    // ---------- Pause and results ----------

    private void DrawPauseMenu(float width, float height)
    {
        DrawRect(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.45f));
        int missionRows = Mathf.Max(1, RunTracker.Active ? RunTracker.ActiveMissions.Count : OutlawProfile.VisibleMissions().Count);
        float panelHeight = 310f + missionRows * MissionRowHeight;
        Rect panel = new Rect((width - 640f) * 0.5f, Mathf.Max(10f, (height - panelHeight) * 0.5f), 640f, panelHeight);
        DrawPanel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 16f, panel.width, 64f), "PAUSED", bigStyle);
        DrawMissionList(new Rect(panel.x + 50f, panel.y + 92f, panel.width - 100f, 0f), true);

        float buttonY = panel.y + panelHeight - 130f;
        if (GUI.Button(new Rect(panel.x + 70f, buttonY, 230f, 60f), "RESUME", buttonStyle))
            game.Resume();
        if (GUI.Button(new Rect(panel.x + panel.width - 300f, buttonY, 230f, 60f), "RESTART", buttonStyle))
            game.Restart();
        GUI.Label(new Rect(panel.x, panel.y + panelHeight - 52f, panel.width, 28f), "Esc / P: resume", smallStyle);
    }

    /// <summary>Game-over card: what ended the run, the distance, best, this run's missions, restart.</summary>
    private void DrawResult(float width, float height)
    {
        int missionRows = Mathf.Max(1, RunTracker.ActiveMissions.Count);
        float panelHeight = 330f + missionRows * MissionRowHeight;
        Rect panel = new Rect((width - 480f) * 0.5f, Mathf.Max(10f, (height - panelHeight) * 0.5f), 480f, panelHeight);
        DrawPanel(panel);
        bool fuelOut = game.EndReason == "fuel";
        GUIStyle heading = new GUIStyle(bigStyle) { fontSize = 32 };
        GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 42f), fuelOut ? "OUT OF FUEL" : "CAUGHT", heading);
        GUIStyle distance = new GUIStyle(titleStyle) { fontSize = 50 };
        GUI.Label(new Rect(panel.x, panel.y + 64f, panel.width, 60f), $"{game.DistanceTravelled:N0} m", distance);
        GUI.Label(new Rect(panel.x, panel.y + 126f, panel.width, 26f), game.IsNewBest
            ? $"<b><color={Hex(Gold)}>NEW BEST!</color></b>"
            : $"<color={Hex(Gold)}>BEST  {OutlawProfile.BestDistance:N0} m</color>", bodyStyle);

        DrawRect(new Rect(panel.x + 30f, panel.y + 162f, panel.width - 60f, 2f), new Color(1f, 1f, 1f, 0.15f));
        float y = DrawMissionList(new Rect(panel.x + 40f, panel.y + 172f, panel.width - 80f, 0f), true);

        if (GUI.Button(new Rect(panel.x + 120f, y + 6f, panel.width - 240f, 54f), "RESTART", buttonStyle))
        {
            game.Restart();
        }
    }

    /// <summary>
    /// "MISSIONS n/10" and a list of missions in order (lowest number on top).
    /// Title: the current missions. During/after a run: the missions the run started with,
    /// ticked when completed, otherwise with live progress.
    /// </summary>
    private float DrawMissionList(Rect area, bool forRun)
    {
        const float rowHeight = MissionRowHeight;
        int done = OutlawProfile.MissionsDoneCount;
        GUIStyle header = new GUIStyle(leftStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(area.x, area.y, 300f, 30f), "MISSIONS", header);
        GUIStyle count = new GUIStyle(rightStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        count.normal.textColor = Gold;
        GUI.Label(new Rect(area.x, area.y, area.width, 30f), $"{done} / {MissionCatalog.All.Length} DONE", count);
        float y = area.y + 50f;

        List<MissionDefinition> missions = forRun && RunTracker.ActiveMissions.Count > 0 && (RunTracker.Active || game.State != OutlawGameState.Paused)
            ? new List<MissionDefinition>(RunTracker.ActiveMissions)
            : OutlawProfile.VisibleMissions();
        missions.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (missions.Count == 0)
        {
            GUI.Label(new Rect(area.x, y, area.width, 34f),
                $"<color={Hex(Good)}>All missions complete. Legend!</color>", leftStyle);
            return y + rowHeight;
        }

        bool live = forRun && RunTracker.ActiveMissions.Count > 0 && !game.IsTutorial;
        MissionRunStats stats = live ? RunTracker.Stats : OutlawProfile.SavedTotals();
        GUIStyle number = new GUIStyle(leftStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUIStyle title = new GUIStyle(leftStyle) { fontSize = 18 };
        GUIStyle progressStyle = new GUIStyle(rightStyle) { fontSize = 14 };
        foreach (MissionDefinition mission in missions)
        {
            bool ticked = live && RunTracker.CompletedThisRun.Contains(mission);
            DrawCheckbox(new Rect(area.x, y + 6f, 24f, 24f), ticked);
            number.normal.textColor = ticked ? Good : Gold;
            GUI.Label(new Rect(area.x + 38f, y - 6f, 200f, 20f), ticked ? $"MISSION {mission.Id} - DONE!" : $"MISSION {mission.Id}", number);
            title.normal.textColor = ticked ? new Color(1f, 1f, 1f, 0.7f) : Color.white;
            FitLabel(new Rect(area.x + 38f, y + 12f, area.width - 38f, 24f), mission.Title, title, 13);

            string progress = null;
            if (!ticked && (live || mission.Cumulative))
                progress = mission.IsFailed(stats) ? $"<color={Hex(Bad)}>{mission.Progress(stats)}</color>" : mission.Progress(stats);
            else if (!ticked && forRun && game.IsTutorial)
                progress = "After the tutorial";
            if (progress != null)
                FitLabel(new Rect(area.x + 220f, y - 6f, area.width - 220f, 20f), progress, progressStyle, 11);
            y += rowHeight;
        }
        return y;
    }

    /// <summary>Draws a label, shrinking the font until the text fits its rect.</summary>
    private static void FitLabel(Rect rect, string text, GUIStyle style, int minimumSize)
    {
        var fitted = new GUIStyle(style);
        while (fitted.fontSize > minimumSize && fitted.CalcSize(new GUIContent(text)).x > rect.width)
            fitted.fontSize--;
        GUI.Label(rect, text, fitted);
    }

    // ---------- Drawing helpers ----------

    private bool DrawSwitch(Rect rect, bool on)
    {
        DrawRect(rect, on ? new Color(0.15f, 0.6f, 0.3f) : new Color(0.3f, 0.3f, 0.34f));
        Rect knob = new Rect(on ? rect.x + rect.width * 0.5f : rect.x, rect.y, rect.width * 0.5f, rect.height);
        DrawRect(new Rect(knob.x + 3f, knob.y + 3f, knob.width - 6f, knob.height - 6f), Color.white);
        GUIStyle label = new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold };
        label.normal.textColor = on ? new Color(0.1f, 0.45f, 0.2f) : new Color(0.25f, 0.25f, 0.3f);
        GUI.Label(knob, on ? "ON" : "OFF", label);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private void DrawCheckbox(Rect rect, bool ticked)
    {
        DrawBorder(rect, 3f, ticked ? Good : Color.white);
        if (ticked) DrawRect(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), Good);
    }

    private void DrawKey(Rect rect, string label, bool active)
    {
        DrawRect(rect, active ? new Color(0.15f, 0.55f, 0.25f) : new Color(0.16f, 0.17f, 0.2f));
        DrawBorder(rect, 3f, active ? Good : new Color(0.8f, 0.82f, 0.88f));
        GUI.Label(rect, label, keyStyle);
    }

    private static void DrawHighlight(Rect rect)
    {
        DrawBorder(rect, 4f, Gold);
    }

    private static void DrawPanel(Rect rect)
    {
        DrawRect(rect, PanelColour);
    }

    private static void DrawRect(Rect rect, Color colour)
    {
        Color previous = GUI.color;
        GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * previous.a);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawBorder(Rect rect, float thickness, Color colour)
    {
        DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), colour);
        DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), colour);
        DrawRect(new Rect(rect.x, rect.y + thickness, thickness, rect.height - thickness * 2f), colour);
        DrawRect(new Rect(rect.xMax - thickness, rect.y + thickness, thickness, rect.height - thickness * 2f), colour);
    }

    private static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);
}
