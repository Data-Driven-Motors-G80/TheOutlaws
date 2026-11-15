using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    // Shared, collider-free ice skirt. Meshes are built once, never during switching.
    public sealed class WorldShiftFrozenIce : MonoBehaviour
    {
        private Mesh ownedMesh;
        private Renderer iceRenderer;
        private bool frozen;
        private float progress;
        public void SetFrozen(bool value, bool immediate = false)
        {
            if (frozen == value && !immediate) return;
            frozen = value;
            progress = immediate ? (value ? 1f : 0f) : 0f;
            ApplyGrowth();
        }
        private void Update()
        {
            if (!frozen || progress >= 1f) return;
            progress = Mathf.Min(1f, progress + Time.deltaTime / .55f);
            ApplyGrowth();
        }
        private void ApplyGrowth()
        {
            if (iceRenderer == null) return;
            iceRenderer.enabled = frozen;
            float growth = Mathf.SmoothStep(0f, 1f, progress);
            transform.localScale = new Vector3(1f, Mathf.Max(.025f, growth), 1f);
        }
        private static Material clearIce, frost;
        public static GameObject Build(Transform parent, Vector3 centre, Vector3 size, float maxHeight = float.PositiveInfinity)
        {
            var root = new GameObject("Frozen ice base");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre - Vector3.up * (size.y * .5f);
            var owner = root.AddComponent<WorldShiftFrozenIce>();
            if (clearIce == null) clearIce = IceMaterial(new Color(.72f, .80f, .87f, 1f), .45f, false);
            if (frost == null) frost = IceMaterial(new Color(.86f, .96f, 1f), .65f, false);
            const int count = 96;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            const int stride = count + 1;
            var body = new List<int>();
            var rims = new List<int>();
            float height = Mathf.Min(1.15f, size.y * .58f, maxHeight);
            Vector3 Point(int i, float spread, float y)
            {
                float a = i * Mathf.PI * 2f / count;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                return new Vector3(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), .32f) * (size.x * .5f + spread), y,
                    Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), .32f) * (size.z * .5f + spread));
            }
            // Low irregular puddle flows into a thick, scalloped translucent collar.
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i <= count; i++)
                {
                    float a = i * Mathf.PI * 2f / count;
                    float wave = .5f + .3f * Mathf.Sin(a * 7f + .7f) + .2f * Mathf.Sin(a * 13f);
                    float crest = height * (.76f + .24f * wave);
                    float spread = ring == 0 ? .20f + wave * .10f : ring == 1 ? .24f : ring == 2 ? .18f + wave * .10f : ring == 3 ? .07f : .012f;
                    float y = ring == 0 ? .025f : ring == 1 ? .10f : ring == 2 ? crest * .46f : ring == 3 ? crest : crest - .055f;
                    vertices.Add(Point(i, spread, y));
                    uv.Add(new Vector2(i / (float)count * (size.x + size.z), y / height));
                }
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < count; i++)
                {
                    int a = ring * stride + i, b = a + 1;
                    var triangles = ring == 3 ? rims : body;
                    triangles.AddRange(new[] { a, a + stride, b, b, a + stride, b + stride });
                }
            // Short tapered icicles hang from the crest; uneven lengths avoid a fence-like pattern.
            for (int i = 1; i < count; i += 5)
            {
                Vector3 top = vertices[3 * stride + i];
                Vector3 outward = new Vector3(top.x / size.x, 0f, top.z / size.z).normalized;
                top += outward * .045f;
                float length = height * (.30f + .20f * Mathf.Abs(Mathf.Sin(i * 1.7f)));
                int first = vertices.Count;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    vertices.Add(top + new Vector3(Mathf.Cos(a) * .065f, 0f, Mathf.Sin(a) * .065f));
                    uv.Add(new Vector2(k / 6f, 1f));
                }
                vertices.Add(top - Vector3.up * length + outward * .015f);
                uv.Add(new Vector2(.5f, 0f));
                for (int k = 0; k < 6; k++) body.AddRange(new[] { first + k, first + (k + 1) % 6, first + 6 });
            }
            var mesh = owner.ownedMesh = new Mesh { name = "Irregular ice base and icicles" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(rims, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            owner.iceRenderer = renderer;
            renderer.sharedMaterials = new[] { clearIce, frost };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private static Material IceMaterial(Color color, float smoothness, bool transparent)
        {
            var template = Resources.Load<Material>("WeeWooRuntimeLit");
            var mat = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            mat.name = transparent ? "Glassy blue ice" : "Pale frost rim";
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", .08f);
            var texture = Resources.Load<Texture2D>("FrozenIceSurface");
            if (texture != null)
            {
                mat.SetTexture("_BaseMap", texture);
                mat.SetTexture("_MainTex", texture);
            }
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(.065f, .08f, .10f));
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Mode", 3f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            return mat;
        }

        private void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
