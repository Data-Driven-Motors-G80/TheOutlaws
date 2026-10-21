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

    private Mesh ownedMesh;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();

    private void Reset()
    {
        GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        if (ownedMesh == null) return;
        if (Application.isPlaying) Destroy(ownedMesh);
        else DestroyImmediate(ownedMesh);
    }

    [ContextMenu("Rebuild Markings")]
    public void Rebuild()
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
        int stripCount = Mathf.Max(0, Mathf.FloorToInt((road.Length - stripLength) / spacing + 0.0001f) + 1);

        for (int i = 0; i < stripCount; i++)
        {
            RoadPathPoint point = road.Sample(i * spacing + stripLength * 0.5f);

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

    private Mesh GetOrCreateMesh(MeshFilter filter)
    {
        if (ownedMesh == null)
            ownedMesh = new Mesh { name = MeshName, hideFlags = HideFlags.HideAndDontSave };
        filter.sharedMesh = ownedMesh;
        return ownedMesh;
    }
}
