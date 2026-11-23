using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain background life: aerial cable cars gliding across the valley above the road, and eagles soaring in
    /// wide banked circles. Purely visual: no colliders, nothing the car can touch. Animation runs on scaled time,
    /// so the pause menu stops it.
    /// </summary>
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private sealed class Ropeway
        {
            public Vector3 From, To;
            public float Sag;
            public readonly List<Transform> Cabins = new List<Transform>();
            public readonly List<float> Offsets = new List<float>();
            public readonly List<int> Lines = new List<int>();
            public Vector3 At(float t, int line)
            {
                Vector3 p = Vector3.Lerp(From, To, t) - Vector3.up * (Sag * 4f * t * (1f - t));
                return p + Vector3.forward * (line == 0 ? -2.6f : 2.6f);
            }
        }

        private sealed class Eagle
        {
            public Transform Body, LeftWing, RightWing;
            public Vector3 Centre;
            public float Radius, Speed, Phase, FlapPhase;
        }

        private readonly List<Ropeway> ropeways = new List<Ropeway>();
        private readonly List<Eagle> eagles = new List<Eagle>();
        private float skyClock;
        private const float CabinSpeed = 6f;

        private static readonly Color CableSteel = new Color(.20f, .22f, .25f);
        private static readonly Color PylonSteel = new Color(.55f, .58f, .62f);
        private static readonly Color[] CabinColours = { new Color(.82f, .16f, .13f), new Color(.95f, .72f, .12f), new Color(.10f, .42f, .70f) };

        private void BuildMountainSkyLife()
        {
            int index = 0;
            for (float at = 170f; at < route.Course.Length - 60f; at += 430f, index++)
            {
                if (NearJump(at, 70f)) at += 150f;
                if (at >= route.Course.Length - 60f) break;
                BuildRopeway(at, index);
            }
            index = 0;
            for (float at = 60f; at < route.Course.Length + 120f; at += 210f, index++)
            {
                float side = index % 3 == 1 ? -1f : 1f;
                BuildEagle(at, side * (32f + index % 4 * 10f), 26f + index % 3 * 7f, 22f + index % 4 * 6f, index);
                // Some eagles hunt in pairs.
                if (index % 2 == 0) BuildEagle(at + 35f, side * (48f + index % 3 * 10f), 40f + index % 2 * 8f, 30f, index + 11);
            }
        }

        // ---------- Cable cars ----------

        private void BuildRopeway(float at, int index)
        {
            var root = Root("Aerial cable car over the valley", at, 0f);
            // The ropeway climbs from the valley (right, downhill) to the uphill peaks (left).
            var way = new Ropeway { From = new Vector3(-190f, 62f, 0f), To = new Vector3(190f, 20f, 0f), Sag = 8f };
            var steel = new WorldShiftArtMesh();
            for (int line = 0; line < 2; line++)
                for (int i = 0; i < 40; i++)
                    steel.Beam(way.At(i / 40f, line), way.At((i + 1) / 40f, line), .22f, .22f, CableSteel);
            // Lattice pylons stand on the slopes either side of the road, clear of every bend.
            foreach (float x in new[] { -78f, 82f })
            {
                if (!Clear(at, x, 8f)) continue;
                float t = Mathf.InverseLerp(way.From.x, way.To.x, x);
                float top = Vector3.Lerp(way.From, way.To, t).y - way.Sag * 4f * t * (1f - t) + 1.2f;
                float ground = MountainGroundHeight(Mathf.Abs(x) - WorldShiftLevel.RoadWidth * .5f, Mathf.Sign(x)) - 2f;
                Pylon(steel, new Vector3(x, ground, 0f), top - ground);
            }
            Render(root, "Ropeway cables and lattice pylons", steel, surface);

            for (int line = 0; line < 2; line++)
                for (int c = 0; c < 3; c++)
                {
                    var cabin = CableCar(root, CabinColours[(index + c + line) % CabinColours.Length]);
                    way.Cabins.Add(cabin);
                    way.Offsets.Add((c + line * .5f) / 3f);
                    way.Lines.Add(line);
                }
            ropeways.Add(way);
            PoseRopeway(way);
        }

        private static void Pylon(WorldShiftArtMesh art, Vector3 foot, float height)
        {
            float spread = 3.2f, topSpread = 1.1f;
            Vector3[] baseCorners = { new Vector3(-spread, 0, -spread), new Vector3(spread, 0, -spread), new Vector3(spread, 0, spread), new Vector3(-spread, 0, spread) };
            Vector3 Corner(int i, float h) => foot + Vector3.Lerp(baseCorners[i], baseCorners[i] * (topSpread / spread), h / height) + Vector3.up * h;
            for (int i = 0; i < 4; i++) art.Beam(Corner(i, 0f), Corner(i, height), .28f, .28f, PylonSteel);
            // Cross bracing every few metres.
            for (float h = 0f; h < height - 4f; h += 6f)
                for (int i = 0; i < 4; i++)
                {
                    art.Beam(Corner(i, h), Corner((i + 1) % 4, h + 6f), .12f, .12f, PylonSteel);
                    art.Beam(Corner(i, h + 6f), Corner((i + 1) % 4, h + 6f), .14f, .14f, PylonSteel);
                }
            // Crossarm carrying both cables, with sheave wheels.
            Vector3 head = foot + Vector3.up * height;
            art.Box(head, new Vector3(1.4f, .6f, 7.4f), PylonSteel);
            foreach (float z in new[] { -2.6f, 2.6f })
                art.Wheel(head + new Vector3(0f, .4f, z), .55f, .25f, CableSteel);
        }

        private Transform CableCar(Transform parent, Color paint)
        {
            var cabin = new GameObject("Cable car cabin").transform;
            cabin.SetParent(parent, false);
            var body = new WorldShiftArtMesh();
            Color roof = new Color(.88f, .88f, .86f), trim = new Color(.16f, .17f, .19f);
            // Hanger arm and grip up to the cable.
            body.Beam(Vector3.zero, new Vector3(0f, -2.6f, 0f), .18f, .18f, trim);
            body.Box(new Vector3(0f, -.1f, 0f), new Vector3(.9f, .35f, .4f), trim);
            body.Beam(new Vector3(0f, -2.6f, 0f), new Vector3(0f, -2.9f, -1.1f), .14f, .14f, trim);
            body.Beam(new Vector3(0f, -2.6f, 0f), new Vector3(0f, -2.9f, 1.1f), .14f, .14f, trim);
            // Rounded cabin: painted body, pale roof cap, dark floor skirt.
            body.ChamferBox(new Vector3(0f, -4.4f, 0f), new Vector3(2.7f, 2.6f, 2.3f), .35f, paint);
            body.ChamferBox(new Vector3(0f, -3.0f, 0f), new Vector3(2.4f, .35f, 2.0f), .15f, roof);
            body.ChamferBox(new Vector3(0f, -5.75f, 0f), new Vector3(2.5f, .25f, 2.1f), .1f, trim);
            Render(cabin, "Cable car body and hanger", body, surface);
            // Window band glows warmly at Night.
            var glass = new WorldShiftArtMesh();
            foreach (float z in new[] { -1.16f, 1.16f })
                glass.Box(new Vector3(0f, -4.05f, z), new Vector3(2.2f, .85f, .04f), Color.white);
            foreach (float x in new[] { -1.36f, 1.36f })
                glass.Box(new Vector3(x, -4.05f, 0f), new Vector3(.04f, .85f, 1.8f), Color.white);
            Render(cabin, "Cable car windows", glass, windows);
            return cabin;
        }

        private void PoseRopeway(Ropeway way)
        {
            float length = Vector3.Distance(way.From, way.To);
            for (int i = 0; i < way.Cabins.Count; i++)
            {
                // One line climbs, the other descends; cabins vanish into the stations at each end and loop.
                float t = Mathf.Repeat(way.Offsets[i] + skyClock * CabinSpeed / length, 1f);
                if (way.Lines[i] == 1) t = 1f - t;
                Vector3 p = way.At(t, way.Lines[i]);
                // Slight swing as the cabin rides the cable.
                way.Cabins[i].localPosition = p;
                way.Cabins[i].localRotation = Quaternion.Euler(1.5f * Mathf.Sin(skyClock * 1.3f + i), 0f, 2.5f * Mathf.Sin(skyClock * .9f + i * 1.7f));
            }
        }

        // ---------- Eagles ----------

        private void BuildEagle(float at, float lateral, float height, float radius, int seed)
        {
            var root = Root("Soaring eagle", at, 0f);
            var eagle = new Eagle
            {
                Centre = new Vector3(lateral, height, 0f), Radius = radius,
                Speed = (seed % 2 == 0 ? 1f : -1f) * 11f / radius, Phase = seed * 1.3f, FlapPhase = seed * .7f
            };
            eagle.Body = new GameObject("Eagle").transform;
            eagle.Body.SetParent(root, false);
            // Larger than life so the silhouette reads against the sky from the road.
            eagle.Body.localScale = Vector3.one * 3.4f;
            Color plumage = new Color(.27f, .17f, .10f), white = new Color(.96f, .95f, .91f), beak = new Color(.98f, .76f, .16f);
            var body = new WorldShiftArtMesh();
            // Faces +Z: dark body, white head and tail (bald eagle), hooked yellow beak.
            WorldShiftDesertObstacleArt.Ellipsoid(body, Vector3.zero, new Vector3(.20f, .18f, .55f), plumage, 12, 8);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(0f, .06f, .55f), new Vector3(.14f, .14f, .17f), white, 10, 6);
            body.Box(new Vector3(0f, .03f, .76f), new Vector3(.07f, .07f, .14f), beak);
            body.Box(new Vector3(0f, -.01f, .83f), new Vector3(.05f, .06f, .05f), beak);
            foreach (float side in new[] { -1f, 1f })
                body.Box(new Vector3(side * .1f, .1f, .64f), new Vector3(.02f, .03f, .03f), Color.black);
            Vector3 t0 = new Vector3(-.10f, 0f, -.45f), t1 = new Vector3(.10f, 0f, -.45f), t2 = new Vector3(.24f, -.02f, -.95f), t3 = new Vector3(-.24f, -.02f, -.95f);
            body.Quad(t0, t1, t2, t3, white); body.Quad(t3, t2, t1, t0, white);
            Render(eagle.Body, "Eagle body, white head and tail", body, surface);
            eagle.LeftWing = EagleWing(eagle.Body, -1f, plumage);
            eagle.RightWing = EagleWing(eagle.Body, 1f, plumage);
            eagles.Add(eagle);
            PoseEagle(eagle);
        }

        // Broad wing with a dark leading edge and splayed primary "fingers" at the tip.
        private Transform EagleWing(Transform body, float side, Color plumage)
        {
            var wing = new GameObject(side < 0f ? "Eagle left wing" : "Eagle right wing").transform;
            wing.SetParent(body, false);
            wing.localPosition = new Vector3(side * .16f, .06f, .05f);
            var art = new WorldShiftArtMesh();
            Color under = plumage * 1.25f; under.a = 1f;
            Vector3 P(float x, float z) => new Vector3(x * side, 0f, z);
            void Both(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour) { art.Quad(a, b, c, d, colour); art.Quad(d, c, b, a, colour); }
            Both(P(0f, .22f), P(.75f, .20f), P(.75f, -.28f), P(0f, -.30f), plumage);
            Both(P(.75f, .20f), P(1.35f, .12f), P(1.35f, -.20f), P(.75f, -.28f), under);
            for (int f = 0; f < 5; f++)
            {
                float z = Mathf.Lerp(.10f, -.18f, f / 4f);
                art.Beam(P(1.33f, z), P(1.62f - .04f * Mathf.Abs(f - 2), z - .03f * (f - 2)), .05f, .02f, plumage);
            }
            Render(wing, "Eagle wing", art, surface);
            return wing;
        }

        private void PoseEagle(Eagle eagle)
        {
            float angle = eagle.Phase + skyClock * eagle.Speed;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * eagle.Radius;
            // Gentle rise and fall on the thermal.
            eagle.Body.localPosition = eagle.Centre + offset + Vector3.up * (2.5f * Mathf.Sin(angle * 2f + eagle.Phase));
            Vector3 travel = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Mathf.Sign(eagle.Speed);
            float bank = 22f * Mathf.Sign(eagle.Speed);
            eagle.Body.localRotation = Quaternion.LookRotation(travel, Vector3.up) * Quaternion.Euler(0f, 0f, bank);
            // Mostly gliding with a slight upward dihedral; a few slow wingbeats every eight seconds.
            float cycle = Mathf.Repeat(skyClock + eagle.FlapPhase, 8f);
            float flap = cycle < 1.8f ? 28f * Mathf.Sin(cycle / 1.8f * Mathf.PI * 3f) : 0f;
            float lift = 8f + flap;
            eagle.LeftWing.localRotation = Quaternion.Euler(0f, 0f, -lift);
            eagle.RightWing.localRotation = Quaternion.Euler(0f, 0f, lift);
        }

        private void AnimateSkyLife()
        {
            if (ropeways.Count == 0 && eagles.Count == 0 && flowMaterials.Count == 0 && gapBeacons.Count == 0) return;
            skyClock += Time.deltaTime;
            foreach (var way in ropeways) PoseRopeway(way);
            foreach (var eagle in eagles) PoseEagle(eagle);
            AnimateMountainDressing(skyClock);
            FlashGapBeacons(skyClock);
            // Twinkling Mountain stars share the same paused-with-the-game clock.
            sky.SetFloat("_SkyTime", skyClock);
        }
    }
}
