using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>One readable silhouette for every solid road hazard, fitted to its existing footprint.</summary>
    public static class WorldShiftObstacleVisual
    {
        /// <summary>Caches obstacle renderers once; palettes change only when the displayed mode changes.</summary>
        public sealed class Palette
        {
            private readonly List<Renderer> bodies = new List<Renderer>();
            private readonly List<Renderer> marks = new List<Renderer>();
            private ShiftWorld? displayed;
            private readonly Color? fixedBody, fixedInk;
            /// <summary>Day-only blocks are teal, Night-only blocks are pale violet.</summary>
            public bool LevelColours { get; set; }

            public Palette() { }
            /// <summary>One colour in both worlds, for hazards that exist in both.</summary>
            public Palette(Color body, Color ink) { fixedBody = body; fixedInk = ink; }

            public void Add(Transform root)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    (renderer.name == "Obstacle block" ? bodies : marks).Add(renderer);
                displayed = null;
            }

            public void SetWorld(ShiftWorld world)
            {
                if (displayed == world) return;
                displayed = world;
                bool night = world == ShiftWorld.Echo;
                if (fixedBody.HasValue || LevelColours)
                {
                    Color bodyColour = fixedBody ?? (night ? WorldShiftLevelProps.NightColour : WorldShiftLevelProps.SwitchColour);
                    Color inkColour = fixedInk ?? new Color(.07f, .09f, .17f);
                    var levelBody = WorldShiftLevelProps.Glow(bodyColour, night ? .4f : .3f);
                    var levelInk = WorldShiftLevelProps.Glow(inkColour, night ? .7f : 0f);
                    foreach (var renderer in bodies) renderer.sharedMaterial = levelBody;
                    foreach (var renderer in marks) renderer.sharedMaterial = levelInk;
                    return;
                }
                var body = RuntimeMaterials.Coloured(night ? WorldShiftLevelProps.NightColour : WorldShiftLevelProps.SwitchColour);
                var ink = RuntimeMaterials.Coloured(new Color(.07f, .09f, .17f));
                // A constant fill keeps the pale Night block legible under dark lighting.
                body.EnableKeyword("_EMISSION");
                body.SetColor("_EmissionColor", night ? WorldShiftLevelProps.NightColour * .4f : Color.black);
                ink.EnableKeyword("_EMISSION");
                ink.SetColor("_EmissionColor", Color.black);
                foreach (var renderer in bodies) renderer.sharedMaterial = body;
                foreach (var renderer in marks) renderer.sharedMaterial = ink;
            }
        }

        public static Transform Build(Transform parent, Vector3 centre, Vector3 size, Palette palette,
            bool switchWall = false, bool oncoming = false)
        {
            var root = new GameObject("X obstacle").transform;
            root.SetParent(parent, false);
            root.localPosition = centre;
            var body = RuntimeMaterials.Coloured(new Color(.48f, .16f, .14f));
            var ink = RuntimeMaterials.Coloured(new Color(.96f, .96f, .94f));
            ink.EnableKeyword("_EMISSION");
            ink.SetColor("_EmissionColor", new Color(.96f, .96f, .94f) * .45f);
            Cube(root, "Obstacle block", Vector3.zero, size, body);
            float halfX = Mathf.Min(size.x * .32f, 1.05f), halfY = size.y * .32f;
            foreach (float face in new[] { -1f, 1f })
            {
                var front = new Vector3(0f, 0f, face * (size.z * .5f + .035f));
                if (switchWall) SwitchArrows(root, front, ink);
                else Cross(root, front, new Vector3(halfX, halfY, 0f), new Vector3(halfX, -halfY, 0f), ink);
            }
            if (oncoming)
            {
                // Raised, front-facing marker stays legible from the chase camera.
                // Keep the white X on the obstacle face below it.
                float y = size.y * .5f + .65f;
                float z = -size.z * .5f - .08f;
                Cube(root, "Obstacle block", new Vector3(0f, y, z), new Vector3(1.5f, .95f, .08f), body);
                Cube(root, "Oncoming direction", new Vector3(0f, y + .14f, z - .07f), new Vector3(.19f, .52f, .07f), ink);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wing = Cube(root, "Oncoming direction", new Vector3(side * .24f, y - .22f, z - .07f),
                        new Vector3(.7f, .16f, .07f), ink);
                    wing.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                }
            }
            else
            {
                // A matching top X stays recognizable from the raised chase camera and during jumps.
                float top = Mathf.Min(size.x, size.z) * .3f;
                if (switchWall) SwitchArrows(root, new Vector3(0f, size.y * .5f + .035f, 0f), ink);
                else Cross(root, new Vector3(0f, size.y * .5f + .035f, 0f),
                    new Vector3(top, 0f, top), new Vector3(top, 0f, -top), ink);
            }
            palette.Add(root);
            return root;
        }

        private static void SwitchArrows(Transform root, Vector3 centre, Material ink)
        {
            // Two opposing arrows remain identifiable when colors are hard to distinguish.
            foreach (float side in new[] { -1f, 1f })
            {
                var shaft = Cube(root, "Switch arrows", centre + new Vector3(side * .65f, side * .2f, 0f),
                    new Vector3(.75f, .11f, .07f), ink);
                if (centre.y > 0f) shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (float wing in new[] { -1f, 1f })
                {
                    var head = Cube(root, "Switch arrows", centre + new Vector3(side * 1.05f, side * .2f + wing * .19f, 0f),
                        new Vector3(.48f, .1f, .07f), ink);
                    head.localRotation = Quaternion.Euler(centre.y > 0f ? 90f : 0f, 0f, side * wing * 35f);
                }
            }
        }

        private static void Cross(Transform parent, Vector3 centre, Vector3 a, Vector3 b, Material ink)
        {
            foreach (var diagonal in new[] { a, b })
            {
                var bar = Cube(parent, "Obstacle X", centre, new Vector3(diagonal.magnitude * 2f, .16f, .06f), ink);
                bar.localRotation = Quaternion.FromToRotation(Vector3.right, diagonal.normalized);
            }
        }

        private static Transform Cube(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = centre;
            cube.transform.localScale = size;
            cube.GetComponent<Collider>().enabled = false;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube.transform;
        }
    }
}
