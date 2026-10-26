using System.Collections.Generic;
using UnityEngine;

/// <summary>Visual-only sand banks belonging to each pooled road instance.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RoadSegment))]
public sealed class DesertRoadsideSand : MonoBehaviour
{
    private const float BankWidth = 6f;
    private const float ShoulderWidth = 2f;
    private const float SurfaceOffset = 0.08f;
    private RoadSegment segment;
    private Mesh mesh;
    private MeshRenderer sandRenderer;
    private float previousLength, previousFloor;
    private Vector3 previousStart, previousMiddle, previousEnd, previousForward;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();

    // Refresh on streaming/reuse, rather than allocating and rebuilding every frame.
    public bool Refresh(Material material, float floor)
    {
        if (segment == null) segment = GetComponent<RoadSegment>();
        if (sandRenderer != null) sandRenderer.sharedMaterial = material;
        RoadPathPoint first = segment.Sample(0f);
        RoadPathPoint middle = segment.Sample(segment.Length * 0.5f);
        RoadPathPoint last = segment.Sample(segment.Length);
        if (mesh != null && previousLength == segment.Length && previousFloor == floor &&
            previousStart == first.Position && previousMiddle == middle.Position &&
            previousEnd == last.Position && previousForward == middle.Forward)
            return false;

        if (mesh == null)
        {
            var surface = new GameObject("Desert roadside sand");
            surface.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Desert slope-following sand" };
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            sandRenderer = surface.AddComponent<MeshRenderer>();
            sandRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sandRenderer.sharedMaterial = material;
        }
        previousLength = segment.Length;
        previousFloor = floor;
        previousStart = first.Position;
        previousMiddle = middle.Position;
        previousEnd = last.Position;
        previousForward = middle.Forward;

        vertices.Clear();
        triangles.Clear();
        int steps = Mathf.Max(1, Mathf.CeilToInt(segment.Length));
        for (int i = 0; i <= steps; i++)
        {
            RoadPathPoint point = segment.Sample(segment.Length * i / steps);
            Vector3 left = point.Position - point.Right * (point.Width * 0.5f + BankWidth);
            Vector3 right = point.Position + point.Right * (point.Width * 0.5f + BankWidth);
            // Each bank connects to the backdrop independently. Nothing spans the road.
            Add(new Vector3(left.x, floor, left.z));
            Add(point.Position - point.Right * (point.Width * 0.5f + ShoulderWidth) - Vector3.up * SurfaceOffset);
            Add(point.Position - point.Right * point.Width * 0.5f - Vector3.up * SurfaceOffset);
            Add(point.Position + point.Right * point.Width * 0.5f - Vector3.up * SurfaceOffset);
            Add(point.Position + point.Right * (point.Width * 0.5f + ShoulderWidth) - Vector3.up * SurfaceOffset);
            Add(new Vector3(right.x, floor, right.z));
            if (i == 0) continue;
            int from = (i - 1) * 6, to = i * 6;
            for (int side = 0; side < 5; side++)
            {
                if (side == 2) continue;
                Quad(from + side, to + side, to + side + 1, from + side + 1);
            }
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return true;
    }

    private void Add(Vector3 world) => vertices.Add(transform.InverseTransformPoint(world));
    private void Quad(int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }
}
