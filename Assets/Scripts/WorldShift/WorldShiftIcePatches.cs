using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Road-hugging sheets of black ice for <see cref="LevelCourse.IceZones"/>. Icy blue and streaked so
    /// the slippery stretch reads from far away in both worlds. Each end has a wavy frost rim, and flecks of
    /// ice thicken on the dry road before it, so the road visibly freezes over where grip is lost. Visual only: handling lives in WorldShiftCity.Ice.cs and collision never changes.
    /// </summary>
    public sealed class WorldShiftIcePatches : MonoBehaviour
    {
        private const int Columns = 25;
        private const float RowSpacing = 1.5f;
        private const float WavyEnds = 6f;
        private const float RimLength = 1.1f;
        private const float FleckReach = 18f;
        private const int FlecksPerEnd = 70;
        private const float Lift = .035f;

        private static readonly Color DayTint = Color.white;
        private static readonly Color NightTint = new Color(.85f, .95f, 1f);
        private static readonly Color NightGlow = new Color(.05f, .13f, .2f);
        private static readonly Color DayFrost = new Color(.9f, .96f, 1f);
        private static readonly Color NightFrost = new Color(.62f, .76f, .9f);
        private static readonly Color NightFrostGlow = new Color(.08f, .16f, .24f);

        private readonly List<Mesh> meshes = new List<Mesh>();
        private WorldShiftCity game;
        private Material material, frost;
        private Texture2D texture;
        private ShiftWorld shown;
        private bool hasShown;

        public static WorldShiftIcePatches Create(WorldShiftCity game, WorldShiftRoute route, LevelCourse course)
        {
            var root = new GameObject("Snow black ice");
            root.transform.SetParent(game.transform, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var view = root.AddComponent<WorldShiftIcePatches>();
            view.Build(game, route, course);
            return view;
        }

        private void Build(WorldShiftCity owner, WorldShiftRoute route, LevelCourse course)
        {
            game = owner;
            texture = BuildTexture();
            material = new Material(RuntimeMaterials.Coloured(Color.white)) { name = "Snow black ice" };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            // A bright Day sky reflected at this low camera angle washes the ice out to white, so the sheen
            // comes from the streak texture instead of sky reflections.
            material.SetFloat("_Smoothness", .3f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            frost = new Material(RuntimeMaterials.Coloured(DayFrost)) { name = "Snow ice frost rim" };
            frost.SetFloat("_Smoothness", .15f);
            frost.SetFloat("_Metallic", 0f);
            frost.EnableKeyword("_EMISSION");
            for (int i = 0; i < course.IceZones.Length; i++)
                BuildPatch(route, course.IceZones[i], i);
            Present(ShiftWorld.Day);
        }

        // Smooth, organic edge: two slow waves across the road, drawn back a little at the kerbs.
        private static float EdgeInset(float across, int seed)
        {
            float phase = Hash(seed) * 6.283f, phase2 = Hash(seed + 17) * 6.283f;
            float wave = .5f + .3f * Mathf.Sin(across * 7.2f + phase) + .2f * Mathf.Sin(across * 15.7f + phase2);
            float kerb = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.62f, 1f, Mathf.Abs(across - .5f) * 2f));
            return WavyEnds * Mathf.Clamp01(.15f + .7f * wave + .35f * kerb);
        }

        private void BuildPatch(WorldShiftRoute route, Vector2 zone, int seed)
        {
            float from = zone.x, to = zone.y;
            var heads = new float[Columns];
            var tails = new float[Columns];
            for (int c = 0; c < Columns; c++)
            {
                float across = c / (Columns - 1f);
                heads[c] = from + EdgeInset(across, seed * 31);
                tails[c] = to - EdgeInset(across, seed * 57 + 5);
            }

            // Main sheet.
            int rows = Mathf.Max(2, Mathf.CeilToInt((to - from) / RowSpacing) + 1);
            var sheet = new Strip(Columns * rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    float across = c / (Columns - 1f);
                    float d = Mathf.Lerp(heads[c], tails[c], r / (rows - 1f));
                    sheet.Add(Surface(route, d, across, Lift), new Vector2(across, Mathf.InverseLerp(from, to, d)));
                }
            sheet.Grid(rows, Columns);
            Render("Black ice " + from.ToString("0") + "-" + to.ToString("0"), sheet, material);

            // Frost rims where the ice begins and ends: a bright, slightly raised lip that follows the wave.
            var rims = new Strip(Columns * 4);
            foreach (bool start in new[] { true, false })
            {
                int first = rims.Count;
                for (int r = 0; r < 2; r++)
                    for (int c = 0; c < Columns; c++)
                    {
                        float across = c / (Columns - 1f);
                        float edge = start ? heads[c] : tails[c];
                        float d = start ? edge + r * RimLength : edge - (1 - r) * RimLength;
                        rims.Add(Surface(route, d, across, Lift + .01f), new Vector2(across, r));
                    }
                rims.Grid(2, Columns, first);
            }
            Render("Ice frost rims", rims, frost);

            // Flecks of ice on the dry road before and after, thickening toward the sheet.
            var flecks = new Strip(FlecksPerEnd * 2 * 9);
            var random = new System.Random(911 + seed * 7919);
            foreach (bool start in new[] { true, false })
                for (int n = 0; n < FlecksPerEnd; n++)
                {
                    float across = .03f + (float)random.NextDouble() * .94f;
                    int column = Mathf.Clamp(Mathf.RoundToInt(across * (Columns - 1)), 0, Columns - 1);
                    float closeness = Mathf.Sqrt((float)random.NextDouble());
                    float gap = .3f + (1f - closeness) * FleckReach;
                    float d = start ? heads[column] - gap : tails[column] + gap;
                    float radius = Mathf.Lerp(.18f, .95f, closeness * closeness) * (.7f + .6f * (float)random.NextDouble());
                    Fleck(route, flecks, d, across, radius, (float)random.NextDouble() * 6.283f);
                }
            Render("Ice flecks " + from.ToString("0"), flecks, material);
        }

        private static Vector3 Surface(WorldShiftRoute route, float d, float across, float lift)
        {
            RoadPathPoint p = route.Sample(d);
            float half = Mathf.Max(1f, p.Width * .5f - .25f);
            return p.Position + p.Right * Mathf.Lerp(-half, half, across) + p.Up * lift;
        }

        // An irregular, road-aligned blob (stretched along travel like skid-polished ice).
        private static void Fleck(WorldShiftRoute route, Strip strip, float d, float across, float radius, float turn)
        {
            RoadPathPoint p = route.Sample(d);
            Vector3 centre = Surface(route, d, across, Lift - .004f);
            int first = strip.Count;
            strip.Add(centre, new Vector2(across, .5f));
            const int points = 8;
            for (int i = 0; i < points; i++)
            {
                float angle = turn + i * Mathf.PI * 2f / points;
                float wobble = .7f + .45f * Mathf.PerlinNoise(across * 31f + i * .7f, d * .13f);
                Vector3 offset = p.Right * (Mathf.Cos(angle) * radius * wobble) + p.Forward * (Mathf.Sin(angle) * radius * wobble * 1.8f);
                strip.Add(centre + offset, new Vector2(across, .5f + Mathf.Sin(angle) * .002f));
            }
            for (int i = 0; i < points; i++)
            {
                int a = first + 1 + i, b = first + 1 + (i + 1) % points;
                // Wind so the face points up (clockwise seen from above).
                strip.Triangle(first, b, a);
            }
        }

        private void Render(string label, Strip strip, Material surface)
        {
            var mesh = strip.ToMesh(label);
            meshes.Add(mesh);
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = surface;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private sealed class Strip
        {
            private readonly List<Vector3> vertices;
            private readonly List<Vector2> uvs;
            private readonly List<int> triangles = new List<int>();
            public Strip(int capacity) { vertices = new List<Vector3>(capacity); uvs = new List<Vector2>(capacity); }
            public int Count => vertices.Count;
            public void Add(Vector3 v, Vector2 uv) { vertices.Add(v); uvs.Add(uv); }
            public void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            // Rows run along the road, columns across it; faces point up.
            public void Grid(int rows, int columns, int first = 0)
            {
                for (int r = 0; r < rows - 1; r++)
                    for (int c = 0; c < columns - 1; c++)
                    {
                        int a = first + r * columns + c, b = a + 1, d = a + columns, e = d + 1;
                        Triangle(a, d, e); Triangle(a, e, b);
                    }
            }
            public Mesh ToMesh(string label)
            {
                var mesh = new Mesh { name = label };
                if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private void LateUpdate()
        {
            if (game != null && (!hasShown || game.World != shown)) Present(game.World);
        }

        private void Present(ShiftWorld world)
        {
            shown = world;
            hasShown = true;
            bool night = world == ShiftWorld.Echo;
            Color tint = night ? NightTint : DayTint;
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            material.SetColor("_EmissionColor", night ? NightGlow : Color.black);
            Color rim = night ? NightFrost : DayFrost;
            frost.SetColor("_BaseColor", rim);
            frost.SetColor("_Color", rim);
            frost.SetColor("_EmissionColor", night ? NightFrostGlow : Color.black);
        }

        // Tall, non-repeating texture: across = road width, down = the whole patch. Streaks run along the
        // road (skid-polished ice), white frost gathers at both road edges and thin cracks wander across.
        private static Texture2D BuildTexture()
        {
            const int width = 128, height = 1024;
            var pixels = new Color32[width * height];
            Color deep = new Color(.12f, .31f, .5f), pale = new Color(.27f, .52f, .7f), frost = new Color(.86f, .94f, .99f);
            Color paint = new Color(.8f, .87f, .9f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f), v = y / (height - 1f);
                    float body = Mathf.PerlinNoise(u * 3.1f + 7.3f, v * 9f + 1.7f);
                    float polish = Mathf.PerlinNoise(u * 38f + 3.9f, v * 3.2f + 11.1f);
                    Color colour = Color.Lerp(deep, pale, Mathf.SmoothStep(.25f, .8f, body));
                    // Long bright streaks along the direction of travel.
                    colour = Color.Lerp(colour, frost, Mathf.SmoothStep(.62f, .78f, polish) * .5f);
                    // Lane lines stay readable under the ice: steering on ice is all about lane position.
                    float lane = Mathf.Min(Mathf.Abs(u - 1f / 3f), Mathf.Abs(u - 2f / 3f)) * width;
                    colour = Color.Lerp(colour, paint, Mathf.Clamp01(1.6f - lane) * .6f);
                    // Frost banks up at the road edges.
                    float edge = Mathf.Abs(u - .5f) * 2f;
                    float rime = Mathf.SmoothStep(.78f, 1f, edge + (Mathf.PerlinNoise(v * 60f, u * 4f) - .5f) * .18f);
                    colour = Color.Lerp(colour, frost, rime * .85f);
                    pixels[y * width + x] = colour;
                }
            var random = new System.Random(4217);
            for (int crack = 0; crack < 70; crack++)
            {
                float x = (float)random.NextDouble() * width, y = (float)random.NextDouble() * height;
                float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                int length = 12 + random.Next(40);
                for (int step = 0; step < length; step++)
                {
                    angle += ((float)random.NextDouble() - .5f) * .9f;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle) * 2.2f;
                    int px = Mathf.FloorToInt(x), py = Mathf.FloorToInt(y);
                    if (px < 0 || px >= width || py < 0 || py >= height) break;
                    pixels[py * width + px] = Color32.Lerp(pixels[py * width + px], new Color32(248, 252, 255, 255), .8f);
                }
            }
            var result = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "Snow black ice streaks",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            result.SetPixels32(pixels);
            result.Apply(true, true);
            return result;
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                return ((x >> 22) ^ x) / (float)uint.MaxValue;
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear();
            if (material != null) Destroy(material);
            if (frost != null) Destroy(frost);
            if (texture != null) Destroy(texture);
        }
    }
}
