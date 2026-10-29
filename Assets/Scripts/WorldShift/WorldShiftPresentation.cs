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
            public TextMeshPro Label;
            public Transform Root;
            public Chunk Chunk;
        }
        private sealed class NightReflector { public Renderer Surface; public Color Colour; }
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
        private readonly List<Mesh> spikeMeshes = new List<Mesh>();
        private WorldShiftCity game;
        private GameObject obstaclePrefab, policePrefab, streetlightPrefab, fuelPrefab;
        private WorldShiftRoute route;
        private Camera view;
        private Light sun;
        private Light playerHeadlight;
        private GameObject headlightHaze;
        private Material headlightHazeMaterial;
        private WorldShiftRoadside roadside;
        private WorldShiftConvergence convergence;
        private WorldShiftRift rift;
        private WorldShiftRelayVisuals relayVisuals;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<NightReflector> nightReflectors = new List<NightReflector>();
        private readonly List<PedestrianView> pedestrians = new List<PedestrianView>();
        private readonly Dictionary<Color, Material> reflectorMaterials = new Dictionary<Color, Material>();
        private readonly Dictionary<(Color, float), Material> glowMaterials = new Dictionary<(Color, float), Material>();
        private readonly Dictionary<Material, Material> obstacleGlowMaterials = new Dictionary<Material, Material>();
        private MaterialPropertyBlock reflectorProperties;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private Material warmEdge, coldEdge;
        private ShiftWorld world;

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute, Transform player, Camera camera, Light directional,
            GameObject obstacle, GameObject police, GameObject streetlight, GameObject fuel)
        {
            game = controller;
            reflectorProperties = new MaterialPropertyBlock();
            obstaclePrefab = obstacle; policePrefab = police; streetlightPrefab = streetlight; fuelPrefab = fuel;
            route = sharedRoute; view = camera; sun = directional;
            warmEdge = RuntimeMaterials.Coloured(new Color(1f, 0.72f, 0.3f));
            coldEdge = RuntimeMaterials.Coloured(new Color(0.2f, 0.85f, 1f));
            // A simple cabin makes facing direction readable without importing art.
            Box(player, "Car cabin", new Vector3(0f, 1.05f, -0.25f), new Vector3(1.25f, 0.5f, 1.4f),
                RuntimeMaterials.Coloured(new Color(0.08f, 0.12f, 0.17f)));
            foreach (float side in new[] { -0.5f, 0.5f })
                Box(player, "Headlight marker", new Vector3(side, 0.6f, 1.51f), new Vector3(0.3f, 0.22f, 0.05f),
                    Reflective(new Color(1f, 1f, 0.85f), .8f));
            BuildHeadlight(player);
            roadside = gameObject.AddComponent<WorldShiftRoadside>();
            roadside.Build(route);
            convergence = gameObject.AddComponent<WorldShiftConvergence>();
            convergence.BuildWallsOnly(route);
            if (!game.SplitViewValidation)
            {
                rift = gameObject.AddComponent<WorldShiftRift>();
                rift.Build(game, route);
            }
            if (!game.SplitViewValidation && !game.TutorialLevel)
            {
                relayVisuals = gameObject.AddComponent<WorldShiftRelayVisuals>();
                relayVisuals.Build(game, route);
            }
            BuildCity();
            foreach (ShiftHazard hazard in game.Hazards) BuildHazard(hazard);
            BuildFuel();
            BuildCheckpoints();
            UpdateVisibility(0f);
        }

        private void BuildCity()
        {
            var root = new GameObject("City blocks - shared across worlds");
            root.transform.SetParent(transform, false);
            Color[] colours = { new Color(0.36f, 0.53f, 0.57f), new Color(0.58f, 0.43f, 0.37f),
                new Color(0.61f, 0.57f, 0.43f), new Color(0.43f, 0.46f, 0.54f) };
            var random = new System.Random(2710);
            for (float at = -80f; at < game.RunLength + 80f; at += 40f)
            {
                RoadPathPoint p = route.Sample(at + 20f);
                Transform block = new GameObject("City block " + at.ToString("0")).transform;
                block.SetParent(root.transform, false);
                block.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
                chunks.Add(new Chunk { Root = block.gameObject, At = at + 20f, CityBlock = true });
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int n = 0; n < 2; n++)
                    {
                        float height = 8f + (float)random.NextDouble() * 19f;
                        Renderer building = Box(block, "Prototype building", new Vector3(side * (13f + n * 7f), height * 0.5f, n == 0 ? -9f : 10f),
                            new Vector3(8f, height, 14f), warmEdge);
                        Tint(building, colours[random.Next(colours.Length)], new Color(0.17f + n * 0.04f, 0.25f, 0.35f));
                        // One geometric strip per facade is enough to establish a city silhouette.
                        Renderer stripe = Box(block, "Facade stripe", new Vector3(side * (8.95f + n * 7f), height * 0.54f, n == 0 ? -9f : 10f),
                            new Vector3(0.1f, 0.35f, 11f), warmEdge);
                        palette.Add(new PaletteItem { Renderer = stripe,
                            Day = RuntimeMaterials.Coloured(new Color(.92f, .8f, .56f)),
                            Echo = Reflective(new Color(.3f, .6f, .82f), .32f) });
                    }
                    int litSide = Mathf.RoundToInt((at + 80f) / 40f) % 2 == 0 ? -1 : 1;
                    if (side == litSide) BuildFacadeFill(block, side);
                    if (streetlightPrefab != null)
                    {
                        foreach (float z in new[] { side < 0 ? -10f : 10f })
                        {
                            RoadPathPoint lampPoint = route.Sample(at + 20f + z);
                            GameObject lamp = Visual(streetlightPrefab, block, Vector3.zero, Vector3.one * 1.35f);
                            lamp.transform.SetPositionAndRotation(lampPoint.Position + lampPoint.Right * side * 5.8f,
                                Quaternion.LookRotation(lampPoint.Forward, lampPoint.Up));
                            lamp.name = "Echo - existing CityStreetlight prefab";
                            foreach (Light light in lamp.GetComponentsInChildren<Light>())
                            {
                                light.shadows = LightShadows.None;
                                // Small roadside pools; the player's forward beam owns road visibility.
                                light.intensity = 48f;
                                light.range = 9f;
                                light.spotAngle = 80f;
                                light.innerSpotAngle = 48f;
                                light.transform.rotation = Quaternion.LookRotation(-lampPoint.Up, lampPoint.Forward);
                            }
                            nightLamps.Add(lamp);
                        }
                    }
                }
            }
        }

        private void BuildFacadeFill(Transform block, int side)
        {
            var root = new GameObject("Echo facade light strip and soft fill");
            root.transform.SetParent(block, false);
            root.transform.localPosition = new Vector3(side * 8.7f, 3.1f, -9f);
            Box(root.transform, "Low facade light strip", Vector3.zero, new Vector3(.12f, .12f, 7f),
                Reflective(new Color(.38f, .6f, .83f), .4f));
            var source = new GameObject("Weak facade pavement fill");
            source.transform.SetParent(root.transform, false);
            source.transform.localPosition = new Vector3(-side * .35f, 0f, 0f);
            Light light = source.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.44f, .64f, 1f);
            light.intensity = 6f;
            light.range = 14f;
            light.shadows = LightShadows.None;
            // One alternating fill per block leaves dark stretches between pools.
            nightLamps.Add(root);
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
            Material outline = data.World == ShiftWorld.Day ? warmEdge : coldEdge;
            Material ghostLine = Reflective(data.World == ShiftWorld.Day
                ? new Color(.62f, .42f, .18f) : new Color(.20f, .50f, .60f), .24f);
            Material fire = RuntimeMaterials.Coloured(data.World == ShiftWorld.Day ? new Color(0.95f, 0.24f, 0.07f) : new Color(0.2f, 0.65f, 0.9f));
            Material body = RuntimeMaterials.Coloured(data.World == ShiftWorld.Day ? new Color(0.14f, 0.18f, 0.24f) : new Color(0.58f, 0.7f, 0.79f));
            float depth = data.End - data.Start;
            if (data.Kind == ShiftHazardKind.Gap)
            {
                WorldShiftRoadSigns.BuildGapWarnings(solid.transform, route, data.Start, data.World == ShiftWorld.Echo);
                BuildPitLighting(solid.transform, data);
                WorldShiftPitVisuals.Build(solid.transform, route, data);
            }
            else if (data.Kind == ShiftHazardKind.HalfBarrier)
            {
                float x = data.HalfSide * WorldShiftLevel.RoadWidth * .25f;
                Material concrete = RuntimeMaterials.Coloured(new Color(.5f, .52f, .55f));
                Material stripe = outline;

                // Route-fitted one-metre slices keep even the wall-side end flush on bends.
                convergence.BuildWall(solid.transform, data, concrete, stripe);
            }
            else if (data.Kind == ShiftHazardKind.Divider)
            {
                // Each part follows the shared road rather than cutting across a bend.
                for (float at = data.Start; at < data.End; at += 10f)
                {
                    float length = Mathf.Min(10f, data.End - at);
                    RoadPathPoint section = route.Sample(at + length * .5f);
                    Renderer median = Box(solid.transform, "Day centre-lane concrete divider", Vector3.zero,
                        new Vector3(3.05f, 1.05f, length - .12f), RuntimeMaterials.Coloured(new Color(.47f, .48f, .49f)));
                    median.transform.SetPositionAndRotation(section.Position + section.Up * .525f,
                        Quaternion.LookRotation(section.Forward, section.Up));
                    Renderer reflector = Box(solid.transform, "Median yellow reflector", Vector3.zero,
                        new Vector3(2.8f, .08f, Mathf.Min(1.2f, length)), warmEdge);
                    reflector.transform.SetPositionAndRotation(section.Position + section.Up * 1.08f,
                        Quaternion.LookRotation(section.Forward, section.Up));
                    var outlineSection = new GameObject("Divider preview segment").transform;
                    outlineSection.SetParent(ghost.transform, false);
                    outlineSection.SetPositionAndRotation(section.Position, Quaternion.LookRotation(section.Forward, section.Up));
                }
            }
            else if (data.Kind == ShiftHazardKind.Pedestrians)
            {
                BuildCrossing(solid.transform, data);
                WorldShiftRoadSigns.BuildCrossing(solid.transform);
            }
            else if (data.Kind == ShiftHazardKind.Oncoming)
            {
                int lane = data.LaneMask == 1 ? 0 : data.LaneMask == 2 ? 1 : 2;
                float x = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                if (policePrefab != null)
                {
                    WorldShiftTrafficVisual.Create(policePrefab, solid.transform, x);
                }
                else
                {
                    Box(solid.transform, "Oncoming police body", new Vector3(x, .55f, 0f), new Vector3(1.95f, 1f, 3.8f), body);
                    Box(solid.transform, "Oncoming police cabin", new Vector3(x, 1.2f, 0f), new Vector3(1.4f, .5f, 1.6f), body);
                }
                Box(solid.transform, "Approaching white headlight bar", new Vector3(x, .65f, -1.8f), new Vector3(1.75f, .15f, .07f),
                    Reflective(new Color(1f, 1f, .86f), .5f));
            }
            else
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    if ((data.LaneMask & (1 << lane)) == 0) continue;
                    float x = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                    if (data.Kind == ShiftHazardKind.Fire)
                    {
                        Box(solid.transform, "Fire footprint", new Vector3(x, 0.14f, 0f), new Vector3(3.22f, 0.28f, depth), fire);
                        for (int z = 0; z < 3; z++)
                            for (int n = 0; n < 3; n++)
                            {
                                float height = n == 1 ? 2.0f : 1.45f;
                                Box(solid.transform, "Prototype flame", new Vector3(x + (n - 1) * 0.98f, height * 0.5f, (z - 1) * depth * 0.32f),
                                    new Vector3(0.87f, height, 0.65f), n == 1 ? outline : fire);
                            }
                    }
                    else if (data.Kind == ShiftHazardKind.Spikes)
                    {
                        Box(solid.transform, "Spike strip base", new Vector3(x, .1f, 0f), new Vector3(3.22f, .2f, depth), RuntimeMaterials.Coloured(new Color(.055f, .06f, .07f)));
                        Box(solid.transform, "Spike strip warning", new Vector3(x, .22f, -depth * .5f + .2f), new Vector3(3.22f, .04f, .4f), warmEdge);
                        SpikeStrip(solid.transform, x, depth);
                    }
                    else if (data.Kind == ShiftHazardKind.Barrier && obstaclePrefab != null)
                    {
                        Box(solid.transform, "Barrier footprint", new Vector3(x, .08f, 0f), new Vector3(3.22f, .16f, depth), body);
                        foreach (float z in new[] { -depth * .3f, depth * .3f })
                        {
                            GameObject barrier = Visual(obstaclePrefab, solid.transform, new Vector3(x, .85f, z), new Vector3(3.05f, 1.7f, 1.5f));
                            foreach (Renderer r in barrier.GetComponentsInChildren<Renderer>()) r.sharedMaterial = body;
                            Box(solid.transform, "Barrier reflector", new Vector3(x, 1.4f, z - .8f), new Vector3(2.9f, .2f, .08f), outline);
                        }
                    }
                    else
                    {
                        Box(solid.transform, "Police road closure", new Vector3(x, .06f, 0f), new Vector3(3.22f, .12f, depth), body);
                        foreach (float z in new[] { -depth * 0.25f, depth * 0.25f })
                        {
                            if (policePrefab != null)
                            {
                                GameObject car = Visual(policePrefab, solid.transform, new Vector3(x, 0f, z), Vector3.one * 1.15f);
                                foreach (Light light in car.GetComponentsInChildren<Light>()) light.enabled = false;
                                continue;
                            }
                            Box(solid.transform, "Police body", new Vector3(x, 0.55f, z), new Vector3(2.55f, 1f, 3.5f), body);
                            Box(solid.transform, "Police cabin", new Vector3(x, 1.2f, z), new Vector3(2f, 0.5f, 1.8f), body);
                            Box(solid.transform, "Police red beacon", new Vector3(x - 0.4f, 1.52f, z), new Vector3(0.65f, 0.15f, 0.35f), RuntimeMaterials.Coloured(new Color(1f, 0.15f, 0.12f)));
                            Box(solid.transform, "Police blue beacon", new Vector3(x + 0.4f, 1.52f, z), new Vector3(0.65f, 0.15f, 0.35f), coldEdge);
                        }
                    }
                }
            }
            if (data.World == ShiftWorld.Echo)
            {
                if (data.Kind != ShiftHazardKind.Gap) AddFaintObstacleGlow(solid.transform);
                BuildNightReflectors(solid.transform, data);
            }
            if (data.Kind == ShiftHazardKind.Pedestrians)
            {
                foreach (Transform person in solid.GetComponentsInChildren<Transform>())
                {
                    if (person.name != "Grey prototype pedestrian") continue;
                    WorldShiftPedestrian figure = person.gameObject.AddComponent<WorldShiftPedestrian>();
                    figure.Initialize(game, (data.Start + data.End) * .5f + person.localPosition.z);
                    pedestrians.Add(new PedestrianView { Hazard = data, Figure = figure, Lateral = person.localPosition.x });
                }
            }
            string label = data.Kind == ShiftHazardKind.Gap ? "ROAD ENDS / SHIFT" : data.Kind == ShiftHazardKind.Fire ? "FIRE"
                : data.Kind == ShiftHazardKind.Spikes ? "SPIKES" : data.Kind == ShiftHazardKind.Barrier ? "BARRIER"
                : data.Kind == ShiftHazardKind.Pedestrians ? "CROSSING" : data.Kind == ShiftHazardKind.Divider ? "CENTRE DIVIDER"
                : data.Kind == ShiftHazardKind.Oncoming ? "ONCOMING" : "BLOCKADE";
            var text = Label(root, data.World.ToString().ToUpperInvariant() + " / " + label,
                new Vector3(data.World == ShiftWorld.Day ? -5.9f : 5.9f, 2.3f, -depth * 0.5f - 2f), 3.5f, outline.color);
            hazards.Add(new HazardView { Data = data, Solid = solid, Ghost = ghost, Label = text, Root = root, Chunk = chunk });
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
            for (int i = 0; i <= game.Checkpoints.Length; i++)
            {
                float at = i == game.Checkpoints.Length ? game.RunLength : game.Checkpoints[i].Distance;
                if (!game.SplitViewValidation && i < game.Checkpoints.Length && game.IsJoinedDistance(at)) continue; // The rift replaces the entrance marker, not extraction.
                RoadPathPoint p = route.Sample(at);
                Transform root = new GameObject("Checkpoint marker " + i).transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
                chunks.Add(new Chunk { Root = root.gameObject, At = at });
                Material marker = RuntimeMaterials.Coloured(new Color(0.25f, 0.93f, 0.7f));
                foreach (float x in new[] { -5.8f, 5.8f }) Box(root, "Checkpoint post", new Vector3(x, 2.5f, 0f), new Vector3(0.18f, 5f, 0.18f), marker);
                Box(root, "Checkpoint lintel", new Vector3(0f, 5f, 0f), new Vector3(11.8f, 0.18f, 0.18f), marker);
                Label(root, i == game.Checkpoints.Length ? "EXTRACTION" : "SECTOR " + (i + 1).ToString("00"),
                    new Vector3(0f, 5.7f, 0f), 6f, new Color(0.65f, 1f, 0.86f));
            }
        }

        public void SetWorld(ShiftWorld next)
        {
            world = next;
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
            RenderSettings.fogColor = sky;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = sky;
            sun.color = day ? new Color(1f, 0.87f, 0.7f) : new Color(0.56f, 0.76f, 1f);
            sun.intensity = day ? 1.25f : 0.40f;

        }

        public void UpdateVisibility(float distance)
        {
            if (headlightHazeMaterial != null)
            {
                float clearRoad = 54f;
                foreach (ShiftHazard hazard in game.Hazards)
                    if (hazard.World == world && hazard.Kind == ShiftHazardKind.Gap && hazard.End > distance)
                        clearRoad = Mathf.Min(clearRoad, Mathf.Max(0f, hazard.Start - distance - 4f));
                if (!game.SplitViewValidation)
                    foreach (var jump in WorldShiftRelay.Sections)
                    {
                        float edge = world == jump.Source ? jump.Takeoff : jump.Ramp;
                        float end = world == jump.Source ? jump.End : jump.Landing;
                        if (end > distance) clearRoad = Mathf.Min(clearRoad, Mathf.Max(0f, edge - distance - 4f));
                    }
                headlightHazeMaterial.SetFloat("_ClearRoadLength", clearRoad);
            }
            if (roadside != null) roadside.UpdateVisibility(distance);
            if (rift != null) rift.Refresh(distance);
            if (relayVisuals != null) relayVisuals.Refresh(distance);
            foreach (var hazard in hazards)
            {
                if (hazard.Data.Kind != ShiftHazardKind.Oncoming) continue;
                hazard.Data.BoundsAt(distance, out float start, out float end);
                RoadPathPoint point = route.Sample((start + end) * .5f);
                hazard.Root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
                hazard.Chunk.At = start;
                hazard.Chunk.End = end;
            }
            foreach (var chunk in chunks)
            {
                bool visible = Mathf.Max(chunk.At, chunk.End) > distance - chunk.Tail && chunk.At < distance + 270f;
                if (chunk.Root.activeSelf != visible) chunk.Root.SetActive(visible);
            }
            foreach (var hazard in hazards)
            {
                bool active = hazard.Data.World == world;
                bool preview = ShouldPreview(hazard, distance);
                hazard.Ghost.SetActive(preview);
                // TMP labels are unlit. Fade distant labels too, otherwise they
                // reveal the entire night layout through the shorter visibility.
                float ahead = hazard.Chunk.At - distance;
                float legibility = world == ShiftWorld.Day ? 1f : 1f - Mathf.InverseLerp(50f, 82f, ahead);
                hazard.Label.gameObject.SetActive(hazard.Data.Kind != ShiftHazardKind.Gap && hazard.Data.Kind != ShiftHazardKind.HalfBarrier && active && legibility > 0f);
                hazard.Label.alpha = (active ? 1f : .65f) * legibility;
            }
            if (game.IsMerged) RefreshFuel();
            UpdateNightReflectors();
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

        private void AddFaintObstacleGlow(Transform parent)
        {
            foreach (Renderer surface in parent.GetComponentsInChildren<Renderer>())
            {
                if (surface.GetComponentInParent<WorldShiftRoadSigns>() != null
                    || surface.name == "Night zebra stripe" || surface.name.StartsWith("Crossing sign")) continue;
                Material[] materials = surface.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material original = materials[index];
                    if (original == null || !original.HasProperty("_BaseColor") || !original.HasProperty(EmissionId)) continue;
                    // Preserve existing police beacons and other intentional lights.
                    if (original.IsKeywordEnabled("_EMISSION") && original.GetColor(EmissionId).maxColorComponent > .03f) continue;
                    if (!obstacleGlowMaterials.TryGetValue(original, out Material glow))
                    {
                        glow = new Material(original) { name = "Night obstacle faint self light" };
                        glow.EnableKeyword("_EMISSION");
                        glow.SetColor(EmissionId, original.GetColor("_BaseColor") * .10f);
                        ownedMaterials.Add(glow);
                        obstacleGlowMaterials.Add(original, glow);
                    }
                    materials[index] = glow;
                }
                surface.sharedMaterials = materials;
            }
        }

        private void BuildNightReflectors(Transform parent, ShiftHazard data)
        {
            Color white = new Color(.92f, .96f, 1f), yellow = new Color(1f, .8f, .22f);
            float depth = data.End - data.Start;
            // Mark the existing shapes rather than brightening whole obstacles.
            foreach (Renderer surface in parent.GetComponentsInChildren<Renderer>())
            {
                if (surface.name == "Barrier reflector" || surface.name == "White cliff reflector"
                    || surface.name == "Crossing sign") RegisterNightReflector(surface, white);
                else if (surface.name == "Spike strip warning") RegisterNightReflector(surface, yellow);
                else if ((surface.name == "Warning-striped cliff lip" || surface.name == "Broken deck hazard lip") && surface.sharedMaterial.color.r > .4f)
                    RegisterNightReflector(surface, yellow);
            }
            if (data.Kind == ShiftHazardKind.Pedestrians)
            {
                foreach (Transform person in parent.GetComponentsInChildren<Transform>())
                    if (person.name == "Grey prototype pedestrian")
                    {
                        RegisterNightReflector(Box(person, "Reflective chest band", new Vector3(0f, 1.2f, -.17f),
                            new Vector3(.47f, .20f, .045f), coldEdge), yellow);
                        RegisterNightReflector(Box(person, "Reflective head marker", new Vector3(0f, 1.77f, -.19f),
                            new Vector3(.39f, .10f, .045f), coldEdge), white);
                        foreach (float side in new[] { -1f, 1f })
                            RegisterNightReflector(Box(person, "Reflective lower leg band", new Vector3(side * .13f, .36f, -.16f),
                                new Vector3(.19f, .16f, .045f), coldEdge), yellow);
                    }
                return;
            }
            if (data.Kind != ShiftHazardKind.Police && data.Kind != ShiftHazardKind.Oncoming
                && data.Kind != ShiftHazardKind.Fire) return;
            for (int lane = 0; lane < 3; lane++)
            {
                if ((data.LaneMask & (1 << lane)) == 0) continue;
                float x = (lane - 1) * WorldShiftLevel.RoadWidth / 3f;
                if (data.Kind == ShiftHazardKind.Police)
                {
                    foreach (float z in new[] { -depth * .25f, depth * .25f })
                        RegisterNightReflector(Box(parent, "Police reflective bumper", new Vector3(x, .62f, z - 1.76f),
                            new Vector3(1.8f, .2f, .045f), coldEdge), yellow);
                }
                else if (data.Kind == ShiftHazardKind.Oncoming)
                    RegisterNightReflector(Box(parent, "Oncoming reflective bumper", new Vector3(x, .43f, -1.76f),
                        new Vector3(1.8f, .17f, .045f), coldEdge), yellow);
                else
                    RegisterNightReflector(Box(parent, "Fire closure reflective edge", new Vector3(x, .3f, -depth * .5f - .035f),
                        new Vector3(3.1f, .24f, .06f), coldEdge), white);
            }
        }

        private void RegisterNightReflector(Renderer surface, Color colour)
        {
            if (!reflectorMaterials.TryGetValue(colour, out Material material))
            {
                material = new Material(RuntimeMaterials.Coloured(colour));
                material.name = "Night reflective tape";
                material.SetFloat("_Metallic", .05f);
                material.SetFloat("_Smoothness", .7f);
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionId, colour * .14f);
                ownedMaterials.Add(material);
                reflectorMaterials.Add(colour, material);
            }
            surface.sharedMaterial = material;
            nightReflectors.Add(new NightReflector { Surface = surface, Colour = colour });
        }

        private void UpdateNightReflectors()
        {
            bool illuminated = world == ShiftWorld.Echo && playerHeadlight != null && playerHeadlight.isActiveAndEnabled;
            float outer = playerHeadlight != null ? Mathf.Cos(playerHeadlight.spotAngle * .5f * Mathf.Deg2Rad) : 1f;
            float inner = playerHeadlight != null ? Mathf.Cos(playerHeadlight.innerSpotAngle * .5f * Mathf.Deg2Rad) : 1f;
            foreach (NightReflector reflector in nightReflectors)
            {
                if (!reflector.Surface.gameObject.activeInHierarchy) continue;
                float brightness = world == ShiftWorld.Echo ? .14f : 0f;
                if (illuminated)
                {
                    Vector3 ray = reflector.Surface.bounds.center - playerHeadlight.transform.position;
                    float distance = ray.magnitude;
                    float alignment = distance > .01f ? Vector3.Dot(ray / distance, playerHeadlight.transform.forward) : 1f;
                    float cone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outer, inner, alignment));
                    float range = 1f - Mathf.InverseLerp(12f, playerHeadlight.range, distance);
                    brightness += cone * range * range * 3.2f;
                }
                // Simulated retroreflection follows the real lamp's cone and range;
                // outside it, only a faint baseline remains to outline the hazard.
                reflectorProperties.SetColor(EmissionId, reflector.Colour * brightness);
                reflector.Surface.SetPropertyBlock(reflectorProperties);
            }
        }

        private void RefreshHazards()
        {
            foreach (var hazard in hazards)
            {
                bool active = hazard.Data.World == world;
                bool preview = ShouldPreview(hazard, game.Distance);
                hazard.Solid.SetActive(active);
                hazard.Ghost.SetActive(preview);
                hazard.Label.gameObject.SetActive(hazard.Data.Kind != ShiftHazardKind.Gap && hazard.Data.Kind != ShiftHazardKind.HalfBarrier && active);
                hazard.Label.alpha = active ? 1f : .65f;
            }
        }

        private bool ShouldPreview(HazardView hazard, float distance) => false;

        private static void BuildCrossing(Transform parent, ShiftHazard data)
        {
            float depth = data.End - data.Start;
            Material white = RuntimeMaterials.Coloured(new Color(.88f, .9f, .92f));
            Material grey = RuntimeMaterials.Coloured(new Color(.58f, .6f, .62f));
            Material dark = RuntimeMaterials.Coloured(new Color(.28f, .3f, .33f));
            // A real transverse zebra crossing, not a solid slab over the road.
            for (float x = -4.65f; x <= 4.7f; x += .8f)
                Box(parent, "Night zebra stripe", new Vector3(x, .035f, 0f), new Vector3(.42f, .06f, depth + 1f), white);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(parent, "Crossing sign post", new Vector3(side * 5.55f, 1f, -depth * .5f), new Vector3(.12f, 2f, .12f), dark);
                Box(parent, "Crossing sign", new Vector3(side * 5.55f, 2f, -depth * .5f), new Vector3(.8f, .6f, .12f), white);
            }
            for (int lane = 0; lane < 3; lane++)
            {
                if ((data.LaneMask & (1 << lane)) == 0) continue;
                for (int n = 0; n < 3; n++)
                {
                    var person = new GameObject("Grey prototype pedestrian").transform;
                    person.SetParent(parent, false);
                    person.localPosition = new Vector3((lane - 1) * WorldShiftLevel.RoadWidth / 3f + (n - 1) * 1.05f, 0f,
                        (n - 1) * .28f);
                    Box(person, "Torso", new Vector3(0f, 1.12f, 0f), new Vector3(.43f, .7f, .3f), grey);
                    Renderer head = Box(person, "Head", new Vector3(0f, 1.68f, 0f), new Vector3(.37f, .38f, .35f), grey);
                    head.transform.localRotation = Quaternion.Euler(0f, lane * 8f - 8f, 0f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Renderer arm = Box(person, "Arm", new Vector3(side * .3f, 1.1f, 0f), new Vector3(.13f, .64f, .15f), grey);
                        arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 9f);
                        Box(person, "Leg", new Vector3(side * .13f, .4f, side * .06f), new Vector3(.16f, .7f, .2f), dark);
                    }
                }
            }
        }

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
                if (fuelPrefab != null) Visual(fuelPrefab, model, new Vector3(0f, .95f, 0f), new Vector3(1.2f, 1.4f, .7f));
                else Box(model, "Fuel", new Vector3(0f, .95f, 0f), new Vector3(1.2f, 1.4f, .7f), warmEdge);
                Label(model, "F", new Vector3(0f, 1f, -.38f), 1f, Color.white).fontSize = 7f;
                Label(model, "FUEL", new Vector3(0f, 2.1f, 0f), 2f, new Color(.55f, 1f, .7f)).fontSize = 4f;
                fuelModels.Add(model.gameObject);
            }
        }

        public void RefreshFuel()
        {
            for (int i = 0; i < fuelModels.Count; i++)
                fuelModels[i].SetActive((game.Pickups[i].World == world) && !game.IsFuelCollected(i));
        }

        private void SpikeStrip(Transform parent, float x, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 6; column++)
                {
                    Vector3 centre = new Vector3(x + (column - 2.5f) * .49f, .2f, (row - 1.5f) * depth / 4f);
                    Vector3[] corners = { new Vector3(-.19f, 0f, -.19f), new Vector3(.19f, 0f, -.19f),
                        new Vector3(.19f, 0f, .19f), new Vector3(-.19f, 0f, .19f) };
                    for (int face = 0; face < 4; face++)
                    {
                        int first = vertices.Count;
                        vertices.Add(centre + corners[face]);
                        vertices.Add(centre + Vector3.up * .7f);
                        vertices.Add(centre + corners[(face + 1) % 4]);
                        triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    }
                }
            var mesh = new Mesh { name = "Prototype police spike strip" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            spikeMeshes.Add(mesh);
            var strip = new GameObject("Metal spikes", typeof(MeshFilter), typeof(MeshRenderer));
            strip.transform.SetParent(parent, false);
            strip.GetComponent<MeshFilter>().sharedMesh = mesh;
            strip.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Coloured(new Color(.88f, .91f, .94f));
        }

        private static GameObject Visual(GameObject prefab, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject instance = Instantiate(prefab, parent);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = scale;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
            // Visual reuse only: the finite controller owns pickup/collision rules.
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            return instance;
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
            playerHeadlight.range = 74f;
            playerHeadlight.spotAngle = 36f;
            playerHeadlight.innerSpotAngle = 22f;
            playerHeadlight.intensity = 900f;
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
                float z = t * 54f;
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
            spikeMeshes.Add(mesh);
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
            foreach (Mesh mesh in spikeMeshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }

        private void Tint(Renderer renderer, Color day, Color echo)
        {
            palette.Add(new PaletteItem { Renderer = renderer, Day = RuntimeMaterials.Coloured(day), Echo = RuntimeMaterials.Coloured(echo) });
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
