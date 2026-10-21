using System.Collections.Generic;
using UnityEngine;

// Rebuilds the existing template's surface and walls, retaining its materials.
// Meshes belong to each pooled instance, never to the shared prefab.
[DisallowMultipleComponent]
public sealed class RoadCurveMesh : MonoBehaviour
{
    private readonly Dictionary<MeshFilter, Mesh> meshes = new Dictionary<MeshFilter, Mesh>();

    // Reuse scratch buffers when pooled road sections are rebuilt.
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();

    public void Rebuild(RoadSegment segment)
    {
        Build(segment, transform.Find("Road"), 0f, 0f, 0f);
        Build(segment, transform.Find("Left Roadside Wall"), -segment.Width * 0.5f - 0.3f, 0.6f, 1.5f);
        Build(segment, transform.Find("Right Roadside Wall"), segment.Width * 0.5f + 0.3f, 0.6f, 1.5f);
    }

    private void Build(RoadSegment segment, Transform target, float offset, float wallWidth, float wallHeight)
    {
        if (target == null || !target.TryGetComponent(out MeshFilter filter)) return;
        if (!meshes.TryGetValue(filter, out Mesh mesh))
        {
            mesh = new Mesh { name = "Curved " + target.name };
            meshes.Add(filter, mesh);
        }
        mesh.Clear();
        bool wall = wallWidth > 0f;
        int stride = wall ? 4 : 2;
        int steps = Mathf.CeilToInt(segment.Length);
        vertices.Clear();
        uvs.Clear();
        triangles.Clear();
        for (int i = 0; i <= steps; i++)
        {
            float distance = segment.Length * i / steps;
            RoadPathPoint point = segment.Sample(distance);
            for (int side = 0; side < stride; side++)
            {
                float lateral = wall ? offset + (side < 2 ? -wallWidth : wallWidth) * 0.5f
                    : (side == 0 ? -segment.Width : segment.Width) * 0.5f;
                float height = wall && (side == 1 || side == 2) ? wallHeight : 0f;
                vertices.Add(target.InverseTransformPoint(point.Position + point.Right * lateral + point.Up * height));
                uvs.Add(new Vector2((float)side / (stride - 1), distance / 10f));
            }
            if (i == 0) continue;
            int a = (i - 1) * stride;
            int b = i * stride;
            for (int side = 0; side < (wall ? 4 : 1); side++)
            {
                int next = (side + 1) % stride;
                AddQuad(triangles, a + side, b + side, b + next, a + next);
            }
        }
        if (wall)
        {
            AddQuad(triangles, 0, 1, 2, 3);
            int end = steps * stride;
            AddQuad(triangles, end + 3, end + 2, end + 1, end);
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        if (target.TryGetComponent(out BoxCollider box)) box.enabled = false;
        if (!target.TryGetComponent(out MeshCollider collider)) collider = target.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in meshes.Values)
        {
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
