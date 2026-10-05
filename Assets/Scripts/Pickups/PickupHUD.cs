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
    private int shownReverseActivationCount;
    private int shownReverseCompletionCount;
    private int shownReverseExtensionCount;

    private void Update()
    {
        if (effects == null) return;

        if (effects.PickupCount != shownPickupCount)
        {
            shownPickupCount = effects.PickupCount;
            bool reverse = effects.ActiveEffect == PickupEffectType.ReverseSteering;
            bool proximity = effects.LastOutcome == RandomPickupOutcome.ProximityRecovery;
            bool shield = effects.LastOutcome == RandomPickupOutcome.Shield;
            bool nitro = effects.ActiveEffect == PickupEffectType.Boost;
            popupMessage = proximity ? "POLICE JAMMER DEPLOYED"
                : shield ? "SHIELD READY!"
                : nitro ? "NITRO ACTIVATED!"
                : reverse ? "CONTROLS REVERSED!"
                : null;
            popupHint = proximity ? "Police distance recovered"
                : shield ? "Next hit is blocked"
                : nitro ? $"Speed boosted by 35% for {effects.RemainingSeconds:0.#} seconds"
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

        if (effects.ReverseActivationCount != shownReverseActivationCount)
        {
            shownReverseActivationCount = effects.ReverseActivationCount;
            popupMessage = "1 — CONTROLS REVERSED!";
            popupHint = "A / Left: move right     D / Right: move left";
            popupRemaining = PopupDuration;
        }

        if (effects.ReverseExtensionCount != shownReverseExtensionCount)
        {
            shownReverseExtensionCount = effects.ReverseExtensionCount;
            popupMessage = $"REVERSED CONTROLS +{effects.ReverseExtensionSeconds:0.#} SECONDS";
            popupHint = $"{Mathf.CeilToInt(effects.RemainingSeconds)} seconds remaining";
            popupRemaining = PopupDuration;
        }

        if (effects.ReverseCompletionCount != shownReverseCompletionCount)
        {
            shownReverseCompletionCount = effects.ReverseCompletionCount;
            popupMessage = "CONTROLS ARE NORMAL";
            popupHint = "A / Left: move left     D / Right: move right";
            popupRemaining = PopupDuration;
        }

        if (popupRemaining > 0f)
            popupRemaining = Mathf.Max(0f, popupRemaining - Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        popupRemaining = 0f;
        shownPickupCount = 0;
        shownShieldUseCount = 0;
        shownReverseActivationCount = 0;
        shownReverseCompletionCount = 0;
        shownReverseExtensionCount = 0;
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
        // OutlawHUD owns the game-over screen; this HUD only shows pickup feedback.
        if (effects == null || effects.RunState.IsGameOver) return;
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
        bool countingDown = effects.ReverseCountdownRemaining > 0f;
        if (popupRemaining > 0f || countingDown)
        {
            const float margin = 24f;
            const float popupHeight = 100f;
            float popupWidth = Mathf.Min(620f, width - margin * 2f);
            Rect popupRect = new Rect(
                width - popupWidth - margin,
                height - popupHeight - margin,
                popupWidth,
                popupHeight
            );
            DrawPanel(popupRect);
            GUI.Label(new Rect(popupRect.x, popupRect.y + 4f, popupWidth, 52f),
                countingDown ? $"CONTROLS REVERSE IN {Mathf.CeilToInt(effects.ReverseCountdownRemaining) + 1}" : popupMessage, popupText);
            GUI.Label(new Rect(popupRect.x + 12f, popupRect.y + 56f, popupWidth - 24f, 40f),
                countingDown ? "Get ready — steering is still normal" : popupHint, popupDetail);
        }

        GUI.matrix = previous;
    }
}
