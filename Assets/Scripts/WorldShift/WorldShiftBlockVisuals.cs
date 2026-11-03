using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Functional car silhouettes: colour identifies the role, a stripe identifies the front.</summary>
    public static class WorldShiftBlockVisuals
    {
        public static GameObject Vehicle(Transform parent, bool police)
        {
            var root = new GameObject(police ? "Police block" : "Player block");
            root.transform.SetParent(parent, false);
            Vector3 size = police ? new Vector3(1.8f, .85f, 3.6f) : new Vector3(1.6f, .85f, 3f);
            Box(root.transform, "Vehicle body", new Vector3(0f, .55f, 0f), size,
                RuntimeMaterials.Coloured(police ? new Color(.25f, .48f, .85f) : new Color(1f, .76f, .18f)));
            Box(root.transform, "Front direction stripe", new Vector3(0f, .65f, size.z * .5f + .025f),
                new Vector3(size.x * .7f, .12f, .04f), RuntimeMaterials.Coloured(Color.white));
            return root;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            body.transform.SetParent(parent, false);
            body.transform.localPosition = position;
            body.transform.localScale = size;
            body.GetComponent<Collider>().enabled = false;
            body.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
