using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Level play HUD: world icon, checkpoint progress, pause and finish. Icons, not sentences.</summary>
    public sealed class WorldShiftLevelHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .9f);
        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);
        private WorldShiftCity game;
        private Image worldIcon, progressFill, crashFlash;
        private RectTransform carMarker;
        private CanvasGroup checkpointNotice;
        private readonly List<Image> pips = new List<Image>();
        private GameObject driving, pause, complete, trainingLesson;
        private bool completeShown;
        private CanvasGroup lessonGroup;
        private RectTransform signPreview;
        private RawImage signImage;
        private TMP_Text signFallback;
        private float lessonFade;
        private int shownLesson = -1;

        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            var canvas = WorldShiftMenu.CreateCanvas(transform, "Level HUD");
            var safe = WorldShiftMenu.Panel(canvas, "Safe area", Vector2.zero, Vector2.one, Color.clear);
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            crashFlash = WorldShiftMenu.Panel(safe, "Crash flash", Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>();
            driving = WorldShiftMenu.Panel(safe, "Driving", Vector2.zero, Vector2.one, Color.clear).gameObject;
            var worldBadge = WorldShiftMenu.Panel(driving.transform, "World badge", new Vector2(.02f, .86f), new Vector2(.02f, .86f), Ink);
            worldBadge.sizeDelta = new Vector2(84f, 84f);
            worldBadge.pivot = new Vector2(0f, 0f);
            worldIcon = Icon(worldBadge, ShiftIcon.Sun, 64f);

            var bar = WorldShiftMenu.Panel(driving.transform, "Progress", new Vector2(.22f, .925f), new Vector2(.78f, .945f), Ink);
            progressFill = WorldShiftMenu.Panel(bar, "Progress fill", Vector2.zero, new Vector2(0f, 1f), Gold).GetComponent<Image>();
            for (int i = 1; i < game.Checkpoints.Length; i++)
            {
                float at = game.Checkpoints[i].Distance / game.RunLength;
                var pip = WorldShiftMenu.Panel(bar, "Checkpoint pip", new Vector2(at, .5f), new Vector2(at, .5f), Color.white);
                pip.sizeDelta = new Vector2(26f, 26f);
                var image = pip.GetComponent<Image>();
                image.sprite = WorldShiftIcons.Get(ShiftIcon.Circle);
                pips.Add(image);
            }
            var flag = WorldShiftMenu.Panel(bar, "Finish", new Vector2(1f, .5f), new Vector2(1f, .5f), Color.white);
            flag.sizeDelta = new Vector2(40f, 40f);
            flag.GetComponent<Image>().sprite = WorldShiftIcons.Get(ShiftIcon.Flag);
            carMarker = WorldShiftMenu.Panel(bar, "You", new Vector2(0f, .5f), new Vector2(0f, .5f), Cream);
            carMarker.sizeDelta = new Vector2(14f, 34f);
            IconButton(driving.transform, ShiftIcon.Pause, new Vector2(.93f, .87f), 72f, game.TogglePause);

            var notice = WorldShiftMenu.Panel(driving.transform, "Checkpoint notice", new Vector2(.5f, .83f), new Vector2(.5f, .83f), Ink);
            notice.sizeDelta = new Vector2(360f, 60f);
            checkpointNotice = notice.gameObject.AddComponent<CanvasGroup>();
            checkpointNotice.blocksRaycasts = false;
            var saved = Icon(notice, ShiftIcon.Check, 40f);
            saved.rectTransform.anchoredPosition = new Vector2(-145f, 0f);
            saved.color = WorldShiftLevelProps.CheckpointColour;
            var noticeText = WorldShiftMenu.Label(notice, "CHECKPOINT", .18f, 0f, .98f, 1f, 26f);
            noticeText.color = WorldShiftLevelProps.CheckpointColour;
            noticeText.fontStyle = FontStyles.Bold;
            noticeText.alignment = TextAlignmentOptions.Center;

            pause = WorldShiftMenu.Panel(safe, "Paused", Vector2.zero, Vector2.one, new Color(.01f, .02f, .03f, .82f)).gameObject;
            IconButton(pause.transform, ShiftIcon.Play, new Vector2(.5f, .6f), 150f, game.TogglePause);
            IconButton(pause.transform, ShiftIcon.Retry, new Vector2(.36f, .6f), 100f, WorldShiftLevels.Retry);
            IconButton(pause.transform, ShiftIcon.Home, new Vector2(.64f, .6f), 100f, WorldShiftLevels.Menu);
            Controls(pause.transform);

            if (game.Course.Training)
            {
                var lessonRoot = WorldShiftMenu.Panel(safe, "Training sign preview", Vector2.zero, Vector2.one, Color.clear);
                lessonGroup = lessonRoot.gameObject.AddComponent<CanvasGroup>();
                lessonGroup.alpha = 0f;
                lessonGroup.blocksRaycasts = false;
                lessonGroup.interactable = false;
                signPreview = WorldShiftMenu.Panel(lessonRoot, "Readable training sign", new Vector2(.06f, .25f),
                    new Vector2(.94f, .87f), new Color(.015f, .025f, .035f, .94f));
                var imageObject = new GameObject("Sign image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                imageObject.transform.SetParent(signPreview, false);
                signImage = imageObject.GetComponent<RawImage>();
                signImage.raycastTarget = false;
                imageObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                signFallback = WorldShiftMenu.Label(signPreview, "RED X: ONCOMING\nDODGE LEFT OR RIGHT", .07f, .08f, .93f, .92f, 52f);
                signFallback.fontStyle = FontStyles.Bold;
                signFallback.alignment = TextAlignmentOptions.Center;
                var card = WorldShiftMenu.Panel(lessonRoot, "Training lesson", new Vector2(.32f, .12f),
                    new Vector2(.68f, .21f), new Color(.025f, .045f, .07f, .94f));
                trainingLesson = card.gameObject;
                var continueText = WorldShiftMenu.Label(card, "ENTER  TO CONTINUE", .04f, .08f, .96f, .92f, 25f);
                continueText.color = WorldShiftLevelProps.CheckpointColour;
                continueText.fontStyle = FontStyles.Bold;
                continueText.alignment = TextAlignmentOptions.Center;

            }

            complete = WorldShiftMenu.Panel(safe, "Level complete", Vector2.zero, Vector2.one, new Color(.01f, .02f, .03f, .82f)).gameObject;
            var check = WorldShiftMenu.Panel(complete.transform, "Done", new Vector2(.5f, .66f), new Vector2(.5f, .66f), new Color(.4f, 1f, .6f));
            check.sizeDelta = new Vector2(180f, 180f);
            check.GetComponent<Image>().sprite = WorldShiftIcons.Get(ShiftIcon.Check);
            IconButton(complete.transform, ShiftIcon.Home, new Vector2(.36f, .34f), 100f, WorldShiftLevels.Menu);
            IconButton(complete.transform, ShiftIcon.Retry, new Vector2(.5f, .34f), 100f, WorldShiftLevels.Retry);
            if (WorldShiftLevels.HasNext) IconButton(complete.transform, ShiftIcon.Next, new Vector2(.64f, .34f), 130f, WorldShiftLevels.Next);
            Refresh();
        }

        // Key caps and the two world icons: the only controls the game needs.
        private void Controls(Transform parent)
        {
            var row = WorldShiftMenu.Panel(parent, "Controls", new Vector2(.5f, .25f), new Vector2(.5f, .25f), Color.clear);
            row.sizeDelta = new Vector2(620f, 90f);
            Key(row, "A", -270f, 80f);
            Key(row, "D", -180f, 80f);
            Key(row, "SPACE", 60f, 200f);
            var sun = Icon(row, ShiftIcon.Sun, 60f); sun.rectTransform.anchoredPosition = new Vector2(210f, 0f); sun.color = Gold;
            var moon = Icon(row, ShiftIcon.Moon, 56f); moon.rectTransform.anchoredPosition = new Vector2(280f, 0f);
        }

        private static void Key(RectTransform parent, string label, float x, float width)
        {
            var key = WorldShiftMenu.Panel(parent, "Key " + label, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Cream);
            key.sizeDelta = new Vector2(width, 70f);
            key.anchoredPosition = new Vector2(x, 0f);
            var text = WorldShiftMenu.Label(key, label, 0f, 0f, 1f, 1f, 30f);
            text.color = Ink; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
        }

        private static Image Icon(RectTransform parent, ShiftIcon icon, float size)
        {
            var rect = WorldShiftMenu.Panel(parent, icon + " icon", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Color.white);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.GetComponent<Image>();
            image.sprite = WorldShiftIcons.Get(icon);
            return image;
        }

        public static RectTransform IconButton(Transform parent, ShiftIcon icon, Vector2 anchor, float size, Action action)
        {
            var rect = WorldShiftMenu.Panel(parent, icon + " button", anchor, anchor, new Color(.14f, .23f, .30f, .95f));
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            var glyph = Icon(rect, icon, size * .6f);
            glyph.color = Cream;
            return rect;
        }

        private void Update()
        {
            if (game.State == ShiftRunState.Won && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                if (WorldShiftLevels.HasNext) WorldShiftLevels.Next(); else WorldShiftLevels.Menu();
                return;
            }
            Refresh();
        }

        private void RefreshLessonPreview()
        {
            if (lessonGroup == null) return;
            bool showing = game.TrainingLessonPaused && game.State == ShiftRunState.Running;
            if (showing && shownLesson != game.CheckpointIndex && game.CheckpointIndex < game.Course.Signs.Length)
            {
                shownLesson = game.CheckpointIndex;
                bool hasArt = WorldShiftLevelProps.TryGetSignArtwork(game.Course.Signs[shownLesson].Kind,
                    out Texture2D texture, out Rect uv);
                signImage.gameObject.SetActive(hasArt);
                signFallback.gameObject.SetActive(!hasArt);
                if (hasArt)
                {
                    signImage.texture = texture;
                    signImage.uvRect = uv;
                    signImage.GetComponent<AspectRatioFitter>().aspectRatio = texture.width * uv.width / (texture.height * uv.height);
                }
            }
            // Unscaled time keeps the transition smooth while the lesson stops driving.
            lessonFade = game.State == ShiftRunState.Running
                ? Mathf.MoveTowards(lessonFade, showing ? 1f : 0f, Time.unscaledDeltaTime / (showing ? .3f : .18f)) : 0f;
            float eased = Mathf.SmoothStep(0f, 1f, lessonFade);
            lessonGroup.alpha = eased;
            signPreview.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, eased);
            lessonGroup.gameObject.SetActive(showing || lessonFade > 0f);
        }

        private void Refresh()
        {
            bool day = game.World == ShiftWorld.Day;
            worldIcon.sprite = WorldShiftIcons.Get(day ? ShiftIcon.Sun : ShiftIcon.Moon);
            worldIcon.color = day ? Gold : Cream;
            float progress = game.Progress;
            progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
            carMarker.anchorMin = carMarker.anchorMax = new Vector2(progress, .5f);
            for (int i = 0; i < pips.Count; i++) pips[i].color = i < game.CheckpointIndex
                ? WorldShiftLevelProps.CheckpointColour : new Color(1f, 1f, 1f, .55f);
            crashFlash.color = game.State == ShiftRunState.Retrying ? new Color(.8f, .05f, .03f, .22f) : Color.clear;
            checkpointNotice.alpha = !game.Course.Training && game.State == ShiftRunState.Running
                ? Mathf.Clamp01(game.CheckpointNoticeRemaining / .3f) : 0f;
            // Space is the switch key; a still-selected button would also receive it as "submit".
            if (game.State == ShiftRunState.Running && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);
            bool won = game.State == ShiftRunState.Won;
            driving.SetActive(!won && game.State != ShiftRunState.Paused);
            RefreshLessonPreview();
            pause.SetActive(game.State == ShiftRunState.Paused);
            complete.SetActive(won);
            if (won && !completeShown && EventSystem.current != null)
            {
                completeShown = true;
                var buttons = complete.GetComponentsInChildren<Button>();
                EventSystem.current.SetSelectedGameObject(buttons[buttons.Length - 1].gameObject);
            }
        }
    }
}
