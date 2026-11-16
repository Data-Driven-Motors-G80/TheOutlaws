using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Home is the level select. Level 0 is training; each level unlocks the next.
    public sealed partial class WorldShiftMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);
        private RectTransform levels;
        private GameObject testBadge;

        private void Awake()
        {
            Time.timeScale = 1f;
            WorldShiftLevelAnalytics.MenuShown();
            var root = CreateCanvas(transform, "WorldShift menu");
            BuildMenuPresentation(root);
            BuildLevels();
        }

        private void Update()
        {
            UpdateMenuLayout();
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.uKey.wasPressedThisFrame && keyboard.shiftKey.isPressed)
            {
                WorldShiftLevels.UnlockAll = !WorldShiftLevels.UnlockAll;
                WorldShiftLevelAnalytics.UnlockAllToggled(WorldShiftLevels.UnlockAll);
                BuildLevels();
            }
        }

        private void BuildLevels()
        {
            for (int i = levels.childCount - 1; i >= 0; i--) Destroy(levels.GetChild(i).gameObject);
            testBadge.SetActive(WorldShiftLevels.UnlockAll);
            completedLabel.text = Mathf.Clamp(WorldShiftLevels.HighestBeaten + 1, 0, WorldShiftLevels.Count).ToString("00") + " / 06";
            GameObject first = null;
            bool firstUnbeaten = false;
            for (int level = 0; level < WorldShiftLevels.Count; level++)
            {
                bool open = WorldShiftLevels.IsUnlocked(level);
                var tile = DestinationCard(level, open);
                if (open && (first == null || WorldShiftLevels.IsBeaten(level - 1) && !firstUnbeaten))
                { first = tile.gameObject; firstUnbeaten = !WorldShiftLevels.IsBeaten(level); }
            }
            UpdateMenuLayout();
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first);
        }

        private static void Badge(RectTransform parent, ShiftIcon icon, Color colour, float size, Vector2 anchor)
        {
            var badge = Panel(parent, icon + " badge", anchor, anchor, colour);
            badge.sizeDelta = new Vector2(size, size);
            badge.GetComponent<Image>().sprite = WorldShiftIcons.Get(icon);
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
            var label = root.GetComponent<TextMeshProUGUI>(); label.font = WorldShiftTypography.Body;
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
