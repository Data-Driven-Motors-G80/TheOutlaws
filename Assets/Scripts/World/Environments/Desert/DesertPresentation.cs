using UnityEngine;
using System.Collections.Generic;

/// <summary>Primitive sand surround. Sunset lighting and road material are saved in the scene.</summary>
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
        float height = environment.Road.TryGetPathPoint(position, out RoadPathPoint point) ? point.Position.y - 0.12f : 0f;
        sand.position = new Vector3(position.x, height, position.z);
        environment.Road.GetComponentsInChildren(false, segments);
        foreach (var segment in segments)
        {
            // Scene-local instances only: keep shared prefabs and their boundary colliders intact.
            foreach (string wallName in WallNames)
            {
                var wall = segment.transform.Find(wallName);
                if (wall != null && wall.TryGetComponent(out Renderer wallRenderer)) wallRenderer.enabled = false;
            }
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
                dune.position = middle.Position + middle.Right * side * (middle.Width * 0.5f + 25f) - middle.Up * 1.5f;
                dune.rotation = Quaternion.LookRotation(middle.Forward, middle.Up);
                dune.localScale = new Vector3(24f, side < 0 ? 9f : 6f, Mathf.Min(40f, segment.Length * 1.5f));
            }
        }
    }
    private void OnDestroy() { if (sand != null) Destroy(sand.gameObject); }
}
