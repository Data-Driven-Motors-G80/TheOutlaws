using UnityEngine;

public sealed class PickupHUD : MonoBehaviour
{
    private const float PopupDuration = 2f;

    [SerializeField] private CarPickupEffects effects;
    private GUIStyle popupText;
    private GUIStyle popupDetail;
    private string popupMessage;
    private string popupHint;
    private float popupRemaining;
    private int shownPickupCount;
    private int shownShieldUseCount;

    private void Update()
    {
        if (effects == null) return;

        if (effects.PickupCount != shownPickupCount)
        {
            shownPickupCount = effects.PickupCount;
            bool reverse = effects.ActiveEffect == PickupEffectType.ReverseSteering;
            bool proximity = effects.LastOutcome == RandomPickupOutcome.ProximityRecovery;
            bool shield = effects.LastOutcome == RandomPickupOutcome.Shield;
            popupMessage = proximity ? "PROXIMITY RESTORED"
                : shield ? "SHIELD READY!"
                : reverse ? "CONTROLS REVERSED!"
                : null;
            popupHint = proximity ? "Police distance recovered"
                : shield ? "Next hit is blocked"
                : reverse ? "A / Left: move right     D / Right: move left"
                : null;
            popupRemaining = popupMessage != null && !effects.RunState.IsGameOver ? PopupDuration : 0f;
        }

        if (effects.ShieldUseCount != shownShieldUseCount)
        {
            shownShieldUseCount = effects.ShieldUseCount;
            if (!effects.RunState.IsGameOver)
            {
                popupMessage = "HIT BLOCKED!";
                popupHint = "Shield used";
                popupRemaining = PopupDuration;
            }
        }

        if (popupRemaining > 0f)
            popupRemaining = Mathf.Max(0f, popupRemaining - Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        popupRemaining = 0f;
        shownPickupCount = 0;
        shownShieldUseCount = 0;
    }

    private static void DrawPanel(Rect rect)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void OnGUI()
    {
        if (effects == null) return;
        if (popupText == null)
        {
            popupText = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            popupDetail = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        float scale = Mathf.Clamp(Screen.width / 1280f, 0.65f, 1.5f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        RiskRunState run = effects.RunState;

        if (popupRemaining > 0f || run.IsGameOver)
        {
            float popupWidth = Mathf.Min(620f, width - 80f);
            Rect popupRect = new Rect(
                (width - popupWidth) * 0.5f,
                height * 0.4f,
                popupWidth,
                100f
            );
            DrawPanel(popupRect);
            GUI.Label(new Rect(popupRect.x, popupRect.y + 4f, popupWidth, 52f),
                run.IsGameOver ? "CAUGHT!" : popupMessage, popupText);
            GUI.Label(new Rect(popupRect.x + 12f, popupRect.y + 56f, popupWidth - 24f, 40f),
                run.IsGameOver ? "Press R to restart" : popupHint, popupDetail);
        }

        GUI.matrix = previous;
    }
}
