using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Fixed roadblock art. Gameplay collision remains owned by the course hazard.</summary>
    internal sealed class WorldShiftPoliceBarricade : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;

        public static void Build(Transform parent, Vector3 centre, Vector3 size)
        {
            var root = new GameObject("Permanent police barricade");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre - Vector3.up * size.y * .5f;
            var owner = root.AddComponent<WorldShiftPoliceBarricade>();
            var art = new WorldShiftArtMesh();
            Color white = new Color(1f, .98f, .91f);
            Color dark = new Color(.055f, .065f, .075f);
            Color metal = new Color(.28f, .31f, .32f);
            // Broad, simple warning face; lane width and longitudinal collision footprint stay unchanged.
            float w = size.x, h = size.y * 1.2f, depth = size.z;
            // Broad top and side surfaces remain legible from the elevated, pixelated gameplay camera.
            art.Box(new Vector3(0, h * .9f, 0), new Vector3(w, .18f, depth), dark);
            art.Box(new Vector3(0, h * .96f, 0), new Vector3(w - .22f, .08f, depth - .22f), white);
            foreach (float side in new[] { -1f, 1f })
            {
                art.Box(new Vector3(side * (w * .5f - .09f), h * .55f, 0), new Vector3(.18f, h * .7f, depth), dark);
                art.Box(new Vector3(side * (w * .5f - .08f), h * .55f, 0), new Vector3(.2f, h * .7f - .2f, depth - .24f), white);
            }
            // Paired panels and side rails describe the entire existing collision footprint.
            float front = -depth * .5f + .14f, back = depth * .5f - .14f;
            foreach (float x in new[] { -w * .39f, w * .39f })
            {
                art.Box(new Vector3(x, .09f, 0), new Vector3(.3f, .18f, depth), dark);
                art.Beam(new Vector3(x, .18f, front), new Vector3(x, h * .76f, 0), .15f, .15f, metal);
                art.Beam(new Vector3(x, .18f, back), new Vector3(x, h * .76f, 0), .15f, .15f, metal);
                art.Box(new Vector3(x, h * .38f, 0), new Vector3(.12f, .17f, depth - .2f), white);
            }
            foreach (float z in new[] { front, back })
            {
                art.Box(new Vector3(0, h * .55f, z), new Vector3(w, h * .9f, .24f), dark);
                art.Box(new Vector3(0, h * .55f, z), new Vector3(w - .28f, h * .9f - .24f, .26f), white);
                foreach (float side in new[] { -1f, 1f })
                {
                    float face = z + side * .137f;
                    // Clip diagonal stripes to a rectangular lower band.
                    float left = -w * .5f + .09f, right = w * .5f - .09f;
                    float bottom = h * .14f, top = h * .35f, shift = .24f;
                    for (float x = left - shift; x < right; x += w / 3f)
                    {
                        float a = Mathf.Clamp(x, left, right), b = Mathf.Clamp(x + w * .14f, left, right);
                        float c = Mathf.Clamp(x + w * .14f + shift, left, right), d = Mathf.Clamp(x + shift, left, right);
                        art.Triangle(new Vector3(a,bottom,face), new Vector3(d,top,face), new Vector3(c,top,face), dark, true);
                        art.Triangle(new Vector3(a,bottom,face), new Vector3(c,top,face), new Vector3(b,bottom,face), dark, true);
                    }
                    // Leave the upper face solid white: small lettering aliases into dark noise at gameplay resolution.
                }
            }
            owner.mesh = art.Mesh("White police barricade");
            owner.material = new Material(Resources.Load<Shader>("WorldShiftOceanSurface"));
            owner.material.name = "Reflective white police barricade";
            owner.material.SetFloat("_Emission", .9f);
            owner.material.SetFloat("_Specular", .02f);
            root.AddComponent<MeshFilter>().sharedMesh = owner.mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = owner.material;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) { Destroy(mesh); Destroy(material); }
            else { DestroyImmediate(mesh); DestroyImmediate(material); }
        }
    }
}
