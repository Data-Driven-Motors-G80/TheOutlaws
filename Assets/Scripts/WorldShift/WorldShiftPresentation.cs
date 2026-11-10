using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Primitive city geometry. Only the current world has visible hazards.</summary>
    public sealed class WorldShiftPresentation : MonoBehaviour
    {
        private sealed class PaletteItem { public Renderer Renderer; public Material Day, Echo; }
        private sealed class Chunk { public GameObject Root; public float At, End; public float Tail = 90f; public bool CityBlock; }
        private sealed class HazardView
        {
            public ShiftHazard Data;
            public GameObject Solid, Ghost;
            public Transform Root;
            public Chunk Chunk;
        }
        private sealed class PedestrianView
        {
            public ShiftHazard Hazard;
            public WorldShiftPedestrian Figure;
            public float Lateral;
        }
        private readonly List<PaletteItem> palette = new List<PaletteItem>();
        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly List<HazardView> hazards = new List<HazardView>();
        private readonly List<GameObject> nightLamps = new List<GameObject>();
        private readonly List<GameObject> fuelModels = new List<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private WorldShiftCity game;
        private WorldShiftRoute route;
        private Camera view;
        private Light sun;
        private Light playerHeadlight;
        private GameObject headlightHaze;
        private Material headlightHazeMaterial;
        private WorldShiftRoadside roadside;
        private WorldShiftConvergence convergence;
        private WorldShiftRelayVisuals relayVisuals;
        private readonly WorldShiftObstacleVisual.Palette obstaclePalette = new WorldShiftObstacleVisual.Palette();
        // Level play: sliding and rolling hazards exist in both worlds and keep one colour.
        private readonly WorldShiftObstacleVisual.Palette moverPalette = new WorldShiftObstacleVisual.Palette(WorldShiftLevelProps.MoverColour, Color.white);
        private readonly WorldShiftObstacleVisual.Palette sideMoverPalette = new WorldShiftObstacleVisual.Palette(WorldShiftLevelProps.SideMoverColour, new Color(.035f, .035f, .04f));
        private readonly WorldShiftObstacleVisual.Palette switchPalette = new WorldShiftObstacleVisual.Palette(WorldShiftLevelProps.SwitchColour, new Color(.04f, .17f, .19f));
        private readonly WorldShiftObstacleVisual.Palette permanentPalette = new WorldShiftObstacleVisual.Palette(WorldShiftLevelProps.PermanentColour, new Color(.06f, .09f, .18f));
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<PedestrianView> pedestrians = new List<PedestrianView>();
        private readonly Dictionary<(Color, float), Material> glowMaterials = new Dictionary<(Color, float), Material>();
        private readonly List<Transform> requiredItems = new List<Transform>();
        private ShiftWorld world;
        private Color normalFogColour;
        private float normalFogStart, normalFogEnd;

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute, Transform player, Camera camera, Light directional)
        {
            game = controller;
            route = sharedRoute; view = camera; sun = directional;
            obstaclePalette.LevelColours = game.LevelMode;
            BuildHeadlight(player);
            roadside = gameObject.AddComponent<WorldShiftRoadside>();
            roadside.Build(route, game.Environment);
            convergence = gameObject.AddComponent<WorldShiftConvergence>();
            convergence.BuildWallsOnly(route);
            if (!game.SplitViewValidation)
            {
                relayVisuals = gameObject.AddComponent<WorldShiftRelayVisuals>();
                relayVisuals.Build(game, route);
            }
            foreach (ShiftHazard hazard in game.Hazards) BuildHazard(hazard);
            BuildFuel();
            BuildCheckpoints();
            BuildCoastalBridge();
            BuildSigns();
            if (game.Collection != null)
                foreach (var item in game.Collection.Items)
                    requiredItems.Add(WorldShiftLevelProps.RequiredItem(transform, route, item));
            UpdateVisibility(0f);
        }

        private void BuildCoastalBridge()
        {
            if (!game.GetawayMode || game.Environment != ShiftLevel.Ocean) return;
            var day = RuntimeMaterials.Coloured(new Color(.65f, .75f, .78f));
            var night = RuntimeMaterials.Coloured(new Color(.32f, .43f, .5f));
            for (float at = 820f; at < 1000f; at += 8f)
            {
                var point = route.Sample(at);
                var root = new GameObject("Coastal bridge edges").transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
                foreach (float side in new[] { -1f, 1f })
                {
                    var rail = Box(root, "Bridge edge", new Vector3(side * 5.3f, .45f, 0f), new Vector3(.12f, .7f, 7.8f), day);
                    palette.Add(new PaletteItem { Renderer = rail, Day = day, Echo = night });
                }
                chunks.Add(new Chunk { Root = root.gameObject, At = at, End = at + 8f });
            }
        }

        private void BuildSigns()
        {
            if (!game.LevelMode) return;
            foreach (var sign in game.Course.Training ? game.Course.Signs : System.Array.Empty<LevelSign>())
            {
                var root = WorldShiftLevelProps.HighwaySign(transform, route, sign);
                chunks.Add(new Chunk { Root = root.gameObject, At = sign.Distance, Tail = 10f });
            }
            foreach (var zone in game.Course.DustZones)
            {
                float at = zone.x;
                Transform root = WorldShiftLevelProps.StormRoadMark(transform, route, at);
                chunks.Add(new Chunk { Root = root.gameObject, At = at, Tail = 15f });
            }
        }

        private void BuildHazard(ShiftHazard data)
        {
            RoadPathPoint point = route.Sample((data.Start + data.End) * 0.5f);
            Transform root = new GameObject(data.World + " " + data.Kind + " " + data.Start).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            var chunk = new Chunk { Root = root.gameObject, At = data.Start, End = data.End,
                Tail = data.Kind == ShiftHazardKind.Oncoming ? 8f : 90f };
            chunks.Add(chunk);
            var solid = new GameObject("Solid - collides in this world"); solid.transform.SetParent(root, false);
            var ghost = new GameObject("Outline - belongs to other world"); ghost.transform.SetParent(root, false);
            if (data.Gate != null)
            {
                WorldShiftObstacleVisual.Build(solid.transform, Vector3.zero, data.Gate.Size, sideMoverPalette);
                WorldShiftLevelProps.SlidingGateHint(solid.transform);
                hazards.Add(new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk });
                return;
            }
            if (data.Rock != null)
            {
                WorldShiftObstacleVisual.Build(solid.transform, Vector3.zero,
                    Vector3.one * (data.Rock.Radius * 2f), sideMoverPalette);
                hazards.Add(new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk });
                return;
            }
            float depth = data.End - data.Start;
            var palette = data.Kind == ShiftHazardKind.SwitchWall ? switchPalette
                : data.LateralMotion ? sideMoverPalette
                : data.Kind == ShiftHazardKind.Oncoming || data.MotionSpeed > 0f
                    ? moverPalette : data.SharedAcrossWorlds ? permanentPalette : obstaclePalette;
            // The other world's still obstacles show as passable dashes in their own colour.
            Material dashes = game.LevelMode && !data.SharedAcrossWorlds
                ? WorldShiftLevelProps.Glow(data.World == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour,
                    data.World == ShiftWorld.Day ? .8f : 0f) : null;
            void Block(Transform frame, Transform ghostFrame, Vector3 centre, Vector3 size)
            {
                WorldShiftObstacleVisual.Build(frame, centre, size, palette,
                    data.Kind == ShiftHazardKind.SwitchWall, data.Kind == ShiftHazardKind.Oncoming && !data.LateralMotion);
                if (game.LevelMode && game.Environment == ShiftLevel.Desert && data.LateralMotion)
                    WorldShiftLevelProps.SlidingGateHint(frame, size.z, smallBlock: true);
                if (dashes != null) WorldShiftLevelProps.DashedOutline(ghostFrame, centre, size, dashes);
            }
            if (data.Kind == ShiftHazardKind.Gap)
            {
                WorldShiftRoadSigns.BuildGapWarnings(solid.transform, route, data.Start, data.World == ShiftWorld.Echo);
                BuildPitLighting(solid.transform, data);
                WorldShiftPitVisuals.Build(solid.transform, route, data);
            }
            else
            {
                // Fit repeated blocks to the existing lane masks and curved collision footprint.
                for (float offset = 0f; offset < depth; offset += 8f)
                {
                    float length = Mathf.Min(8f, depth - offset);
                    float at = data.Start + offset + length * .5f;
                    var frame = new GameObject("Obstacle footprint section").transform;
                    frame.SetParent(solid.transform, false);
                    var sample = route.Sample(at);
                    frame.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Forward, sample.Up));
                    var ghostFrame = new GameObject("Other-world footprint section").transform;
                    ghostFrame.SetParent(ghost.transform, false);
                    ghostFrame.SetPositionAndRotation(frame.position, frame.rotation);
                    if (data.Kind == ShiftHazardKind.SwitchWall)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(WorldShiftLevel.RoadWidth, 1.8f, length));
                    else if (data.Kind == ShiftHazardKind.HalfBarrier)
                        Block(frame, ghostFrame, new Vector3(data.HalfSide * 2.5f, .9f, 0f), new Vector3(5f, 1.8f, length));
                    else if (data.Kind == ShiftHazardKind.Divider)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(3.05f, 1.8f, length));
                    else if (game.LevelMode && data.LaneMask == 7 && !data.LateralMotion)
                        Block(frame, ghostFrame, new Vector3(0f, .9f, 0f), new Vector3(WorldShiftLevel.RoadWidth, 1.8f, length));
                    else for (int lane = 0; lane < 3; lane++)
                    {
                        if ((data.LaneMask & (1 << lane)) == 0) continue;
                        float x = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                        Block(frame, ghostFrame, new Vector3(x, .9f, 0f), new Vector3(data.LateralMotion ? 1.6f : 3.05f, 1.8f, length));
                    }
                }
            }
            hazards.Add(new HazardView { Data = data, Solid = solid, Ghost = ghost, Root = root, Chunk = chunk });
        }

        private void BuildPitLighting(Transform parent, ShiftHazard gap)
        {
            Material housing = RuntimeMaterials.Coloured(new Color(.13f, .14f, .15f));
            Material lens = Reflective(new Color(1f, .65f, .22f), .65f);
            foreach (bool entrance in new[] { true, false })
            {
                float edge = entrance ? gap.Start : gap.End;
                RoadPathPoint point = route.Sample(edge + (entrance ? 1.5f : -1.5f));
                foreach (float side in new[] { -1f, 1f })
                {
                    var fixture = new GameObject("Pit edge floodlight");
                    fixture.transform.SetParent(parent, false);
                    fixture.transform.SetPositionAndRotation(point.Position + point.Right * side * 5.5f + point.Up * 1.8f,
                        Quaternion.LookRotation(point.Forward, point.Up));
                    Box(fixture.transform, "Pit lamp support", Vector3.down * .9f, new Vector3(.12f, 1.8f, .12f), housing);
                    Box(fixture.transform, "Pit lamp overhang", new Vector3(-side * .55f, 0f, 0f), new Vector3(1.1f, .09f, .09f), housing);
                    Vector3 overOpening = new Vector3(-side * 1.1f, 0f, 0f);
                    Box(fixture.transform, "Pit warning lamp lens", overOpening, new Vector3(.36f, .22f, .32f), lens);
                    var source = new GameObject("Pit lip and inner wall light");
                    source.transform.SetParent(fixture.transform, false);
                    // Overhang the opening so the light reaches the visible inner
                    // faces, rather than only the outside of the exposed deck.
                    source.transform.localPosition = overOpening;
                    Light light = source.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, .72f, .38f);
                    light.intensity = 18f;
                    light.range = 22f;
                    light.shadows = LightShadows.None;
                }
            }
        }

        private void BuildCheckpoints()
        {
            if (game.LevelMode)
            {
                // Mark the actual retry locations too; Desert checkpoints precede its ramps.
                var marks = new HashSet<float>(game.Course.Gates) { game.RunLength };
                for (int i = 1; i < game.Checkpoints.Length; i++) marks.Add(game.Checkpoints[i].Distance);
                foreach (float at in marks) CheckpointGate(at, at >= game.RunLength);
                return;
            }
            if (game.GetawayMode || game.TutorialLevel) return;
            for (int i = 0; i <= game.Checkpoints.Length; i++)
            {
                float at = i == game.Checkpoints.Length ? game.RunLength : game.Checkpoints[i].Distance;
                if (!game.SplitViewValidation && i < game.Checkpoints.Length && game.IsJoinedDistance(at)) continue; // The rift replaces the entrance marker, not extraction.
                RoadPathPoint p = route.Sample(at);
                Transform root = new GameObject("Checkpoint marker " + i).transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
                chunks.Add(new Chunk { Root = root.gameObject, At = at });
                WorldShiftLevelProps.CheckpointMark(root);
            }
        }

        private void CheckpointGate(float at, bool finish)
        {
            RoadPathPoint p = route.Sample(at);
            Transform root = new GameObject(finish ? "Finish gate" : "Checkpoint gate").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
            chunks.Add(new Chunk { Root = root.gameObject, At = at });
            Material marker = WorldShiftLevelProps.Glow(finish ? Color.white : WorldShiftLevelProps.CheckpointColour, .5f);
            if (!finish) { WorldShiftLevelProps.CheckpointMark(root); return; }
            foreach (float x in new[] { -5.8f, 5.8f }) Box(root, "Gate post", new Vector3(x, 2.5f, 0f), new Vector3(.18f, 5f, .18f), marker);
            Box(root, "Gate lintel", new Vector3(0f, 5f, 0f), new Vector3(11.8f, .18f, .18f), marker);
            // A chequered strip across the road: no words needed.
            Material black = RuntimeMaterials.Coloured(new Color(.03f, .03f, .03f));
            for (int i = 0; i < 10; i++)
                for (int row = 0; row < 2; row++)
                    Box(root, "Finish chequer", new Vector3(-4.5f + i, .03f, row - .5f), new Vector3(1f, .04f, 1f), (i + row) % 2 == 0 ? marker : black);
        }

        public void SetWorld(ShiftWorld next)
        {
            world = next;
            obstaclePalette.SetWorld(next);
            moverPalette.SetWorld(next);
            sideMoverPalette.SetWorld(next);
            switchPalette.SetWorld(next);
            permanentPalette.SetWorld(next);
            bool day = world == ShiftWorld.Day;
            foreach (var item in palette) item.Renderer.sharedMaterial = day ? item.Day : item.Echo;
            RefreshHazards();
            RefreshFuel();
            if (relayVisuals != null) relayVisuals.SetWorld(next);
            foreach (GameObject lamp in nightLamps) lamp.SetActive(!day);
            if (playerHeadlight != null) playerHeadlight.enabled = !day;
            if (headlightHaze != null) headlightHaze.SetActive(!day);
            if (roadside != null) roadside.SetWorld(next);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = day ? new Color(0.72f, 0.72f, 0.68f) : new Color(0.28f, 0.34f, 0.43f);
            // Update the probe too: an old daylight ambient probe otherwise keeps
            // lighting every facade after the visible sky has turned black.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(RenderSettings.ambientLight.linear);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = day ? 1f : 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = day ? 140f : 48f;
            RenderSettings.fogEndDistance = day ? 260f : 150f;
            Color sky = day ? new Color(0.57f, 0.7f, 0.79f) : new Color(0.065f, 0.10f, 0.155f);
            if (game.Environment == ShiftLevel.Ocean) sky = day ? new Color(.48f, .72f, .8f) : new Color(.025f, .07f, .13f);
            if (game.Environment == ShiftLevel.Snow) sky = day ? new Color(.74f, .82f, .89f) : new Color(.08f, .14f, .23f);
            if (game.Environment == ShiftLevel.Desert) sky = day ? new Color(.86f, .72f, .52f) : new Color(.07f, .07f, .13f);
            if (game.Environment == ShiftLevel.Mountain) sky = day ? new Color(.57f, .65f, .7f) : new Color(.06f, .09f, .13f);
            RenderSettings.fogColor = sky;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = sky;
            sun.color = day ? new Color(1f, 0.87f, 0.7f) : new Color(0.56f, 0.76f, 1f);
            sun.intensity = day ? 1.25f : 0.40f;
            normalFogColour = sky;
            normalFogStart = RenderSettings.fogStartDistance;
            normalFogEnd = RenderSettings.fogEndDistance;
        }

        public void UpdateVisibility(float distance)
        {
            for (int i = 0; i < requiredItems.Count; i++)
                requiredItems[i].gameObject.SetActive(!game.Collection.IsTaken(i)
                    && game.Collection.Items[i].x >= distance - 10f && game.Collection.Items[i].x <= distance + 240f);
            if (game.LevelMode && game.Environment == ShiftLevel.Desert)
                WorldShiftLevelSandstorm.Apply(game.Course, view, game.Player.position, distance, game.Speed,
                    world, normalFogColour, normalFogStart, normalFogEnd);
            if (headlightHazeMaterial != null)
            {
                float clearRoad = 54f * (playerHeadlight != null ? playerHeadlight.range / 74f : 1f);
                foreach (ShiftHazard hazard in game.Hazards)
                    if (hazard.World == world && hazard.Kind == ShiftHazardKind.Gap && hazard.End > distance)
                        clearRoad = Mathf.Min(clearRoad, Mathf.Max(0f, hazard.Start - distance - 4f));
                if (!game.SplitViewValidation)
                    foreach (var jump in game.RelayLayout)
                    {
                        float edge = world == jump.Source ? jump.Takeoff : jump.Ramp;
                        float end = world == jump.Source ? jump.End : jump.Landing;
                        if (end > distance) clearRoad = Mathf.Min(clearRoad, Mathf.Max(0f, edge - distance - 4f));
                    }
                headlightHazeMaterial.SetFloat("_ClearRoadLength", clearRoad);
            }
            if (roadside != null) roadside.UpdateVisibility(distance);
            if (relayVisuals != null) relayVisuals.Refresh(distance);
            foreach (var hazard in hazards)
            {
                if (hazard.Data.Gate != null)
                {
                    var gate = hazard.Data.Gate;
                    var gatePoint = gate.Airborne ? game.Route.Sample(gate.Flight.Takeoff) : route.Sample(gate.Distance);
                    hazard.Root.SetPositionAndRotation(game.LevelGatePosition(gate), Quaternion.LookRotation(gatePoint.Forward, gatePoint.Up));
                    continue;
                }
                if (hazard.Data.Rock != null)
                {
                    var rock = hazard.Data.Rock;
                    hazard.Root.position = game.LevelRockPosition(rock);
                    hazard.Solid.transform.localRotation = Quaternion.AngleAxis(
                        -rock.Motion.DaySeconds * rock.Motion.MotionSpeed * rock.TravelScale * rock.Motion.Direction
                        / rock.Radius * Mathf.Rad2Deg, Vector3.forward);
                    continue;
                }
                if (hazard.Data.Kind != ShiftHazardKind.Oncoming && !hazard.Data.LateralMotion) continue;
                hazard.Data.BoundsAt(distance, out float start, out float end);
                RoadPathPoint point = route.Sample((start + end) * .5f);
                hazard.Root.SetPositionAndRotation(point.Position + point.Right * hazard.Data.LateralOffset, Quaternion.LookRotation(point.Forward, point.Up));
                hazard.Chunk.At = start;
                hazard.Chunk.End = end;
            }
            foreach (var chunk in chunks)
            {
                bool visible = Mathf.Max(chunk.At, chunk.End) > distance - chunk.Tail && chunk.At < distance + 270f
                    && (!chunk.CityBlock || !route.IsForkScenery(chunk.At));
                if (chunk.Root.activeSelf != visible) chunk.Root.SetActive(visible);
            }
            foreach (var hazard in hazards)
            {
                bool preview = ShouldPreview(hazard, distance);
                hazard.Ghost.SetActive(preview);
            }
            if (game.IsMerged) RefreshFuel();
        }

        public void OnHazardImpact(ShiftHazard hit, float lateral)
        {
            if (hit.Kind != ShiftHazardKind.Pedestrians) return;
            foreach (PedestrianView person in pedestrians)
                if (person.Hazard == hit && Mathf.Abs(person.Lateral - lateral) <= 1.1f)
                    person.Figure.Launch(game.Player.position, game.Player.forward, game.Speed);
        }

        public void StepPedestrians(float delta)
        {
            foreach (PedestrianView person in pedestrians) person.Figure.Step(delta);
        }

        public void ResetPedestrians()
        {
            foreach (PedestrianView person in pedestrians) person.Figure.RestoreStanding();
        }

        private void RefreshHazards()
        {
            foreach (var hazard in hazards)
            {
                bool active = hazard.Data.VisibleIn(world) && WorldShiftCombat.IsActive(game.Combat, hazard.Data);
                bool preview = ShouldPreview(hazard, game.Distance);
                hazard.Solid.SetActive(active);
                hazard.Ghost.SetActive(preview);
            }
        }

        // Level play shows every still obstacle of the other world as dashes, so a switch is never a surprise.
        private bool ShouldPreview(HazardView hazard, float distance) => game.LevelMode && !hazard.Data.SharedAcrossWorlds
            && hazard.Data.Kind != ShiftHazardKind.Gap && hazard.Data.World != world;

        private void BuildFuel()
        {
            for (int i = 0; i < game.Pickups.Length; i++)
            {
                ShiftFuelPickup data = game.Pickups[i];
                RoadPathPoint point = route.Sample(data.Distance);
                var root = new GameObject(data.World + " fuel " + data.Distance).transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(point.Position + point.Right * data.Lane * WorldShiftLevel.RoadWidth / 3f,
                    Quaternion.LookRotation(point.Forward, point.Up));
                chunks.Add(new Chunk { Root = root.gameObject, At = data.Distance });
                var model = new GameObject("Collectible fuel").transform;
                model.SetParent(root, false);
                var green = RuntimeMaterials.Coloured(new Color(.2f, .85f, .45f));
                WorldShiftPickupVisual.Build(model, "FUEL", green);
                fuelModels.Add(model.gameObject);
            }
        }

        public void RefreshFuel()
        {
            for (int i = 0; i < fuelModels.Count; i++)
                fuelModels[i].SetActive((game.GetawayMode ? world == ShiftWorld.Day : game.Pickups[i].World == world) && !game.IsFuelCollected(i));
        }

        private void BuildHeadlight(Transform player)
        {
            var lamp = new GameObject("Player forward cone headlight");
            // Attach to the stable car root, not the swaying visual body. The beam
            // follows road pitch and steering without flickering with suspension.
            lamp.transform.SetParent(player.parent, false);
            lamp.transform.localPosition = new Vector3(0f, 1.15f, 1.6f);
            lamp.transform.localRotation = Quaternion.Euler(1f, 0f, 0f);
            playerHeadlight = lamp.AddComponent<Light>();
            playerHeadlight.type = LightType.Spot;
            playerHeadlight.color = new Color(.88f, .94f, 1f);
            playerHeadlight.range = game.GetawayMode ? WorldShiftFlow.Garage.HeadlightRange : 74f;
            playerHeadlight.spotAngle = 36f;
            playerHeadlight.innerSpotAngle = 22f;
            playerHeadlight.intensity = 900f * Mathf.Pow(playerHeadlight.range / 74f, 2f);
            playerHeadlight.renderMode = LightRenderMode.ForcePixel;
            playerHeadlight.shadows = LightShadows.Soft;
            playerHeadlight.shadowStrength = .85f;
            playerHeadlight.shadowBias = .035f;
            playerHeadlight.shadowNormalBias = .1f;
            playerHeadlight.shadowNearPlane = .15f;
            BuildHeadlightHaze(lamp.transform);
        }

        private void BuildHeadlightHaze(Transform lamp)
        {
            Shader shader = Resources.Load<Shader>("WorldShiftHeadlight");
            if (shader == null) return; // The physical spotlight is still usable without haze.
            var material = new Material(shader);
            headlightHazeMaterial = material;
            ownedMaterials.Add(material);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            const int columns = 8, rows = 24;
            // A faint horizontal slice of haze makes the expanding spotlight
            // legible from the chase camera without a bright tunnel in the view.
            // Both side edges and the far end fade completely to transparent.
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                float z = t * 54f * (playerHeadlight.range / 74f);
                float radius = .12f + z * Mathf.Tan(16f * Mathf.Deg2Rad);
                for (int column = 0; column <= columns; column++)
                {
                    float across = column / (float)columns * 2f - 1f;
                    vertices.Add(new Vector3(across * radius, 0f, z));
                    float fade = Mathf.Sin(t * Mathf.PI) * (1f - across * across);
                    colours.Add(new Color(.58f, .73f, 1f, .065f * fade));
                    if (row == 0 || column == 0) continue;
                    int b = row * (columns + 1) + column, a = b - columns - 1;
                    triangles.Add(a - 1); triangles.Add(b - 1); triangles.Add(b);
                    triangles.Add(a - 1); triangles.Add(b); triangles.Add(a);
                }
            }
            var mesh = new Mesh { name = "Soft prototype headlight cone" };
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            headlightHaze = new GameObject("Headlight atmosphere", typeof(MeshFilter), typeof(MeshRenderer));
            headlightHaze.transform.SetParent(lamp, false);
            headlightHaze.GetComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = headlightHaze.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material Reflective(Color colour, float glow)
        {
            var key = (colour, glow);
            if (glowMaterials.TryGetValue(key, out Material shared)) return shared;
            var material = new Material(RuntimeMaterials.Coloured(colour));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * glow);
            ownedMaterials.Add(material);
            glowMaterials.Add(key, material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in generatedMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }

        private static Renderer Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            var collider = shape.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static TextMeshPro Label(Transform parent, string value, Vector3 position, float width, Color colour)
        {
            var root = new GameObject(value);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var text = root.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.sizeDelta = new Vector2(width, 2f);
            text.color = colour;
            return text;
        }
    }
}
