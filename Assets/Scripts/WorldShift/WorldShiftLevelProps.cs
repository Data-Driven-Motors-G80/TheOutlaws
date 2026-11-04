using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Level-mode geometry: other-world dashed outlines and pictogram highway signs.</summary>
    public static class WorldShiftLevelProps
    {
        public static readonly Color DayColour = new Color(1f, .78f, .12f);
        public static readonly Color NightColour = new Color(.05f, .05f, .06f);
        public static readonly Color MoverColour = new Color(.95f, .42f, .12f);
        private static readonly Color SignGreen = new Color(.02f, .38f, .2f);
        private static readonly Color SignWhite = new Color(.95f, .97f, .95f);
        private static readonly Dictionary<(Color, float), Material> glowing = new Dictionary<(Color, float), Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => glowing.Clear();

        /// <summary>Lit colour that stays readable in both Day and Night lighting.</summary>
        public static Material Glow(Color colour, float strength = .45f)
        {
            if (glowing.TryGetValue((colour, strength), out Material cached) && cached != null) return cached;
            var material = new Material(RuntimeMaterials.Coloured(colour)) { name = "Level glow " + colour };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * strength);
            glowing[(colour, strength)] = material;
            return material;
        }

        /// <summary>Dashed edges of a box, as one mesh. Passable: it marks where the other world's obstacle is.</summary>
        public static GameObject DashedOutline(Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            const float dash = .6f, gap = .35f, thickness = .16f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 h = size * .5f;
            void Edge(Vector3 a, Vector3 b)
            {
                Vector3 dir = b - a;
                float length = dir.magnitude;
                dir /= length;
                for (float t = 0f; t < length; t += dash + gap)
                {
                    float end = Mathf.Min(length, t + dash);
                    Cuboid(vertices, triangles, centre + a + dir * ((t + end) * .5f),
                        new Vector3(Mathf.Abs(dir.x) * (end - t) + thickness, Mathf.Abs(dir.y) * (end - t) + thickness, Mathf.Abs(dir.z) * (end - t) + thickness) * .5f);
                }
            }
            foreach (float y in new[] { -h.y, h.y })
            {
                Edge(new Vector3(-h.x, y, -h.z), new Vector3(h.x, y, -h.z));
                Edge(new Vector3(-h.x, y, h.z), new Vector3(h.x, y, h.z));
                Edge(new Vector3(-h.x, y, -h.z), new Vector3(-h.x, y, h.z));
                Edge(new Vector3(h.x, y, -h.z), new Vector3(h.x, y, h.z));
            }
            foreach (float x in new[] { -h.x, h.x })
                foreach (float z in new[] { -h.z, h.z })
                    Edge(new Vector3(x, -h.y, z), new Vector3(x, h.y, z));
            var mesh = new Mesh { name = "Dashed other-world outline" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var shape = new GameObject("Dashed outline - other world, passable", typeof(MeshFilter), typeof(MeshRenderer));
            shape.transform.SetParent(parent, false);
            shape.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shape.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return shape;
        }

        private static void Cuboid(List<Vector3> vertices, List<int> triangles, Vector3 c, Vector3 e)
        {
            int start = vertices.Count;
            for (int i = 0; i < 8; i++)
                vertices.Add(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
            int[] faces = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 1, 5, 4, 2, 6, 7, 3, 0, 4, 6, 2, 1, 3, 7, 5 };
            for (int f = 0; f < 6; f++)
            {
                int a = start + faces[f * 4], b = start + faces[f * 4 + 1], cc = start + faces[f * 4 + 2], d = start + faces[f * 4 + 3];
                triangles.AddRange(new[] { a, b, cc, a, cc, d });
            }
        }

        /// <summary>Overhead green highway sign. Pictograms and key caps only.</summary>
        public static Transform HighwaySign(Transform parent, WorldShiftRoute route, LevelSign sign)
        {
            RoadPathPoint point = route.Sample(sign.Distance);
            var root = new GameObject("Highway sign " + sign.Kind).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, Vector3.up));
            Material pole = RuntimeMaterials.Coloured(new Color(.45f, .47f, .48f));
            Material green = Glow(SignGreen, .25f), white = Glow(SignWhite, .5f);
            // Supports stay outside the road; the board hangs above both lanes.
            foreach (float side in new[] { -1f, 1f })
                Box(root, new Vector3(side * 5.9f, 3.3f, 0f), new Vector3(.2f, 6.6f, .2f), pole);
            Box(root, new Vector3(0f, 6.5f, .1f), new Vector3(12f, .2f, .2f), pole);
            Box(root, new Vector3(0f, 5.4f, 0f), new Vector3(8.4f, 2.8f, .12f), green);
            Box(root, new Vector3(0f, 5.4f, .01f), new Vector3(8.6f, 3f, .1f), white);
            // The face points back toward the approaching driver (local -z).
            var face = new GameObject("Sign face").transform;
            face.SetParent(root, false);
            face.localPosition = new Vector3(0f, 5.4f, -.1f);
            Material day = Glow(DayColour, .6f), night = Glow(new Color(.08f, .09f, .12f), 0f);
            switch (sign.Kind)
            {
                case LevelSignKind.Steer:
                    Key(face, "A", new Vector3(-3.2f, 0f, 0f), white);
                    Arrow(face, new Vector3(-1.7f, 0f, 0f), -1f, white);
                    Box(face, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), day);
                    Arrow(face, new Vector3(1.7f, 0f, 0f), 1f, white);
                    Key(face, "D", new Vector3(3.2f, 0f, 0f), white);
                    break;
                case LevelSignKind.Switch:
                    Key(face, "SPACE", new Vector3(-2.3f, 0f, 0f), white, 2.6f);
                    Sun(face, new Vector3(.4f, 0f, 0f), day);
                    Arrow(face, new Vector3(1.9f, 0f, 0f), 1f, white);
                    Moon(face, new Vector3(3.3f, 0f, 0f), white, green);
                    break;
                case LevelSignKind.Dashes:
                    DashedOutline(face, new Vector3(-2.9f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), day);
                    Arrow(face, new Vector3(-1.4f, 0f, 0f), 1f, white);
                    Key(face, "SPACE", new Vector3(.4f, 0f, 0f), white, 2.2f);
                    Arrow(face, new Vector3(2.1f, 0f, 0f), 1f, white);
                    Box(face, new Vector3(3.3f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), day);
                    break;
                case LevelSignKind.Freeze:
                    Arrow(face, new Vector3(-3.6f, 0f, 0f), -1f, white);
                    Box(face, new Vector3(-2.6f, 0f, 0f), new Vector3(1f, 1f, .05f), Glow(MoverColour, .5f));
                    Arrow(face, new Vector3(-1.6f, 0f, 0f), 1f, white);
                    Key(face, "SPACE", new Vector3(.5f, 0f, 0f), white, 2.2f);
                    Moon(face, new Vector3(2.4f, 0f, 0f), white, green);
                    Box(face, new Vector3(3.5f, 0f, 0f), new Vector3(1f, 1f, .05f), Glow(MoverColour, .5f));
                    break;
                case LevelSignKind.Ramp:
                    var ramp = Box(face, new Vector3(-2.6f, -.6f, 0f), new Vector3(2.2f, .25f, .05f), white);
                    ramp.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    for (int i = 0; i < 6; i++)
                    {
                        float t = i / 5f;
                        Box(face, new Vector3(-1.4f + t * 4.6f, -.2f + 1.3f * 4f * t * (1f - t), 0f), new Vector3(.22f, .22f, .05f), white);
                    }
                    Key(face, "SPACE", new Vector3(.9f, -.55f, 0f), white, 1.9f);
                    break;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private static void Key(Transform parent, string label, Vector3 at, Material white, float width = 1.1f)
        {
            Box(parent, at, new Vector3(width, 1.1f, .05f), white);
            var text = new GameObject("Key " + label, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = at + new Vector3(0f, 0f, -.05f);
            text.font = TMP_Settings.defaultFontAsset;
            text.text = label;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = label.Length > 1 ? 5f : 8f;
            text.color = new Color(.02f, .2f, .1f);
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(width, 1.1f);
        }

        private static void Arrow(Transform parent, Vector3 at, float direction, Material material)
        {
            Box(parent, at - new Vector3(direction * .15f, 0f, 0f), new Vector3(.7f, .18f, .05f), material);
            foreach (float tilt in new[] { 40f, -40f })
            {
                var head = Box(parent, at + new Vector3(direction * .2f, tilt > 0f ? .14f : -.14f, 0f), new Vector3(.45f, .18f, .05f), material);
                head.localRotation = Quaternion.Euler(0f, 0f, direction > 0f ? -tilt : tilt);
            }
        }

        private static void Sun(Transform parent, Vector3 at, Material material)
        {
            Disc(parent, at, .45f, material);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var ray = Box(parent, at + Quaternion.Euler(0f, 0f, angle) * Vector3.right * .65f, new Vector3(.25f, .09f, .05f), material);
                ray.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private static void Moon(Transform parent, Vector3 at, Material moon, Material background)
        {
            Disc(parent, at, .5f, moon);
            Disc(parent, at + new Vector3(.25f, .15f, -.06f), .42f, background);
        }

        private static void Disc(Transform parent, Vector3 at, float radius, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.GetComponent<Collider>().enabled = false;
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = at;
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(radius * 2f, .025f, radius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform Box(Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.GetComponent<Collider>().enabled = false;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = at;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box.transform;
        }
    }
}
