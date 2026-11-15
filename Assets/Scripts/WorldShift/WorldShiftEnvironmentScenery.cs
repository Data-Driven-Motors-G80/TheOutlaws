using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Environment art anchored to the shared road. No gameplay colliders.</summary>
    public sealed partial class WorldShiftEnvironmentScenery : MonoBehaviour
    {
        private sealed class Chunk { public float At; public GameObject Root; public bool Deck; }
        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Light> lamps = new List<Light>();
        private readonly List<Vector3> roadPoints = new List<Vector3>();
        private WorldShiftRoute route;
        private Material surface, windows, water, sky;
        private bool night;
        private float distance;

        public void Build(WorldShiftRoute sharedRoute, GameObject streetlight)
        {
            route = sharedRoute;
            surface = MakeMaterial("City and oasis surfaces", 0f);
            windows = MakeMaterial("City facade light strips", .15f);
            water = new Material(Resources.Load<Shader>("WorldShiftOceanWater")) { name = "Calm oasis water" };
            water.SetFloat("_ChannelMask", 0f); water.SetFloat("_RippleScale", .6f);
            water.SetFloat("_WaveSpeed", .4f); water.SetFloat("_FoamAmount", .015f); materials.Add(water);
            sky = new Material(Resources.Load<Shader>("WorldShiftEnvironmentSky")); materials.Add(sky);
            surface.SetFloat("_Finish", 3f); surface.SetFloat("_Wear", .15f);
            for (float at = -80; at <= route.Course.Length + 80; at += 3f) roadPoints.Add(route.Sample(at).Position);
            if (route.Course.Environment == ShiftLevel.City) { BuildCity(streetlight); route.VisitRoadPieces(StreetEdges); }
            else if (route.Course.Environment == ShiftLevel.Desert) BuildOases();
            else BuildAlpine();
            SetWorld(ShiftWorld.Day);
            UpdateVisibility(0f);
        }

        private Material MakeMaterial(string label, float emission)
        {
            var material = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = label };
            material.SetFloat("_Emission", emission);
            materials.Add(material);
            return material;
        }

        private Transform Root(string label, float at, float lateral)
        {
            var p = route.Sample(at);
            var root = new GameObject(label).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(p.Position + p.Right * lateral,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(p.Forward, Vector3.up), Vector3.up));
            chunks.Add(new Chunk { At = at, Root = root.gameObject });
            return root;
        }

        private void Render(Transform parent, string label, WorldShiftArtMesh geometry, Material material)
        {
            var root = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            var mesh = geometry.Mesh(label); meshes.Add(mesh);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Check the whole route, including the opposite arm of any bend. Clearance
        // includes the decoration's radius, the road, and a sampling margin.
        private bool Clear(float at, float lateral, float radius)
        {
            var p = route.Sample(at);
            Vector3 centre = p.Position + p.Right * lateral;
            float limit = radius + WorldShiftLevel.RoadWidth * .5f + 2f;
            foreach (var road in roadPoints)
                if (new Vector2(centre.x - road.x, centre.z - road.z).sqrMagnitude < limit * limit) return false;
            return true;
        }

        private bool NearJump(float at, float margin)
        {
            foreach (var jump in route.Course.Ramps)
                if (at > jump.Ramp - margin && at < jump.End + margin) return true;
            return false;
        }

        private void BuildCity(GameObject streetlight)
        {
            // Original city palette and facade strips, batched into small meshes.
            Color[] colours = { new Color(.36f,.53f,.57f), new Color(.58f,.43f,.37f),
                new Color(.61f,.57f,.43f), new Color(.43f,.46f,.54f) };
            int index = 0;
            for (float at = -35; at < route.Course.Length + 50; at += 32f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float along = at + (side > 0 ? 12f : 0f);
                    float lateral = side * 20f;
                    if (!Clear(along, lateral, 10f)) continue;
                    int n = index++;
                    float height = 11f + n * 7 % 19;
                    float width = 9f + n % 3;
                    var root = Root("City block", along, lateral);
                    Architecture(root, side, n, height, width, colours[n % 4]);
                    if (n % 3 == 0) CityFurniture(along + 12f, side, n);
                    float lampAt = along + 7;
                    if (streetlight == null || NearJump(lampAt, 12f) || !Clear(lampAt, side * 8f, .55f)) continue;
                    var p = route.Sample(lampAt);
                    // Reuse the existing CityStreetlight prefab and its materials.
                    var lamp = Instantiate(streetlight, transform);
                    lamp.name = "City roadside streetlight";
                    lamp.transform.SetPositionAndRotation(p.Position + p.Right * side * 8f, Quaternion.LookRotation(p.Forward, p.Up));
                    foreach (var collider in lamp.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
                    foreach (var light in lamp.GetComponentsInChildren<Light>())
                    { light.shadows = LightShadows.None; light.range = 10f; light.intensity = 5f; lamps.Add(light); }
                    chunks.Add(new Chunk { At = lampAt, Root = lamp });
                }
        }

        private void BuildOases()
        {
            int index = 0;
            for (float at = 135f; at < route.Course.Length - 80f; at += 180f)
            {
                float side = index++ % 2 == 0 ? -1f : 1f;
                if (NearJump(at, 55f) || !Clear(at, side * 24f, 13f)) continue;
                var root = Root("Desert oasis", at, side * 24f);
                var bank = new WorldShiftArtMesh(); var pool = new WorldShiftArtMesh(); var palms = new WorldShiftArtMesh();
                // The pond and shore sample the sloped road frame, just like the sand.
                // No flat landscape plane may span a jump or intrude into a bend.
                Vector3 Point(float angle, float scale, float lift)
                {
                    float irregular = 1f + .055f * Mathf.Sin(angle * 3) + .035f * Mathf.Cos(angle * 5);
                    float along = at + Mathf.Sin(angle) * 12f * scale * irregular;
                    float across = side * 24f + Mathf.Cos(angle) * 6.5f * scale * irregular;
                    var p = route.Sample(along);
                    return root.InverseTransformPoint(p.Position + p.Right * across + p.Up * lift);
                }
                var centre = route.Sample(at);
                Vector3 middle = root.InverseTransformPoint(centre.Position + centre.Right * (side * 24f) + centre.Up * .13f);
                for (int j = 0; j < 40; j++)
                {
                    float a = j * Mathf.PI / 20, b = (j + 1) * Mathf.PI / 20;
                    pool.Triangle(middle, Point(b, 1, .13f), Point(a, 1, .13f), new Color(.10f, .51f, .55f), true);
                    bank.Quad(Point(a, 1, .11f), Point(b, 1, .11f), Point(b, 1.12f, .09f), Point(a, 1.12f, .09f), new Color(.54f, .61f, .36f));
                    bank.Quad(Point(a, 1.12f, .09f), Point(b, 1.12f, .09f), Point(b, 1.30f, .045f), Point(a, 1.30f, .045f), new Color(.63f, .62f, .40f));
                    if (j % 3 == 0)
                    {
                        var rock = Point(a, 1.22f, .04f);
                        bank.Rock(rock, new Vector3(.55f, .35f, .4f), new Color(.62f, .50f, .33f), j);
                        for (int reed = 0; reed < 5; reed++)
                        {
                            var foot = Point(a + reed * .018f, 1.08f, .13f);
                            palms.Beam(foot, foot + new Vector3(.08f, .45f + reed * .07f, .05f), .035f, .025f, new Color(.37f, .47f, .24f));
                        }
                    }
                }
                for (int i = 0; i < 5; i++)
                {
                    float a = (i + .35f) * Mathf.PI * 2 / 5;
                    Palm(palms, Point(a, 1.35f, 0), 5.8f + (i % 3) * .8f, i);
                }
                Render(root, "Oasis shore", bank, surface); Render(root, "Oasis water", pool, water); Render(root, "Coconut palms", palms, surface);
            }
            // Small roadside palm groups also make the driving stretches recognisable.
            for (float at = 65; at < route.Course.Length; at += 95)
            {
                float side = ((int)(at / 95) % 2 == 0) ? 1 : -1;
                if (NearJump(at, 25) || !Clear(at, side * 12.5f, 5f)) continue;
                var root = Root("Roadside coconut palm", at, side * 12.5f);
                var tree = new WorldShiftArtMesh(); Palm(tree, new Vector3(0, -.2f, 0), 6.4f, (int)at);
                Render(root, "Palm mesh", tree, surface);
            }
        }

        private static void Palm(WorldShiftArtMesh mesh, Vector3 foot, float height, int seed)
            => PalmDetailed(mesh, foot, height, seed);

        public void ApplyAtmosphere(Camera camera, Light sun)
        {
            if (IsAlpine) { ApplyAlpineAtmosphere(camera, sun); return; }
            bool desert = route.Course.Environment == ShiftLevel.Desert;
            Color horizon = night ? new Color(.06f, .105f, .17f) : desert ? new Color(.84f, .74f, .57f) : new Color(.73f, .80f, .82f);
            sky.SetFloat("_Night", night ? 1 : 0);
            sky.SetColor("_Horizon", horizon);
            sky.SetColor("_Zenith", night ? new Color(.012f, .025f, .065f) : new Color(.22f, .46f, .66f));
            RenderSettings.skybox = sky; camera.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fogColor = horizon;
            if (desert && !night) sun.color = new Color(1f, .85f, .65f);
        }

        public void SetWorld(ShiftWorld world)
        {
            night = world == ShiftWorld.Echo;
            surface.SetColor("_BaseColor", night ? new Color(.42f, .53f, .69f) : Color.white);
            windows.SetFloat("_Emission", night ? .72f : .08f);
            windows.SetColor("_BaseColor", night ? new Color(.85f, .90f, 1) : new Color(.72f, .82f, .88f));
            water.SetColor("_DeepColor", night ? new Color(.018f, .07f, .11f) : new Color(.015f, .27f, .29f));
            water.SetColor("_ShallowColor", night ? new Color(.06f, .20f, .23f) : new Color(.09f, .54f, .47f));
            water.SetColor("_SkyColor", night ? new Color(.055f, .11f, .17f) : new Color(.67f, .76f, .68f));
            water.SetColor("_FoamColor", night ? new Color(.13f, .26f, .3f) : new Color(.64f, .81f, .66f));
            UpdateVisibility(distance);
        }

        public void UpdateVisibility(float at)
        {
            distance = at;
            bool mountain = route.Course.Environment == ShiftLevel.Mountain;
            foreach (var chunk in chunks)
            {
                // A ridge may extend far beyond its anchor or reappear around a bend.
                // Keep mountain art alive; Unity culls each renderer against the camera.
                bool inRange = mountain || (chunk.At > at - 110f && chunk.At < at + 310f);
                bool hasDeck = !chunk.Deck || route.HasSurface(chunk.At, night ? ShiftWorld.Echo : ShiftWorld.Day);
                bool visible = inRange && hasDeck;
                if (chunk.Root.activeSelf != visible) chunk.Root.SetActive(visible);
            }
            water.SetFloat("_OceanTime", distance * .025f);
            UpdateLamps();
        }

        private void UpdateLamps()
        {
            // Only nearby lamps cast light; distant emissive lenses remain visible.
            Vector3 player = route.Sample(distance).Position;
            foreach (var lamp in lamps) lamp.enabled = night && (lamp.transform.position - player).sqrMagnitude < 65f * 65f;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in materials) Destroy(material);
        }
    }
}
