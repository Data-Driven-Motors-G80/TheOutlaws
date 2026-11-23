using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain's airborne mover: a big bald eagle soaring across a jump. Drawn well over life size (about a 10 m
    /// wingspan) so it reads from the high chase camera; wings level, dark body, white head and tail, yellow beak,
    /// splayed wingtips. It faces the way it glides, beats its wings on the Day clock (so Night freezes it) and carries the shared direction arrow.
    /// Collision is the unchanged 1.1 m airborne sphere; no colliders, no ring underneath.
    /// </summary>
    internal sealed class WorldShiftMountainEagle : MonoBehaviour
    {
        private static Material finish;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Transform model, leftWing, rightWing, arrow;
        private float yaw, lastClock = float.NaN;
        public const float Scale = 2.2f;

        public static WorldShiftMountainEagle Build(Transform parent, Transform cueParent, float radius)
        {
            var go = new GameObject("Gliding eagle over the jump");
            go.transform.SetParent(parent, false);
            var eagle = go.AddComponent<WorldShiftMountainEagle>();
            // The model is drawn facing +Z; turn it so yaw 0 faces +X (road right), like the goats and camels.
            eagle.model = new GameObject("Eagle model").transform;
            eagle.model.SetParent(go.transform, false);
            eagle.model.localRotation = Quaternion.Euler(0f, 90f, 0f);
            eagle.model.localScale = Vector3.one * Scale;
            Color plumage = new Color(.30f, .19f, .10f), white = new Color(.97f, .96f, .92f), beak = new Color(1f, .78f, .15f);
            var body = new WorldShiftArtMesh();
            // Body: a long dark torso, a white head on a short neck, a hooked yellow beak.
            WorldShiftDesertObstacleArt.Ellipsoid(body, Vector3.zero, new Vector3(.22f, .17f, .58f), plumage, 12, 8);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(0f, .03f, .5f), new Vector3(.15f, .14f, .16f), white, 10, 6);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(0f, .05f, .64f), new Vector3(.12f, .12f, .14f), white, 10, 6);
            body.Box(new Vector3(0f, .03f, .8f), new Vector3(.07f, .07f, .1f), beak);
            body.Triangle(new Vector3(-.035f, .0f, .85f), new Vector3(.035f, .0f, .85f), new Vector3(0f, -.06f, .87f), beak, true);
            foreach (float side in new[] { -1f, 1f })
                body.Box(new Vector3(side * .1f, .09f, .7f), new Vector3(.02f, .03f, .03f), Color.black);
            // Fanned white tail with a ragged tip, and yellow feet tucked back under it.
            for (int f = -3; f <= 3; f++)
            {
                float a = f * .11f, len = .62f - .03f * Mathf.Abs(f);
                Vector3 root = new Vector3(0f, .02f, -.45f), tip = root + new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * len;
                Vector3 across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * .07f;
                body.Quad(root - across * .6f, root + across * .6f, tip + across, tip - across, white);
                body.Quad(tip - across, tip + across, root + across * .6f, root - across * .6f, white);
            }
            foreach (float side in new[] { -1f, 1f })
                body.Beam(new Vector3(side * .08f, -.12f, -.2f), new Vector3(side * .09f, -.16f, -.48f), .05f, .05f, beak);
            eagle.Draw(eagle.model.gameObject, body, "Eagle body, white head and tail");
            eagle.leftWing = eagle.Wing(-1f, plumage);
            eagle.rightWing = eagle.Wing(1f, plumage);
            // The shared arrow rides above the wings on the non-turning cue parent.
            eagle.arrow = WorldShiftMoverArrow.Attach(cueParent, radius * 1.1f, new Vector3(0f, radius + 1.5f, -radius * .25f),
                Quaternion.identity, Vector3.back);
            eagle.Pose(0f, 1f);
            return eagle;
        }

        // A soaring eagle's wing seen from above: a bent wrist, a light covert band along the leading edge, dark flight
        // feathers with a ragged trailing edge, and six splayed "finger" primaries that curl up at the tip.
        private Transform Wing(float side, Color plumage)
        {
            var wing = new GameObject(side < 0f ? "Eagle left wing" : "Eagle right wing").transform;
            wing.SetParent(model, false);
            wing.localPosition = new Vector3(side * .16f, .06f, .05f);
            var art = new WorldShiftArtMesh();
            Color coverts = new Color(.46f, .31f, .16f), flight = new Color(.19f, .12f, .07f);
            // Slight upward dihedral: the wing rises toward the tip.
            Vector3 P(float x, float z) => new Vector3(x * side, x * .07f, z);
            void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
            {
                if (side > 0f) { art.Quad(a, b, c, d, colour); art.Quad(d, c, b, a, colour); }
                else { art.Quad(d, c, b, a, colour); art.Quad(a, b, c, d, colour); }
            }
            float[] x = { 0f, .55f, 1.0f, 1.45f };
            float[] lead = { .26f, .33f, .36f, .2f }, mid = { .02f, .06f, .08f, .02f }, trail = { -.34f, -.38f, -.3f, -.14f };
            for (int i = 0; i < 3; i++)
            {
                Panel(P(x[i], lead[i]), P(x[i + 1], lead[i + 1]), P(x[i + 1], mid[i + 1]), P(x[i], mid[i]), i == 0 ? plumage : coverts);
                Panel(P(x[i], mid[i]), P(x[i + 1], mid[i + 1]), P(x[i + 1], trail[i + 1]), P(x[i], trail[i]), flight);
            }
            // Ragged trailing edge: a feather tip every 0.15 m.
            for (float t = .05f; t < 1.4f; t += .15f)
            {
                int seg = Mathf.Min(2, (int)(t / .5f)); float k = Mathf.InverseLerp(x[seg], x[seg + 1], t);
                float edge = Mathf.Lerp(trail[seg], trail[seg + 1], k);
                Vector3 a = P(t - .06f, edge), b = P(t + .06f, edge), c = P(t + .01f, edge - .09f);
                art.Triangle(a, b, c, flight, true);
            }
            // Six splayed primaries.
            for (int f = 0; f < 6; f++)
            {
                float z = Mathf.Lerp(.18f, -.12f, f / 5f), spread = (f - 2.5f) * .09f, len = .58f - .04f * Mathf.Abs(f - 2.5f);
                Vector3 root = P(1.42f, z), dir = new Vector3(Mathf.Cos(spread) * side, .12f, Mathf.Sin(spread)).normalized;
                Vector3 tip = root + dir * len, across = new Vector3(0f, 0f, .05f), narrow = new Vector3(0f, 0f, .02f);
                Panel(root - across, tip - narrow, tip + narrow, root + across, flight);
            }
            Draw(wing.gameObject, art, "Eagle wing");
            return wing;
        }

        /// <summary>Level, soaring flight facing the glide direction; slow wingbeats now and then on the Day clock.</summary>
        public void Pose(float clock, float heading)
        {
            // It faces the way it glides, turned a little toward the oncoming car.
            float target = heading >= 0f ? 15f : 165f;
            yaw = float.IsNaN(lastClock) ? target : Mathf.MoveTowardsAngle(yaw, target, 240f * Mathf.Abs(clock - lastClock));
            lastClock = clock;
            // Wings level (horizontal) with a gentle rocking roll, like an eagle riding the wind.
            float roll = 5f * Mathf.Sin(clock * .9f);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(roll, 0f, 0f);
            // Mostly gliding; every few seconds a short burst of deep, slow wingbeats.
            float burst = Mathf.Clamp01(Mathf.Sin(clock * .8f) * 2.5f);
            float lift = 4f + 20f * burst * Mathf.Sin(clock * Mathf.PI * 2f * 1.1f);
            leftWing.localRotation = Quaternion.Euler(0f, 0f, -lift);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, lift);
            model.localPosition = Vector3.up * (.06f * burst * Mathf.Sin(clock * Mathf.PI * 2f * 1.1f + 1.4f));
        }

        private void Draw(GameObject target, WorldShiftArtMesh art, string label)
        {
            if (finish == null)
            {
                finish = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Mountain eagle finish" };
                finish.SetFloat("_Emission", .22f);
                finish.SetFloat("_Specular", .02f);
            }
            Mesh mesh = art.Mesh(label); meshes.Add(mesh);
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = finish;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (arrow != null) { if (Application.isPlaying) Destroy(arrow.gameObject); else DestroyImmediate(arrow.gameObject); }
        }
    }
}
