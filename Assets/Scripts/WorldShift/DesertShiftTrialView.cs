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
        private readonly Dictionary<object, GameObject> extraBlocks = new Dictionary<object, GameObject>();
        private readonly List<Transform> fieldSigns = new List<Transform>();
        private readonly List<Renderer> sand = new List<Renderer>();
        private DesertShiftTrial game;
        private Camera view;
        private Light sun;
        private ShiftWorld? displayed;
        private Material rockDay, rockNight, sandDay, sandNight;
        private TMP_Text hint, modalText;
        private WorldShiftStatusStrip statusStrip;
        private GameObject modal, practiceHud;
        private GameObject obstaclePrefab;
        private readonly List<GameObject> fuelViews = new List<GameObject>();
        private TMP_Text actionLabel;
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
                }
                fieldSigns.Add(entrance);
            }
            foreach (var gate in game.ActiveGates) BuildGate(gate);
            foreach (var block in game.ActiveObstacles)
            {
                Transform row = RoadRoot("Original desert ordinary row", block.x);
                extraBlocks.Add(block, row.gameObject);
                OriginalObstacle(row, new Vector3(block.y * WorldShiftLevel.RoadWidth / 3f, .7f, 0f),
                    new Vector3(1.8f, 1.4f, 2.5f));
            }
            for (float at = game.GetawayMode ? float.PositiveInfinity : game.CheckpointSpacing; at <= game.RunLength; at += game.CheckpointSpacing)
            {
                Transform marker = RoadRoot("Checkpoint", at);
                Box(marker, "Checkpoint road marker", new Vector3(0f, .06f, 0f), new Vector3(10f, .09f, .5f), Material(Cyan, .2f));
            }
            for (int i = 0; i < game.FuelCount; i++)
            {
                Transform pickup = RoadRoot("Shared fuel " + i, game.FuelDistance(i));
                var green = Material(new Color(.2f, .85f, .45f));
                WorldShiftPickupVisual.Build(pickup, "FUEL", green);
                fuelViews.Add(pickup.gameObject);
            }
            if (!game.GetawayMode) BuildHud();
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
            WorldShiftObstacleVisual.Build(parent, position, size);
        }

        private void BuildRock(DesertTrialRock data)
        {
            var root = new GameObject("Shared rolling rock " + data.Distance).transform;
            root.SetParent(transform, false);
            var sphere = WorldShiftObstacleVisual.Build(root, Vector3.zero, Vector3.one * (data.Radius * 2f)).gameObject;
            Renderer surface = sphere.transform.Find("Obstacle block").GetComponent<Renderer>();
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
                {
                    var guard = new GameObject("Crossing X guard").transform;
                    guard.SetParent(crossing, false);
                    OriginalObstacle(guard, new Vector3(side * 3.625f, .45f, z), new Vector3(2.75f, .9f, 2.5f));
                    extraBlocks.Add(DesertShiftTrial.GuardKey(data, side, z), guard.gameObject);
                }
            }
            if (!data.Airborne && data.MergeSide == 0 && (!game.FullLevel || !DesertShiftLevel.InRockField(data.Distance) || data.Guarded))
                DesertCrossingController.CreateWarning(crossing, game.Route.Sample(data.Distance), data.Direction > 0f ? 1 : -1, true);
            rocks.Add(new RockView { Root = root, Ball = sphere.transform, Surface = surface, Lock = frozen, Marker = crossing.gameObject });
        }

        private void BuildTraffic(DesertShiftTraffic data)
        {
            Transform root = RoadRoot("Freezeable oncoming police " + data.Start, data.Start);
            float x = data.Lane * WorldShiftLevel.RoadWidth / 3f;
            WorldShiftObstacleVisual.Build(root, new Vector3(x, .9f, 0f), new Vector3(2.4f, 1.8f, 3.8f));
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
            }
            else for (int lane = -1; lane <= 1; lane++)
            {
                OriginalObstacle(solid.transform, new Vector3(lane * 3.33f, .7f, 0f), new Vector3(3.3f, 1.4f, 8f), Material(colour * .55f, .1f));
            }
            gates.Add(new GateView { Root = root, Solid = solid });
        }

        public void Present(ShiftWorld world)
        {
            if (game == null) return;
            game.Getaway?.Present(world);
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
                item.Root.gameObject.SetActive(WorldShiftCombat.IsActive(game.Combat, data) && game.RockVisible(data, world) && data.Distance > game.Distance - 40f && data.Distance < game.Distance + 240f);
                item.Marker.SetActive(data.Distance > game.Distance - 40f && data.Distance < game.Distance + 240f
                    && (WorldShiftCombat.IsActive(game.Combat, data) || game.HasGuards(data)));
                RoadPathPoint point = game.RockPoint(data);
                float x = game.RockLateral(data, world);
                item.Root.SetPositionAndRotation(point.Position + point.Right * x + point.Up * data.Radius,
                    Quaternion.LookRotation(point.Forward, point.Up));
                // Moving and frozen hazards keep the same upright X silhouette.
                item.Ball.localRotation = Quaternion.identity;
                item.Lock.SetActive(game.FrozenView(world));
            }
            for (int i = 0; i < traffic.Count; i++)
            {
                var data = game.Traffic[i]; var item = traffic[i];
                float at = data.Position(world, game.IsMerged);
                item.Root.gameObject.SetActive(WorldShiftCombat.IsActive(game.Combat, data) && at > game.Distance - 40f && at < game.Distance + 240f);
                var point = game.Route.Sample(at);
                item.Root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
                item.Lock.SetActive(game.FrozenView(world));
            }
            foreach (var block in extraBlocks) block.Value.SetActive(WorldShiftCombat.IsActive(game.Combat, block.Key));
            for (int i = 0; i < fieldSigns.Count; i++)
            {
                float ahead = DesertShiftLevel.RockFields[i].x - 50f - game.Distance;
                fieldSigns[i].gameObject.SetActive(ahead > -35f && ahead < 220f);
            }
            for (int i = 0; i < gates.Count; i++)
            {
                var data = game.ActiveGates[i]; GateView item = gates[i];
                bool active = (game.GetawayMode || data.World == world) && WorldShiftCombat.IsActive(game.Combat, data);
                item.Root.SetActive(data.End > game.Distance - 20f && data.Start < game.Distance + 220f);
                item.Solid.SetActive(active);
            }
            for (int i = 0; i < fuelViews.Count; i++) fuelViews[i].SetActive((!game.GetawayMode || day) && !game.IsFuelTaken(i) && game.FuelDistance(i) > game.Distance - 5f && game.FuelDistance(i) < game.Distance + 200f);
        }

        public void Refresh()
        {
            if (game == null) return;
            Present(game.World);
            bool day = game.World == ShiftWorld.Day;
            RoadPathPoint follow = game.DriverPoint(game.Distance);
            const float followX = 1f;
            Vector3 cameraPosition = follow.Position - follow.Forward * 12f + Vector3.up * 6.6f + follow.Right * game.Lateral * .28f * followX;
            view.transform.SetPositionAndRotation(cameraPosition,
                Quaternion.LookRotation(game.DriverPoint(game.Distance + 22f).Position + Vector3.up * .7f - cameraPosition));
            if (game.GetawayMode) return;
            statusStrip.SetAmmo(game.Combat != null ? game.Combat.Ammo : 0, game.Combat != null);
            statusStrip.Refresh(game.World, game.FuelFraction, game.RearView != null ? game.RearView.Texture : null,
                game.PursuitGap, game.FullLevel);
            hint.text = !game.IsAirborne && game.Combat != null && game.Combat.Hint.Length > 0 ? game.Combat.Hint : game.State == ShiftRunState.Retrying || game.Duration < 10f || game.Relay?.SectionAt(game.Distance) != null ? game.Hint : "";
            bool menu = game.State == ShiftRunState.Ready || game.State == ShiftRunState.Paused || game.State == ShiftRunState.Won;
            modal.SetActive(menu);
            practiceHud.SetActive(!menu);
            modalText.text = game.State == ShiftRunState.Ready
                ? "Desert practice\n\nObstacles move in Day and freeze in Nightshift.\nGive Space a try."
                : game.State == ShiftRunState.Won ? "Nice driving!\n\nFancy another go?"
                : game.FullLevel ? "Paused\n\nA / D to steer. Space to switch. F / click to shoot." : "Paused\n\nA / D to steer. Space to switch.";
            actionLabel.text = game.State == ShiftRunState.Ready ? "Let's drive" : game.State == ShiftRunState.Won ? "Try again" : "Keep driving";
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("Desert HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = .5f;
            var driving = Panel(canvasObject.transform, "Practice drive", Vector2.zero, Vector2.one);
            driving.GetComponent<Image>().color = Color.clear;
            practiceHud = driving.gameObject;
            Transform canvas = driving;
            statusStrip = WorldShiftStatusStrip.Create(canvas);
            hint = Text(canvas, new Vector2(.14f, .77f), new Vector2(.75f, .85f), 26f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.outlineWidth = .18f; hint.outlineColor = Color.black;
            modal = Panel(canvasObject.transform, "State", new Vector2(.17f, .22f), new Vector2(.83f, .71f)).gameObject;
            modalText = Text(modal.transform, new Vector2(.04f, .24f), new Vector2(.96f, .96f), 25f);
            modalText.alignment = TextAlignmentOptions.Center;
            if (UnityEngine.EventSystems.EventSystem.current == null)
                new GameObject("Desert UI input", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform, false);
            actionLabel = WorldShiftMenu.Button(modal.transform, "START", new Vector2(.04f, .04f), new Vector2(.48f, .21f), () =>
            { if (game.State == ShiftRunState.Ready) game.Begin(); else if (game.State == ShiftRunState.Won) game.Restart(); else game.TogglePause(); }).GetComponentInChildren<TMP_Text>();
            WorldShiftMenu.Button(modal.transform, "Back to menu", new Vector2(.52f, .04f), new Vector2(.96f, .21f), WorldShiftFlow.Menu);
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
