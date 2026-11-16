using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        // A game moment, not a menu: one stamped word with an icon, a thin retry underline, and
        // police-light (or dark) vignette at the screen edges. Retry timing is unchanged.
        private GameObject failurePanel;
        private TMP_Text failureTitle;
        private Image failureIcon, retryFill, failureVignette;
        private RectTransform failureStamp;
        private float failureShownAt = -1f;

        private static readonly Color FailureAccent = WorldShiftUiStyle.Danger;
        private static readonly Color PoliceRed = new Color(.95f, .12f, .12f), PoliceBlue = new Color(.15f, .35f, 1f);

        private void BuildFailurePanel(Transform parent)
        {
            var panel = WorldShiftMenu.Panel(parent, "Checkpoint retry notice", Vector2.zero, Vector2.one, Color.clear);
            failurePanel = panel.gameObject;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            failureVignette = WorldShiftMenu.Panel(panel, "Crash vignette", Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>();
            failureVignette.sprite = VignetteSprite(); failureVignette.raycastTarget = false;
            // High in the frame, clear of the car and the police pulling up behind it.
            failureStamp = WorldShiftMenu.Panel(panel, "Crash stamp", new Vector2(.5f, .74f), new Vector2(.5f, .74f), Color.clear);
            failureStamp.sizeDelta = new Vector2(760f, 120f);
            failureIcon = Icon(failureStamp, ShiftIcon.Siren, 72f);
            failureIcon.rectTransform.anchorMin = failureIcon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            failureTitle = PauseLabel(failureStamp, "BUSTED", 0f, 0f, 1f, 1f, 76, Cream, FontStyles.Bold);
            failureTitle.alignment = TextAlignmentOptions.Center;
            failureTitle.enableAutoSizing = true; failureTitle.fontSizeMin = 40; failureTitle.fontSizeMax = 76;
            failureTitle.textWrappingMode = TextWrappingModes.NoWrap;
            failureTitle.outlineWidth = .18f; failureTitle.outlineColor = new Color32(10, 14, 20, 230);
            var track = WorldShiftMenu.Panel(failureStamp, "Retry track", new Vector2(.32f, -.06f), new Vector2(.68f, -.02f),
                new Color(1f, 1f, 1f, .18f));
            Round(track, new Color(1f, 1f, 1f, .18f));
            retryFill = WorldShiftMenu.Panel(track, "Retry progress", Vector2.zero, Vector2.one, Cream).GetComponent<Image>();
            failurePanel.SetActive(false);
        }

        private void RefreshFailurePanel()
        {
            bool failed = game.State == ShiftRunState.Retrying;
            failurePanel.SetActive(failed);
            if (!failed) { failureShownAt = -1f; return; }
            if (failureShownAt < 0f) failureShownAt = Time.unscaledTime;
            bool busted = game.Capturing && !game.MissedRequiredItem;
            failureTitle.text = game.MissedRequiredItem ? "MISSED DIAMOND" : game.Capturing ? "BUSTED"
                : game.FailureReason == "MISSED RELAY LANDING" ? "MISSED LANDING" : "OFF THE ROAD";
            failureIcon.sprite = WorldShiftIcons.Get(game.MissedRequiredItem ? ShiftIcon.Diamond : busted ? ShiftIcon.Siren : ShiftIcon.Fall);
            // Icon sits just left of the word, whatever its length.
            failureTitle.ForceMeshUpdate();
            float half = Mathf.Min(failureTitle.preferredWidth, failureStamp.sizeDelta.x) * .5f;
            failureIcon.rectTransform.anchoredPosition = new Vector2(-half - 52f, 2f);
            // Stamp on: a quick scale-in with a slight overshoot.
            float t = Mathf.Clamp01((Time.unscaledTime - failureShownAt) / .18f);
            float scale = t < 1f ? Mathf.Lerp(1.35f, 1f, 1f - (1f - t) * (1f - t)) : 1f;
            failureStamp.localScale = Vector3.one * scale;
            Color accent = game.MissedRequiredItem ? WorldShiftLevelProps.CollectionColour : busted ? Cream : FailureAccent;
            failureIcon.color = game.MissedRequiredItem ? WorldShiftLevelProps.CollectionColour
                : busted ? (Mathf.Repeat(Time.unscaledTime * 3f, 1f) < .5f ? PoliceRed : PoliceBlue) : Cream;
            retryFill.color = accent;
            retryFill.rectTransform.anchorMax = new Vector2(game.CheckpointRetryProgress, 1);
            // Police lights pulse red/blue at the edges; falls and misses use a soft dark edge.
            failureVignette.color = busted
                ? Color.Lerp(PoliceRed, PoliceBlue, Mathf.PingPong(Time.unscaledTime * 3f, 1f)) * new Color(1, 1, 1, .38f)
                : new Color(0f, 0f, 0f, .5f);
        }

        private static Sprite vignetteSprite;
        private static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Crash vignette", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size * 2f - 1f), dy = Mathf.Abs((y + .5f) / size * 2f - 1f);
                    float edge = Mathf.Max(dx, dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.74f, 1f, edge));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            vignetteSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            return vignetteSprite;
        }
    }
}
