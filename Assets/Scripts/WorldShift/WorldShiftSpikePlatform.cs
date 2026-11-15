using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Visual-only police spike platform fitted to the existing hazard bounds.</summary>
    public sealed class WorldShiftSpikePlatform : MonoBehaviour
    {
        public const float Width = 3.2f;
        private Mesh spikes;
        private static Material teal, steel, white, dark;
        public static void Build(Transform parent, Vector3 centre, Vector3 size)
        {
            var root = new GameObject("Sliding police spikes");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre - Vector3.up * size.y * .5f;
            var owner = root.AddComponent<WorldShiftSpikePlatform>();
            if (teal == null)
            {
                teal = Material(new Color(.025f, .58f, .62f), .15f);
                steel = Material(new Color(.3f, .35f, .4f), .65f);
                white = Material(new Color(.92f, .95f, .96f), .2f);
                dark = Material(new Color(.035f, .045f, .055f), .05f);
            }
            float deck = size.y * .42f;
            Box(root.transform, new Vector3(0, deck * .5f + .22f, 0), new Vector3(size.x, deck, size.z), teal);
            // High-contrast diagonal markings on both approach faces.
            foreach (float face in new[] { -1f, 1f })
            {
                float z = face * (size.z * .5f + .008f);
                Box(root.transform, new Vector3(0, deck * .6f, z), new Vector3(size.x * .96f, deck * .4f, .012f), dark);
                int bands = Mathf.Max(2, Mathf.CeilToInt(size.x / .5f));
                for (int i = 0; i < bands; i++)
                {
                    var stripe = Box(root.transform, new Vector3((i + .5f) / bands * size.x * .88f - size.x * .44f, deck * .6f, z + face * .012f),
                        new Vector3(.12f, deck * .37f, .015f), white);
                    stripe.localRotation = Quaternion.Euler(0, 0, -30);
                }
            }
            // Wheels roll sideways: axles run along the road, with visible hubs on approach faces.
            foreach (float x in new[] { -.32f, .32f })
                foreach (float z in new[] { -.5f, .5f })
                {
                    var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wheel.name = "Small sliding wheel";
                    wheel.transform.SetParent(root.transform, false);
                    wheel.transform.localPosition = new Vector3(size.x*x, .19f, size.z*z - Mathf.Sign(z)*.06f);
                    wheel.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    wheel.transform.localScale = new Vector3(.38f, Mathf.Min(.12f, size.z*.06f), .38f);
                    var collider = wheel.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
                    wheel.GetComponent<Renderer>().sharedMaterial=steel;
                }
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            int columns = Mathf.Max(2, Mathf.CeilToInt(size.x / .6f));
            int rows = Mathf.Max(1, Mathf.FloorToInt(size.z / 1.8f));
            float radius = Mathf.Min(.23f, size.x / columns * .38f, size.z / rows * .3f);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    Vector3 bottom = new Vector3(((col+.5f)/columns-.5f)*size.x, deck+.22f, ((row+.5f)/rows-.5f)*size.z);
                    for (int side = 0; side < 10; side++)
                    {
                        float a = side*Mathf.PI*.2f, b=(side+1)*Mathf.PI*.2f;
                        int start=vertices.Count;
                        vertices.Add(bottom + new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                        vertices.Add(bottom + Vector3.up*(size.y-deck-.22f));
                        vertices.Add(bottom + new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius));
                        triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                    }
                }
            owner.spikes = new Mesh { name="Police spike cones" };
            owner.spikes.SetVertices(vertices); owner.spikes.SetTriangles(triangles,0); owner.spikes.RecalculateNormals(); owner.spikes.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=owner.spikes;
            root.AddComponent<MeshRenderer>().sharedMaterial=steel;
        }
        private static Transform Box(Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name="Spike platform detail";box.transform.SetParent(parent,false);
            box.transform.localPosition=at;box.transform.localScale=size;
            var collider=box.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial=material;return box.transform;
        }
        private static Material Material(Color color, float metal)
        {
            var template=Resources.Load<Material>("WeeWooRuntimeLit");
            var material=template!=null?new Material(template):new Material(Shader.Find("Standard"));
            material.SetColor("_BaseColor",color);material.SetColor("_Color",color);
            material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",.55f);material.SetFloat("_Glossiness",.55f);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.18f);return material;
        }
        private void OnDestroy(){if(spikes!=null)Destroy(spikes);}
    }
}
