using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    /// <summary>Geometry, clear feedback and HUD for the standalone mechanic trial.</summary>
    public sealed class DesertShiftTrialView : MonoBehaviour
    {
        private sealed class RockView { public Transform Root, Ball; public Renderer Surface; public GameObject Lock, Marker; }
        private sealed class TrafficView { public Transform Root; public GameObject Lock; }
        private sealed class GateView { public GameObject Root, Solid; }
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<(Color, float), Material> cache = new Dictionary<(Color, float), Material>();
        private readonly List<RockView> rocks = new List<RockView>();
        private readonly List<GateView> gates = new List<GateView>();
        private readonly List<TrafficView> traffic = new List<TrafficView>();
        private readonly List<Transform> fieldSigns = new List<Transform>();
        private readonly List<Renderer> sand = new List<Renderer>();
        private DesertShiftTrial game;
        private Camera view;
        private Light sun;
        private ShiftWorld? displayed;
        private Material rockDay, rockNight, sandDay, sandNight;
        private TMP_Text title, worldLabel, hint, progress, modalText;
        private GameObject modal;
        private RectTransform progressFill;
        private GameObject obstaclePrefab;
        private readonly List<GameObject> fuelViews = new List<GameObject>();
        private TMP_Text status, actionLabel, controls;
        private RawImage mirror;
        private GameObject mirrorPanel;
        private WorldShiftRelayVisuals relayViews;
        private static readonly Color Gold = new Color(1f, .70f, .26f), Cyan = new Color(.32f, .84f, 1f);

        public void Build(DesertShiftTrial controller, Camera camera, Light directional, Material originalRoad, Material originalSand, GameObject obstacle)
        {
            game = controller; view = camera; sun = directional;
            obstaclePrefab = obstacle;
            rockDay = Material(new Color(1f, .42f, 0f), .08f);
            rockNight = Material(new Color(.35f, .64f, .76f), .25f);
            sandDay = originalSand != null ? originalSand : Material(new Color(.52f, .32f, .16f));
            sandNight = Material(new Color(.17f, .24f, .31f));
            game.Route.SetSurfaceMaterials(originalRoad, Material(new Color(.10f, .14f, .19f)));
            BuildOriginalDesertSand();
            if (game.FullLevel)
            {
                relayViews = gameObject.AddComponent<WorldShiftRelayVisuals>();
                relayViews.Build(game.Route, DesertShiftLevel.Relays);
            }
            foreach (var rock in game.Rocks) BuildRock(rock);
            foreach (var car in game.Traffic) BuildTraffic(car);
            if (game.FullLevel) foreach (var field in DesertShiftLevel.RockFields)
            {
                var entrance = RoadRoot("Rock field warning", field.x - 50f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(entrance, "Field marker post", new Vector3(side * 5.6f, 1.8f, 0f), new Vector3(.15f, 3.6f, .15f), Material(Gold, .3f));
                    WorldText(entrance, "ROCK FIELD\nSPACE: FREEZE", new Vector3(side * 7.5f, 4f, 0f), Gold, 3f);
                }
                fieldSigns.Add(entrance);
            }
            foreach (var gate in game.ActiveGates) BuildGate(gate);
            foreach (var block in game.ActiveObstacles)
            {
                Transform row = RoadRoot("Original desert ordinary row", block.x);
                OriginalObstacle(row, new Vector3(block.y * WorldShiftLevel.RoadWidth / 3f, .7f, 0f),
                    new Vector3(1.8f, 1.4f, 2.5f));
            }
            for (float at = game.CheckpointSpacing; at <= game.RunLength; at += game.CheckpointSpacing)
            {
                if (Mathf.Abs(at - game.MergeStart) < 1f) continue;
                Transform marker = RoadRoot("Checkpoint", at);
                Box(marker, "Checkpoint road marker", new Vector3(0f, .06f, 0f), new Vector3(10f, .09f, .5f), Material(Cyan, .2f));
                WorldText(marker, at == game.RunLength ? "FINISH" : "CHECKPOINT", new Vector3(0f, 5f, 0f), Cyan, 4f);
            }
            for (int i = 0; i < game.FuelCount; i++)
            {
                Transform pickup = RoadRoot("Shared fuel " + i, game.FuelDistance(i));
                Box(pickup, "Fuel can", new Vector3(0f, 1f, 0f), new Vector3(.8f, 1.4f, .7f), Material(Gold, .25f));
                WorldText(pickup, "F", new Vector3(0f, 2.4f, 0f), Gold, 6f);
                fuelViews.Add(pickup.gameObject);
            }
            BuildHud();
        }

        private void BuildOriginalDesertSand()
        {
            var segments = new List<RoadSegment>();
            Bounds bounds = new Bounds(game.Route.Sample(0f).Position, Vector3.zero);
            float lowest = float.PositiveInfinity;
            game.Route.VisitRoadPieces((segment, start, end) =>
            {
                segments.Add(segment);
                for (int i = 0; i < segment.PathPointCount; i++)
                {
                    Vector3 point = segment.GetPathPoint(i);
                    bounds.Encapsulate(point); lowest = Mathf.Min(lowest, point.y);
                }
            });
            float floor = lowest - 2f;
            BuildSandBackdrop(bounds, floor);
            foreach (var segment in segments)
            {
                // Reuse the slope/seam fix from Env_Desert rather than placing
                // a player-height plane over the downhill road.
                var banks = segment.GetComponent<DesertRoadsideSand>();
                if (banks == null) banks = segment.gameObject.AddComponent<DesertRoadsideSand>();
                banks.Refresh(sandDay, floor);
                sand.Add(segment.transform.Find("Desert roadside sand").GetComponent<Renderer>());
            }
            for (float at = 30f; at < game.RunLength + 60f; at += 80f)
            {
                RoadPathPoint point = game.Route.Sample(at);
                foreach (float side in new[] { -1f, 1f })
                {
                    var dune = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dune.name = "Prototype dune " + side; dune.transform.SetParent(transform, false); StripCollider(dune);
                    Vector3 position = point.Position + point.Right * side * (point.Width * .5f + 25f);
                    position.y = floor - 1.5f;
                    dune.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(point.Forward, Vector3.up), Vector3.up));
                    dune.transform.localScale = new Vector3(24f, side < 0f ? 9f : 6f, 40f);
                    var surface = dune.GetComponent<Renderer>(); surface.sharedMaterial = sandDay; sand.Add(surface);
                }
            }
        }

        private void BuildSandBackdrop(Bounds bounds, float floor)
        {
            float left = bounds.min.x - 120f, right = bounds.max.x + 120f;
            float near = bounds.min.z - 120f, far = bounds.max.z + 120f;
            if (!game.FullLevel) { SandRect(left, right, near, far, floor); return; }
            // Only the straight relay's shaft is cut out of the low ground plane.
            // Keep the surrounding sand at its original height so dunes stay grounded.
            var jump = DesertShiftLevel.Relays[0];
            Vector3 start = game.Route.Sample(jump.Ramp).Position, end = game.Route.Sample(jump.End).Position;
            float holeLeft = start.x - 5.02f, holeRight = start.x + 5.02f;
            SandRect(left, right, near, start.z, floor);
            SandRect(left, right, end.z, far, floor);
            SandRect(left, holeLeft, start.z, end.z, floor);
            SandRect(holeRight, right, start.z, end.z, floor);
            SandRect(holeLeft, holeRight, start.z, end.z, floor - 32f);
        }

        private void SandRect(float left, float right, float near, float far, float floor)
        {
            if (right <= left || far <= near) return;
            sand.Add(Box(transform, "Sand backdrop with relay cutout",
                new Vector3((left + right) * .5f, floor - .5f, (near + far) * .5f),
                new Vector3(right - left, 1f, far - near), sandDay));
        }

        private Transform RoadRoot(string name, float distance)
        {
            var root = new GameObject(name).transform; root.SetParent(transform, false);
            RoadPathPoint point = game.Route.Sample(distance);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            return root;
        }

        private void OriginalObstacle(Transform parent, Vector3 position, Vector3 size, Material material = null)
        {
            if (material == null) material = Material(new Color(1f, .42f, 0f), .08f);
            if (obstaclePrefab == null) { Box(parent, "Desert roadblock", position, size, material); return; }
            GameObject block = Instantiate(obstaclePrefab, parent);
            block.name = "Original desert obstacle prefab";
            block.transform.localPosition = position; block.transform.localRotation = Quaternion.identity; block.transform.localScale = size;
            foreach (Collider collider in block.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Renderer surface in block.GetComponentsInChildren<Renderer>()) surface.sharedMaterial = material;
        }

        private void BuildRock(DesertTrialRock data)
        {
            var root = new GameObject("Shared rolling rock " + data.Distance).transform;
            root.SetParent(transform, false);
            var sphere = DesertRollingRock.CreateVisual(root, data.Radius).gameObject;
            Renderer surface = sphere.GetComponent<Renderer>();
            surface.sharedMaterial = rockDay;
            // A simple belt makes real rotation visible on a low-detail sphere.
            var belt = new GameObject("Rolling orientation marker").AddComponent<LineRenderer>();
            belt.transform.SetParent(sphere.transform, false);
            belt.useWorldSpace = false; belt.loop = true; belt.positionCount = 28; belt.widthMultiplier = .035f;
            belt.sharedMaterial = Material(Gold, .15f);
            for (int i = 0; i < belt.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / belt.positionCount;
                belt.SetPosition(i, new Vector3(0f, Mathf.Sin(angle) * .505f, Mathf.Cos(angle) * .505f));
            }
            var frozen = new GameObject("Frozen pause pictogram");
            frozen.transform.SetParent(root, false);
            foreach (float x in new[] { -.2f, .2f })
                Box(frozen.transform, "Pause bar", new Vector3(x, 1.9f, 0f), new Vector3(.18f, .65f, .15f), Material(Cyan, .65f));
            // Original obstacle prefab frames the crossing, without placing
            // solid props on the rock's transverse travel line.
            Transform crossing = RoadRoot("Original desert crossing barriers", data.Distance);
            if (game.HasGuards(data)) foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -4.5f, 4.5f })
                    OriginalObstacle(crossing, new Vector3(side * 3.625f, .45f, z), new Vector3(2.75f, .9f, 2.5f));
            }
            if (!data.Airborne && data.MergeSide == 0 && (!game.FullLevel || !DesertShiftLevel.InRockField(data.Distance) || data.Guarded))
                DesertCrossingController.CreateWarning(crossing, game.Route.Sample(data.Distance), data.Direction > 0f ? 1 : -1, true);
            rocks.Add(new RockView { Root = root, Ball = sphere.transform, Surface = surface, Lock = frozen, Marker = crossing.gameObject });
        }

        private void BuildTraffic(DesertShiftTraffic data)
        {
            Transform root = RoadRoot("Freezeable oncoming police " + data.Start, data.Start);
            float x = data.Lane * WorldShiftLevel.RoadWidth / 3f;
            WorldShiftTrafficVisual.Create(game.PolicePrefab, root, x);
            Box(root, "Oncoming headlight bar", new Vector3(x, .65f, -1.8f), new Vector3(1.75f, .15f, .07f), Material(new Color(1f, 1f, .85f), .6f));
            var frozen = new GameObject("Frozen traffic pause pictogram"); frozen.transform.SetParent(root, false);
            foreach (float offset in new[] { -.2f, .2f })
                Box(frozen.transform, "Pause bar", new Vector3(x + offset, 2.25f, 0f), new Vector3(.18f, .65f, .15f), Material(Cyan, .65f));
            traffic.Add(new TrafficView { Root = root, Lock = frozen });
        }

        private void BuildGate(DesertTrialGate data)
        {
            var root = new GameObject(data.World + " world wall");
            root.transform.SetParent(transform, false);
            RoadPathPoint point = game.Route.Sample(data.Start + 4f);
            root.transform.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            var solid = new GameObject("Active wall"); solid.transform.SetParent(root.transform, false);
            Color colour = data.World == ShiftWorld.Day ? Gold : Cyan;
            if (data.HalfSide != 0)
            {
                OriginalObstacle(solid.transform, new Vector3(data.HalfSide * 2.5f, .7f, 0f), new Vector3(5f, 1.4f, 8f), Material(colour * .55f, .1f));
                Box(solid.transform, "Reflective half barrier", new Vector3(data.HalfSide * 2.5f, 1.1f, -4.05f), new Vector3(4.9f, .16f, .12f), Material(colour, .35f));
            }
            else for (int lane = -1; lane <= 1; lane++)
            {
                OriginalObstacle(solid.transform, new Vector3(lane * 3.33f, .7f, 0f), new Vector3(3.3f, 1.4f, 8f), Material(colour * .55f, .1f));
                Box(solid.transform, "Wall reflective band", new Vector3(lane * 3.33f, 1.1f, -4.05f), new Vector3(3.2f, .16f, .12f), Material(colour, .35f));
            }
            WorldText(solid.transform, data.HalfSide != 0 ? "CROSS" : data.World == ShiftWorld.Day ? "NIGHT PASSAGE" : "DAY PASSAGE", new Vector3(data.HalfSide * 2.5f, 2.6f, -4f), colour, 2.6f);
            gates.Add(new GateView { Root = root, Solid = solid });
        }

        public void Present(ShiftWorld world)
        {
            if (game == null) return;
            bool day = world == ShiftWorld.Day;
            if (relayViews != null) { relayViews.SetWorld(world); relayViews.Refresh(game.Distance); }
            if (displayed != world)
            {
                displayed = world;
                game.Route.SetWorld(world);
                foreach (var surface in sand) surface.sharedMaterial = day ? sandDay : sandNight;
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = day ? new Color(.66f, .58f, .47f) : new Color(.38f, .44f, .54f);
                var ambient = new SphericalHarmonicsL2(); ambient.AddAmbientLight(RenderSettings.ambientLight.linear);
                RenderSettings.ambientProbe = ambient;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 120f; RenderSettings.fogEndDistance = 240f;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = RenderSettings.fogColor = day ? new Color(.57f, .39f, .23f) : new Color(.075f, .13f, .21f);
                sun.color = day ? new Color(1f, .81f, .57f) : new Color(.65f, .8f, 1f);
                sun.intensity = day ? 1.2f : .7f;
            }
            for (int i = 0; i < rocks.Count; i++)
            {
                RockView item = rocks[i]; DesertTrialRock data = game.Rocks[i];
                item.Root.gameObject.SetActive(data.VisibleIn(world) && data.Distance > game.Distance - 40f && data.Distance < game.Distance + 240f);
                item.Marker.SetActive(item.Root.gameObject.activeSelf);
                RoadPathPoint point = game.RockPoint(data);
                float x = game.RockLateral(data, world);
                item.Root.SetPositionAndRotation(point.Position + point.Right * x + point.Up * data.Radius,
                    Quaternion.LookRotation(point.Forward, point.Up));
                // Absolute pose prevents the two camera passes from rotating the
                // shared visual twice or leaking Day motion into the frozen view.
                item.Ball.localRotation = Quaternion.AngleAxis(-x / data.Radius * Mathf.Rad2Deg, Vector3.forward);
                item.Surface.sharedMaterial = day ? rockDay : rockNight;
                item.Lock.SetActive(game.FrozenView(world));
            }
            for (int i = 0; i < traffic.Count; i++)
            {
                var data = game.Traffic[i]; var item = traffic[i];
                float at = data.Position(world, game.IsMerged);
                item.Root.gameObject.SetActive(at > game.Distance - 40f && at < game.Distance + 240f);
                var point = game.Route.Sample(at);
                item.Root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
                item.Lock.SetActive(game.FrozenView(world));
            }
            for (int i = 0; i < fieldSigns.Count; i++)
            {
                float ahead = DesertShiftLevel.RockFields[i].x - 50f - game.Distance;
                fieldSigns[i].gameObject.SetActive(ahead > -35f && ahead < 220f);
            }
            for (int i = 0; i < gates.Count; i++)
            {
                var data = game.ActiveGates[i]; GateView item = gates[i];
                bool active = data.World == world;
                item.Root.SetActive(data.End > game.Distance - 20f && data.Start < game.Distance + 220f);
                item.Solid.SetActive(active);
            }
            for (int i = 0; i < fuelViews.Count; i++) fuelViews[i].SetActive(!game.IsFuelTaken(i) && game.FuelDistance(i) > game.Distance - 5f && game.FuelDistance(i) < game.Distance + 200f);
        }

        public void Refresh()
        {
            if (game == null) return;
            Present(game.World);
            bool day = game.World == ShiftWorld.Day;
            RoadPathPoint follow = game.DriverPoint(game.Distance);
            float followX = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(game.MergeStart - 56f, game.MergeStart - 16f, game.Distance));
            if (!game.FullLevel) followX = 1f;
            Vector3 cameraPosition = follow.Position - follow.Forward * 12f + Vector3.up * 6.6f + follow.Right * game.Lateral * .28f * followX;
            view.transform.SetPositionAndRotation(cameraPosition,
                Quaternion.LookRotation(game.DriverPoint(game.Distance + 22f).Position + Vector3.up * .7f - cameraPosition));
            title.text = "DESERT / " + (game.FullLevel ? DesertShiftLevel.Titles[game.Stage] : new[] { "1  FREEZE AN OPENING", "2  CONNECT TWO OPENINGS", "3  RELEASE + FREEZE AGAIN" }[game.Stage]);
            worldLabel.text = game.IsMerged ? "LEFT MOVES | RIGHT FROZEN" : day ? "DAY  /  ROCKS + TRAFFIC MOVE" : "NIGHT  /  ROCKS + TRAFFIC FROZEN";
            worldLabel.color = day ? Gold : Cyan;
            hint.text = game.Hint;
            controls.text = game.IsMerged ? "A / D  CROSS WORLDS     SPACE  RESTING     R  RETRY     ESC  PAUSE"
                : "A / D  STEER     SPACE  ROLL / FREEZE     R  RETRY     ESC  PAUSE";
            progress.text = Mathf.FloorToInt(game.Distance / game.RunLength * 100f) + "%     " + game.Duration.ToString("0.0") + "s     RETRIES " + game.Retries;
            progressFill.anchorMax = new Vector2(game.Distance / game.RunLength, 1f);
            status.text = game.FullLevel ? "FUEL " + Mathf.CeilToInt(game.FuelFraction * 100f) + "%    POLICE " + Mathf.CeilToInt(game.PursuitGap) + " m" : "";
            status.color = game.FuelFraction < .25f || game.PursuitGap < 23f ? new Color(1f, .3f, .2f) : Gold;
            mirrorPanel.SetActive(game.RearView != null);
            if (game.RearView != null) mirror.texture = game.RearView.Texture;
            bool menu = game.State == ShiftRunState.Ready || game.State == ShiftRunState.Paused || game.State == ShiftRunState.Won;
            modal.SetActive(menu);
            modalText.text = game.State == ShiftRunState.Ready
                ? "ROLL. FREEZE. PASS.\n\nDay moves the rocks. Night stops them.\nFrozen rocks still block the road.\n\nA / D steer     SPACE changes world\n" + (game.FullLevel ? "Two hits let police catch you. Collect F. / ~2 minutes" : "~40 seconds")
                : game.State == ShiftRunState.Won ? "ROUTE CONNECTED\n\n" + game.Duration.ToString("0.0") + " seconds   /   " + game.Switches + " switches\n\n" + (game.FullLevel ? "BOTH WORLDS. ONE ESCAPE." : "PRACTICE COMPLETE")
                : "PAUSED\n\nESC TO CONTINUE";
            actionLabel.text = game.State == ShiftRunState.Ready ? "START / ENTER" : game.State == ShiftRunState.Won ? "REPLAY" : "RESUME / ESC";
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("Desert HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = .5f;
            Transform canvas = canvasObject.transform;
            var header = Panel(canvas, "Header", new Vector2(.02f, .83f), new Vector2(.98f, .98f));
            title = Text(header, new Vector2(.02f, .50f), new Vector2(.65f, .98f), 24f);
            worldLabel = Text(header, new Vector2(.02f, .05f), new Vector2(.62f, .50f), 22f);
            progress = Text(header, new Vector2(.64f, .10f), new Vector2(.98f, .9f), 20f);
            progressFill = Panel(canvas, "Progress", new Vector2(0f, .817f), new Vector2(1f, .825f));
            progressFill.GetComponent<Image>().color = Cyan;
            // Child fill keeps vertical anchors fixed while horizontal progress changes.
            var track = progressFill; track.GetComponent<Image>().color = new Color(.12f, .18f, .23f);
            progressFill = Panel(track, "Fill", Vector2.zero, Vector2.one); progressFill.GetComponent<Image>().color = Cyan;
            hint = Text(canvas, new Vector2(.04f, .72f), new Vector2(.75f, .80f), 20f);
            hint.alignment = TextAlignmentOptions.TopLeft;
            status = Text(canvas, new Vector2(.04f, .66f), new Vector2(.60f, .72f), 22f);
            mirrorPanel = Panel(canvas, "Police mirror", new Vector2(.78f, .61f), new Vector2(.98f, .79f)).gameObject;
            mirror = new GameObject("Rear view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mirror.transform.SetParent(mirrorPanel.transform, false); mirror.raycastTarget = false;
            mirror.rectTransform.anchorMin = new Vector2(.015f, .03f); mirror.rectTransform.anchorMax = new Vector2(.985f, .97f);
            mirror.rectTransform.offsetMin = mirror.rectTransform.offsetMax = Vector2.zero;
            var footer = Panel(canvas, "Controls", new Vector2(.13f, .025f), new Vector2(.87f, .09f));
            controls = Text(footer, Vector2.zero, Vector2.one, 20f); controls.alignment = TextAlignmentOptions.Center;
            controls.text = "A / D  STEER     SPACE  ROLL / FREEZE     R  RETRY     ESC  PAUSE";
            modal = Panel(canvas, "State", new Vector2(.17f, .22f), new Vector2(.83f, .71f)).gameObject;
            modalText = Text(modal.transform, new Vector2(.04f, .24f), new Vector2(.96f, .96f), 25f);
            modalText.alignment = TextAlignmentOptions.Center;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("Desert UI input", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform, false);
            actionLabel = WorldShiftMenu.Button(modal.transform, "START", new Vector2(.04f, .04f), new Vector2(.48f, .21f), () =>
            { if (game.State == ShiftRunState.Ready) game.Begin(); else if (game.State == ShiftRunState.Won) game.Restart(); else game.TogglePause(); }).GetComponentInChildren<TMP_Text>();
            WorldShiftMenu.Button(modal.transform, "LEVEL SELECT", new Vector2(.52f, .04f), new Vector2(.96f, .21f), WorldShiftFlow.Menu);
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = obj.GetComponent<Image>(); image.color = new Color(.025f, .045f, .07f, .93f); image.raycastTarget = false;
            return rect;
        }
        private static TMP_Text Text(Transform parent, Vector2 min, Vector2 max, float size)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }
        private static void WorldText(Transform parent, string value, Vector3 position, Color colour, float size)
        {
            var obj = new GameObject(value); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            var text = obj.AddComponent<TextMeshPro>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = size;
            text.text = value; text.color = colour; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(12f, 2f);
        }
        private Material Material(Color colour, float emission = 0f)
        {
            if (cache.TryGetValue((colour, emission), out var cached)) return cached;
            var material = new Material(RuntimeMaterials.Coloured(colour));
            if (emission > 0f) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colour * emission); }
            materials.Add(material); cache.Add((colour, emission), material); return material;
        }
        private static Renderer Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube); shape.name = name; shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position; shape.transform.localScale = scale; StripCollider(shape);
            var surface = shape.GetComponent<Renderer>(); surface.sharedMaterial = material; return surface;
        }
        private static void StripCollider(GameObject shape)
        { var collider = shape.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); }
        private void OnDestroy()
        { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
