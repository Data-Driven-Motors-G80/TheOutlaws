using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// One-time hints: the first time the car meets something new in a level (not at the start), a short card says
    /// how it works, for a few seconds. Each hint shows once per level per game session, so checkpoint retries and
    /// restarts do not repeat it. HUD text is allowed; the "no text on obstacles" rule covers the road and obstacles.
    /// Ice: first contact with ice. Goats: a goat crossing coming up. Gaps: a guardrail gap coming up.
    /// </summary>
    public sealed partial class WorldShiftLevelHud
    {
        public const string IceHintText = "ICE: the car slides, steer early";
        public const string GoatHintText = "GOATS: they walk in Day, freeze them in Night";
        public const string GapHintText = "GAPS: no guardrail here, don't steer off the edge";
        private const float HintSeconds = 2.8f, HintFade = .25f, HintLookAhead = 45f;
        private enum HintKind { Ice, Goat, Gap }
        private static readonly HashSet<(int level, HintKind kind)> hintsShown = new HashSet<(int, HintKind)>();
        private readonly Queue<HintKind> pendingHints = new Queue<HintKind>();
        private CanvasGroup hintCard;
        private TMP_Text hintLabel;
        private float hintElapsed = -1f;
        private bool hasGoats, hasGaps, hasIce;

        /// <summary>True while a one-time hint card is on screen (for tests).</summary>
        public bool HintVisible => hintCard != null && hintCard.gameObject.activeSelf;
        public string HintText => HintVisible ? hintLabel.text : "";
        public bool IceHintVisible => HintVisible && hintLabel.text == IceHintText;

        private void BuildIceHint(Transform parent)
        {
            hasIce = game.Course.IceZones.Length > 0;
            hasGaps = game.Course.EdgeGaps.Length > 0;
            foreach (var hazard in game.Hazards) if (hazard.Rock != null && !hazard.Rock.Airborne && game.Environment == ShiftLevel.Mountain) { hasGoats = true; break; }
            if (!hasIce && !hasGaps && !hasGoats) return;
            var card = WorldShiftMenu.Panel(parent, "One-time hint", new Vector2(.27f, .73f), new Vector2(.73f, .83f), Color.clear);
            CardChrome(card, shadowSpread: 4f, shadowDrop: 9f);
            hintLabel = PauseLabel(card, "", .04f, .08f, .96f, .92f, 30f, Cream, FontStyles.Bold);
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
            hintLabel.enableAutoSizing = true; hintLabel.fontSizeMin = 16f; hintLabel.fontSizeMax = 30f;
            hintCard = card.gameObject.AddComponent<CanvasGroup>();
            hintCard.blocksRaycasts = false; hintCard.interactable = false; hintCard.alpha = 0f;
            card.gameObject.SetActive(false);
        }

        private void RefreshIceHint()
        {
            if (hintCard == null) return;
            bool running = game.State == ShiftRunState.Running;
            if (running)
            {
                if (hasIce && game.OnIce) Want(HintKind.Ice);
                if (hasGoats)
                    foreach (var hazard in game.Hazards)
                        if (hazard.Rock != null && !hazard.Rock.Airborne && hazard.Start > game.Distance && hazard.Start < game.Distance + HintLookAhead) { Want(HintKind.Goat); break; }
                if (hasGaps)
                    foreach (var gap in game.Course.EdgeGaps)
                        if (gap.x > game.Distance && gap.x < game.Distance + HintLookAhead) { Want(HintKind.Gap); break; }
            }
            if (hintElapsed < 0f)
            {
                if (!running || pendingHints.Count == 0) return;
                var kind = pendingHints.Dequeue();
                hintLabel.text = kind == HintKind.Ice ? IceHintText : kind == HintKind.Goat ? GoatHintText : GapHintText;
                hintElapsed = 0f;
                hintCard.gameObject.SetActive(true);
            }
            // Counts only while driving, so a pause or crash during the hint does not eat it.
            if (running) hintElapsed += Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01(hintElapsed / HintFade), fadeOut = Mathf.Clamp01((HintSeconds - hintElapsed) / HintFade);
            hintCard.alpha = running ? Mathf.Min(fadeIn, fadeOut) : 0f;
            hintCard.transform.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, fadeIn);
            if (hintElapsed > HintSeconds) { hintCard.gameObject.SetActive(false); hintElapsed = -1f; }
        }

        private void Want(HintKind kind)
        {
            if (hintsShown.Add((game.LevelIndex, kind))) pendingHints.Enqueue(kind);
        }
    }
}
