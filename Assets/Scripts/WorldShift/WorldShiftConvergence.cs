using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Fixed half-world geometry along the shared path, never a screen-space split.</summary>
    public sealed class WorldShiftConvergence : MonoBehaviour
    {
        private sealed class Chunk { public GameObject Root; public float Start, End; }
        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<(Color, ShiftWorld), Material> materials = new Dictionary<(Color, ShiftWorld), Material>();
        private WorldShiftRoute route;
        private WorldShiftCity game;
        private Shader surfaceShader;
        private Material splitSky;

        public void BuildWallsOnly(WorldShiftRoute sharedRoute) => route = sharedRoute;

        public void Build(WorldShiftCity controller, WorldShiftRoute sharedRoute, GameObject lampPrefab)
        {
            game = controller;
            route = sharedRoute;
            surfaceShader = Resources.Load<Shader>("WorldShiftConvergenceSurface");
            splitSky = new Material(Resources.Load<Shader>("WorldShiftConvergenceSky"));
            splitSky.name = "Day and Night share the same sky";
            route.VisitRoadPieces((segment, start, end) =>
            {
                if (!WorldShiftLevel.IsMerged((start + end) * .5f)) return;
                BuildRoad(segment, start, end);
            });
            for (int zone = 0; zone < WorldShiftLevel.MergeBounds.Length; zone += 2)
            {
                float start = WorldShiftLevel.MergeBounds[zone], end = WorldShiftLevel.MergeBounds[zone + 1];
                BuildGate(start, true);
                BuildGate(end, false);
                for (float at = start; at < end; at += 40f)
                    BuildBlock(at, Mathf.Min(at + 40f, end), lampPrefab);
            }
        }

        public Material Surface(Color colour, ShiftWorld world)
        {
            if (!materials.TryGetValue((colour, world), out Material material))
            {
                material = new Material(surfaceShader) { name = world + " overlap prototype surface" };
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Night", world == ShiftWorld.Echo ? 1f : 0f);
                materials.Add((colour, world), material);
            }
            return material;
        }

        private Transform Root(string name, float start, float end)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            chunks.Add(new Chunk { Root = root.gameObject, Start = start, End = end });
            return root;
        }

        private void BuildRoad(RoadSegment segment, float start, float end)
        {
            Transform root = Root("Joined road " + start, start, end);
            MeshFilter road = segment.transform.Find("Road").GetComponent<MeshFilter>();
            Vector3[] vertices = road.sharedMesh.vertices;
            var left = new Vector3[vertices.Length / 2];
            var right = new Vector3[left.Length];
            var up = new Vector3[left.Length];
            for (int i = 0; i < left.Length; i++)
            {
                left[i] = road.transform.TransformPoint(vertices[i * 2]);
                right[i] = road.transform.TransformPoint(vertices[i * 2 + 1]);
                up[i] = segment.Sample(segment.Length * i / (left.Length - 1)).Up;
            }
            foreach (int side in new[] { -1, 1 })
            {
                ShiftWorld world = side < 0 ? ShiftWorld.Day : ShiftWorld.Echo;
                float min = side < 0 ? -5f : 0f, max = side < 0 ? 0f : 5f;
                Strip("Half deck " + world, min, max, 0f, Surface(side < 0 ? new Color(.27f, .28f, .29f) : new Color(.16f, .21f, .29f), world));
                Strip("Half pavement " + world, side < 0 ? -8.5f : 5f, side < 0 ? -5f : 8.5f, 0f,
                    Surface(new Color(.55f, .55f, .51f), world));
                Strip("Half background " + world, side < 0 ? -60f : 8.5f, side < 0 ? -8.5f : 60f, -.005f,
                    Surface(side < 0 ? new Color(.4f, .42f, .38f) : new Color(.16f, .21f, .29f), world));
                float edge = side * 4.8f;
                Strip("Outer boundary " + world, edge - .1f, edge + .1f, .035f,
                    Surface(new Color(.95f, .9f, .7f), world));
                Strip("Passable world seam " + world, side < 0 ? -.07f : 0f, side < 0 ? 0f : .07f, .035f,
                    Surface(side < 0 ? new Color(1f, .75f, .35f) : new Color(.3f, .85f, 1f), world));
            }

            void Strip(string name, float min, float max, float lift, Material material)
            {
                var points = new List<Vector3>();
                var normals = new List<Vector3>();
                var triangles = new List<int>();
                for (int i = 0; i < left.Length; i++)
                {
                    Vector3 centre = (left[i] + right[i]) * .5f;
                    Vector3 across = (right[i] - left[i]).normalized;
                    points.Add(root.InverseTransformPoint(centre + across * min + up[i] * lift));
                    points.Add(root.InverseTransformPoint(centre + across * max + up[i] * lift));
                    normals.Add(root.InverseTransformDirection(up[i]));
                    normals.Add(root.InverseTransformDirection(up[i]));
                    if (i == 0) continue;
                    int a = (i - 1) * 2, b = i * 2;
                    triangles.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
                MakeMesh(root, name, points, normals, triangles, material);
            }
        }

        // Exact transverse bounds and one-metre path samples agree with the swept
        // collision interval. No invisible second lane protrudes across the seam.
        public void BuildWall(Transform parent, ShiftHazard wall, Material body, Material band)
        {
            float min = wall.HalfSide < 0 ? -5f : 0f, max = wall.HalfSide < 0 ? 0f : 5f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            for (float at = wall.Start; at < wall.End; at += 1f)
            {
                RoadPathPoint a = route.Sample(at), b = route.Sample(Mathf.Min(wall.End, at + 1f));
                Vector3 al = a.Position + a.Right * min, ar = a.Position + a.Right * max;
                Vector3 bl = b.Position + b.Right * min, br = b.Position + b.Right * max;
                Vector3 alt = al + a.Up * 1.5f, art = ar + a.Up * 1.5f;
                Vector3 blt = bl + b.Up * 1.5f, brt = br + b.Up * 1.5f;
                Quad(alt, blt, brt, art);
                Quad(al, bl, blt, alt);
                Quad(ar, art, brt, br);
                if (at == wall.Start) Quad(al, alt, art, ar);
                if (at + 1f >= wall.End) Quad(bl, br, brt, blt);
            }
            MakeMesh(parent, "Wall-to-centre half roadblock", v, n, t, body);
            RoadPathPoint front = route.Sample(wall.Start);
            Transform stripe = Box(parent, "Half roadblock contrast band", new Vector3(4.9f, .22f, .035f), band);
            stripe.SetPositionAndRotation(front.Position + front.Right * wall.HalfSide * 2.5f + front.Up * 1.12f - front.Forward * .025f,
                Quaternion.LookRotation(front.Forward, front.Up));

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                foreach (Vector3 p in new[] { a, b, c, d }) { v.Add(parent.InverseTransformPoint(p)); n.Add(parent.InverseTransformDirection(normal)); }
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        private void BuildBlock(float start, float end, GameObject lampPrefab)
        {
            Transform root = Root("Joined city block " + start, start, end);
            RoadPathPoint p = route.Sample((start + end) * .5f);
            root.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
            foreach (int side in new[] { -1, 1 })
            {
                ShiftWorld world = side < 0 ? ShiftWorld.Day : ShiftWorld.Echo;
                for (int j = 0; j < 2; j++)
                {
                    float height = 12f + ((Mathf.RoundToInt(start / 40f) + j * 3) % 4) * 3f;
                    Color colour = side < 0 ? (j == 0 ? new Color(.65f, .48f, .35f) : new Color(.42f, .58f, .57f)) : new Color(.28f, .4f, .58f);
                    Transform building = Box(root, world + " block building", new Vector3(8f, height, 14f), Surface(colour, world));
                    building.localPosition = new Vector3(side * (14f + j * 8f), height * .5f, j == 0 ? -9f : 10f);
                    Transform strip = Box(root, world + " simple facade stripe", new Vector3(.12f, .35f, 11f),
                        Surface(side < 0 ? new Color(.93f, .8f, .56f) : new Color(.35f, .68f, 1f), world));
                    strip.localPosition = new Vector3(side * (9.95f + j * 8f), height * .54f, j == 0 ? -9f : 10f);
                }
                if (side > 0 && lampPrefab != null)
                {
                    GameObject lamp = Instantiate(lampPrefab, root);
                    lamp.name = "Night half streetlight";
                    lamp.transform.localPosition = new Vector3(5.8f, 0f, 0f);
                    lamp.transform.localScale = Vector3.one * 1.35f;
                    foreach (Collider c in lamp.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    foreach (MonoBehaviour b in lamp.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                    foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = Surface(r.sharedMaterial != null ? r.sharedMaterial.color : Color.gray, world);
                    foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
                    { light.intensity = 48f; light.range = 9f; light.shadows = LightShadows.None; }
                }
            }
        }

        private void BuildGate(float at, bool entry)
        {
            Transform root = Root(entry ? "World overlap entrance" : "World overlap exit", at, at);
            RoadPathPoint p = route.Sample(at);
            root.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Forward, p.Up));
            foreach (int side in new[] { -1, 1 })
            {
                ShiftWorld world = side < 0 ? ShiftWorld.Day : ShiftWorld.Echo;
                Material colour = Surface(side < 0 ? new Color(1f, .73f, .3f) : new Color(.3f, .85f, 1f), world);
                Transform post = Box(root, "Overlap portal side", new Vector3(.15f, 5.8f, .15f), colour);
                post.localPosition = new Vector3(side * 5.5f, 2.9f, 0f);
                Transform beam = Box(root, "Overlap portal half", new Vector3(5.5f, .15f, .15f), colour);
                beam.localPosition = new Vector3(side * 2.75f, 5.8f, 0f);
                var text = new GameObject("Overlap direction").AddComponent<TextMeshPro>();
                text.transform.SetParent(root, false);
                text.transform.localPosition = new Vector3(side * 2.6f, 6.4f, 0f);
                text.text = entry ? (side < 0 ? "DAY   <" : ">   NIGHT") : (side < 0 ? "SPACE" : "RESTORED");
                text.fontSize = 4f;
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(5f, 1f);
                text.color = side < 0 ? new Color(1f, .8f, .5f) : new Color(.5f, .85f, 1f);
            }
        }

        public void SetMergedView(bool merged, Camera camera)
        {
            if (!merged) return; // Normal presentation restores its own sky and light.
            RenderSettings.skybox = splitSky;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        public void UpdateVisibility(float distance)
        {
            foreach (Chunk chunk in chunks)
                chunk.Root.SetActive(chunk.End > distance - 110f && chunk.Start < distance + 270f);
            RoadPathPoint p = route.Sample(distance + 70f);
            Shader.SetGlobalVector("_ShiftSeamRight", p.Right);
            Shader.SetGlobalVector("_ShiftSeamPoint", p.Position);
            if (game.Player != null)
            {
                Shader.SetGlobalVector("_ShiftLampPosition", game.Player.position + Vector3.up + game.Player.forward * 1.6f);
                Shader.SetGlobalVector("_ShiftLampForward", game.Player.forward);
            }
        }

        private static Transform Box(Transform parent, string name, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localScale = size;
            box.GetComponent<Collider>().enabled = false;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box.transform;
        }

        private void MakeMesh(Transform parent, string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void OnDestroy()
        {
            if (RenderSettings.skybox == splitSky) RenderSettings.skybox = null;
            foreach (Mesh mesh in meshes) Release(mesh);
            foreach (Material material in materials.Values) Release(material);
            Release(splitSky);
        }
        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
