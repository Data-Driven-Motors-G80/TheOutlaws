using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Desert-only static obstacle art, following the Snow format: world blockers rise as sandstone hoodoos
    /// with Lydia's sun/moon panels (WorldShiftSnowObstacleArt.WorldPanel). Some blockers are saguaro cactus stands instead (same panels). Sideways movers are animals
    /// (WorldShiftDesertCreatures.cs). Collision footprints are unchanged; no colliders.
    /// </summary>
    internal static class WorldShiftDesertObstacleArt
    {
        private static readonly Color Sand = new Color(.86f, .70f, .47f);

        // ---------- Day/Night world blocker: sandstone hoodoos (Desert counterpart of Snow's upright pines) ----------

        public static void Hoodoos(WorldShiftArtMesh art, float w, float h, float d, ShiftWorld world)
        {
            Color[] strata = { new Color(.72f, .38f, .22f), new Color(.86f, .57f, .35f), new Color(.62f, .31f, .19f),
                new Color(.80f, .48f, .29f), new Color(.68f, .35f, .21f) };
            Color cap = new Color(.40f, .25f, .17f);
            // A sandstone bank fills the blocked area, so gaps between spires never suggest a safe lane.
            art.ChamferBox(new Vector3(0, h * .15f, 0), new Vector3(w, h * .30f, d), .16f, strata[0]);
            art.ChamferBox(new Vector3(0, h * .28f, 0), new Vector3(w * .97f, h * .20f, d * .98f), .14f, Sand);
            int count = Mathf.Max(1, Mathf.RoundToInt(w / 3f));
            float spacing = w / count;
            for (int spire = 0; spire < count; spire++)
            {
                float x = -w * .5f + spacing * (spire + .5f);
                float height = h * (2.6f + (spire % 2) * .3f);
                float bw = Mathf.Min(spacing * .62f, 1.7f), bd = Mathf.Min(d * .32f, 1.5f);
                // Banded column with a pinched neck under a wider cap rock.
                float[] taper = { 1f, .86f, .72f, .58f, .52f };
                float y = h * .30f, tier = (height * .88f - y) / taper.Length;
                for (int t = 0; t < taper.Length; t++)
                {
                    art.ChamferBox(new Vector3(x, y + tier * .5f, 0), new Vector3(bw * taper[t], tier * 1.02f, bd * taper[t]),
                        .12f, strata[(t + spire) % strata.Length]);
                    y += tier;
                }
                art.ChamferBox(new Vector3(x, height * .94f, 0), new Vector3(bw * .92f, height * .12f, bd * 1.05f), .1f, cap);
                foreach (float side in new[] { -1f, 1f })
                {
                    art.Rock(new Vector3(x + side * bw * .42f, h * .36f, side * bd * .25f), new Vector3(.32f, .42f, .28f),
                        strata[(spire + 2) % strata.Length], spire * 3 + (side > 0 ? 1 : 0));
                    WorldShiftSnowObstacleArt.WorldPanel(art, new Vector3(x, h * .62f, side * (d * .5f - .05f)),
                        Mathf.Min(spacing * .68f, 2.1f), h * .56f, world);
                }
            }
            // Bright, continuous lower edges make the full blocked width readable on approach.
            Color marker = world == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour;
            foreach (float side in new[] { -1f, 1f })
                art.Box(new Vector3(0, h * .19f, side * (d * .5f - .04f)), new Vector3(w, h * .16f, .06f), marker);
        }

        // ---------- Day/Night world blocker: a saguaro cactus stand (variation on the hoodoos) ----------

        public static void Saguaros(WorldShiftArtMesh art, float w, float h, float d, ShiftWorld world)
        {
            Color green = new Color(.24f, .50f, .26f), rib = new Color(.15f, .34f, .17f), light = new Color(.42f, .66f, .36f);
            Color dune = new Color(.78f, .60f, .38f), bloom = new Color(.98f, .86f, .9f), pink = new Color(.93f, .38f, .55f);
            // A sand bank fills the blocked area, so gaps between the cacti never suggest a safe lane.
            art.ChamferBox(new Vector3(0, h * .15f, 0), new Vector3(w, h * .30f, d), .16f, dune);
            art.ChamferBox(new Vector3(0, h * .28f, 0), new Vector3(w * .97f, h * .20f, d * .98f), .14f, Sand);
            int count = Mathf.Max(1, Mathf.RoundToInt(w / 3f));
            float spacing = w / count;
            for (int i = 0; i < count; i++)
            {
                float x = -w * .5f + spacing * (i + .5f);
                float height = h * (3.2f + (i % 2) * .35f), radius = Mathf.Min(.42f, d * .28f);
                float baseY = h * .3f, reach = Mathf.Min(spacing * .4f, 1.25f);
                Saguaro(art, new Vector3(x, baseY, 0), height - baseY, radius, reach, i % 2 == 0 ? 1f : -1f, green, rib, light, bloom);
                foreach (float side in new[] { -1f, 1f })
                {
                    art.Rock(new Vector3(x + side * spacing * .36f, h * .36f, -side * d * .2f), new Vector3(.3f, .34f, .26f),
                        new Color(.66f, .46f, .30f), i * 5 + (side > 0 ? 1 : 0));
                    WorldShiftSnowObstacleArt.WorldPanel(art, new Vector3(x, h * .62f, side * (d * .5f - .05f)),
                        Mathf.Min(spacing * .68f, 2.1f), h * .56f, world);
                }
                // Low barrel cacti and prickly pear pads fill the gaps between the tall saguaros.
                if (i < count - 1)
                {
                    float gx = x + spacing * .5f;
                    Ellipsoid(art, new Vector3(gx, h * .5f, 0), new Vector3(.36f, .48f, .36f), green, 10, 6);
                    for (int r = 0; r < 8; r++)
                    {
                        float a = r * Mathf.PI / 4f;
                        Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .35f;
                        art.Beam(new Vector3(gx, h * .5f - .38f, 0) + o * .7f, new Vector3(gx, h * .5f + .38f, 0) + o * .7f, .035f, .035f, rib);
                    }
                    Ellipsoid(art, new Vector3(gx, h * .5f + .48f, 0), new Vector3(.14f, .07f, .14f), pink, 8, 4);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 pad = new Vector3(gx + side * .5f, h * .52f, side * d * .18f);
                        Ellipsoid(art, pad, new Vector3(.28f, .34f, .07f), light, 8, 5);
                        Ellipsoid(art, pad + new Vector3(side * .18f, .38f, 0), new Vector3(.2f, .25f, .06f), green, 8, 5);
                        Ellipsoid(art, pad + new Vector3(side * .26f, .66f, 0), new Vector3(.07f, .07f, .07f), pink, 6, 4);
                    }
                }
            }
            Color marker = world == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour;
            foreach (float side in new[] { -1f, 1f })
                art.Box(new Vector3(0, h * .19f, side * (d * .5f - .04f)), new Vector3(w, h * .16f, .06f), marker);
        }

        /// <summary>A tall ribbed saguaro with two raised arms; arms stay inside the blocker's width.</summary>
        private static void Saguaro(WorldShiftArtMesh art, Vector3 foot, float height, float radius, float reach, float lean,
            Color green, Color rib, Color light, Color bloom)
        {
            Vector3 top = foot + Vector3.up * height;
            Tube(art, foot, top, radius, new[] { green, green, light, green }, 12);
            Ellipsoid(art, top, new Vector3(radius, radius * .8f, radius), green, 12, 6);
            // Vertical ribs give the trunk the pleated saguaro look from the driver's seat.
            for (int r = 0; r < 10; r++)
            {
                float a = r * Mathf.PI / 5f;
                Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * 1.02f;
                art.Beam(foot + o, top + o * .96f, radius * .1f, radius * .1f, rib);
            }
            for (int f = 0; f < 4; f++)
            {
                float a = f * Mathf.PI * .5f + .4f;
                Ellipsoid(art, top + new Vector3(Mathf.Cos(a) * radius * .55f, radius * .62f, Mathf.Sin(a) * radius * .55f),
                    Vector3.one * radius * .26f, bloom, 6, 4);
            }
            // Two arms at different heights: out sideways, then bending up.
            float[] at = { .42f, .58f }, rise = { .30f, .22f };
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? lean : -lean, armR = radius * .68f;
                Vector3 root = foot + Vector3.up * height * at[arm];
                Vector3 elbow = root + new Vector3(side * reach, height * .05f, 0);
                Vector3 tip = elbow + Vector3.up * height * rise[arm];
                Tube(art, root, elbow, armR, new[] { green }, 10);
                Ellipsoid(art, elbow, Vector3.one * armR, green, 10, 5);
                Tube(art, elbow, tip, armR, new[] { green, light }, 10);
                Ellipsoid(art, tip, new Vector3(armR, armR * .8f, armR), green, 10, 5);
                for (int r = 0; r < 6; r++)
                {
                    float a = r * Mathf.PI / 3f;
                    Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * armR * 1.03f;
                    art.Beam(elbow + o, tip + o, armR * .12f, armR * .12f, rib);
                }
            }
        }

        // ---------- Shared mesh helpers (also used by the Desert vulture) ----------

        /// <summary>Cylinder between two points; colours cycle along its length.</summary>
        internal static void Tube(WorldShiftArtMesh art, Vector3 a, Vector3 b, float radius, Color[] colours, int sides)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            int segments = Mathf.Max(1, colours.Length);
            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = Vector3.Lerp(a, b, (float)s / segments), p1 = Vector3.Lerp(a, b, (float)(s + 1) / segments);
                for (int j = 0; j < sides; j++)
                {
                    float t0 = j * Mathf.PI * 2f / sides, t1 = (j + 1) * Mathf.PI * 2f / sides;
                    Vector3 n0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius, n1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                    art.Quad(p0 + n0, p1 + n0, p1 + n1, p0 + n1, colours[s]);
                }
            }
            for (int j = 0; j < sides; j++)
            {
                float t0 = j * Mathf.PI * 2f / sides, t1 = (j + 1) * Mathf.PI * 2f / sides;
                Vector3 n0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius, n1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                art.Triangle(a, a + n1, a + n0, colours[0]);
                art.Triangle(b, b + n0, b + n1, colours[segments - 1]);
            }
        }

        internal static void Ellipsoid(WorldShiftArtMesh art, Vector3 centre, Vector3 radii, Color colour, int segments, int rings)
        {
            Vector3 P(int ring, int seg)
            {
                float lat = -Mathf.PI * .5f + ring * Mathf.PI / rings, lon = seg * Mathf.PI * 2f / segments;
                return centre + Vector3.Scale(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)), radii);
            }
            for (int ring = 0; ring < rings; ring++)
                for (int seg = 0; seg < segments; seg++)
                {
                    Color c = colour * (.9f + .1f * ((ring + seg) % 2)); c.a = 1f;
                    art.Quad(P(ring, seg), P(ring + 1, seg), P(ring + 1, seg + 1), P(ring, seg + 1), c);
                }
        }
    }
}
