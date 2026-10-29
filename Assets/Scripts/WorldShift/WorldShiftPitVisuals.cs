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
        private readonly List<Material> materials = new List<Material>();

        public static WorldShiftPitVisuals Build(Transform parent, WorldShiftRoute route, ShiftHazard gap, bool warnings = true)
        {
            var root = new GameObject("Open pit - fitted edges and depth cues");
            root.transform.SetParent(parent, false);
            var pit = root.AddComponent<WorldShiftPitVisuals>();
            List<Row> rows = CollectEdges(route, gap);
            if (rows.Count < 2) return pit;
            bool night = gap.World == ShiftWorld.Echo && warnings;
            Material wall = pit.Glow(new Color(.13f, .16f, .2f), night ? .12f : .018f);
            Material farWall = pit.Glow(new Color(.17f, .22f, .27f), night ? .25f : .045f);
            Material bottom = RuntimeMaterials.Coloured(new Color(.012f, .016f, .022f));
            Material yellow = pit.Glow(new Color(1f, .7f, .16f), night ? .65f : .2f);
            Material dark = RuntimeMaterials.Coloured(new Color(.012f, .016f, .02f));
            Material depthMark = pit.Glow(new Color(1f, .62f, .19f), night ? .6f : .11f);

            pit.BuildShaft(rows, wall, farWall, bottom);
            if (!warnings) return pit;
            pit.BuildLip(route, gap.Start, true, yellow, dark);
            pit.BuildLip(route, gap.End, false, yellow, dark);
            pit.BuildDepthMarks(route, gap, depthMark);
            pit.BuildApproachCrosses(route, gap.Start, yellow);
            if (night) pit.BuildNightRim(route, gap, yellow);
            return pit;
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
                    rows.Add(new Row { At = at,
                        Left = road.transform.TransformPoint(vertices[row * 2]),
                        Right = road.transform.TransformPoint(vertices[row * 2 + 1]),
                        Up = point.Up, Forward = point.Forward });
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
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
