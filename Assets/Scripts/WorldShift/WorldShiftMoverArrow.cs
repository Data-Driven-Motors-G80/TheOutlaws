using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// The one sideways arrow for anything that moves sideways (sliders, sliding blocks, crossing
    /// rocks, tumbleweeds, drums, vultures, trolleys) in every level: a filled white single-headed
    /// arrow with a dark outline that always points the way the mover is going (or will go when Day
    /// resumes) and flips when it turns around. WorldShiftPresentation aims it every frame.
    /// See Docs/OBSTACLE_VISUAL_RULES.md.
    /// </summary>
    internal static class WorldShiftMoverArrow
    {
        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();
        private static Material white, outline;

        /// <summary>Adds an arrow whose shaft runs along local X, pointing +X (road right) until aimed.
        /// Returns the container to destroy with its owner.</summary>
        public static Transform Attach(Transform parent, float half, Vector3 position, Quaternion rotation, Vector3 outward)
        {
            EnsureMaterials();
            Mesh mesh = MeshFor(half);
            var root = new GameObject("Sideways mover arrow").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = rotation;
            root.gameObject.AddComponent<WorldShiftMoverArrowHeading>();
            Vector3 lift = Quaternion.Inverse(rotation) * outward.normalized;
            for (int layer = 0; layer < 2; layer++)
            {
                var obj = new GameObject(layer == 0 ? "Direction arrow outline" : "White triangular direction arrow",
                    typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root, false);
                obj.transform.localPosition = lift * (layer * .012f);
                obj.transform.localScale = layer == 0 ? new Vector3(1.045f, 1.22f, 1f) : Vector3.one;
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = layer == 0 ? outline : white;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        /// <summary>Front and back approach-face arrows, the standard placement for ground movers.</summary>
        public static void OnBothFaces(Transform parent, float half, float y, float depth)
        {
            foreach (float face in new[] { -1f, 1f })
                Attach(parent, half, new Vector3(0f, y, face * (depth * .5f + .04f)), Quaternion.identity, Vector3.forward * face);
        }

        private static Mesh MeshFor(float half)
        {
            half = Mathf.Max(.6f, half);
            int key = Mathf.RoundToInt(half * 100f);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            // One head at +X and a thicker tail shaft, so the direction reads at a glance.
            float neck = half - .5f;
            var vertices = new[] {
                new Vector3(-half,-.08f,0),new Vector3(-half,.08f,0),
                new Vector3(neck,.08f,0),new Vector3(neck,-.08f,0),
                new Vector3(half,0,0),new Vector3(neck,-.21f,0),new Vector3(neck,.21f,0)
            };
            int[] front = { 0,1,2,0,2,3,4,5,6 };
            var triangles = new List<int>(front);
            for (int i = 0; i < front.Length; i += 3) { triangles.Add(front[i+2]); triangles.Add(front[i+1]); triangles.Add(front[i]); }
            var mesh = new Mesh { name = "Filled direction of travel arrow" };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Meshes[key] = mesh;
            return mesh;
        }

        private static void EnsureMaterials()
        {
            if (white != null && outline != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            white = Unlit(shader, "Always readable white direction arrows", Color.white);
            outline = Unlit(shader, "Direction arrow dark outline", new Color(.035f, .045f, .055f));
        }

        private static Material Unlit(Shader shader, string name, Color colour)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            return material;
        }
    }

    /// <summary>Points a mover arrow along its mover's current sideways heading (+1 road right, -1 road left).</summary>
    internal sealed class WorldShiftMoverArrowHeading : MonoBehaviour
    {
        public float Heading { get; private set; } = 1f;

        public void Point(float heading)
        {
            heading = heading < 0f ? -1f : 1f;
            if (heading == Heading) return;
            Heading = heading;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * heading;
            transform.localScale = scale;
        }
    }
}
