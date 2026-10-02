using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RoadSegment), typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class RoadMarking : MonoBehaviour
{
    private const int LaneCount = 3;
    private const string MeshName = "RoadMarkingMesh";

    [Header("Strip Settings")]
    [SerializeField, Min(0.01f)] private float stripWidth = 0.15f;
    [SerializeField, Min(0.1f)] private float stripLength = 2f;
    [SerializeField, Min(0f)] private float gapLength = 2f;
    [SerializeField, Min(0f)] private float height = 0.01f;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();

    private void Reset()
    {
        GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void OnEnable()
    {
        if (GetComponent<MeshFilter>().sharedMesh == null)
        {
            Rebuild();
        }
    }

    private void OnValidate()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        MeshFilter filter = GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != MeshName)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(filter.sharedMesh);
        }
        else
        {
            DestroyImmediate(filter.sharedMesh);
        }
    }

    [ContextMenu("Rebuild Markings")]
    private void Rebuild()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        RoadSegment road = GetComponent<RoadSegment>();
        Mesh mesh = GetOrCreateMesh(GetComponent<MeshFilter>());
        mesh.Clear();

        if (!road.IsConfigured)
        {
            return;
        }

        vertices.Clear();
        uvs.Clear();
        triangles.Clear();

        float spacing = stripLength + gapLength;
        float dividerOffset = road.Width / LaneCount * 0.5f;
        int stripCount = Mathf.Max(0, Mathf.FloorToInt((GetPathLength(road) - stripLength) / spacing + 0.0001f) + 1);

        for (int i = 0; i < stripCount; i++)
        {
            RoadPathPoint point = SamplePath(road, i * spacing + stripLength * 0.5f);

            AddStrip(point, -dividerOffset);
            AddStrip(point, dividerOffset);
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddStrip(RoadPathPoint point, float lateralOffset)
    {
        Vector3 center = point.Position + point.Right * lateralOffset + point.Up * height;
        Vector3 halfWidth = point.Right * (stripWidth * 0.5f);
        Vector3 halfLength = point.Forward * (stripLength * 0.5f);
        int first = vertices.Count;

        vertices.Add(transform.InverseTransformPoint(center - halfWidth - halfLength));
        vertices.Add(transform.InverseTransformPoint(center + halfWidth - halfLength));
        vertices.Add(transform.InverseTransformPoint(center + halfWidth + halfLength));
        vertices.Add(transform.InverseTransformPoint(center - halfWidth + halfLength));

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        triangles.Add(first);
        triangles.Add(first + 3);
        triangles.Add(first + 2);
        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 1);
    }

    private static Mesh GetOrCreateMesh(MeshFilter filter)
    {
        Mesh mesh = filter.sharedMesh;

        if (mesh == null || mesh.name != MeshName)
        {
            mesh = new Mesh { name = MeshName, hideFlags = HideFlags.HideAndDontSave };
            filter.sharedMesh = mesh;
        }

        return mesh;
    }

    private static float GetPathLength(RoadSegment road)
    {
        float length = 0f;
        Vector3 from = road.GetPathPoint(0);

        for (int i = 1; i < road.PathPointCount; i++)
        {
            Vector3 to = road.GetPathPoint(i);
            length += Vector3.Distance(from, to);
            from = to;
        }

        return length;
    }

    private static RoadPathPoint SamplePath(RoadSegment road, float distance)
    {
        Vector3 from = road.GetPathPoint(0);
        Vector3 direction = Vector3.forward;
        float remaining = distance;

        for (int i = 1; i < road.PathPointCount; i++)
        {
            Vector3 to = road.GetPathPoint(i);
            Vector3 delta = to - from;
            float length = delta.magnitude;

            if (length > Mathf.Epsilon)
            {
                direction = delta / length;

                if (remaining <= length)
                {
                    return new RoadPathPoint(from + direction * remaining, direction, road.Width);
                }

                remaining -= length;
            }

            from = to;
        }

        return new RoadPathPoint(from, direction, road.Width);
    }
}