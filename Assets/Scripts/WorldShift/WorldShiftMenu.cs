using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    // Escape starts immediately; the optional tutorial teaches the controls separately.
    public sealed class WorldShiftMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.025f, .045f, .07f, .98f);
        private static readonly Color Accent = new Color(.35f, .85f, 1f);
        private GameObject home;

        private Sprite rounded, backdrop;
        private Texture2D roundedTexture, backdropTexture;
        private static readonly Color Cream = new Color(1f, .95f, .84f);
        private static readonly Color Gold = new Color(1f, .75f, .32f);

        private void Awake()
        {
            Time.timeScale = 1f;
            BuildSprites();
            var root = CreateCanvas(transform, "WorldShift menu");
            var background = Panel(root, "Day into night", Vector2.zero, Vector2.one, Color.white).GetComponent<Image>();
            background.sprite = backdrop;
            // Keep every action inside the display's usable area.
            var safe = Panel(root, "Safe area", Vector2.zero, Vector2.one, Color.clear);
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            home = Panel(safe, "Choose a mode", Vector2.zero, Vector2.one, Color.clear).gameObject;
            var wordmark = Label(home.transform, "WorldShift", .075f, .78f, .8f, .94f, 60f);
            wordmark.fontStyle = FontStyles.Bold;
            wordmark.color = Cream;
            Label(home.transform, "Pick your drive.", .079f, .71f, .8f, .78f, 23f).color = new Color(.70f, .77f, .82f);

            var escape = ModeCard(home.transform, "Endless escape", .075f, .49f, false);
            Label(escape, "Endless escape", .065f, .35f, .94f, .48f, 30f).fontStyle = FontStyles.Bold;
            Label(escape, "Grab loot. Cash out before you're caught.", .065f, .22f, .94f, .34f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(escape, "Play escape", new Vector2(.065f, .055f), new Vector2(.935f, .20f),
                () => WorldShiftFlow.StartGetaway(false), true);

            var training = ModeCard(home.transform, "Tutorial", .515f, .93f, true);
            Label(training, "Tutorial", .065f, .35f, .94f, .48f, 30f).fontStyle = FontStyles.Bold;
            Label(training, "A quick spin to learn the controls.", .065f, .22f, .94f, .34f, 19f).color = new Color(.76f, .81f, .85f);
            MenuButton(training, "Start tutorial", new Vector2(.065f, .055f), new Vector2(.935f, .20f),
                () => WorldShiftFlow.StartGetaway(true), false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(escape.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
        }

        private RectTransform ModeCard(Transform parent, string name, float left, float right, bool desert)
        {
            RoundedPanel(parent, name + " shadow", new Vector2(left, .174f), new Vector2(right, .686f), new Color(0f, 0f, 0f, .27f));
            var card = RoundedPanel(parent, name, new Vector2(left, .185f), new Vector2(right, .697f), new Color(.075f, .105f, .145f));
            var art = RoundedPanel(card, "Road preview", new Vector2(.022f, .52f), new Vector2(.978f, .97f), Color.white);
            art.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var illustration = new GameObject("Day and night road", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorldShiftMenuRoad));
            illustration.transform.SetParent(art, false);
            var rect = (RectTransform)illustration.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var road = illustration.GetComponent<WorldShiftMenuRoad>();
            road.Desert = desert;
            road.raycastTarget = false;
            return card;
        }

        private RectTransform RoundedPanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var rect = Panel(parent, name, min, max, color);
            var image = rect.GetComponent<Image>();
            image.sprite = rounded; image.type = Image.Type.Sliced;
            return rect;
        }

        private RectTransform MenuButton(Transform parent, string title, Vector2 min, Vector2 max, Action action, bool primary)
        {
            var rect = Button(parent, title, min, max, action);
            var image = rect.GetComponent<Image>();
            image.sprite = rounded; image.type = Image.Type.Sliced;
            image.color = primary ? Gold : new Color(.14f, .23f, .30f);
            var label = rect.GetComponentInChildren<TMP_Text>();
            label.color = primary ? new Color(.12f, .10f, .07f) : Cream;
            label.fontStyle = FontStyles.Bold;
            label.fontSizeMin = label.fontSizeMax = 20f;
            var button = rect.GetComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            return rect;
        }

        private void BuildSprites()
        {
            // A sliced sprite keeps the corner radius constant as cards and buttons resize.
            roundedTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "Menu rounded corners", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 63.5f) - 36f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 63.5f) - 36f, 0f);
                pixels[y * 128 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(27.5f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            roundedTexture.SetPixels(pixels); roundedTexture.Apply(false, true);
            rounded = Sprite.Create(roundedTexture, new Rect(0f, 0f, 128f, 128f), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 32f, 32f, 32f));
            backdropTexture = new Texture2D(256, 128, TextureFormat.RGBA32, false) { name = "Menu dusk gradient", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color[256 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++)
            {
                float u = x / 255f, v = y / 127f;
                Color color = Color.Lerp(new Color(.10f, .15f, .18f), new Color(.035f, .055f, .12f), u);
                color = Color.Lerp(new Color(.022f, .035f, .065f), color, v * .8f + .2f);
                float glow = Mathf.Exp(-((u - .12f) * (u - .12f) * 12f + (v - 1f) * (v - 1f) * 4f));
                pixels[y * 256 + x] = color + new Color(.10f, .055f, .01f, 0f) * glow;
            }
            backdropTexture.SetPixels(pixels); backdropTexture.Apply(false, true);
            backdrop = Sprite.Create(backdropTexture, new Rect(0f, 0f, 256f, 128f), new Vector2(.5f, .5f));
        }

        private void OnDestroy()
        {
            if (rounded != null) Destroy(rounded);
            if (backdrop != null) Destroy(backdrop);
            if (roundedTexture != null) Destroy(roundedTexture);
            if (backdropTexture != null) Destroy(backdropTexture);
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
            var label = root.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset;
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

    /// <summary>Lightweight vector previews; no cameras, scene loads or external art assets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class WorldShiftMenuRoad : MaskableGraphic
    {
        public bool Desert { get; set; }
        private VertexHelper mesh;
        private Rect bounds;

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear(); mesh = vertices; bounds = rectTransform.rect;
            Color daySky = Desert ? new Color(.72f, .46f, .32f) : new Color(.43f, .58f, .63f);
            Color nightSky = new Color(.065f, .105f, .23f);
            Block(0f, 0f, 1f, 1f, daySky);
            Block(.5f, 0f, .5f, 1f, nightSky);
            Circle(.15f, .76f, .13f, new Color(1f, .83f, .5f));
            Circle(.84f, .77f, .105f, new Color(.77f, .88f, 1f));
            Circle(.852f, .805f, .096f, nightSky);
            for (int i = 0; i < 11; i++)
                Circle(.55f + Mathf.Repeat(i * .137f, .43f), .62f + Mathf.Repeat(i * .113f, .32f), .008f, new Color(.6f, .75f, .94f, .65f));
            if (Desert)
            {
                Triangle(new Vector2(-.12f, .29f), new Vector2(.17f, .67f), new Vector2(.46f, .29f), new Color(.49f, .32f, .27f));
                Triangle(new Vector2(.12f, .25f), new Vector2(.36f, .57f), new Vector2(.60f, .25f), new Color(.64f, .39f, .27f));
                Triangle(new Vector2(.52f, .26f), new Vector2(.76f, .60f), new Vector2(1.12f, .26f), new Color(.13f, .21f, .34f));
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    float h = .24f + (i % 3) * .1f;
                    Block(i * .087f, .29f, .068f, h, new Color(.22f + i * .025f, .32f, .36f));
                    float x = .64f + i * .08f;
                    Block(x, .29f, .06f, h + .04f, new Color(.09f, .16f + i * .008f, .27f));
                    for (int j = 0; j < 3; j++) Block(x + .012f, .36f + j * .055f, .008f, .017f, new Color(.35f, .64f, .80f));
                }
            }
            Block(0f, 0f, .5f, .32f, Desert ? new Color(.64f, .39f, .23f) : new Color(.31f, .40f, .39f));
            Block(.5f, 0f, .5f, .32f, Desert ? new Color(.12f, .20f, .28f) : new Color(.09f, .16f, .23f));
            Quad(new Vector2(.13f, 0f), new Vector2(.55f, 0f), new Vector2(.54f, .52f), new Vector2(.50f, .52f), new Color(.20f, .25f, .28f));
            Quad(new Vector2(.55f, 0f), new Vector2(.97f, 0f), new Vector2(.58f, .52f), new Vector2(.54f, .52f), new Color(.055f, .10f, .18f));
            Quad(new Vector2(.135f, 0f), new Vector2(.15f, 0f), new Vector2(.504f, .52f), new Vector2(.50f, .52f), new Color(.92f, .80f, .53f));
            Quad(new Vector2(.95f, 0f), new Vector2(.965f, 0f), new Vector2(.58f, .52f), new Vector2(.576f, .52f), new Color(.40f, .70f, .82f));
            for (int i = 0; i < 5; i++)
            {
                float y = .035f + i * .093f;
                float t = y / .52f;
                Block(Mathf.Lerp(.34f, .52f, t), y, Mathf.Lerp(.012f, .003f, t), .042f * (1f - t * .7f), new Color(.9f, .87f, .72f));
                Block(Mathf.Lerp(.75f, .56f, t), y, Mathf.Lerp(.012f, .003f, t), .042f * (1f - t * .7f), new Color(.45f, .68f, .82f));
            }
            // The same gold car anchors both worlds, just as it does in the game.
            Block(.424f, .10f, .018f, .16f, new Color(.035f, .055f, .08f));
            Block(.532f, .10f, .018f, .16f, new Color(.035f, .055f, .08f));
            Quad(new Vector2(.435f, .075f), new Vector2(.54f, .075f), new Vector2(.527f, .30f), new Vector2(.448f, .30f), new Color(1f, .72f, .22f));
            Block(.452f, .21f, .072f, .055f, new Color(.12f, .20f, .26f));
            Block(.445f, .105f, .021f, .021f, new Color(1f, .96f, .78f));
            Block(.51f, .105f, .021f, .021f, new Color(1f, .96f, .78f));
            if (!Desert)
            {
                for (int i = 0; i < 3; i++) Block(.565f + i * .016f, .33f + i * .045f, .018f - i * .003f, .04f - i * .005f, new Color(1f, .77f, .32f));
            }
        }

        private Vector2 Point(Vector2 p) => new Vector2(bounds.xMin + p.x * bounds.width, bounds.yMin + p.y * bounds.height);
        private void Block(float x, float y, float w, float h, Color color)
            => Quad(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), color);
        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(Point(a), color, Vector2.zero); mesh.AddVert(Point(b), color, Vector2.zero);
            mesh.AddVert(Point(c), color, Vector2.zero); mesh.AddVert(Point(d), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(Point(a), color, Vector2.zero); mesh.AddVert(Point(b), color, Vector2.zero); mesh.AddVert(Point(c), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }
        private void Circle(float x, float y, float radius, Color color)
        {
            float rx = radius * bounds.height / Mathf.Max(1f, bounds.width);
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16f, b = (i + 1) * Mathf.PI / 16f;
                Triangle(new Vector2(x, y), new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * radius),
                    new Vector2(x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * radius), color);
            }
        }
    }

}
