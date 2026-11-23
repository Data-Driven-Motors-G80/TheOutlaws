using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain sideways movers: a goat trotting across the road (the rockfall `Crossing` hazard). It walks left and
    /// right exactly like the old crossing boulder, turns to face its direction of travel, trots in step with its
    /// ground speed and freezes mid-stride in Night. Two looks alternate: a shaggy white mountain goat and a tan
    /// alpine ibex with big swept-back horns. Collision is the unchanged 1 m sphere; no colliders.
    /// </summary>
    internal sealed class WorldShiftMountainGoat : MonoBehaviour
    {
        private const float Stride = 1.25f;
        private static Material finish;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Transform> legs = new List<Transform>();
        private Transform body, head, tail, arrow;
        private float speed, offset, yaw, lastClock = float.NaN;

        public static WorldShiftMountainGoat Build(Transform parent, Transform cueParent, float radius, float speed, int variant)
        {
            bool ibex = (variant & 1) == 1;
            var go = new GameObject(ibex ? "Alpine ibex crossing the road" : "Mountain goat crossing the road");
            go.transform.SetParent(parent, false);
            // The hazard sits at the sphere centre, one radius above the road: stand the goat on the asphalt.
            go.transform.localPosition = Vector3.down * radius;
            // A touch larger than life so it reads from the driving camera; nose to tail it stays close to the 2 m sphere.
            go.transform.localScale = Vector3.one * 1.25f;
            var goat = go.AddComponent<WorldShiftMountainGoat>();
            goat.speed = Mathf.Max(.5f, Mathf.Abs(speed));
            goat.offset = variant * 1.7f;
            float k = radius;
            Color coat = ibex ? new Color(.66f, .50f, .33f) : new Color(.95f, .94f, .90f);
            Color shade = ibex ? new Color(.47f, .34f, .22f) : new Color(.80f, .78f, .72f);
            Color belly = ibex ? new Color(.80f, .67f, .49f) : new Color(.86f, .84f, .79f);
            Color stripe = ibex ? new Color(.24f, .17f, .11f) : shade;
            Color horn = ibex ? new Color(.52f, .44f, .34f) : new Color(.09f, .09f, .10f);
            Color ink = new Color(.07f, .07f, .08f);

            // Torso, shoulder hump, rump and belly; the white goat adds a shaggy chest ruff and flank fringe.
            goat.body = new GameObject("Goat body").transform;
            goat.body.SetParent(go.transform, false);
            var torso = new WorldShiftArtMesh();
            Ellipsoid(torso, new Vector3(0f, .95f, 0f) * k, new Vector3(.62f, .30f, .27f) * k, coat);
            Ellipsoid(torso, new Vector3(.30f, 1.08f, 0f) * k, new Vector3(.34f, .30f, .28f) * k, coat);
            Ellipsoid(torso, new Vector3(-.40f, .98f, 0f) * k, new Vector3(.27f, .27f, .26f) * k, coat);
            Ellipsoid(torso, new Vector3(.02f, .76f, 0f) * k, new Vector3(.48f, .12f, .21f) * k, belly);
            if (!ibex)
            {
                Ellipsoid(torso, new Vector3(.47f, .90f, 0f) * k, new Vector3(.16f, .24f, .22f) * k, coat);
                // Lumpy winter coat hanging below the flanks.
                for (int i = 0; i < 6; i++)
                    foreach (float side in new[] { -1f, 1f })
                        Ellipsoid(torso, new Vector3(-.42f + i * .17f, .74f, side * .15f) * k, new Vector3(.11f, .12f, .10f) * k,
                            i % 2 == 0 ? coat : shade);
            }
            // Neck rising to the head.
            Tube(torso, new Vector3(.42f, 1.05f, 0f) * k, new Vector3(.66f, 1.30f, 0f) * k, .15f * k, coat);
            goat.Draw(goat.body.gameObject, torso, "Goat torso and neck");

            // Short upturned tail that flicks.
            var tailArt = new WorldShiftArtMesh();
            Ellipsoid(tailArt, new Vector3(-.07f, .03f, 0f) * k, new Vector3(.09f, .05f, .05f) * k, ibex ? stripe : coat);
            goat.tail = goat.Part(goat.body, "Goat tail", tailArt, new Vector3(-.64f, 1.08f, 0f) * k);

            // Head on a neck pivot so it nods with the trot.
            var headArt = new WorldShiftArtMesh();
            // A long goat face: skull, then a narrower muzzle with a dark nose.
            Ellipsoid(headArt, new Vector3(.12f, .07f, 0f) * k, new Vector3(.19f, .12f, .11f) * k, coat);
            Ellipsoid(headArt, new Vector3(.30f, .01f, 0f) * k, new Vector3(.13f, .075f, .075f) * k, ibex ? shade : coat);
            headArt.Box(new Vector3(.42f, .02f, 0f) * k, new Vector3(.03f, .05f, .08f) * k, ink);
            foreach (float side in new[] { -1f, 1f })
            {
                headArt.Box(new Vector3(.19f, .13f, side * .095f) * k, new Vector3(.045f, .045f, .02f) * k, ink);
                // Leaf-shaped ears out to the side, just behind the horns.
                Ellipsoid(headArt, new Vector3(.0f, .14f, side * .15f) * k, new Vector3(.05f, .025f, .09f) * k, shade);
                Horn(headArt, side, k, ibex, horn);
            }
            // Beard: a white tuft on the mountain goat, a short dark goatee on the ibex.
            Tube(headArt, new Vector3(.22f, -.06f, 0f) * k, new Vector3(.19f, ibex ? -.16f : -.26f, 0f) * k, (ibex ? .035f : .055f) * k,
                ibex ? stripe : coat);
            goat.head = goat.Part(goat.body, "Goat head, horns and beard", headArt, new Vector3(.66f, 1.30f, 0f) * k);

            // Legs pivot at shoulder and hip. Order: front-left, front-right, back-left, back-right (diagonal trot).
            foreach (float x in new[] { .36f, -.40f })
                foreach (float side in new[] { 1f, -1f })
                {
                    var leg = new WorldShiftArtMesh();
                    // The white goat wears shaggy "pantaloons" on its upper legs.
                    Tube(leg, Vector3.zero, new Vector3(0f, -.40f, 0f) * k, (ibex ? .075f : x > 0f ? .11f : .095f) * k, coat);
                    Tube(leg, new Vector3(0f, -.40f, 0f) * k, new Vector3(0f, -.74f, 0f) * k, .045f * k, ibex ? stripe : shade);
                    leg.Box(new Vector3(.015f, -.77f, 0f) * k, new Vector3(.10f, .06f, .09f) * k, ink);
                    goat.legs.Add(goat.Part(goat.body, side > 0f ? "Goat left leg" : "Goat right leg", leg, new Vector3(x, .80f, side * .14f) * k));
                }

            // The shared direction arrow rides above the horns on the non-turning cue parent.
            goat.arrow = WorldShiftMoverArrow.Attach(cueParent, radius * .84f, new Vector3(0f, radius + 1.25f, -radius * .25f),
                Quaternion.identity, Vector3.back);
            goat.Pose(0f, 1f);
            return goat;
        }

        // Black dagger horns curving back on the mountain goat; long ridged scimitar horns on the ibex.
        private static void Horn(WorldShiftArtMesh art, float side, float k, bool ibex, Color colour)
        {
            Vector3[] path = ibex
                ? new[] { new Vector3(.08f, .16f, .05f), new Vector3(.06f, .34f, .07f), new Vector3(-.04f, .50f, .09f),
                    new Vector3(-.20f, .58f, .10f), new Vector3(-.34f, .54f, .11f) }
                : new[] { new Vector3(.08f, .17f, .05f), new Vector3(.06f, .30f, .06f), new Vector3(-.01f, .38f, .065f) };
            float width = ibex ? .075f : .05f;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector3 a = Vector3.Scale(path[i], new Vector3(1f, 1f, side)) * k, b = Vector3.Scale(path[i + 1], new Vector3(1f, 1f, side)) * k;
                float w = Mathf.Lerp(width, width * .45f, (float)i / (path.Length - 1)) * k;
                art.Beam(a, b, w, w, colour);
                // Ibex horn ridges.
                if (ibex)
                {
                    Color ridge = colour * .78f; ridge.a = 1f;
                    art.Beam(Vector3.Lerp(a, b, .42f), Vector3.Lerp(a, b, .58f), w * 1.3f, w * 1.3f, ridge);
                }
            }
        }

        /// <summary>Faces the direction of travel and trots on the Day clock, so Night freezes it mid-stride.</summary>
        public void Pose(float clock, float heading)
        {
            float target = heading >= 0f ? 0f : 180f;
            yaw = float.IsNaN(lastClock) ? target : Mathf.MoveTowardsAngle(yaw, target, 600f * Mathf.Abs(clock - lastClock));
            lastClock = clock;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float step = clock * speed / Stride * Mathf.PI * 2f + offset;
            float swing = 30f * Mathf.Sin(step);
            body.localPosition = Vector3.up * (.035f * Mathf.Abs(Mathf.Cos(step)));
            body.localRotation = Quaternion.Euler(0f, 0f, 1.5f * Mathf.Sin(step * 2f));
            head.localRotation = Quaternion.Euler(0f, 0f, -10f + 5f * Mathf.Sin(step * 2f + .6f));
            tail.localRotation = Quaternion.Euler(0f, 0f, 20f + 15f * Mathf.Sin(step * 3f));
            // Diagonal pairs move together: front-left with back-right, front-right with back-left.
            for (int i = 0; i < legs.Count; i++)
                legs[i].localRotation = Quaternion.Euler(0f, 0f, i == 0 || i == 3 ? swing : -swing);
        }

        private static void Ellipsoid(WorldShiftArtMesh art, Vector3 centre, Vector3 radii, Color colour)
            => WorldShiftDesertObstacleArt.Ellipsoid(art, centre, radii, colour, 12, 8);

        private static void Tube(WorldShiftArtMesh art, Vector3 a, Vector3 b, float radius, Color colour)
            => WorldShiftDesertObstacleArt.Tube(art, a, b, radius, new[] { colour }, 8);

        private Transform Part(Transform parent, string label, WorldShiftArtMesh art, Vector3 pivot)
        {
            var part = new GameObject(label);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pivot;
            Draw(part, art, label);
            return part.transform;
        }

        private void Draw(GameObject target, WorldShiftArtMesh art, string label)
        {
            if (finish == null)
            {
                finish = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Mountain goat finish" };
                finish.SetFloat("_Emission", .3f);
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
            // The arrow lives on the cue parent, outside this hierarchy.
            if (arrow != null) { if (Application.isPlaying) Destroy(arrow.gameObject); else DestroyImmediate(arrow.gameObject); }
        }
    }
}
