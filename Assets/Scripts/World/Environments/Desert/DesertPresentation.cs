using UnityEngine;
using System.Collections.Generic;

/// <summary>Road-following sand banks and a backdrop below the entire streamed road.</summary>
[RequireComponent(typeof(EnvironmentRoot))]
public sealed class DesertPresentation : MonoBehaviour
{
    [SerializeField] private Material sandMaterial;
    private static readonly string[] WallNames = { "Left Roadside Wall", "Right Roadside Wall" };
    private static readonly int[] DuneSides = { -1, 1 };
    private static readonly string[] DuneNames = { "Prototype dune -1", "Prototype dune 1" };
    private EnvironmentRoot environment;
    private Transform sand;
    private readonly List<RoadSegment> segments = new List<RoadSegment>();
    private int previousSegmentCount;
    private void Start()
    {
        environment = GetComponent<EnvironmentRoot>();
        if (!environment.IsDesert || environment.Player == null) { enabled = false; return; }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Prototype sand surround";
        ground.transform.SetParent(transform, false);
        ground.transform.localScale = new Vector3(160f, 1f, 160f);
        var collider = ground.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        ground.GetComponent<Renderer>().sharedMaterial = sandMaterial;
        sand = ground.transform;
        LateUpdate();
    }
    private void LateUpdate()
    {
        if (sand == null) return;
        Vector3 position = environment.Player.transform.position;
        environment.Road.GetComponentsInChildren(false, segments);
        if (segments.Count == 0) return;

        // A player-height plane hides downhill road ahead and lower pursuit behind.
        // Keep the backdrop below ALL loaded road, including the retained rear segments.
        float lowest = float.PositiveInfinity;
        foreach (var segment in segments)
            for (int i = 0; i < segment.PathPointCount; i++)
                lowest = Mathf.Min(lowest, segment.GetPathPoint(i).y);
        float height = lowest - 2f;
        sand.position = new Vector3(position.x, height, position.z);
        bool layoutChanged = previousSegmentCount != segments.Count;
        previousSegmentCount = segments.Count;
        foreach (var segment in segments)
        {
            var banks = segment.GetComponent<DesertRoadsideSand>();
            if (banks == null) banks = segment.gameObject.AddComponent<DesertRoadsideSand>();
            layoutChanged |= banks.Refresh(sandMaterial, height);
            // Scene-local instances only: keep shared prefabs and their boundary colliders intact.
            foreach (string wallName in WallNames)
            {
                var wall = segment.transform.Find(wallName);
                if (wall != null && wall.TryGetComponent(out Renderer wallRenderer)) wallRenderer.enabled = false;
            }
        }
        if (!layoutChanged) return;
        foreach (var segment in segments)
        {
            RoadPathPoint middle = segment.Sample(segment.Length * 0.5f);
            for (int i = 0; i < DuneSides.Length; i++)
            {
                int side = DuneSides[i];
                string name = DuneNames[i];
                Transform dune = segment.transform.Find(name);
                if (dune == null)
                {
                    var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    shape.name = name;
                    shape.transform.SetParent(segment.transform, false);
                    var collider = shape.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    shape.GetComponent<Renderer>().sharedMaterial = sandMaterial;
                    dune = shape.transform;
                }
                Vector3 dunePosition = middle.Position + middle.Right * side * (middle.Width * 0.5f + 25f);
                dunePosition.y = height - 1.5f;
                dune.position = dunePosition;
                dune.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(middle.Forward, Vector3.up), Vector3.up);
                dune.localScale = new Vector3(24f, side < 0 ? 9f : 6f, Mathf.Min(40f, segment.Length * 1.5f));
                // A dune beside one arm of a hairpin must not cover its returning arm.
                var renderer = dune.GetComponent<Renderer>();
                renderer.enabled = !OverlapsRoad(renderer.bounds);
            }
        }
    }

    private bool OverlapsRoad(Bounds bounds)
    {
        foreach (var segment in segments)
        {
            // Path vertices are at most one metre apart; include a margin between samples.
            float margin = segment.Width * 0.5f + 1f;
            Vector3 min = bounds.min - new Vector3(margin, 0f, margin);
            Vector3 max = bounds.max + new Vector3(margin, 0f, margin);
            for (int i = 0; i < segment.PathPointCount; i++)
            {
                Vector3 point = segment.GetPathPoint(i);
                if (point.x >= min.x && point.x <= max.x && point.z >= min.z && point.z <= max.z)
                    return true;
            }
        }
        return false;
    }
    private void OnDestroy() { if (sand != null) Destroy(sand.gameObject); }
}
