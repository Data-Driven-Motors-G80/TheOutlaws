using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Non-colliding, geometric traffic signals and broken-road warnings.</summary>
    public sealed class WorldShiftRoadSigns : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();

        public static WorldShiftRoadSigns BuildCrossing(Transform parent)
        {
            var signs = Create(parent, "Overhead crossing traffic signals - decoration");
            Material metal = RuntimeMaterials.Coloured(new Color(.18f, .2f, .22f));
            Material housing = RuntimeMaterials.Coloured(new Color(.025f, .03f, .035f));
            Material red = signs.Glow(new Color(1f, .035f, .015f), 2.8f);
            Material amber = RuntimeMaterials.Coloured(new Color(.14f, .075f, .015f));
            Material green = RuntimeMaterials.Coloured(new Color(.015f, .10f, .045f));
            // All supports are off the carriageway; the gantry is safely overhead.
            foreach (float side in new[] { -1f, 1f })
                signs.Box(signs.transform, "Traffic signal roadside pole", new Vector3(side * 5.65f, 2.85f, 0f),
                    new Vector3(.18f, 5.7f, .18f), metal);
            signs.Box(signs.transform, "Traffic signal overhead arm", new Vector3(0f, 5.65f, 0f),
                new Vector3(11.5f, .18f, .18f), metal);
            for (int lane = -1; lane <= 1; lane++)
            {
                float x = lane * WorldShiftLevel.RoadWidth / 3f;
                signs.Box(signs.transform, "Suspended signal housing", new Vector3(x, 4.85f, -.12f),
                    new Vector3(.78f, 1.66f, .32f), housing);
                signs.SignalLens(new Vector3(x, 5.37f, -.32f), red, "Red traffic light - pedestrian crossing");
                signs.SignalLens(new Vector3(x, 4.85f, -.32f), amber, "Amber traffic light - inactive");
                signs.SignalLens(new Vector3(x, 4.33f, -.32f), green, "Green traffic light - inactive");
            }
            return signs;
        }

        public static WorldShiftRoadSigns BuildGapWarnings(Transform parent, WorldShiftRoute route, float gapStart, bool night = false)
        {
            var signs = Create(parent, "Broken-road pictogram warnings - decoration");
            Material pole = RuntimeMaterials.Coloured(new Color(.27f, .28f, .29f));
            Material red = signs.Glow(new Color(.95f, .045f, .025f), night ? .65f : .22f);
            Material yellow = signs.Glow(new Color(1f, .78f, .16f), night ? .38f : .075f);
            Material black = RuntimeMaterials.Coloured(new Color(.015f, .02f, .025f));
            // Two advance signs are anchored to the actual path, not the gap's
            // midpoint orientation, so they remain beside the road on a bend.
            foreach (float side in new[] { -1f, 1f })
            {
                RoadPathPoint point = route.Sample(gapStart - 38f);
                Transform board = new GameObject("Triangular broken bridge warning").transform;
                board.SetParent(signs.transform, false);
                board.SetPositionAndRotation(point.Position + point.Right * side * 5.8f,
                    Quaternion.LookRotation(point.Forward, point.Up));
                signs.Box(board, "Warning sign pole", new Vector3(0f, 1.1f, .04f), new Vector3(.12f, 2.2f, .12f), pole);
                signs.Triangle(board, "Red triangular warning border", new Vector3(0f, 2.35f, 0f), 1f, red);
                signs.Triangle(board, "Yellow triangular warning face", new Vector3(0f, 2.35f, -.018f), .81f, yellow);
                // Broken deck, falling fragment and small vehicle are recognizable
                // silhouettes. There is no text to read while steering.
                signs.Box(board, "Broken road left deck", new Vector3(-.36f, 2.13f, -.05f), new Vector3(.59f, .14f, .04f), black);
                Renderer broken = signs.Box(board, "Collapsed right deck", new Vector3(.42f, 2.01f, -.05f),
                    new Vector3(.51f, .14f, .04f), black);
                broken.transform.localRotation = Quaternion.Euler(0f, 0f, -27f);
                Renderer fragment = signs.Box(board, "Falling road fragment", new Vector3(.04f, 1.89f, -.055f),
                    new Vector3(.13f, .13f, .04f), black);
                fragment.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                signs.Box(board, "Warning car silhouette", new Vector3(-.36f, 2.36f, -.06f), new Vector3(.46f, .16f, .04f), black);
                signs.Box(board, "Warning car roof", new Vector3(-.35f, 2.49f, -.06f), new Vector3(.25f, .12f, .04f), black);
                foreach (float x in new[] { -.51f, -.21f })
                    signs.Box(board, "Warning car wheel", new Vector3(x, 2.23f, -.06f), new Vector3(.10f, .11f, .04f), black);
            }
            return signs;
        }

        private static WorldShiftRoadSigns Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root.AddComponent<WorldShiftRoadSigns>();
        }

        private Material Glow(Color colour, float strength)
        {
            var material = new Material(RuntimeMaterials.Coloured(colour));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * strength);
            material.SetFloat("_Smoothness", .45f);
            materials.Add(material);
            return material;
        }

        private void Triangle(Transform parent, string name, Vector3 position, float scale, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-1.12f, -.68f, 0f), new Vector3(0f, 1.12f, 0f), new Vector3(1.12f, -.68f, 0f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var shape = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = Vector3.one * scale;
            shape.GetComponent<MeshFilter>().sharedMesh = mesh;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void SignalLens(Vector3 position, Material material, string name)
        {
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = name;
            lens.transform.SetParent(transform, false);
            lens.transform.localPosition = position;
            lens.transform.localScale = new Vector3(.42f, .42f, .16f);
            RemoveCollider(lens);
            lens.GetComponent<Renderer>().sharedMaterial = material;
        }

        private Renderer Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            RemoveCollider(shape);
            Renderer renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private static void RemoveCollider(GameObject shape)
        {
            Collider collider = shape.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
