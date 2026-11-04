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
            /// <summary>Level play: Day obstacles are yellow, Night obstacles are black.</summary>
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
                    Color bodyColour = fixedBody ?? (night ? WorldShiftLevelProps.NightColour : WorldShiftLevelProps.DayColour);
                    Color inkColour = fixedInk ?? (night ? WorldShiftLevelProps.DayColour : WorldShiftLevelProps.NightColour);
                    var levelBody = WorldShiftLevelProps.Glow(bodyColour, night && !fixedBody.HasValue ? 0f : .3f);
                    var levelInk = WorldShiftLevelProps.Glow(inkColour, night ? .7f : 0f);
                    foreach (var renderer in bodies) renderer.sharedMaterial = levelBody;
                    foreach (var renderer in marks) renderer.sharedMaterial = levelInk;
                    return;
                }
                var body = RuntimeMaterials.Coloured(night ? new Color(.64f, .84f, .96f) : new Color(.48f, .16f, .14f));
                var ink = RuntimeMaterials.Coloured(night ? new Color(.025f, .06f, .12f) : new Color(.96f, .96f, .94f));
                // A small constant fill keeps the ice legible under Nightshift lighting; no flash or particles.
                body.EnableKeyword("_EMISSION");
                body.SetColor("_EmissionColor", night ? new Color(.64f, .84f, .96f) * .35f : Color.black);
                ink.EnableKeyword("_EMISSION");
                ink.SetColor("_EmissionColor", night ? Color.black : new Color(.96f, .96f, .94f) * .45f);
                foreach (var renderer in bodies) renderer.sharedMaterial = body;
                foreach (var renderer in marks) renderer.sharedMaterial = ink;
            }
        }

        public static Transform Build(Transform parent, Vector3 centre, Vector3 size, Palette palette)
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
                Cross(root, new Vector3(0f, 0f, face * (size.z * .5f + .035f)),
                    new Vector3(halfX, halfY, 0f), new Vector3(halfX, -halfY, 0f), ink);
            // A matching top X stays recognizable from the raised chase camera and during jumps.
            float top = Mathf.Min(size.x, size.z) * .3f;
            Cross(root, new Vector3(0f, size.y * .5f + .035f, 0f),
                new Vector3(top, 0f, top), new Vector3(top, 0f, -top), ink);
            palette.Add(root);
            return root;
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
