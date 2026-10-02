using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OutlawHUD : MonoBehaviour
{
    private OutlawGameManager game;
    private Image ammoBar;
    private TMP_Text ammoLabel;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;

    public void Configure(OutlawGameManager manager)
    {
        game = manager;
        BuildLabeledBars();
    }

    private void Update()
    {
        if (ammoBar != null && game != null && game.Shooting != null)
        {
            ammoBar.fillAmount = game.Shooting.NormalizedAmmo;
        }

        if (ammoLabel != null && game != null && game.Shooting != null)
        {
            ammoLabel.SetText("AMMO  {0} / {1}",
                game.Shooting.CurrentAmmo,
                game.Shooting.MaximumAmmo);
        }
    }

    private void BuildLabeledBars()
    {
        Image fuelBar = FindImage("FuelBar");
        Image policeBar = FindImage("ProximityFill");

        if (fuelBar != null)
        {
            RectTransform fuelRect = fuelBar.rectTransform;
            fuelRect.anchoredPosition += Vector2.down * 30f;
            CreateLabel(fuelRect, "FUEL");

            GameObject ammoObject = Instantiate(fuelBar.gameObject, fuelRect.parent);
            ammoObject.name = "AmmoBar";
            ammoBar = ammoObject.GetComponent<Image>();
            ammoBar.color = new Color(0.12f, 0.55f, 1f);
            RectTransform ammoRect = ammoBar.rectTransform;
            ammoRect.anchoredPosition = fuelRect.anchoredPosition + Vector2.down * 104f;
            ammoLabel = CreateLabel(ammoRect, "AMMO");
        }

        if (policeBar != null)
        {
            CreateLabel(policeBar.rectTransform, "POLICE DISTANCE");
        }
    }

    private static Image FindImage(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform bar, string text)
    {
        GameObject labelObject = new GameObject(text + " Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.layer = bar.gameObject.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(bar.parent, false);
        labelRect.anchorMin = bar.anchorMin;
        labelRect.anchorMax = bar.anchorMax;
        labelRect.pivot = bar.pivot;
        labelRect.sizeDelta = new Vector2(bar.sizeDelta.x, 28f);
        labelRect.anchoredPosition = bar.anchoredPosition + Vector2.up * (bar.sizeDelta.y * 0.5f + 18f);

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
            fontSize = 46,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        bodyStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter
        };
        smallStyle.normal.textColor = Color.white;

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
        float scale = Mathf.Clamp(Screen.width / 1280f, 0.65f, 1.5f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        if (game.State == OutlawGameState.Ready)
        {
            DrawStartMenu(width, height);
        }
        else if (game.State == OutlawGameState.Won || game.State == OutlawGameState.Lost)
        {
            DrawResult(width, height);
        }
        else
        {
            DrawRunningHUD(width);
        }

        GUI.matrix = previous;
    }

    private void DrawStartMenu(float width, float height)
    {
        Rect panel = new Rect((width - 520f) * 0.5f, (height - 390f) * 0.5f, 520f, 390f);
        DrawPanel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 65f), "THE OUTLAWS", titleStyle);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 90f, panel.width - 60f, 80f),
            "Outrun the police, manage your fuel, collect ammo, and reach the extraction point.", bodyStyle);

        if (GUI.Button(new Rect(panel.x + 125f, panel.y + 185f, panel.width - 250f, 64f), "START", buttonStyle))
        {
            game.BeginGame();
        }

        GUI.Label(new Rect(panel.x + 30f, panel.y + 265f, panel.width - 60f, 80f),
            "A / D or arrows: steer\nW / Up: shoot forward    S / Down: shoot backward", smallStyle);
        GUI.Label(new Rect(panel.x, panel.y + 350f, panel.width, 28f), "Click START or press Enter", smallStyle);
    }

    private void DrawRunningHUD(float width)
    {
        Rect status = new Rect(width - 315f, 18f, 290f, 132f);
        DrawPanel(status);
        GUI.Label(new Rect(status.x, status.y + 4f, status.width, 30f),
            $"EXTRACTION: {game.DistanceRemaining:0} m", smallStyle);
        GUI.Label(new Rect(status.x, status.y + 34f, status.width, 28f),
            $"TIME: {game.ElapsedSeconds:0.0}s", smallStyle);
        GUI.Label(new Rect(status.x, status.y + 64f, status.width, 28f),
            $"AMMO: {game.Shooting.CurrentAmmo} / {game.Shooting.MaximumAmmo}", smallStyle);
        GUI.Label(new Rect(status.x, status.y + 94f, status.width, 28f),
            "W/UP forward shot  |  S/DOWN rear shot", smallStyle);

        Rect progressBackground = new Rect(width - 295f, 158f, 250f, 16f);
        DrawPanel(progressBackground);
        Color previous = GUI.color;
        GUI.color = new Color(0.9f, 0.12f, 0.1f);
        GUI.DrawTexture(new Rect(progressBackground.x, progressBackground.y,
            progressBackground.width * game.Progress, progressBackground.height), Texture2D.whiteTexture);
        GUI.color = previous;

        if (game.PoliceAlertActive)
        {
            Rect warning = new Rect((width - 430f) * 0.5f, 175f, 430f, 58f);
            DrawPanel(warning);
            GUIStyle warningStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold
            };
            warningStyle.normal.textColor = new Color(1f, 0.25f, 0.2f);
            GUI.Label(warning, "POLICE CLOSING IN!", warningStyle);
        }
    }

    private void DrawResult(float width, float height)
    {
        Rect panel = new Rect((width - 500f) * 0.5f, (height - 270f) * 0.5f, 500f, 270f);
        DrawPanel(panel);
        string title = game.State == OutlawGameState.Won ? "ESCAPED!" : "CAUGHT!";
        string detail = game.State == OutlawGameState.Won
            ? "You reached the extraction point."
            : "The chase is over. Try another run.";
        GUI.Label(new Rect(panel.x, panel.y + 22f, panel.width, 65f), title, titleStyle);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 90f, panel.width - 60f, 50f), detail, bodyStyle);
        GUI.Label(new Rect(panel.x, panel.y + 145f, panel.width, 35f),
            $"Distance: {game.DistanceTravelled:0} m   Time: {game.ElapsedSeconds:0.0}s", smallStyle);

        if (GUI.Button(new Rect(panel.x + 120f, panel.y + 190f, panel.width - 240f, 55f), "RESTART", buttonStyle))
        {
            game.Restart();
        }
    }

    private static void DrawPanel(Rect rect)
    {
        Color previous = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
