using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Desert sideways movers are animals that really move that way: camels walking across the road
    /// (single sliders), camel caravans (ground sliding blocks), vultures (airborne spheres) and a vulture
    /// flock (airborne sliding blocks). Animation runs on the hazard's Day clock, so Night and pause freeze
    /// them mid-step / mid-flap. Collision is unchanged; no colliders.
    /// </summary>
    internal sealed class WorldShiftDesertMover : MonoBehaviour
    {
        private readonly List<WorldShiftDesertCreature> creatures = new List<WorldShiftDesertCreature>();
        private Transform arrow;

        public int CreatureCount => creatures.Count;

        public void Pose(float clock, float heading)
        {
            foreach (var creature in creatures) creature.Pose(clock, heading);
        }

        /// <summary>One camel crossing the road (Desert slider).</summary>
        public static WorldShiftDesertMover Camel(Transform parent, Vector3 centre, Vector3 size)
        {
            var mover = Root(parent, centre, size, "Crossing camel");
            mover.creatures.Add(WorldShiftDesertCreature.Camel(mover.transform, Vector3.zero, 1f, true, 0f, 0));
            mover.arrow = WorldShiftMoverArrow.Attach(mover.transform, Mathf.Min(size.x * .38f, 1.8f),
                new Vector3(0f, 2.75f, -size.z * .25f), Quaternion.identity, Vector3.back);
            return mover;
        }

        /// <summary>Three camels nose to tail (Desert ground sliding block).</summary>
        public static WorldShiftDesertMover Caravan(Transform parent, Vector3 centre, Vector3 size)
        {
            var mover = Root(parent, centre, size, "Camel caravan");
            int count = Mathf.Max(1, Mathf.RoundToInt(size.x / 3.3f));
            float spacing = size.x / count;
            for (int i = 0; i < count; i++)
                mover.creatures.Add(WorldShiftDesertCreature.Camel(mover.transform,
                    new Vector3(-size.x * .5f + spacing * (i + .5f), 0f, 0f), 1f, true, i * 1.7f, i));
            mover.arrow = WorldShiftMoverArrow.Attach(mover.transform, Mathf.Min(size.x * .38f, 1.8f),
                new Vector3(0f, 2.85f, -size.z * .25f), Quaternion.identity, Vector3.back);
            return mover;
        }

        /// <summary>A staggered two-row flock filling the block (Desert airborne sliding block).</summary>
        public static WorldShiftDesertMover Flock(Transform parent, Vector3 centre, Vector3 size)
        {
            var mover = Root(parent, centre, size, "Vulture flock");
            float w = size.x;
            float[] low = { -.375f, -.125f, .125f, .375f }, high = { -.25f, 0f, .25f };
            for (int i = 0; i < low.Length; i++)
                mover.creatures.Add(WorldShiftDesertCreature.Vulture(mover.transform, new Vector3(low[i] * w, size.y * .30f, (i % 2) * .2f - .1f), 1.25f, i * 1.3f));
            for (int i = 0; i < high.Length; i++)
                mover.creatures.Add(WorldShiftDesertCreature.Vulture(mover.transform, new Vector3(high[i] * w, size.y * .72f, (i % 2) * -.2f + .1f), 1.25f, i * 1.3f + .6f));
            mover.arrow = WorldShiftMoverArrow.Attach(mover.transform, Mathf.Min(w * .38f, 1.8f),
                new Vector3(0f, size.y + .55f, -size.z * .25f), Quaternion.identity, Vector3.back);
            return mover;
        }

        /// <summary>A single large vulture on the airborne crossing sphere; the arrow stays on the non-rotating cue parent.</summary>
        public static WorldShiftDesertMover Vulture(Transform rollingParent, Transform cueParent, float radius)
        {
            var go = new GameObject("Desert vulture");
            go.transform.SetParent(rollingParent, false);
            var mover = go.AddComponent<WorldShiftDesertMover>();
            // Visibly larger than the 1.1 m collision sphere; from the driver's view the wings are foreshortened.
            mover.creatures.Add(WorldShiftDesertCreature.Vulture(go.transform, Vector3.zero, 1.6f * radius / 1.1f, 0f));
            mover.arrow = WorldShiftMoverArrow.Attach(cueParent, radius * .84f, new Vector3(0, radius + .75f, -radius * .25f),
                Quaternion.identity, Vector3.back);
            return mover;
        }

        private static WorldShiftDesertMover Root(Transform parent, Vector3 centre, Vector3 size, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre - Vector3.up * size.y * .5f;
            return go.AddComponent<WorldShiftDesertMover>();
        }

        private void OnDestroy()
        {
            // The rock vulture's arrow lives outside this hierarchy.
            if (arrow != null && !arrow.IsChildOf(transform))
            {
                if (Application.isPlaying) Destroy(arrow.gameObject); else DestroyImmediate(arrow.gameObject);
            }
        }
    }

    /// <summary>One animated animal. Faces +X at yaw 0; turns to face its direction of travel.</summary>
    internal sealed class WorldShiftDesertCreature : MonoBehaviour
    {
        private enum Kind { Vulture, Camel }
        private static Material finish;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Transform> limbs = new List<Transform>();
        private Kind kind;
        private Vector3 basePosition;
        private float scale, offset, yaw, lastClock = float.NaN;

        public Transform Limb(int i) => limbs[i];

        // ---------- Vulture ----------

        public static WorldShiftDesertCreature Vulture(Transform parent, Vector3 at, float scale, float phaseOffset)
        {
            var c = Create(parent, at, "Vulture", Kind.Vulture, scale, phaseOffset);
            float k = scale;
            Color feathers = new Color(.15f, .11f, .08f), ruff = new Color(.80f, .74f, .66f);
            Color skin = new Color(.82f, .30f, .26f), neck = new Color(.62f, .40f, .38f), beak = new Color(.93f, .88f, .70f);
            var body = new WorldShiftArtMesh();
            WorldShiftDesertObstacleArt.Ellipsoid(body, Vector3.zero, new Vector3(.42f, .22f, .24f) * k, feathers, 12, 8);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(.30f, .08f, 0) * k, new Vector3(.15f, .16f, .19f) * k, ruff, 10, 6);
            WorldShiftDesertObstacleArt.Tube(body, new Vector3(.33f, .10f, 0) * k, new Vector3(.50f, .20f, 0) * k, .055f * k, new[] { neck }, 8);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(.57f, .22f, 0) * k, new Vector3(.11f, .085f, .08f) * k, skin, 10, 6);
            body.Box(new Vector3(.68f, .20f, 0) * k, new Vector3(.09f, .05f, .05f) * k, beak);
            body.Box(new Vector3(.71f, .17f, 0) * k, new Vector3(.035f, .05f, .045f) * k, beak);
            foreach (float side in new[] { -1f, 1f })
                body.Box(new Vector3(.60f, .25f, side * .06f) * k, new Vector3(.025f, .025f, .02f) * k, Color.black);
            Vector3 t0 = new Vector3(-.34f, .02f, -.10f) * k, t1 = new Vector3(-.34f, .02f, .10f) * k,
                t2 = new Vector3(-.66f, -.01f, .20f) * k, t3 = new Vector3(-.66f, -.01f, -.20f) * k;
            body.Quad(t0, t1, t2, t3, feathers); body.Quad(t3, t2, t1, t0, feathers);
            c.Draw(c.gameObject, body, "Vulture body, ruff and red head");
            c.limbs.Add(c.Part("Vulture right wing", WingMesh(1f, k), new Vector3(.04f, .12f, .18f) * k));
            c.limbs.Add(c.Part("Vulture left wing", WingMesh(-1f, k), new Vector3(.04f, .12f, -.18f) * k));
            c.Pose(0f, 1f);
            return c;
        }

        // Dark coverts in front, silver flight feathers behind (readable against the Night sky), splayed fingers.
        private static WorldShiftArtMesh WingMesh(float side, float k)
        {
            Color covert = new Color(.17f, .13f, .10f), silver = new Color(.72f, .69f, .64f), finger = new Color(.12f, .09f, .07f);
            var art = new WorldShiftArtMesh();
            Vector3 P(float x, float z) => new Vector3(x, 0, z * side) * k;
            void Both(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour) { art.Quad(a, b, c, d, colour); art.Quad(d, c, b, a, colour); }
            float[] span = { 0f, .62f, .88f }, lead = { .22f, .16f, .06f }, mid = { -.04f, -.04f, -.05f }, trail = { -.32f, -.27f, -.18f };
            for (int i = 0; i < 2; i++)
            {
                Both(P(lead[i], span[i]), P(lead[i + 1], span[i + 1]), P(mid[i + 1], span[i + 1]), P(mid[i], span[i]), covert);
                Both(P(mid[i], span[i]), P(mid[i + 1], span[i + 1]), P(trail[i + 1], span[i + 1]), P(trail[i], span[i]), silver);
            }
            for (int f = 0; f < 5; f++)
            {
                float x = Mathf.Lerp(.04f, -.16f, f / 4f);
                art.Beam(P(x, .86f), P(x - .03f * (f - 2), 1.08f - .03f * Mathf.Abs(f - 2)), .05f * k, .02f * k, finger);
            }
            return art;
        }

        // ---------- Camel ----------

        private static readonly Color[] Blankets = { new Color(.72f, .14f, .14f), new Color(.12f, .30f, .55f), new Color(.85f, .55f, .12f) };

        public static WorldShiftDesertCreature Camel(Transform parent, Vector3 at, float scale, bool packs, float phaseOffset, int variant)
        {
            var c = Create(parent, at, "Camel", Kind.Camel, scale, phaseOffset);
            float k = scale;
            Color fur = new Color(.80f, .62f, .40f), furDark = new Color(.64f, .47f, .29f), hoof = new Color(.26f, .19f, .12f);
            Color gold = new Color(.95f, .76f, .26f), burlap = new Color(.66f, .55f, .36f);
            Color blanket = Blankets[((variant % Blankets.Length) + Blankets.Length) % Blankets.Length];
            var body = new WorldShiftArtMesh();
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(0f, 1.45f, 0) * k, new Vector3(.95f, .42f, .38f) * k, fur, 14, 8);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(-.05f, 1.90f, 0) * k, new Vector3(.48f, .40f, .31f) * k, fur, 12, 8);
            // Long neck dips forward, then rises to the head.
            WorldShiftDesertObstacleArt.Tube(body, new Vector3(.78f, 1.62f, 0) * k, new Vector3(1.22f, 1.48f, 0) * k, .17f * k, new[] { fur }, 10);
            WorldShiftDesertObstacleArt.Tube(body, new Vector3(1.20f, 1.46f, 0) * k, new Vector3(1.42f, 2.02f, 0) * k, .14f * k, new[] { fur }, 10);
            WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(1.56f, 2.08f, 0) * k, new Vector3(.28f, .13f, .12f) * k, furDark, 10, 6);
            foreach (float side in new[] { -1f, 1f })
            {
                body.Box(new Vector3(1.42f, 2.22f, side * .08f) * k, new Vector3(.06f, .09f, .04f) * k, furDark);
                body.Box(new Vector3(1.62f, 2.13f, side * .1f) * k, new Vector3(.03f, .03f, .02f) * k, Color.black);
            }
            body.Beam(new Vector3(-.94f, 1.55f, 0) * k, new Vector3(-1.04f, 1.12f, 0) * k, .06f * k, .06f * k, furDark);
            // Saddle blanket over the hump, gold trim, and side packs.
            body.ChamferBox(new Vector3(-.05f, 1.62f, 0) * k, new Vector3(1.1f, .5f, .8f) * k, .05f * k, blanket);
            foreach (float side in new[] { -1f, 1f })
                body.Box(new Vector3(-.05f, 1.39f, side * .40f) * k, new Vector3(1.12f, .06f, .02f) * k, gold);
            if (packs)
                foreach (float side in new[] { -1f, 1f })
                    WorldShiftDesertObstacleArt.Ellipsoid(body, new Vector3(-.1f, 1.32f, side * .52f) * k, new Vector3(.36f, .28f, .16f) * k, burlap, 10, 6);
            c.Draw(c.gameObject, body, "Camel body, hump and saddle");
            // Legs pivot at the hip; camels pace, so same-side legs swing together.
            foreach (float x in new[] { .62f, -.62f })
                foreach (float side in new[] { 1f, -1f })
                {
                    var leg = new WorldShiftArtMesh();
                    WorldShiftDesertObstacleArt.Tube(leg, Vector3.zero, new Vector3(0, -.62f, 0) * k, .11f * k, new[] { fur }, 8);
                    WorldShiftDesertObstacleArt.Tube(leg, new Vector3(0, -.62f, 0) * k, new Vector3(0, -1.15f, 0) * k, .075f * k, new[] { furDark }, 8);
                    leg.Box(new Vector3(.02f, -1.17f, 0) * k, new Vector3(.2f, .06f, .18f) * k, hoof);
                    c.limbs.Add(c.Part(side > 0 ? "Camel left leg" : "Camel right leg", leg, new Vector3(x, 1.22f, side * .2f) * k));
                }
            c.Pose(0f, 1f);
            return c;
        }

        // ---------- Animation ----------

        public void Pose(float clock, float heading)
        {
            float target = heading >= 0f ? 0f : 180f;
            float turnRate = kind == Kind.Camel ? 360f : 720f;
            yaw = float.IsNaN(lastClock) ? target : Mathf.MoveTowardsAngle(yaw, target, turnRate * Mathf.Abs(clock - lastClock));
            lastClock = clock;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (kind == Kind.Vulture)
            {
                float phase = clock * Mathf.PI * 2f * 1.4f + offset;
                float flap = 10f + 32f * Mathf.Sin(phase);
                transform.localPosition = basePosition + Vector3.up * (.06f * scale * Mathf.Sin(phase + 1.2f));
                limbs[0].localRotation = Quaternion.Euler(-flap, 0f, 0f);
                limbs[1].localRotation = Quaternion.Euler(flap, 0f, 0f);
                return;
            }
            float step = clock * Mathf.PI * 2f * 1.3f + offset;
            float swing = 24f * Mathf.Sin(step);
            transform.localPosition = basePosition + Vector3.up * (.04f * scale * Mathf.Abs(Mathf.Sin(step)));
            // Order: front-left, front-right, back-left, back-right. Pacing gait: left pair vs right pair.
            for (int i = 0; i < limbs.Count; i++)
                limbs[i].localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? swing : -swing);
        }

        // ---------- Helpers ----------

        private static WorldShiftDesertCreature Create(Transform parent, Vector3 at, string name, Kind kind, float scale, float offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var c = go.AddComponent<WorldShiftDesertCreature>();
            c.kind = kind; c.scale = scale; c.offset = offset; c.basePosition = at;
            return c;
        }

        private Transform Part(string label, WorldShiftArtMesh art, Vector3 pivot)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = pivot;
            Draw(part, art, label);
            return part.transform;
        }

        private void Draw(GameObject target, WorldShiftArtMesh art, string label)
        {
            if (finish == null)
            {
                finish = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = "Desert animal finish" };
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
        }
    }
}
