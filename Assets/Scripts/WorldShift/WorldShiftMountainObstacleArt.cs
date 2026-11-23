using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain Day/Night blockers: a fresh rockslide pile across the blocked lanes. Follows Snow and Desert's format
    /// (Docs/OBSTACLE_VISUAL_RULES.md rule 5): same footprint, a themed body, the world-coloured sun/moon panel on both
    /// faces and a bright world-coloured foot. It sinks into the road like every retractable barrier.
    /// </summary>
    internal static class WorldShiftMountainObstacleArt
    {
        private static readonly Color[] Granite =
        {
            new Color(.47f, .49f, .51f), new Color(.39f, .41f, .44f), new Color(.55f, .55f, .54f),
            new Color(.34f, .36f, .38f), new Color(.51f, .49f, .46f)
        };
        // Freshly broken faces are paler, so the slide reads as new.
        private static readonly Color Fracture = new Color(.72f, .70f, .65f);

        public static void RockslidePile(WorldShiftArtMesh art, float w, float h, float d, ShiftWorld world, int variant)
        {
            var random = new System.Random(4127 + variant * 31 + Mathf.RoundToInt(w * 10f));
            float R() => (float)random.NextDouble();
            float half = w * .5f, depth = d * .5f;
            int seed = variant * 17;

            // Bottom course: large flattened boulders shoulder to shoulder across the whole blocked width, so the
            // pile has no gap that could read as a safe lane.
            int low = Mathf.Max(2, Mathf.CeilToInt(w / 1.25f));
            float step = w / low;
            for (int i = 0; i < low; i++)
            {
                float rx = step * .62f, rz = Mathf.Min(depth * .8f, 1.5f) * (.8f + R() * .2f);
                float x = Mathf.Clamp(-half + step * (i + .5f) + (R() - .5f) * step * .2f, -half + rx * .9f, half - rx * .9f);
                Boulder(art, new Vector3(x, h * .22f, (R() - .5f) * depth * .25f), new Vector3(rx, h * (.34f + R() * .1f), rz),
                    Granite[(i + variant) % Granite.Length], seed + i);
            }
            // Middle course: rounder boulders heaped on top, offset half a step.
            for (int i = 0; i < low - 1; i++)
            {
                float r = step * (.5f + R() * .12f);
                float x = -half + step * (i + 1f) + (R() - .5f) * step * .25f;
                Boulder(art, new Vector3(x, h * .62f, (R() - .5f) * depth * .35f), new Vector3(r, h * (.36f + R() * .12f), r * .95f),
                    Granite[(i + 2 + variant) % Granite.Length], seed + 20 + i);
            }
            // Crest: one or two big boulders that give the pile its peak, each with a pale fresh-break chip.
            int crest = Mathf.Max(1, Mathf.RoundToInt(w / 4f));
            for (int i = 0; i < crest; i++)
            {
                float x = -half + w * (i + .5f) / crest + (R() - .5f) * .4f;
                float r = Mathf.Min(w / crest * .3f, .85f);
                Boulder(art, new Vector3(x, h * 1.0f, (R() - .5f) * depth * .2f), new Vector3(r, h * .34f, r * .9f),
                    (i + variant) % 2 == 0 ? Granite[2] : Granite[4], seed + 40 + i);
                Boulder(art, new Vector3(x + r * .35f, h * 1.22f, 0f), new Vector3(r * .35f, h * .1f, r * .3f), Fracture, seed + 50 + i);
            }
            // Rubble spilling onto the road at both approach faces.
            int rubble = Mathf.Max(4, Mathf.RoundToInt(w * 1.4f));
            for (int i = 0; i < rubble; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f, r = .16f + R() * .16f;
                float x = Mathf.Clamp(-half + (i + .5f) * w / rubble + (R() - .5f) * .3f, -half + r, half - r);
                Boulder(art, new Vector3(x, r * .4f, side * (depth - .5f - R() * .3f)), new Vector3(r, r * .7f, r),
                    i % 4 == 0 ? Fracture : Granite[(i + variant) % Granite.Length], seed + 60 + i);
            }

            // The world panel propped in front of the pile on both faces, one per lane-width, like Desert's hoodoos.
            int panels = Mathf.Max(1, Mathf.RoundToInt(w / 3f));
            float panelSpacing = w / panels;
            for (int p = 0; p < panels; p++)
            {
                float x = -half + panelSpacing * (p + .5f);
                foreach (float side in new[] { -1f, 1f })
                    WorldShiftSnowObstacleArt.WorldPanel(art, new Vector3(x, h * .56f, side * (depth - .05f)),
                        Mathf.Min(panelSpacing * .52f, 1.6f), h * .5f, world);
            }
            // Bright, continuous lower edges make the full blocked width readable on approach.
            Color marker = world == ShiftWorld.Day ? WorldShiftLevelProps.SwitchColour : WorldShiftLevelProps.NightColour;
            foreach (float side in new[] { -1f, 1f })
                art.Box(new Vector3(0f, h * .08f, side * (depth - .04f)), new Vector3(w, h * .16f, .06f), marker);
        }

        /// <summary>A lumpy low-poly boulder: a coarse ellipsoid with deterministic per-vertex bumps and flat facets.</summary>
        private static void Boulder(WorldShiftArtMesh art, Vector3 centre, Vector3 radii, Color colour, int seed)
        {
            const int segments = 8, rings = 5;
            Vector3 P(int ring, int segment)
            {
                segment %= segments;
                float lat = -Mathf.PI * .5f + ring * Mathf.PI / rings, lon = segment * Mathf.PI * 2f / segments + seed * .37f;
                float bump = ring == 0 || ring == rings ? 1f : .84f + .3f * Mathf.Abs(Mathf.Sin(segment * 2.3f + ring * 1.7f + seed * 1.13f));
                return centre + Vector3.Scale(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)), radii) * bump;
            }
            for (int ring = 0; ring < rings; ring++)
                for (int segment = 0; segment < segments; segment++)
                {
                    float shade = .84f + .16f * ((segment * 3 + ring + seed) % 4) / 3f;
                    Color c = colour * shade; c.a = 1f;
                    Color c2 = c * .96f; c2.a = 1f;
                    art.Triangle(P(ring, segment), P(ring + 1, segment), P(ring + 1, segment + 1), c);
                    art.Triangle(P(ring, segment), P(ring + 1, segment + 1), P(ring, segment + 1), c2);
                }
        }
    }
}
