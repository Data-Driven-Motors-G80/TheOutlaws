using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Continuous city ground attached to the actual curved road edges.</summary>
    public sealed class WorldShiftRoadside : MonoBehaviour
    {
        private const float SidewalkWidth = 3.5f;
        private const float GroundWidth = 55f;
        private sealed class Piece
        {
            public float Start, End;
            public GameObject Root;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public MeshRenderer EdgeRenderer;
            public Mesh EdgeMesh;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private Material[] day, echo;
        private Material dayEdge, echoEdge;
        private WorldShiftRoute route;

        public void Build(WorldShiftRoute route)
        {
            this.route = route;
            day = new[]
            {
                RuntimeMaterials.Coloured(new Color(.52f, .53f, .51f)),
                RuntimeMaterials.Coloured(new Color(.35f, .38f, .37f))
            };
            echo = new[]
            {
                RuntimeMaterials.Coloured(new Color(.48f, .47f, .43f)),
                RuntimeMaterials.Coloured(new Color(.065f, .075f, .085f))
            };
            dayEdge = RuntimeMaterials.Coloured(new Color(.83f, .82f, .73f));
            echoEdge = new Material(RuntimeMaterials.Coloured(new Color(.94f, .88f, .64f)));
            echoEdge.name = "Night cream road-edge paint";
            echoEdge.EnableKeyword("_EMISSION");
            echoEdge.SetColor("_EmissionColor", new Color(.94f, .88f, .64f) * .045f);
            echoEdge.SetFloat("_Smoothness", .55f);
            route.VisitRoadPieces(BuildPiece);
            SetWorld(ShiftWorld.Day);
            UpdateVisibility(0f);
        }

        private void BuildPiece(RoadSegment segment, float start, float end)
        {
            MeshFilter road = segment.transform.Find("Road").GetComponent<MeshFilter>();
            // RoadCurveMesh emits left/right pairs at every path sample. Reuse those
            // exact vertices, including gap boundaries, instead of approximating
            // each bend with one long rectangular sidewalk.
            Vector3[] edges = road.sharedMesh.vertices;
            int rows = edges.Length / 2;
            var vertices = new List<Vector3>(rows * 6);
            var normals = new List<Vector3>(rows * 6);
            var sidewalks = new List<int>((rows - 1) * 12);
            var background = new List<int>((rows - 1) * 12);
            var edgeVertices = new List<Vector3>(rows * 4);
            var edgeNormals = new List<Vector3>(rows * 4);
            var edgeTriangles = new List<int>((rows - 1) * 12);
            for (int row = 0; row < rows; row++)
            {
                Vector3 left = road.transform.TransformPoint(edges[row * 2]);
                Vector3 right = road.transform.TransformPoint(edges[row * 2 + 1]);
                Vector3 across = (right - left).normalized;
                Vector3 up = segment.Sample(segment.Length * row / (rows - 1)).Up;
                Add(left - across * GroundWidth, up);
                Add(left - across * SidewalkWidth, up);
                Add(left, up);
                Add(right, up);
                Add(right + across * SidewalkWidth, up);
                Add(right + across * GroundWidth, up);
                // Paint stays just inside the actual carriageway edge, following
                // exactly the same curved/sloped samples as the rendered road.
                Edge(left + across * .10f + up * .035f, up);
                Edge(left + across * .32f + up * .035f, up);
                Edge(right - across * .32f + up * .035f, up);
                Edge(right - across * .10f + up * .035f, up);
                if (row == 0) continue;
                int a = (row - 1) * 6, b = row * 6;
                Quad(background, a, b, b + 1, a + 1);
                Quad(sidewalks, a + 1, b + 1, b + 2, a + 2);
                // Never span the carriageway: actual road gaps must remain holes.
                Quad(sidewalks, a + 3, b + 3, b + 4, a + 4);
                Quad(background, a + 4, b + 4, b + 5, a + 5);
                int edgeA = (row - 1) * 4, edgeB = row * 4;
                Quad(edgeTriangles, edgeA, edgeB, edgeB + 1, edgeA + 1);
                Quad(edgeTriangles, edgeA + 2, edgeB + 2, edgeB + 3, edgeA + 3);
            }

            var mesh = new Mesh { name = "Continuous city roadside " + start.ToString("0") };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sidewalks, 0);
            mesh.SetTriangles(background, 1);
            mesh.RecalculateBounds();
            var root = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var edgeMesh = new Mesh { name = "Road boundary stripes " + start.ToString("0") };
            edgeMesh.SetVertices(edgeVertices);
            edgeMesh.SetNormals(edgeNormals);
            edgeMesh.SetTriangles(edgeTriangles, 0);
            edgeMesh.RecalculateBounds();
            var edgeRoot = new GameObject("Road boundary stripes", typeof(MeshFilter), typeof(MeshRenderer));
            edgeRoot.transform.SetParent(root.transform, false);
            edgeRoot.GetComponent<MeshFilter>().sharedMesh = edgeMesh;
            MeshRenderer edgeRenderer = edgeRoot.GetComponent<MeshRenderer>();
            edgeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pieces.Add(new Piece { Start = start, End = end, Root = root, Renderer = renderer, Mesh = mesh,
                EdgeRenderer = edgeRenderer, EdgeMesh = edgeMesh });

            void Add(Vector3 position, Vector3 normal)
            {
                vertices.Add(transform.InverseTransformPoint(position));
                normals.Add(transform.InverseTransformDirection(normal));
            }

            void Edge(Vector3 position, Vector3 normal)
            {
                edgeVertices.Add(transform.InverseTransformPoint(position));
                edgeNormals.Add(transform.InverseTransformDirection(normal));
            }
        }

        public void SetWorld(ShiftWorld world)
        {
            Material[] materials = world == ShiftWorld.Day ? day : echo;
            foreach (var piece in pieces)
            {
                piece.Renderer.sharedMaterials = materials;
                piece.EdgeRenderer.sharedMaterial = world == ShiftWorld.Day ? dayEdge : echoEdge;
                // Gap boundaries are exact road-piece cuts. Never leave painted
                // edge lines floating over a bridge that does not exist here.
                piece.EdgeRenderer.enabled = route.HasSurface((piece.Start + piece.End) * .5f, world);
            }
        }

        public void UpdateVisibility(float distance)
        {
            foreach (var piece in pieces)
            {
                bool visible = piece.End > distance - 110f && piece.Start < distance + 300f;
                if (piece.Root.activeSelf != visible) piece.Root.SetActive(visible);
            }
        }

        private static void Quad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private void OnDestroy()
        {
            foreach (var piece in pieces)
            {
                Release(piece.Mesh);
                Release(piece.EdgeMesh);
            }
            Release(echoEdge);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
