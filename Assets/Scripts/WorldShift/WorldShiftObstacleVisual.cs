using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>One readable silhouette for every solid road hazard, fitted to its existing footprint.</summary>
    public static class WorldShiftObstacleVisual
    {
        public static Transform Build(Transform parent, Vector3 centre, Vector3 size)
        {
            var root = new GameObject("X obstacle").transform;
            root.SetParent(parent, false);
            root.localPosition = centre;
            var body = RuntimeMaterials.Coloured(new Color(.48f, .16f, .14f));
            var ink = RuntimeMaterials.Coloured(new Color(.99f, .92f, .74f));
            ink.EnableKeyword("_EMISSION");
            ink.SetColor("_EmissionColor", new Color(.99f, .92f, .74f) * .45f);
            Cube(root, "Obstacle block", Vector3.zero, size, body);
            float halfX = Mathf.Min(size.x * .32f, 1.05f), halfY = size.y * .32f;
            foreach (float face in new[] { -1f, 1f })
                Cross(root, new Vector3(0f, 0f, face * (size.z * .5f + .035f)),
                    new Vector3(halfX, halfY, 0f), new Vector3(halfX, -halfY, 0f), ink);
            // A matching top X stays recognizable from the raised chase camera and during jumps.
            float top = Mathf.Min(size.x, size.z) * .3f;
            Cross(root, new Vector3(0f, size.y * .5f + .035f, 0f),
                new Vector3(top, 0f, top), new Vector3(top, 0f, -top), ink);
            return root;
        }

        private static void Cross(Transform parent, Vector3 centre, Vector3 a, Vector3 b, Material ink)
        {
            foreach (var diagonal in new[] { a, b })
            {
                var bar = Cube(parent, "Obstacle X", centre, new Vector3(diagonal.magnitude * 2f, .16f, .06f), ink);
                bar.localRotation = Quaternion.FromToRotation(Vector3.right, diagonal.normalized);
            }
        }

        private static Transform Cube(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = centre;
            cube.transform.localScale = size;
            cube.GetComponent<Collider>().enabled = false;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube.transform;
        }
    }
}
