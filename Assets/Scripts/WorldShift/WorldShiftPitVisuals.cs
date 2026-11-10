using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>A readable open shaft, fitted to the same edge vertices as the missing road.</summary>
    public sealed class WorldShiftPitVisuals : MonoBehaviour
    {
        private sealed class Row
        {
            public float At;
            public Vector3 Left, Right, Up, Forward;
        }

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Material lavaMaterial;
        private float lavaTime;
        private readonly List<Material> materials = new List<Material>();

        public static WorldShiftPitVisuals Build(Transform parent, WorldShiftRoute route, ShiftHazard gap, bool warnings = true)
        {
            var root = new GameObject("Open pit - fitted edges and depth cues");
            root.transform.SetParent(parent, false);
            var pit = root.AddComponent<WorldShiftPitVisuals>();
            List<Row> rows = CollectEdges(route, gap);
            if (rows.Count < 2) return pit;
            bool night = gap.World == ShiftWorld.Echo;
            Material wall = pit.Glow(new Color(.13f, .16f, .2f), night ? .12f : .018f);
            Material farWall = pit.Glow(new Color(.17f, .22f, .27f), night ? .25f : .045f);
            Material bottom = RuntimeMaterials.Coloured(new Color(.012f, .016f, .022f));
            Material yellow = pit.Glow(new Color(1f, .7f, .16f), night ? .65f : .2f);
            Material dark = RuntimeMaterials.Coloured(new Color(.012f, .016f, .02f));
            Material depthMark = pit.Glow(new Color(1f, .62f, .19f), night ? .6f : .11f);

            pit.BuildShaft(rows, wall, farWall, bottom);
            // A flat red floor marks the opening as deadly in both worlds (no decorative lava/spike art).
            pit.BuildSurface(rows, 3.4f, "Pit danger floor", pit.Glow(new Color(.85f, .12f, .08f), night ? .45f : .2f));
            if (!warnings) return pit;
            pit.BuildLip(route, gap.Start, true, yellow, dark);
            pit.BuildLip(route, gap.End, false, yellow, dark);
            pit.BuildDepthMarks(route, gap, depthMark);
            pit.BuildApproachCrosses(route, gap.Start, yellow);
            if (night) pit.BuildNightRim(route, gap, yellow);
            return pit;
        }

        private void BuildSpikes(WorldShiftRoute route, ShiftHazard gap, List<Row> rows)
        {
            Material steel = Glow(new Color(.62f, .68f, .73f), .18f);
            if (steel.HasProperty("_Metallic")) steel.SetFloat("_Metallic", .7f);
            if (steel.HasProperty("_Smoothness")) steel.SetFloat("_Smoothness", .6f);
            Material blood = Glow(new Color(.65f, .015f, .025f), .12f);
            if (blood.HasProperty("_Smoothness")) blood.SetFloat("_Smoothness", .65f);
            Material bed = Glow(new Color(.12f, .14f, .16f), .05f);
            BuildSurface(rows, 4.8f, "Day spike bed", bed);
            var triangles = new List<Vector3>();
            var redTips = new List<Vector3>();
            int row = 0;
            // Staggered pyramids fill the opening while their tips stay below road height.
            for (float at = gap.Start + 1.2f; at < gap.End - .8f; at += 2.4f, row++)
            {
                var point = route.Sample(at);
                float halfWidth = point.Width * .5f;
                int column = 0;
                for (float x = -halfWidth + .8f + (row % 2) * .45f; x < halfWidth - .6f; x += 1.65f, column++)
                {
                    Vector3 centre = point.Position + point.Right * x + Vector3.down * 4.8f;
                    float height = 2.8f + .65f * (.5f + .5f * Mathf.Sin(row * 2.1f + column * 1.7f));
                    Vector3 tip = centre + Vector3.up * height;
                    Vector3 right = point.Right * .58f, forward = point.Forward * .7f;
                    Vector3 a = centre - right - forward, b = centre + right - forward;
                    Vector3 c = centre + right + forward, d = centre - right + forward;
                    // Uneven stain edges keep the red caps from looking like painted bands.
                    float stain = .69f + .035f * Mathf.Sin(row * 1.6f + column * 2.4f);
                    Vector3 ca = Vector3.Lerp(a, tip, stain), cb = Vector3.Lerp(b, tip, stain + .06f);
                    Vector3 cc = Vector3.Lerp(c, tip, stain - .05f), cd = Vector3.Lerp(d, tip, stain + .025f);
                    void Face(Vector3 left, Vector3 rightEdge, Vector3 cutLeft, Vector3 cutRight)
                    {
                        Triangle(triangles, left, cutLeft, rightEdge);
                        Triangle(triangles, rightEdge, cutLeft, cutRight);
                        Triangle(redTips, cutLeft, tip, cutRight);
                    }
                    Face(a, b, ca, cb); Face(b, c, cb, cc);
                    Face(c, d, cc, cd); Face(d, a, cd, ca);
                }
            }
            TriangleObject("Day pit metal spikes", triangles, steel);
            TriangleObject("Blood-stained spike tips", redTips, blood);
        }

        private void BuildLava(WorldShiftRoute route, ShiftHazard gap, List<Row> rows)
        {
            // Orange molten flow surrounds scattered, irregular black volcanic rocks.
            Texture2D texture = CreateLavaTexture();
            lavaMaterial = Glow(Color.white, .65f);
            if (lavaMaterial.HasProperty("_BaseMap")) lavaMaterial.SetTexture("_BaseMap", texture);
            if (lavaMaterial.HasProperty("_MainTex")) lavaMaterial.SetTexture("_MainTex", texture);
            lavaMaterial.SetTexture("_EmissionMap", texture);
            if (lavaMaterial.HasProperty("_Smoothness")) lavaMaterial.SetFloat("_Smoothness", .38f);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            const int columns = 12;
            foreach (Row row in rows)
            {
                float width = Vector3.Distance(row.Left, row.Right);
                for (int column = 0; column <= columns; column++)
                {
                    float t = column / (float)columns;
                    float ripple = .06f * Mathf.Sin(row.At * .7f + column * 1.3f);
                    vertices.Add(transform.InverseTransformPoint(Vector3.Lerp(row.Left, row.Right, t)
                        + Vector3.down * (3.4f + ripple)));
                    uv.Add(new Vector2(t * width / 10f, row.At / 12f));
                }
            }
            for (int row = 1; row < rows.Count; row++)
                for (int column = 0; column < columns; column++)
                {
                    int a = (row - 1) * (columns + 1) + column, b = a + 1;
                    int c = row * (columns + 1) + column, d = c + 1;
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }
            var mesh = new Mesh { name = "Continuous molten lava surface" };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var surface = new GameObject("Flowing night lava", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(transform, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            surface.GetComponent<MeshRenderer>().sharedMaterial = lavaMaterial;
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            BuildLavaRocks(route, gap);
        }

        private void BuildLavaRocks(WorldShiftRoute route, ShiftHazard gap)
        {
            Material basalt = Glow(new Color(.022f, .019f, .018f), .01f);
            if (basalt.HasProperty("_Smoothness")) basalt.SetFloat("_Smoothness", .12f);
            var triangles = new List<Vector3>();
            var random = new System.Random(Mathf.RoundToInt(gap.Start * 17f) ^ 7391);
            float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            int count = Mathf.CeilToInt((gap.End - gap.Start) * .65f);
            for (int i = 0; i < count; i++)
            {
                float at = Range(gap.Start + 1.4f, gap.End - 1.4f);
                var point = route.Sample(at);
                float radiusX = Range(.4f, 1f), radiusZ = Range(.55f, 1.25f);
                float x = Range(-point.Width * .5f + radiusX + .15f, point.Width * .5f - radiusX - .15f);
                Vector3 centre = point.Position + point.Right * x + Vector3.down * 3.4f;
                Vector3 peak = centre + Vector3.up * Range(.2f, .4f);
                int sides = random.Next(6, 10);
                var rim = new Vector3[sides];
                float rotation = Range(0f, Mathf.PI * 2f);
                // Unequal angular chunks are scattered freely rather than arranged in a grid.
                for (int corner = 0; corner < sides; corner++)
                {
                    float angle = rotation + (corner + Range(-.18f, .18f)) * Mathf.PI * 2f / sides;
                    float irregularity = Range(.7f, 1f);
                    rim[corner] = centre + point.Right * (Mathf.Cos(angle) * radiusX * irregularity)
                        + point.Forward * (Mathf.Sin(angle) * radiusZ * irregularity) + Vector3.up * Range(.1f, .23f);
                }
                for (int corner = 0; corner < sides; corner++)
                {
                    Vector3 a = rim[corner], b = rim[(corner + 1) % sides];
                    Vector3 lowA = new Vector3(a.x, centre.y - .15f, a.z);
                    Vector3 lowB = new Vector3(b.x, centre.y - .15f, b.z);
                    Triangle(triangles, peak, b, a);
                    Triangle(triangles, a, b, lowA); Triangle(triangles, b, lowB, lowA);
                }
            }
            TriangleObject("Irregular black volcanic rocks", triangles, basalt);
        }

        private Texture2D CreateLavaTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = "Organic molten lava", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Periodic domain warp bends the veins and hides the texture's repeat.
                    float warpX = SeamlessNoise(u, v, 3f, 12f) - .5f;
                    float warpY = SeamlessNoise(u, v, 3f, 37f) - .5f;
                    float a = u + warpX * .55f, b = v + warpY * .55f;
                    float broad = SeamlessNoise(a, b, 5f, 71f);
                    float fine = SeamlessNoise(a, b, 13f, 101f);
                    float detail = SeamlessNoise(a, b, 28f, 151f);
                    float rock = broad * .72f + fine * .22f + detail * .06f;
                    float vein = 1f - Mathf.SmoothStep(.015f, .105f, Mathf.Abs(rock - .5f));
                    Color baseColour = Color.Lerp(new Color(.7f, .065f, .003f), new Color(1f, .19f, .004f), broad);
                    Color molten = Color.Lerp(new Color(1f, .18f, .003f), new Color(1f, .34f, .012f), vein * vein);
                    pixels[y * size + x] = Color.Lerp(baseColour, molten, vein);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            textures.Add(texture);
            return texture;
        }

        private static float SeamlessNoise(float u, float v, float frequency, float seed)
        {
            u = Mathf.Repeat(u, 1f); v = Mathf.Repeat(v, 1f);
            float x = u * frequency, y = v * frequency;
            float a = Mathf.PerlinNoise(seed + x, seed + y);
            float b = Mathf.PerlinNoise(seed + x - frequency, seed + y);
            float c = Mathf.PerlinNoise(seed + x, seed + y - frequency);
            float d = Mathf.PerlinNoise(seed + x - frequency, seed + y - frequency);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private void Update()
        {
            if (lavaMaterial == null) return;
            lavaTime += Time.deltaTime;
            Vector2 drift = new Vector2(Mathf.Sin(lavaTime * .09f) * .025f, lavaTime * .012f);
            if (lavaMaterial.HasProperty("_BaseMap")) lavaMaterial.SetTextureOffset("_BaseMap", drift);
            if (lavaMaterial.HasProperty("_MainTex")) lavaMaterial.SetTextureOffset("_MainTex", drift);
            lavaMaterial.SetTextureOffset("_EmissionMap", drift);
            lavaMaterial.SetColor("_EmissionColor", Color.white * (.65f + .06f * Mathf.Sin(lavaTime * .8f)));
        }

        private void BuildSurface(List<Row> rows, float depth, string name, Material material)
        {
            var vertices = new List<Vector3>();
            Vector3 drop = Vector3.down * depth;
            for (int i = 1; i < rows.Count; i++)
                Quad(vertices, rows[i - 1].Left + drop, rows[i].Left + drop,
                    rows[i].Right + drop, rows[i - 1].Right + drop);
            MeshObject(name, vertices, material);
        }

        private static void Triangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c)
        { vertices.Add(a); vertices.Add(b); vertices.Add(c); }

        private void TriangleObject(string name, List<Vector3> worldVertices, Material material)
        {
            if (worldVertices.Count == 0) return;
            var vertices = new List<Vector3>(worldVertices.Count);
            var indices = new List<int>(worldVertices.Count);
            foreach (Vector3 vertex in worldVertices)
            { indices.Add(vertices.Count); vertices.Add(transform.InverseTransformPoint(vertex)); }
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            root.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private void BuildNightRim(WorldShiftRoute route, ShiftHazard gap, Material marker)
        {
            // Trace the opening, never cover its empty interior with a bright plane.
            for (float at = gap.Start; at < gap.End; at += 2f)
            {
                float end = Mathf.Min(at + 2f, gap.End);
                RoadPathPoint p = route.Sample((at + end) * .5f);
                foreach (float side in new[] { -1f, 1f })
                    Box("Night pit continuous rim", p.Position + p.Right * side * (p.Width * .5f - .04f) + p.Up * .09f,
                        Quaternion.LookRotation(p.Forward, p.Up), new Vector3(.18f, .12f, end - at + .03f), marker);
            }
            foreach (float at in new[] { gap.Start, gap.End })
            {
                RoadPathPoint p = route.Sample(at);
                foreach (float side in new[] { -1f, 1f })
                    Box("Night pit corner beacon", p.Position + p.Right * side * (p.Width * .5f + .22f) + p.Up * .95f,
                        Quaternion.LookRotation(p.Forward, p.Up), new Vector3(.16f, 1.9f, .16f), marker);
            }
        }

        private static List<Row> CollectEdges(WorldShiftRoute route, ShiftHazard gap)
        {
            var rows = new List<Row>();
            route.VisitRoadPieces((segment, start, end) =>
            {
                if (end <= gap.Start + .001f || start >= gap.End - .001f) return;
                MeshFilter road = segment.transform.Find("Road").GetComponent<MeshFilter>();
                Vector3[] vertices = road.sharedMesh.vertices;
                int count = vertices.Length / 2;
                for (int row = 0; row < count; row++)
                {
                    float at = Mathf.Lerp(start, end, row / (float)(count - 1));
                    if (at < gap.Start - .001f || at > gap.End + .001f
                        || (rows.Count > 0 && at <= rows[rows.Count - 1].At + .001f)) continue;
                    RoadPathPoint point = segment.Sample(at - start);
                    rows.Add(new Row
                    {
                        At = at,
                        Left = road.transform.TransformPoint(vertices[row * 2]),
                        Right = road.transform.TransformPoint(vertices[row * 2 + 1]),
                        Up = point.Up,
                        Forward = point.Forward
                    });
                }
            });
            return rows;
        }

        private void BuildShaft(List<Row> rows, Material wall, Material farWall, Material bottom)
        {
            var wallVertices = new List<Vector3>();
            var floorVertices = new List<Vector3>();
            Vector3 drop = Vector3.down * 18f;
            for (int i = 1; i < rows.Count; i++)
            {
                Row a = rows[i - 1], b = rows[i];
                Quad(wallVertices, a.Left, b.Left, b.Left + drop, a.Left + drop);
                Quad(wallVertices, b.Right, a.Right, a.Right + drop, b.Right + drop);
                Quad(floorVertices, a.Left + drop, b.Left + drop, b.Right + drop, a.Right + drop);
            }
            Row first = rows[0], last = rows[rows.Count - 1];
            Quad(wallVertices, first.Right, first.Left, first.Left + drop, first.Right + drop);
            MeshObject("Curved pit side and entrance faces", wallVertices, wall);
            MeshObject("Pit floor 18 metres below", floorVertices, bottom);
            var farVertices = new List<Vector3>();
            Quad(farVertices, last.Left, last.Right, last.Right + drop, last.Left + drop);
            MeshObject("Visible far wall of missing bridge", farVertices, farWall);
        }

        private void BuildLip(WorldShiftRoute route, float edge, bool entrance, Material yellow, Material dark)
        {
            RoadPathPoint p = route.Sample(edge + (entrance ? -.75f : .75f));
            Quaternion rotation = Quaternion.LookRotation(p.Forward, p.Up);
            // Low paint-sized pieces sit on the remaining deck; none spans the opening.
            for (int i = 0; i < 12; i++)
            {
                float width = p.Width / 12f;
                float x = -p.Width * .5f + width * (i + .5f);
                float brokenEdge = (i % 3 - 1) * .035f;
                Box("Broken deck hazard lip", p.Position + p.Right * x + p.Up * .055f + p.Forward * brokenEdge,
                    rotation, new Vector3(width - .025f, .1f, 1.3f), i % 2 == 0 ? yellow : dark);
            }
            if (!entrance) return;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = p.Position + p.Right * side * (p.Width * .5f + .12f);
                Box("Cliff warning post", at + p.Up * .55f, rotation, new Vector3(.15f, 1.1f, .15f), dark);
                Box("White cliff reflector", at + p.Up * 1.08f, rotation, new Vector3(.35f, .2f, .22f), yellow);
            }
        }

        private void BuildDepthMarks(WorldShiftRoute route, ShiftHazard gap, Material material)
        {
            RoadPathPoint far = route.Sample(gap.End);
            Quaternion farRotation = Quaternion.LookRotation(far.Forward, far.Up);
            // These short rungs lie on the far vertical face. Their descending
            // perspective distinguishes an open drop from a dark patch of asphalt.
            foreach (float depth in new[] { .7f, 2.1f, 4.5f, 8f, 13f })
                foreach (float x in new[] { -3.25f, 0f, 3.25f })
                    Box("Pit far-wall depth tick", far.Position + far.Right * x - Vector3.up * depth - far.Forward * .07f,
                        farRotation, new Vector3(.9f, .12f, .055f), material);

            for (float at = gap.Start + 1f; at < gap.End; at += 4f)
            {
                RoadPathPoint p = route.Sample(at);
                Quaternion rotation = Quaternion.LookRotation(p.Forward, p.Up);
                foreach (float side in new[] { -1f, 1f })
                    Box("Pit side-edge amber guide", p.Position + p.Right * side * (p.Width * .5f + .055f) + p.Up * .04f,
                        rotation, new Vector3(.12f, .07f, 1.25f), material);
            }
            foreach (float at in new[] { gap.Start + 6f, (gap.Start + gap.End) * .5f, gap.End - 6f })
            {
                RoadPathPoint p = route.Sample(at);
                Quaternion rotation = Quaternion.LookRotation(p.Forward, p.Up);
                foreach (float side in new[] { -1f, 1f })
                    foreach (float depth in new[] { 1.2f, 3.5f, 7f })
                        Box("Pit inner-side depth tick", p.Position + p.Right * side * (p.Width * .5f - .035f) - Vector3.up * depth,
                            rotation, new Vector3(.055f, .5f, .22f), material);
            }
        }

        private void BuildApproachCrosses(WorldShiftRoute route, float edge, Material material)
        {
            RoadPathPoint p = route.Sample(edge - 11f);
            Quaternion rotation = Quaternion.LookRotation(p.Forward, p.Up);
            for (int lane = -1; lane <= 1; lane++)
                foreach (float angle in new[] { -43f, 43f })
                    Box("Missing-road X marking", p.Position + p.Right * lane * p.Width / 3f + p.Up * .035f,
                        rotation * Quaternion.Euler(0f, angle, 0f), new Vector3(1.8f, .045f, .22f), material);
        }

        private void MeshObject(string name, List<Vector3> worldVertices, Material material)
        {
            var vertices = new List<Vector3>(worldVertices.Count * 2);
            var normals = new List<Vector3>(worldVertices.Count * 2);
            var triangles = new List<int>();
            for (int q = 0; q < worldVertices.Count; q += 4)
            {
                Vector3 a = worldVertices[q], b = worldVertices[q + 1], c = worldVertices[q + 2], d = worldVertices[q + 3];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Face(a, b, c, d, normal);
                Face(d, c, b, a, -normal);
            }
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            root.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int first = vertices.Count;
                foreach (Vector3 v in new[] { a, b, c, d })
                {
                    vertices.Add(transform.InverseTransformPoint(v));
                    normals.Add(transform.InverseTransformDirection(normal));
                }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
        }

        private static void Quad(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        { vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d); }

        private void Box(string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(transform, false);
            box.transform.SetPositionAndRotation(position, rotation);
            box.transform.localScale = scale;
            Collider collider = box.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Renderer renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Material Glow(Color colour, float strength)
        {
            var material = new Material(RuntimeMaterials.Coloured(colour));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * strength);
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) Release(mesh);
            foreach (Material material in materials) Release(material);
            foreach (Texture2D texture in textures) Release(texture);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
