using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Desert ground crossing spheres: dense, high-contrast tumbleweeds with the shared sideways arrow.
    /// Airborne spheres are vultures (WorldShiftDesertMover.Vulture). Same spherical collision; no colliders.
    /// </summary>
    internal sealed class WorldShiftDesertRockArt : MonoBehaviour
    {
        private Mesh bodyMesh;
        private Material finish;
        private Transform arrow;

        public static WorldShiftDesertRockArt Tumbleweed(Transform rollingParent, Transform cueParent, float radius)
        {
            var root = new GameObject("Desert tumbleweed");
            root.transform.SetParent(rollingParent, false);
            var owner = root.AddComponent<WorldShiftDesertRockArt>();
            // Outside the rolling hierarchy, so the arrow always reads left/right.
            owner.arrow = WorldShiftMoverArrow.Attach(cueParent, radius * .84f, new Vector3(0, radius + .32f, -radius * .25f),
                Quaternion.identity, Vector3.back);
            owner.BuildTumbleweed(radius);
            return owner;
        }

        private void BuildTumbleweed(float radius)
        {
            var art = new WorldShiftArtMesh();
            var random = new System.Random(7341);
            float R() => (float)random.NextDouble();
            Vector3 OnSphere(float s)
            {
                float z = R() * 2f - 1f, a = R() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
                return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a)) * radius * s;
            }
            // More contrast against the sand than before: warm straw mixed with darker rust browns.
            Color[] palette =
            {
                new Color(.90f, .76f, .50f), new Color(.72f, .52f, .30f), new Color(.58f, .40f, .22f),
                new Color(.48f, .32f, .18f), new Color(.84f, .66f, .40f)
            };
            Color Twig() { Color c = palette[random.Next(palette.Length)] * (.9f + R() * .14f); c.a = 1f; return c; }
            // A dense, dark inner tangle gives the ball a readable silhouette.
            Color stem = new Color(.30f, .20f, .11f);
            for (int i = 0; i < 46; i++)
                art.Beam(OnSphere(.15f), OnSphere(.8f), radius * .06f, radius * .06f, stem);
            // Thicker, kinked branches arcing around the ball.
            for (int i = 0; i < 150; i++)
            {
                Vector3 axis = OnSphere(1f).normalized, start = Vector3.Cross(axis, OnSphere(1f)).normalized;
                float span = 60f + R() * 100f, shell = .55f + R() * .45f;
                float width = radius * (.024f + R() * .024f);
                Color colour = Twig();
                Vector3 prev = start * radius * shell;
                for (int step = 1; step <= 6; step++)
                {
                    float wobble = shell + (R() - .5f) * .14f;
                    Vector3 next = Quaternion.AngleAxis(span * step / 6f, axis) * start * radius * Mathf.Min(wobble, 1f);
                    art.Beam(prev, next, width, width, colour);
                    if (R() < .3f) art.Beam(next, Vector3.ClampMagnitude(next + OnSphere(.18f), radius), width * .7f, width * .7f, colour);
                    prev = next;
                }
            }
            bodyMesh = art.Mesh("Dense tumbleweed twigs");
            finish = Material("Tumbleweed finish", .45f);
            Draw(gameObject, bodyMesh);
        }

        // ---------- Helpers ----------

        private void Draw(GameObject target, Mesh mesh)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = finish;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material Material(string name, float emission)
        {
            var material = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = name };
            material.SetFloat("_Emission", emission);
            material.SetFloat("_Specular", .02f);
            return material;
        }

        private void OnDestroy()
        {
            Release(bodyMesh); Release(finish);
            if (arrow != null) Release(arrow.gameObject);
        }

        private static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
