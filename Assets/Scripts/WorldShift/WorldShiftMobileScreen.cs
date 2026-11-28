using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Fit the original landscape presentation inside the phone's safe area, without cropping.</summary>
    public sealed class WorldShiftMobileScreen : MonoBehaviour
    {
        public static bool Active => Application.isMobilePlatform || Touchscreen.current != null;
        public static Rect FitArea(Rect available)
        {
            const float aspect = 1280f / 720f;
            float width = Mathf.Min(available.width, available.height * aspect);
            var size = new Vector2(width, width / aspect);
            return new Rect(available.center - size * .5f, size);
        }
        public static Rect ContentArea => Active ? FitArea(Screen.safeArea) : Screen.safeArea;
        public static Rect NormalizedArea => Normalize(ContentArea);
        private static Rect Normalize(Rect area) => new Rect(area.x / Mathf.Max(1, Screen.width),
            area.y / Mathf.Max(1, Screen.height), area.width / Mathf.Max(1, Screen.width), area.height / Mathf.Max(1, Screen.height));

        public static void ApplySafeArea(RectTransform rect)
        {
            Rect area = NormalizedArea;
            rect.anchorMin = area.min;
            rect.anchorMax = area.max;
        }

        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasScaler.ScaleMode originalMode;
        private float originalScale;
        private Camera view;
        private Rect originalCameraRect;
        private RectTransform[] margins;
        private bool fitted;

        public void Configure(Canvas target, CanvasScaler scaling)
        {
            canvas = target; scaler = scaling;
            originalMode = scaler.uiScaleMode; originalScale = scaler.scaleFactor;
            Apply();
        }
        private void OnEnable() => Canvas.preWillRenderCanvases += Apply;
        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Apply;
            Restore();
        }
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (canvas == null || scaler == null) return;
            if (!Active) { Restore(); return; }
            fitted = true;
            Rect area = NormalizedArea;
            // A fixed 1280x720 content space keeps every original panel and font in proportion.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Max(.01f, canvas.renderingDisplaySize.x * area.width / 1280f);
            if (!Mathf.Approximately(canvas.scaleFactor, scaler.scaleFactor)) canvas.scaleFactor = scaler.scaleFactor;
            if (view == null && Camera.main != null)
            {
                view = Camera.main;
                originalCameraRect = view.rect;
            }
            if (view != null && view.targetTexture == null && view.rect != area) view.rect = area;
            if (margins == null)
            {
                margins = new RectTransform[4];
                for (int i = 0; i < margins.Length; i++)
                    margins[i] = WorldShiftMenu.Panel(transform, "Screen fit margin", Vector2.zero, Vector2.zero, Color.black);
                for (int i = margins.Length - 1; i >= 0; i--) margins[i].SetAsFirstSibling();
            }
            SetMargin(0, Vector2.zero, new Vector2(area.xMin, 1));
            SetMargin(1, new Vector2(area.xMax, 0), Vector2.one);
            SetMargin(2, new Vector2(area.xMin, 0), new Vector2(area.xMax, area.yMin));
            SetMargin(3, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, 1));
        }
        private void SetMargin(int index, Vector2 min, Vector2 max)
        {
            if (!margins[index].gameObject.activeSelf) margins[index].gameObject.SetActive(true);
            if (margins[index].anchorMin != min) margins[index].anchorMin = min;
            if (margins[index].anchorMax != max) margins[index].anchorMax = max;
        }
        private void Restore()
        {
            if (!fitted) return;
            fitted = false;
            scaler.uiScaleMode = originalMode; scaler.scaleFactor = originalScale;
            if (view != null) view.rect = originalCameraRect;
            if (margins != null) foreach (var margin in margins) if (margin != null) margin.gameObject.SetActive(false);
        }
    }
}
