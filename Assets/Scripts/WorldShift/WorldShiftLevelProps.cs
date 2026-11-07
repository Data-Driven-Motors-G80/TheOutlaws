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
        public static readonly Color NightColour = new Color(.76f, .67f, .95f);
        public static readonly Color SwitchColour = new Color(.15f, .85f, .77f);
        public static readonly Color MoverColour = new Color(.88f, .16f, .19f);
        public static readonly Color SideMoverColour = new Color(1f, .48f, .12f);
        public static readonly Color PermanentColour = new Color(.96f, .94f, .86f);
        public static readonly Color CheckpointColour = new Color(.2f, .95f, .55f);
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

        /// <summary>Overhead highway sign using supplied lesson art where available.</summary>
        public static Transform HighwaySign(Transform parent, WorldShiftRoute route, LevelSign sign)
        {
            RoadPathPoint point = route.Sample(sign.Distance);
            var root = new GameObject("Highway sign " + sign.Kind).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(point.Position, Quaternion.LookRotation(point.Forward, Vector3.up));
            Material pole = RuntimeMaterials.Coloured(new Color(.45f, .47f, .48f));
            Material green = Glow(SignGreen, .25f), white = Glow(SignWhite, .5f);
            SignArtworkSpec artwork = ArtworkFor(sign.Kind);
            const float legibilityScale = 1.25f;
            float boardWidth = (artwork.Resource == null ? 10.5f : artwork.Width) * legibilityScale;
            float boardHeight = (artwork.Resource == null ? 3.5f : artwork.Height) * legibilityScale;
            float boardY = Mathf.Max(5.4f, 3.7f + boardHeight * .5f);
            float supportHeight = boardY + boardHeight * .5f + .2f;
            // Supports stay outside the road; the board hangs above both lanes.
            foreach (float side in new[] { -1f, 1f })
                Box(root, new Vector3(side * (boardWidth * .5f + .65f), supportHeight * .5f, 0f), new Vector3(.2f, supportHeight, .2f), pole);
            Box(root, new Vector3(0f, supportHeight - .1f, .1f), new Vector3(boardWidth + 1.3f, .2f, .2f), pole);
            Box(root, new Vector3(0f, boardY, -.03f), new Vector3(boardWidth, boardHeight, .1f), green);
            Box(root, new Vector3(0f, boardY, .03f), new Vector3(boardWidth + .2f, boardHeight + .2f, .1f), white);
            // The face points back toward the approaching driver (local -z).
            var face = new GameObject("Sign face").transform;
            face.SetParent(root, false);
            face.localPosition = new Vector3(0f, boardY, -.1f);
            face.localScale = Vector3.one * legibilityScale * (artwork.Resource == null ? 1.25f : 1f);
            Material day = Glow(SwitchColour, .6f);
            if (!SignArtwork(face, artwork)) switch (sign.Kind)
            {
                case LevelSignKind.Steer:
                    Key(face, "", new Vector3(-3.2f, 0f, 0f), white);
                    Arrow(face, new Vector3(-3.2f, 0f, -.08f), -1f, green);
                    Arrow(face, new Vector3(-1.7f, 0f, 0f), -1f, white);
                    Box(face, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), Glow(PermanentColour));
                    Arrow(face, new Vector3(1.7f, 0f, 0f), 1f, white);
                    Key(face, "", new Vector3(3.2f, 0f, 0f), white);
                    Arrow(face, new Vector3(3.2f, 0f, -.08f), 1f, green);
                    break;
                case LevelSignKind.Switch:
                    Key(face, "SPACE", new Vector3(-2.3f, 0f, 0f), white, 2.6f);
                    Box(face, new Vector3(.4f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), Glow(SwitchColour));
                    Arrow(face, new Vector3(1.9f, 0f, 0f), 1f, white);
                    Moon(face, new Vector3(3.3f, 0f, 0f), white, green);
                    break;
                case LevelSignKind.Dashes:
                    DashedOutline(face, new Vector3(-2.9f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), day);
                    Arrow(face, new Vector3(-1.4f, 0f, 0f), 1f, white);
                    Key(face, "SPACE", new Vector3(.4f, 0f, 0f), white, 2.2f);
                    Arrow(face, new Vector3(2.1f, 0f, 0f), 1f, white);
                    Box(face, new Vector3(3.3f, 0f, 0f), new Vector3(1.1f, 1.1f, .05f), Glow(NightColour));
                    break;
                case LevelSignKind.Oncoming:
                    Box(face, new Vector3(0f, .25f, 0f), new Vector3(1.5f, 1.3f, .05f), Glow(MoverColour, .5f));
                    DownArrow(face, new Vector3(0f, .25f, -.08f), white);
                    break;
                case LevelSignKind.Freeze:
                    Arrow(face, new Vector3(-3.6f, 0f, 0f), -1f, white);
                    Box(face, new Vector3(-2.6f, 0f, 0f), new Vector3(1f, 1f, .05f), Glow(SideMoverColour, .5f));
                    Arrow(face, new Vector3(-1.6f, 0f, 0f), 1f, white);
                    Key(face, "SPACE", new Vector3(.5f, 0f, 0f), white, 2.2f);
                    Moon(face, new Vector3(2.4f, 0f, 0f), white, green);
                    Box(face, new Vector3(3.5f, 0f, 0f), new Vector3(1f, 1f, .05f), Glow(SideMoverColour, .5f));
                    break;
                case LevelSignKind.AirFreeze:
                    Arrow(face, new Vector3(-3.7f, .35f, 0f), -1f, white);
                    Disc(face, new Vector3(-2.8f, .35f, 0f), .43f, Glow(SideMoverColour));
                    Arrow(face, new Vector3(-1.8f, .35f, 0f), 1f, white);
                    Key(face, "SPACE", new Vector3(.1f, .35f, 0f), white, 2.1f);
                    Moon(face, new Vector3(2f, .35f, 0f), white, green);
                    foreach (float x in new[] { 3.05f, 3.5f })
                        Box(face, new Vector3(x, .35f, 0f), new Vector3(.16f, .7f, .05f), white);
                    break;
                case LevelSignKind.AirRelease:
                    Box(face, new Vector3(-2.9f, .35f, 0f), new Vector3(1.9f, .55f, .05f), Glow(MoverColour));
                    Key(face, "SPACE", new Vector3(-.15f, .35f, 0f), white, 2.1f);
                    Arrow(face, new Vector3(1.45f, .35f, 0f), 1f, white);
                    Sun(face, new Vector3(3f, .35f, 0f), day);
                    break;
                case LevelSignKind.Ramp:
                    var ramp = Box(face, new Vector3(-2.9f, -.55f, 0f), new Vector3(2.1f, .25f, .05f), white);
                    ramp.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    for (int i = 0; i < 6; i++)
                    {
                        float t = i / 5f;
                        Box(face, new Vector3(-2.1f + t * 1.8f, -.15f + 1.3f * 4f * t * (1f - t), 0f), new Vector3(.22f, .22f, .05f), white);
                    }
                    var rampText = WorldLabel(face, "DRIVE ON RAMP", new Vector3(1.7f, .55f, -.08f), new Vector2(4.6f, .6f), 3.2f);
                    rampText.enableAutoSizing = true;
                    rampText.fontSizeMin = 2.1f;
                    var airText = WorldLabel(face, "SPACE IN AIR", new Vector3(1.7f, -.45f, -.08f), new Vector2(4.6f, .6f), 3.2f);
                    airText.enableAutoSizing = true;
                    airText.fontSizeMin = 2.1f;
                    break;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private struct SignArtworkSpec
        {
            public string Resource;
            public RectInt Crop;
            public float Width;
            public float Height;

            public SignArtworkSpec(string resource, RectInt crop, float width, float height)
            {
                Resource = resource;
                Crop = crop;
                Width = width;
                Height = height;
            }
        }

        private static SignArtworkSpec ArtworkFor(LevelSignKind kind)
        {
            switch (kind)
            {
                case LevelSignKind.Steer: return new SignArtworkSpec("TrainingSteerSign", new RectInt(0, 0, 1360, 576), 10.5f, 4.45f);
                case LevelSignKind.Switch: return new SignArtworkSpec("TrainingSwitchSign", new RectInt(24, 38, 1914, 713), 11.5f, 4.28f);
                case LevelSignKind.Dashes: return new SignArtworkSpec("TrainingDashesSign", new RectInt(118, 112, 1938, 454), 14f, 3.28f);
                case LevelSignKind.Oncoming: return new SignArtworkSpec("TrainingOncomingSign", new RectInt(170, 158, 1434, 406), 14f, 3.96f);
                case LevelSignKind.Freeze: return new SignArtworkSpec("TrainingFreezeSign", new RectInt(165, 163, 1842, 363), 14f, 2.76f);
                case LevelSignKind.Ramp: return new SignArtworkSpec("TrainingRampSign", new RectInt(123, 187, 1529, 415), 12.5f, 3.39f);
                default: return default;
            }
        }

        public static bool TryGetSignArtwork(LevelSignKind kind, out Texture2D texture, out Rect uv)
        {
            SignArtworkSpec spec = ArtworkFor(kind);
            texture = spec.Resource == null ? null : Resources.Load<Texture2D>(spec.Resource);
            uv = Rect.zero;
            if (texture == null) return false;
            uv = new Rect(spec.Crop.xMin / (float)texture.width,
                1f - spec.Crop.yMax / (float)texture.height,
                spec.Crop.width / (float)texture.width, spec.Crop.height / (float)texture.height);
            return true;
        }

        private static bool SignArtwork(Transform face, SignArtworkSpec spec)
        {
            if (spec.Resource == null) return false;
            Texture2D texture = Resources.Load<Texture2D>(spec.Resource);
            if (texture == null) return false;
            Material template = Resources.Load<Material>("WorldShiftRuntimeUnlit");
            Material material = new Material(template != null ? template : RuntimeMaterials.Coloured(Color.white));
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            var artwork = new GameObject("Sign artwork", typeof(MeshFilter), typeof(MeshRenderer));
            artwork.transform.SetParent(face, false);
            artwork.transform.localPosition = new Vector3(0f, 0f, -.06f);
            float halfWidth = spec.Width * .5f, halfHeight = spec.Height * .5f;
            var mesh = new Mesh { name = spec.Resource + " face" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f), new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f), new Vector3(halfWidth, -halfHeight, 0f)
            };
            float u0 = spec.Crop.xMin / (float)texture.width, u1 = spec.Crop.xMax / (float)texture.width;
            float v0 = 1f - spec.Crop.yMax / (float)texture.height, v1 = 1f - spec.Crop.yMin / (float)texture.height;
            mesh.uv = new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            artwork.GetComponent<MeshFilter>().sharedMesh = mesh;
            artwork.GetComponent<MeshRenderer>().sharedMaterial = material;
            return true;
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

        public static void SlidingGateHint(Transform parent, float depth = 1.8f, bool smallBlock = false)
        {
            float y = smallBlock ? 2.4f : .05f;
            float z = -depth * .5f - .04f;
            float width = smallBlock ? 4.8f : 7.8f, height = smallBlock ? 1f : 1.25f;
            Box(parent, new Vector3(0f, y, z), new Vector3(width, height, .05f),
                Glow(new Color(.025f, .06f, .04f), .15f));
            WorldLabel(parent, "DAY TO MOVE", new Vector3(0f, y, z - .05f),
                new Vector2(width - .3f, height - .1f), smallBlock ? 4f : 6f);
        }

        public static Transform StormRoadMark(Transform parent, WorldShiftRoute route, float distance)
        {
            RoadPathPoint point = route.Sample(distance);
            var root = new GameObject("STORM road marking").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(point.Position + point.Up * .065f, Quaternion.LookRotation(point.Forward, point.Up));
            var label = WorldLabel(root, "STORM", Vector3.zero, new Vector2(8f, 2f), 12f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.transform.localScale = new Vector3(1f, 1.8f, 1f);
            label.color = DayColour;
            foreach (float z in new[] { -2.2f, 2.2f })
                Box(root, new Vector3(0f, 0f, z), new Vector3(8.4f, .04f, .14f), Glow(DayColour, .5f));
            return root;
        }

        public static void CheckpointMark(Transform parent)
        {
            // A single bright stripe across the road marks the saved position.
            Box(parent, new Vector3(0f, .045f, 0f), new Vector3(WorldShiftLevel.RoadWidth, .05f, .42f), Glow(CheckpointColour, 1.2f));
        }

        private static TextMeshPro WorldLabel(Transform parent, string words, Vector3 at, Vector2 size, float fontSize)
        {
            var text = new GameObject(words + " cue", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = at;
            text.font = TMP_Settings.defaultFontAsset;
            text.text = words;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = SignWhite;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return text;
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

        private static void DownArrow(Transform parent, Vector3 at, Material material)
        {
            Box(parent, at + new Vector3(0f, .13f, 0f), new Vector3(.17f, .55f, .05f), material);
            foreach (float side in new[] { -1f, 1f })
            {
                var wing = Box(parent, at + new Vector3(side * .2f, -.23f, 0f), new Vector3(.48f, .16f, .05f), material);
                wing.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
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
