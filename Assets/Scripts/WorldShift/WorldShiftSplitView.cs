using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Render the existing complete worlds from identical
    /// cameras, then crop their full-frame images. Gameplay still advances once.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class WorldShiftSplitView : MonoBehaviour
    {
        public const float Length = 560f;
        public const float JoinStart = 112f;
        public const float JoinEnd = 448f;
        public static readonly ShiftHazard[] Layout =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 200f, 206f, 3, halfSide: -1),
            // These belong to the cropped-away halves. They must neither appear
            // nor collide while the two views are joined.
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 260f, 266f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 300f, 306f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 350f, 356f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 400f, 406f, 3, halfSide: -1)
        };
        public static readonly ShiftFuelPickup[] Fuel =
        {
            new ShiftFuelPickup(ShiftWorld.Echo, 230f, 1),
            new ShiftFuelPickup(ShiftWorld.Day, 340f, -1)
        };
        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "TWO WORLDS / ONE ROAD",
                "First Space switches worlds. When they join, steer across the centre.")
        };

        private IWorldShiftView game;
        private Camera mainView, dayCamera, nightCamera;
        private int originalMask;
        private RenderTexture dayFrame, nightFrame;
        private Material composite;
        private GameObject outputCanvas;
        private RawImage output;
        private bool rendering;
        public RenderTexture DayFrame => dayFrame;
        public RenderTexture NightFrame => nightFrame;
        public Material CompositeMaterial => composite;

        public void Initialize(IWorldShiftView controller, Camera view)
        {
            game = controller;
            mainView = view;
            originalMask = view.cullingMask;
            Shader shader = Resources.Load<Shader>("WorldShiftSplitView");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("Dual-world compositor shader is missing or unsupported.", this);
                enabled = false;
                return;
            }
            composite = new Material(shader) { name = "Day left / Night right - full-frame crop" };
            dayCamera = CreateCamera("Complete Day view");
            nightCamera = CreateCamera("Complete Night view");
            outputCanvas = new GameObject("Dual-world composite behind HUD", typeof(RectTransform), typeof(Canvas));
            outputCanvas.transform.SetParent(transform, false);
            Canvas canvas = outputCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var image = new GameObject("Full-frame crop (never squeeze either view)", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(outputCanvas.transform, false);
            output = image.GetComponent<RawImage>();
            output.material = composite;
            output.raycastTarget = false;
            output.rectTransform.anchorMin = Vector2.zero;
            output.rectTransform.anchorMax = Vector2.one;
            output.rectTransform.offsetMin = output.rectTransform.offsetMax = Vector2.zero;
            // Keep the real camera for HUD/input/mirror; avoid rendering geometry a third time.
            SetOutputActive(game.SplitViewValidation || game.IsMerged);
        }

        private Camera CreateCamera(string label)
        {
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            Camera camera = root.AddComponent<Camera>();
            camera.enabled = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderType = CameraRenderType.Base;
            return camera;
        }

        private void LateUpdate() => RenderViews();

        public void RenderViews()
        {
            if (rendering || composite == null || mainView == null) return;
            bool active = game.SplitViewValidation || game.IsMerged;
            SetOutputActive(active);
            if (!active) return;
            rendering = true;
            try
            {
                EnsureFrames();
                // Overlay UI belongs to the final output, never either world image.
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                var visibleOverlays = new System.Collections.Generic.List<Canvas>();
                foreach (Canvas canvas in canvases)
                    if (canvas.enabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    { visibleOverlays.Add(canvas); canvas.enabled = false; }
                try
                {
                    if (game.IsMerged || game.World == ShiftWorld.Day) RenderWorld(ShiftWorld.Day, dayCamera, dayFrame);
                    if (game.IsMerged || game.World == ShiftWorld.Echo) RenderWorld(ShiftWorld.Echo, nightCamera, nightFrame);
                }
                finally
                {
                    game.PresentWorldForCamera(game.World);
                    foreach (Canvas canvas in visibleOverlays) if (canvas != null) canvas.enabled = true;
                }
                composite.SetFloat("_SplitPosition", game.JoinedViewSplit);
                composite.SetFloat("_ViewMode", game.IsMerged ? 2f : game.World == ShiftWorld.Day ? 0f : 1f);
            }
            finally { rendering = false; }
        }

        private void RenderWorld(ShiftWorld world, Camera camera, RenderTexture destination)
        {
            game.PresentWorldForCamera(world);
            camera.CopyFrom(mainView);
            camera.enabled = false;
            camera.cullingMask = originalMask;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.aspect = mainView.aspect;
            camera.transform.SetPositionAndRotation(mainView.transform.position, mainView.transform.rotation);
            camera.projectionMatrix = mainView.projectionMatrix;
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = destination });
        }

        private void EnsureFrames()
        {
            // Bound this proof's GPU cost without changing the projection or cropping.
            int width = Mathf.Min(1920, Mathf.Max(64, mainView.pixelWidth));
            int height = Mathf.Max(64, Mathf.RoundToInt(width / mainView.aspect));
            if (dayFrame != null && dayFrame.width == width && dayFrame.height == height) return;
            ReleaseFrame(ref dayFrame);
            ReleaseFrame(ref nightFrame);
            dayFrame = MakeFrame("Complete Day image", width, height);
            nightFrame = MakeFrame("Complete Night image", width, height);
            output.texture = dayFrame;
            composite.SetTexture("_MainTex", dayFrame);
            composite.SetTexture("_NightTex", nightFrame);
        }

        private static RenderTexture MakeFrame(string label, int width, int height)
        {
            var frame = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = label, antiAliasing = 1, filterMode = FilterMode.Bilinear };
            frame.Create();
            return frame;
        }

        private static void ReleaseFrame(ref RenderTexture frame)
        {
            if (frame == null) return;
            frame.Release();
            Destroy(frame);
            frame = null;
        }

        private void OnDisable()
        {
            if (mainView != null) mainView.cullingMask = originalMask;
            if (outputCanvas != null) outputCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            if (game != null && composite != null) SetOutputActive(game.SplitViewValidation || game.IsMerged);
        }

        private void SetOutputActive(bool active)
        {
            if (mainView != null) mainView.cullingMask = active ? 0 : originalMask;
            if (outputCanvas != null && outputCanvas.activeSelf != active) outputCanvas.SetActive(active);
        }

        private void OnDestroy()
        {
            ReleaseFrame(ref dayFrame);
            ReleaseFrame(ref nightFrame);
            if (dayCamera != null) Destroy(dayCamera.gameObject);
            if (nightCamera != null) Destroy(nightCamera.gameObject);
            if (composite != null) Destroy(composite);
            if (outputCanvas != null) Destroy(outputCanvas);
        }
    }
}
