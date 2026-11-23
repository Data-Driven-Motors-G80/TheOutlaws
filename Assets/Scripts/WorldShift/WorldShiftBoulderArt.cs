using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Faceted rock inside the existing spherical collision envelope, with a non-rotating cue.</summary>
    internal sealed class WorldShiftBoulderArt : MonoBehaviour
    {
        private Mesh rockMesh;
        private Material rockMaterial;
        private GameObject arrow;

        public static void Build(Transform rollingParent, Transform cueParent, float radius, bool tumbleweed = false, bool oilDrum = false,
            bool sidewaysCue = true)
        {
            var root = new GameObject(oilDrum ? "Harbour oil drum" : tumbleweed ? "Desert tumbleweed"
                : sidewaysCue ? "Teal-banded desert boulder" : "Rolling mountain boulder");
            root.transform.SetParent(rollingParent, false);
            var owner = root.AddComponent<WorldShiftBoulderArt>();
            var rock = new WorldShiftArtMesh();
            const int segments = 16, rings = 12;
            Vector3 Point(int ring, int segment)
            {
                float latitude = -Mathf.PI * .5f + ring * Mathf.PI / rings;
                float longitude = segment * Mathf.PI * 2f / segments;
                // Small deterministic facets keep the silhouette close to the gameplay sphere.
                float variation = ring == 0 || ring == rings ? 1f : .97f + .03f * Mathf.Sin(segment * 2.7f + ring * 3.1f);
                return new Vector3(Mathf.Cos(latitude)*Mathf.Cos(longitude), Mathf.Sin(latitude),
                    Mathf.Cos(latitude)*Mathf.Sin(longitude)) * radius * variation;
            }
            if (oilDrum) OilDrum(rock, radius);
            else if (tumbleweed) Tumbleweed(rock, radius);
            else for (int ring=0; ring<rings; ring++)
                for (int segment=0; segment<segments; segment++)
                {
                    bool band = ring == 3 || ring == 4 || ring == 7 || ring == 8;
                    float shade = .78f + .22f * ((segment + ring * 3) % 5) / 4f;
                    Color colour = (band ? WorldShiftLevelProps.SideMoverColour : new Color(.23f,.25f,.28f)) * shade;
                    colour.a = 1f;
                    Vector3 a=Point(ring,segment), b=Point(ring+1,segment),
                        c=Point(ring+1,(segment+1)%segments), d=Point(ring,(segment+1)%segments);
                    rock.Triangle(a,b,c,colour,true);
                    rock.Triangle(a,c,d,colour*.94f,true);
                }
            owner.rockMesh=rock.Mesh("Faceted dark stone and teal bands");
            owner.rockMaterial=Material("Boulder stone and teal paint",.3f);
            root.AddComponent<MeshFilter>().sharedMesh=owner.rockMesh;
            root.AddComponent<MeshRenderer>().sharedMaterial=owner.rockMaterial;
            // Boulders rolling straight down the road do not move sideways, so they carry no left/right arrow (rule 2).
            if (!sidewaysCue) return;
            // Outside the rolling hierarchy: the shared arrow stays level and points the way the rock is going.
            owner.arrow=WorldShiftMoverArrow.Attach(cueParent,radius*.84f,new Vector3(0,radius+.32f,-radius*.25f),
                Quaternion.identity,Vector3.back).gameObject;
        }

        // Tangled, sun-bleached twigs filling the gameplay sphere, like a real tumbleweed.
        private static void Tumbleweed(WorldShiftArtMesh art, float radius)
        {
            var random = new System.Random(7341);
            float R() => (float)random.NextDouble();
            Vector3 OnSphere(float scale)
            {
                float z = R() * 2f - 1f, a = R() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
                return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a)) * radius * scale;
            }
            // Dry tumbleweed palette: bleached straw, greyish beige and dusty light brown.
            Color[] palette =
            {
                new Color(.86f, .76f, .58f), new Color(.78f, .68f, .52f), new Color(.70f, .62f, .50f),
                new Color(.64f, .50f, .34f), new Color(.82f, .72f, .50f)
            };
            Color Twig() { Color c = palette[random.Next(palette.Length)] * (.9f + R() * .14f); c.a = 1f; return c; }
            // Sparse darker inner stems give depth without a solid core.
            Color stem = new Color(.50f, .40f, .28f);
            for (int i = 0; i < 14; i++)
                art.Beam(OnSphere(.2f), OnSphere(.75f), radius * .035f, radius * .035f, stem);
            // Many thin, kinked branches arcing around the ball.
            for (int i = 0; i < 110; i++)
            {
                Vector3 axis = OnSphere(1f).normalized, start = Vector3.Cross(axis, OnSphere(1f)).normalized;
                float span = 60f + R() * 100f, shell = .55f + R() * .45f;
                float width = radius * (.012f + R() * .016f);
                Color colour = Twig();
                Vector3 prev = start * radius * shell;
                for (int step = 1; step <= 6; step++)
                {
                    float wobble = shell + (R() - .5f) * .14f;
                    Vector3 next = Quaternion.AngleAxis(span * step / 6f, axis) * start * radius * Mathf.Min(wobble, 1f);
                    art.Beam(prev, next, width, width, colour);
                    // Short side twigs make it look scratchy and tangled.
                    if (R() < .3f) art.Beam(next, Vector3.ClampMagnitude(next + OnSphere(.18f), radius), width * .7f, width * .7f, colour);
                    prev = next;
                }
            }
        }

        // An orange oil drum lying along the road, so it rolls sideways like the Desert rocks.
        // Body radius and half-length stay inside the gameplay sphere.
        private static void OilDrum(WorldShiftArtMesh art, float radius)
        {
            const int sides = 20;
            float r = radius * .86f, half = radius * .48f;
            Color body = WorldShiftLevelProps.SideMoverColour, rib = body * .72f, lid = new Color(.12f, .12f, .13f);
            rib.a = 1f;
            Vector3 P(float a, float z, float scale) => new Vector3(Mathf.Cos(a) * r * scale, Mathf.Sin(a) * r * scale, z);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                float shade = .86f + .14f * Mathf.Sin(a * 3f);
                Color c = body * shade; c.a = 1f;
                art.Quad(P(a, -half, 1f), P(b, -half, 1f), P(b, half, 1f), P(a, half, 1f), c);
                // Two raised rolling ribs.
                foreach (float z in new[] { -half * .45f, half * .45f })
                    art.Quad(P(a, z - .05f, 1.03f), P(b, z - .05f, 1.03f), P(b, z + .05f, 1.03f), P(a, z + .05f, 1.03f), rib);
                // Dark lids with a rim.
                foreach (float z in new[] { -half, half })
                {
                    art.Triangle(new Vector3(0, 0, z), P(a, z, .97f), P(b, z, .97f), lid, true);
                    art.Quad(P(a, z, 1f), P(b, z, 1f), P(b, z, .9f), P(a, z, .9f), rib);
                }
            }
            // A pale hazard diamond on each side of the drum.
            foreach (float a in new[] { Mathf.PI * .5f, Mathf.PI * 1.5f })
            {
                Vector3 n = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), t = new Vector3(-n.y, n.x, 0f);
                Vector3 c = n * r * 1.005f; float s = half * .35f;
                art.Quad(c + Vector3.forward * s, c + t * s, c - Vector3.forward * s, c - t * s, new Color(.96f, .93f, .82f));
                art.Quad(c - t * s, c - Vector3.forward * s, c + t * s, c + Vector3.forward * s, new Color(.96f, .93f, .82f));
            }
        }

        private static Material Material(string name,float emission)
        {
            var material=new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) {name=name};
            material.SetFloat("_Emission",emission);
            material.SetFloat("_Specular",.02f);
            return material;
        }

        private void OnDestroy()
        {
            Release(rockMesh); Release(rockMaterial);
            if(arrow != null) Release(arrow);
        }
        private static void Release(Object obj)
        {
            if(obj == null) return;
            if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
    }
}
