using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>One checkpoint design in every level: a steel gantry over the actual save, a green road
    /// stripe, a CHECKPOINT sign, and a one-time light sweep, tick and chime when it saves. The gantry
    /// itself is the confirmation, so the HUD has no checkpoint pop-up. Supports stay outside the road
    /// and nothing here has a collider.</summary>
    internal sealed class WorldShiftCheckpointGantries : MonoBehaviour
    {
        private sealed class Gantry
        {
            public Transform Root, Sweep, Saved;
            public float At, PulseAt = -10f;
            public int Index;
        }

        private readonly List<Gantry> checkpointGates = new List<Gantry>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private WorldShiftCity game;
        private WorldShiftRoute route;
        private Material steelFinish, panelFinish, luminous, footingFinish;
        private AudioSource audioSource;
        private AudioClip chime;
        private Color footing;
        private int lastSeen;
        private float animationTime;

        public void Bind(WorldShiftCity city, WorldShiftRoute road)
        {
            game = city; route = road;
            steelFinish = Finish("Checkpoint galvanised steel", .35f);
            panelFinish = Finish("Checkpoint sign backing", .2f);
            footingFinish = Finish("Checkpoint footings", .1f);
            luminous = Finish("Checkpoint reflectors", 1f);
            // The base follows the level; everything else is identical everywhere.
            footing = city.Environment switch
            {
                ShiftLevel.Ocean => new Color(.37f, .48f, .48f),
                ShiftLevel.Desert => new Color(.72f, .58f, .4f),
                ShiftLevel.Snow => new Color(.78f, .82f, .86f),
                ShiftLevel.Mountain => new Color(.45f, .43f, .4f),
                _ => new Color(.55f, .56f, .56f)
            };
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; audioSource.volume = .3f;
            WorldShiftAudioGain.Bind(audioSource, game, .3f);
            BuildChime();
        }

        private Material Finish(string label, float emission)
        {
            var m = new Material(Resources.Load<Shader>("WorldShiftOceanSurface")) { name = label };
            m.SetFloat("_Emission", emission);
            m.SetFloat("_Specular", .05f);
            materials.Add(m);
            return m;
        }

        private GameObject Render(string label, WorldShiftArtMesh art, Material material, Transform parent)
        {
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            var mesh = art.Mesh(label); meshes.Add(mesh);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return obj;
        }

        /// <summary>Builds the gantry at a save point and returns its root for distance culling.</summary>
        public Transform Build(float distance)
        {
            var point = route.Sample(distance);
            var root = new GameObject("Checkpoint gate").transform; root.SetParent(transform, false);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, point.Up));
            var structure = new WorldShiftArtMesh(); var reflectors = new WorldShiftArtMesh();
            var panel = new WorldShiftArtMesh(); var stripe = new WorldShiftArtMesh(); var bases = new WorldShiftArtMesh();
            Color steel = new Color(.70f, .76f, .76f), green = WorldShiftLevelProps.CheckpointColour;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 6.3f;
                bases.ChamferBox(new Vector3(x, .25f, 0), new Vector3(.86f, .5f, .9f), .08f, footing);
                structure.Cylinder(new Vector3(x, .5f, 0), .13f, .11f, 6.1f, steel, 12);
                structure.Beam(new Vector3(x, 5.65f, 0), new Vector3(x - side * .8f, 6.50f, 0), .095f, .095f, steel);
                structure.Box(new Vector3(x, 6.48f, 0), new Vector3(.34f, .40f, .40f), steel);
                foreach (float y in new[] { 1.3f, 1.7f, 2.1f })
                    reflectors.Box(new Vector3(x, y, -.14f), new Vector3(.12f, .18f, .025f), green);
                foreach (float bolt in new[] { -.23f, .23f })
                    structure.Cylinder(new Vector3(x + bolt, .51f, bolt), .045f, .045f, .045f, steel, 8);
            }
            structure.ChamferBox(new Vector3(0, 6.50f, 0), new Vector3(12.8f, .25f, .36f), .055f, steel);
            panel.ChamferBox(new Vector3(0, 6.50f, -.20f), new Vector3(4.9f, .90f, .14f), .05f, new Color(.035f, .09f, .105f));
            foreach (float y in new[] { 6.10f, 6.90f })
                reflectors.Box(new Vector3(0, y, -.285f), new Vector3(4.78f, .045f, .025f), green);
            foreach (float x in new[] { -2.38f, 2.38f })
                reflectors.Box(new Vector3(x, 6.5f, -.285f), new Vector3(.045f, .82f, .025f), green);
            reflectors.Box(new Vector3(0, 5.99f, -.10f), new Vector3(11.6f, .055f, .055f), green * .60f);
            stripe.Box(new Vector3(0, .045f, 0), new Vector3(WorldShiftLevel.RoadWidth, .05f, .42f), green);
            var dressing = new WorldShiftArtMesh(); var lit = new WorldShiftArtMesh();
            Dress(dressing, lit);
            Render("Checkpoint gantry footings", bases, footingFinish, root);
            Render("Checkpoint gantry dressing", dressing, steelFinish, root);
            Render("Checkpoint gantry lamps", lit, luminous, root);
            Render("Checkpoint gantry structure", structure, steelFinish, root);
            Render("Checkpoint sign backing", panel, panelFinish, root);
            Render("Reflective green checkpoint accents", reflectors, luminous, root);
            Render("Actual checkpoint road stripe", stripe, luminous, root);
            var label = new GameObject("CHECKPOINT sign", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(root, false); label.transform.localPosition = new Vector3(.20f, 6.5f, -.30f);
            label.rectTransform.sizeDelta = new Vector2(4.1f, .72f);
            label.font = TMP_Settings.defaultFontAsset; label.text = "CHECKPOINT"; label.fontSize = 5f;
            label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.color = new Color(.93f, 1f, .96f);
            label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var sweep = new WorldShiftArtMesh();
            sweep.ChamferBox(Vector3.zero, new Vector3(.95f, .085f, .055f), .018f, new Color(.8f, 1f, .85f));
            var sweepRoot = Render("Checkpoint confirmation light sweep", sweep, luminous, root).transform;
            sweepRoot.localPosition = new Vector3(-5.3f, 5.99f, -.15f); sweepRoot.gameObject.SetActive(false);
            var saved = new WorldShiftArtMesh();
            saved.Beam(new Vector3(-2.17f, 6.49f, -.31f), new Vector3(-2.04f, 6.35f, -.31f), .07f, .045f, green);
            saved.Beam(new Vector3(-2.04f, 6.35f, -.31f), new Vector3(-1.78f, 6.67f, -.31f), .07f, .045f, green);
            var savedRoot = Render("Saved checkpoint tick", saved, luminous, root).transform;
            savedRoot.gameObject.SetActive(false);
            int index = 0;
            for (int i = 1; i < game.Checkpoints.Length; i++)
                if (Mathf.Abs(game.Checkpoints[i].Distance - distance) < .1f) index = i;
            checkpointGates.Add(new Gantry { Root = root, At = distance, Index = index, Sweep = sweepRoot, Saved = savedRoot });
            return root;
        }

        private void Update()
        {
            if (game != null && game.State != ShiftRunState.Paused) animationTime += Time.deltaTime;
        }

        /// <summary>Called from the presentation's visibility pass, so stepped simulation updates it too.</summary>
        public void Refresh()
        {
            if (game == null) return;
            int reached = game.CheckpointIndex;
            float distance = game.Distance;
            if (reached < lastSeen) foreach (var gate in checkpointGates) gate.PulseAt = -10f;
            if (reached > lastSeen && game.State == ShiftRunState.Running && game.CheckpointNoticeRemaining > 0)
                foreach (var gate in checkpointGates)
                    if (gate.Index == reached && distance >= gate.At)
                    {
                        gate.PulseAt = animationTime;
                        if (chime != null) audioSource.PlayOneShot(chime, .70f);
                    }
            lastSeen = reached;
            foreach (var gate in checkpointGates)
            {
                gate.Saved.gameObject.SetActive(gate.Index > 0 && reached >= gate.Index);
                if (game.CheckpointNoticeRemaining <= 0) gate.PulseAt = -10f;
                float t = (animationTime - gate.PulseAt) / .75f;
                gate.Sweep.gameObject.SetActive(t >= 0 && t < 1f);
                gate.Sweep.localPosition = new Vector3(Mathf.Lerp(-5.3f, 5.3f, Mathf.SmoothStep(0, 1, t)), 5.99f, -.15f);
            }
        }

        // Level dressing around the shared gantry. The green stripe, CHECKPOINT panel, reflectors and
        // save confirmation never change; anything below 5 m stays outside the road (|x| > 5.5).
        private void Dress(WorldShiftArtMesh art, WorldShiftArtMesh lit)
        {
            Color wood = new Color(.47f, .33f, .2f), woodDark = new Color(.32f, .22f, .13f);
            Color snow = new Color(.94f, .97f, 1f), ice = new Color(.8f, .92f, 1f);
            if (game.Course != null && game.Course.Training)
            {
                // Depot frame: yellow-and-black hazard bands up both posts and along the beam.
                Color yellow = new Color(1f, .8f, .1f), black = new Color(.06f, .06f, .07f);
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 10; i++)
                        art.Box(new Vector3(side * 6.3f, .75f + i * .5f, 0), new Vector3(.32f, .5f, .32f), i % 2 == 0 ? yellow : black);
                for (int i = 0; i < 12; i++)
                    art.Box(new Vector3(-5.9f + i * 1.07f, 6.67f, 0), new Vector3(1.07f, .1f, .38f), i % 2 == 0 ? yellow : black);
                return;
            }
            switch (game.Environment)
            {
                case ShiftLevel.City:
                    // Overhead sign gantry with small signal heads hanging from the beam.
                    foreach (float x in new[] { -4.4f, 4.4f })
                    {
                        art.Box(new Vector3(x, 6.0f, -.05f), new Vector3(.42f, 1.0f, .36f), new Color(.12f, .13f, .14f));
                        lit.Box(new Vector3(x, 6.3f, -.24f), new Vector3(.22f, .22f, .03f), new Color(.35f, .06f, .05f));
                        lit.Box(new Vector3(x, 6.0f, -.24f), new Vector3(.22f, .22f, .03f), new Color(.4f, .28f, .05f));
                        lit.Box(new Vector3(x, 5.7f, -.24f), new Vector3(.22f, .22f, .03f), WorldShiftLevelProps.CheckpointColour);
                    }
                    foreach (float side in new[] { -1f, 1f })
                        art.Box(new Vector3(side * 6.3f, 3.2f, 0), new Vector3(.36f, .06f, .36f), new Color(.5f, .52f, .54f));
                    break;
                case ShiftLevel.Desert:
                    // Weathered timber posts on adobe footings, with planks along the beam.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        art.Box(new Vector3(side * 6.3f, 3.3f, 0), new Vector3(.46f, 5.6f, .46f), wood);
                        foreach (float y in new[] { 1.6f, 3.4f, 5.0f })
                            art.Box(new Vector3(side * 6.3f, y, 0), new Vector3(.5f, .07f, .5f), woodDark);
                    }
                    foreach (float y in new[] { 6.82f, 6.18f })
                        art.Box(new Vector3(0, y, .05f), new Vector3(12.6f, .16f, .3f), wood);
                    break;
                case ShiftLevel.Snow:
                    // Snow piled on the beam, icicles, and ski-lift style cross arms on the posts.
                    art.ChamferBox(new Vector3(0, 6.72f, 0), new Vector3(12.9f, .2f, .46f), .07f, snow);
                    for (float x = -5.8f; x <= 5.8f; x += .55f)
                    {
                        if (Mathf.Abs(x) < 2.6f) continue; // keep the CHECKPOINT panel clear
                        float len = .25f + .18f * Mathf.Abs(Mathf.Sin(x * 3.1f));
                        lit.Triangle(new Vector3(x - .07f, 6.37f, -.19f), new Vector3(x + .07f, 6.37f, -.19f),
                            new Vector3(x, 6.37f - len, -.19f), ice, true);
                    }
                    foreach (float side in new[] { -1f, 1f })
                    {
                        art.Box(new Vector3(side * 6.3f, 4.6f, 0), new Vector3(1.3f, .1f, .14f), new Color(.6f, .64f, .68f));
                        art.ChamferBox(new Vector3(side * 6.3f, .55f, 0), new Vector3(1.2f, .35f, 1.1f), .12f, snow);
                    }
                    break;
                case ShiftLevel.Mountain:
                    // Alpine pass gate: dry-stone pillars the full height of the posts, a heavy timber beam with
                    // carved brackets, a small shingle roof and lanterns, instead of the default steel look.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        for (int i = 0; i < 14; i++)
                        {
                            float shade = .86f + .1f * ((i * 7 + (side > 0 ? 3 : 0)) % 4) / 3f;
                            Color block = new Color(.50f, .48f, .44f) * shade; block.a = 1f;
                            art.ChamferBox(new Vector3(side * 6.3f + ((i % 2) - .5f) * .06f, .25f + i * .45f, 0),
                                new Vector3(1.0f - i * .012f, .43f, .95f - i * .012f), .08f, block);
                        }
                        // Timber brackets under the beam and a hanging lantern.
                        art.Beam(new Vector3(side * 5.85f, 5.6f, 0), new Vector3(side * 5.1f, 6.45f, 0), .2f, .26f, wood);
                        lit.Box(new Vector3(side * 5.0f, 5.55f, -.12f), new Vector3(.22f, .32f, .22f), new Color(1f, .74f, .35f));
                        art.Box(new Vector3(side * 5.0f, 5.8f, -.12f), new Vector3(.03f, .22f, .03f), woodDark);
                    }
                    foreach (float y in new[] { 6.86f, 6.2f })
                        art.Box(new Vector3(0, y, .04f), new Vector3(13.4f, .3f, .48f), y > 6.5f ? wood : woodDark);
                    // Shingle roof over the beam.
                    foreach (float z in new[] { -1f, 1f })
                        art.Quad(new Vector3(-7f, 7.05f, z * .62f), new Vector3(7f, 7.05f, z * .62f), new Vector3(7f, 7.45f, 0), new Vector3(-7f, 7.45f, 0),
                            new Color(.36f, .27f, .2f));
                    foreach (float z in new[] { -1f, 1f })
                        art.Quad(new Vector3(7f, 7.05f, z * .62f), new Vector3(-7f, 7.05f, z * .62f), new Vector3(-7f, 7.45f, 0), new Vector3(7f, 7.45f, 0),
                            new Color(.36f, .27f, .2f));
                    break;
            }
        }

        private void BuildChime()
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * .45f)];
            float[] notes = { 523.25f, 659.25f, 783.99f };
            for (int i = 0; i < samples.Length; i++)
                for (int note = 0; note < notes.Length; note++)
                {
                    float t = (float)i / rate - note * .055f;
                    if (t < 0) continue;
                    samples[i] += Mathf.Sin(t * 2 * Mathf.PI * notes[note]) * Mathf.Min(1, t * 100f) * Mathf.Exp(-t * 13f) * .18f;
                }
            chime = AudioClip.Create("Checkpoint confirmation", samples.Length, 1, rate, false);
            chime.SetData(samples, 0);
        }

        private void OnDestroy()
        {
            foreach (var m in meshes) Release(m);
            foreach (var m in materials) Release(m);
            if (chime != null) Release(chime);
        }
        private static void Release(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    }
}
