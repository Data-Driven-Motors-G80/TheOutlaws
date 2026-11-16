using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Distant working harbour, a passing cargo ship, lighthouse beams and Night harbour light.
    /// Scenery only: far from the road, no colliders, no effect on gameplay or visibility of hazards.</summary>
    public sealed partial class WorldShiftOceanPresentation
    {
        private readonly List<Vector3> lighthouseTops = new List<Vector3>();
        private readonly List<Transform> lighthouseBeams = new List<Transform>();
        private GameObject harbourNightLights, harbourReflections;
        private Transform cargoShip;
        private Vector3 shipStart, shipDirection;
        private float shipTravel;

        private static readonly Color[] ContainerPaint =
        {
            new Color(.78f,.33f,.22f), new Color(.16f,.42f,.62f), new Color(.86f,.7f,.3f),
            new Color(.25f,.5f,.4f), new Color(.6f,.62f,.64f), new Color(.5f,.25f,.35f)
        };

        private static void Q2(WorldShiftArtMesh art, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        { art.Triangle(a, b, c, colour, true); art.Triangle(a, c, d, colour, true); }

        private void BuildHarbour()
        {
            var g = new WorldShiftArtMesh(); var lights = new WorldShiftArtMesh(); var streaks = new WorldShiftArtMesh();
            float length = game.RunLength;
            int site = 0;
            for (float d = 160f; d < length + 200f; d += 240f, site++)
            {
                var road = route.Sample(Mathf.Min(d, length));
                Vector3 right = Vector3.ProjectOnPlane(road.Right, Vector3.up).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(road.Forward, Vector3.up).normalized;
                float side = site % 2 == 0 ? 1f : -1f;
                Vector3 origin = road.Position + right * side * (190f + (site * 53) % 90) + forward * ((site * 37) % 80 - 40);
                origin.y = SeaHeight;
                Vector3 toward = -right * side; // the faces that look back at the road
                // Quay slab every site.
                Slab(g, origin, forward, right, new Vector3(150f, 2.2f, 60f), new Color(.52f, .54f, .55f));
                if (site % 3 == 0) Crane(g, lights, origin + forward * -35f + toward * 18f, forward, toward, site);
                if (site % 3 == 0) Crane(g, lights, origin + forward * 15f + toward * 18f, forward, toward, site + 1);
                Containers(g, origin + toward * -4f, forward, right, site);
                Town(g, lights, streaks, origin + toward * -24f, forward, toward, site);
            }
            Render("Distant harbour", g, scenery);
            harbourNightLights = Render("Harbour windows and crane lamps", lights, luminous);
            harbourReflections = Render("Harbour light reflections", streaks, luminous);
            BuildCargoShip();
            BuildLighthouseBeams();
        }

        private static void Slab(WorldShiftArtMesh g, Vector3 c, Vector3 forward, Vector3 right, Vector3 size, Color colour)
        {
            Vector3 h = size * .5f;
            Vector3 P(float x, float y, float z) => c + right * x + Vector3.up * y + forward * z;
            Vector3 a = P(-h.z, 0, -h.x), b = P(h.z, 0, -h.x), e = P(h.z, 0, h.x), f = P(-h.z, 0, h.x);
            Vector3 up = Vector3.up * size.y;
            Q2(g, a + up, f + up, e + up, b + up, colour);
            Q2(g, a, a + up, b + up, b, colour * .85f); Q2(g, b, b + up, e + up, e, colour * .8f);
            Q2(g, e, e + up, f + up, f, colour * .85f); Q2(g, f, f + up, a + up, a, colour * .8f);
        }

        // Axis-aligned-to-road box helper: x across (right), y up, z along (forward).
        private static void Block(WorldShiftArtMesh g, Vector3 c, Vector3 forward, Vector3 right, Vector3 size, Color colour)
        {
            Vector3 h = size * .5f;
            Vector3 P(float x, float y, float z) => c + right * x * h.x + Vector3.up * y * h.y + forward * z * h.z;
            Q2(g, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), colour);
            Q2(g, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), colour * .86f);
            Q2(g, P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), colour * .86f);
            Q2(g, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), colour * .78f);
            Q2(g, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), colour * .78f);
        }

        private static void Crane(WorldShiftArtMesh g, WorldShiftArtMesh lights, Vector3 p, Vector3 forward, Vector3 toward, int seed)
        {
            Color frame = seed % 2 == 0 ? new Color(.88f, .42f, .22f) : new Color(.2f, .55f, .62f);
            Vector3 across = toward; // boom reaches out over the water toward the road
            Vector3 lift = Vector3.up * 2.2f;
            foreach (float a in new[] { -6f, 6f })
                foreach (float b in new[] { -5f, 5f })
                    g.Beam(p + lift + forward * a + across * b, p + lift + forward * a + across * b + Vector3.up * 26f, .9f, .9f, frame);
            g.Beam(p + lift + Vector3.up * 26f - across * 14f, p + lift + Vector3.up * 26f + across * 34f, 1.4f, 2.2f, frame);
            g.Beam(p + lift + Vector3.up * 30f, p + lift + Vector3.up * 26f + across * 30f, .35f, .35f, frame * .8f);
            g.Beam(p + lift + Vector3.up * 30f, p + lift + Vector3.up * 26f - across * 12f, .35f, .35f, frame * .8f);
            g.Beam(p + lift + Vector3.up * 26f, p + lift + Vector3.up * 32f, .8f, .8f, frame);
            lights.Box(p + lift + Vector3.up * 32.6f, Vector3.one * .9f, new Color(1f, .3f, .25f));
            lights.Box(p + lift + Vector3.up * 26.8f + across * 34f, Vector3.one * .7f, new Color(1f, .3f, .25f));
        }

        private static void Containers(WorldShiftArtMesh g, Vector3 p, Vector3 forward, Vector3 right, int seed)
        {
            for (int row = 0; row < 6; row++)
                for (int col = 0; col < 3; col++)
                {
                    int height = 1 + (row * 7 + col * 3 + seed) % 3;
                    for (int level = 0; level < height; level++)
                    {
                        Color c = ContainerPaint[(row * 5 + col * 2 + level + seed) % ContainerPaint.Length];
                        Vector3 at = p + forward * (-50f + row * 13f) + right * (col * 3.2f - 3.2f) + Vector3.up * (2.2f + 1.3f + level * 2.6f);
                        Block(g, at, forward, right, new Vector3(2.9f, 2.5f, 12f), c);
                    }
                }
        }

        private static void Town(WorldShiftArtMesh g, WorldShiftArtMesh lights, WorldShiftArtMesh streaks,
            Vector3 p, Vector3 forward, Vector3 toward, int seed)
        {
            Color[] pastel = { new Color(.92f, .86f, .72f), new Color(.72f, .84f, .86f), new Color(.88f, .7f, .62f), new Color(.8f, .82f, .72f) };
            Vector3 right = -toward;
            for (int i = 0; i < 7; i++)
            {
                float w = 9f + (seed * 3 + i * 5) % 8, h = 8f + (seed * 7 + i * 11) % 18;
                Vector3 c = p + forward * (-48f + i * 15f) + right * ((i % 2) * 6f) + Vector3.up * (2.2f + h * .5f);
                Block(g, c, forward, right, new Vector3(8f, h, w), pastel[(i + seed) % pastel.Length]);
                // Windows on the road-facing wall; each casts a light streak on the water at Night.
                Vector3 face = c + toward * 4.05f;
                for (float y = -h * .5f + 2.2f; y < h * .5f - 1f; y += 2.6f)
                    for (float z = -w * .5f + 1.4f; z < w * .5f - .8f; z += 2.2f)
                    {
                        if (((int)(y * 3 + z * 5 + seed + i) & 3) == 0) continue;
                        Vector3 window = face + Vector3.up * y + forward * z;
                        Q2(lights, window - forward * .45f - Vector3.up * .55f, window - forward * .45f + Vector3.up * .55f,
                            window + forward * .45f + Vector3.up * .55f, window + forward * .45f - Vector3.up * .55f, new Color(1f, .84f, .55f));
                        if (y < h * .5f - 6f) continue;
                        Vector3 water = new Vector3(window.x, SeaHeight + .06f, window.z) + toward * 6f;
                        Q2(streaks, water - forward * .18f, water + toward * 22f - forward * .1f,
                            water + toward * 22f + forward * .1f, water + forward * .18f, new Color(.9f, .7f, .42f));
                    }
            }
        }

        private void BuildCargoShip()
        {
            var g = new WorldShiftArtMesh(); var lamps = new WorldShiftArtMesh();
            Vector3 f = Vector3.forward, r = Vector3.right;
            Block(g, Vector3.up * 3f, f, r, new Vector3(22f, 6f, 120f), new Color(.18f, .22f, .27f));
            Block(g, Vector3.up * 6.3f, f, r, new Vector3(22.2f, .6f, 120.2f), new Color(.7f, .2f, .18f));
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 4; col++)
                    for (int level = 0; level < 1 + (row + col) % 3; level++)
                        Block(g, new Vector3(col * 4.6f - 6.9f, 8f + level * 2.7f, -40f + row * 13.5f), f, r,
                            new Vector3(4.2f, 2.6f, 12.5f), ContainerPaint[(row * 3 + col + level) % ContainerPaint.Length]);
            Block(g, new Vector3(0, 13f, 50f), f, r, new Vector3(18f, 13f, 10f), new Color(.92f, .92f, .88f));
            for (float x = -7f; x <= 7f; x += 2.5f) lamps.Box(new Vector3(x, 16f, 44.9f), new Vector3(1.4f, .9f, .2f), new Color(1f, .85f, .6f));
            lamps.Box(new Vector3(0, 21f, 50f), Vector3.one * 1.1f, new Color(1f, .3f, .25f));
            cargoShip = new GameObject("Passing cargo ship").transform; cargoShip.SetParent(transform, false);
            Render("Cargo ship hull and containers", g, scenery, cargoShip);
            Render("Cargo ship lights", lamps, luminous, cargoShip);
            // Crosses far out, parallel to the overall course, and loops.
            Vector3 start = route.Sample(0f).Position, end = route.Sample(game.RunLength).Position;
            shipDirection = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, shipDirection);
            shipStart = start + side * 760f - shipDirection * 400f; shipStart.y = SeaHeight;
            cargoShip.rotation = Quaternion.LookRotation(shipDirection, Vector3.up);
            cargoShip.position = shipStart;
        }

        private void BuildLighthouseBeams()
        {
            foreach (var top in lighthouseTops)
            {
                var pivot = new GameObject("Lighthouse beam").transform; pivot.SetParent(transform, false);
                pivot.position = top + Vector3.up * 17.4f;
                var beam = new WorldShiftArtMesh();
                foreach (float side in new[] { -1f, 1f })
                    beam.Beam(Vector3.forward * side * 1.6f, Vector3.forward * side * 70f + Vector3.down * 2f, .6f, .25f, new Color(1f, .95f, .7f) * .75f);
                Render("Lighthouse beam light", beam, luminous, pivot);
                pivot.gameObject.SetActive(false);
                lighthouseBeams.Add(pivot);
            }
        }

        private void SetHarbourWorld(bool day)
        {
            if (harbourNightLights != null) harbourNightLights.SetActive(!day);
            if (harbourReflections != null) harbourReflections.SetActive(!day);
            foreach (var beam in lighthouseBeams) beam.gameObject.SetActive(!day);
            water.SetFloat("_MoonPath", day ? 0f : 1f);
            UpdateMoonPath();
            water.SetFloat("_FoamAmount", day ? .24f : .14f);
        }

        // The moon hangs ahead of the player, so its path on the water runs along the view, slightly off-centre.
        private void UpdateMoonPath()
        {
            if (cameraView == null) return;
            Vector3 dir = Quaternion.Euler(0, 12f, 0) * Vector3.ProjectOnPlane(cameraView.transform.forward, Vector3.up).normalized;
            water.SetVector("_MoonDir", new Vector4(dir.x, dir.z, 0, 0));
        }

        private void AnimateHarbour(float delta)
        {
            UpdateMoonPath();
            foreach (var beam in lighthouseBeams) beam.Rotate(0f, 38f * delta, 0f, Space.World);
            if (cargoShip == null) return;
            shipTravel = Mathf.Repeat(shipTravel + 5f * delta, game.RunLength + 800f);
            cargoShip.position = shipStart + shipDirection * shipTravel;
        }
    }
}
