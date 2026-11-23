using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain jumps cross a deep, clean gorge: layered dark rock walls under a pale rim with snow tufts, falling away
    /// into two soft cloud layers, so the view under the car in flight is a simple drop into mist. Visual only; every
    /// vertex stays below the deck and nothing bridges the missing road.
    /// </summary>
    public sealed partial class WorldShiftLevelDropScenery
    {
        private Material gorgeMist;

        private void BuildMountainGorge(Transform root, ShiftRelay jump, float low)
        {
            float floor = low - 70f;
            var walls = new WorldShiftArtMesh();
            Color rim = new Color(.62f, .60f, .56f), upper = new Color(.40f, .42f, .45f), lower = new Color(.26f, .28f, .32f);
            Color deep = new Color(.17f, .19f, .23f), snow = new Color(.93f, .96f, .99f);
            for (float d = jump.Ramp; d < jump.End; d += 4f)
            {
                float end = Mathf.Min(d + 4f, jump.End);
                var a = route.Sample(d); var b = route.Sample(end);
                float half = a.Width * .5f + .65f;
                Vector3 AL(float x, float y) => new Vector3(a.Position.x, y, a.Position.z) + a.Right * x;
                Vector3 BL(float x, float y) => new Vector3(b.Position.x, y, b.Position.z) + b.Right * x;
                foreach (float side in new[] { -1f, 1f })
                {
                    // Four bands from the rim down: each steps slightly in or out, so the face reads as layered rock.
                    float[] depthAt = { 1.3f, 4.2f, 14f, 32f, 70f };
                    float[] offset = { 0f, .45f, -.35f, .55f, 1.6f };
                    Color[] band = { rim, upper, lower, deep };
                    for (int k = 0; k < 4; k++)
                    {
                        float jA = .25f * Mathf.Sin(d * .61f + k * 1.7f + side), jB = .25f * Mathf.Sin(end * .61f + k * 1.7f + side);
                        Vector3 a0 = AL(side * (half - offset[k] - jA), a.Position.y - depthAt[k]);
                        Vector3 b0 = BL(side * (half - offset[k] - jB), b.Position.y - depthAt[k]);
                        Vector3 a1 = AL(side * (half - offset[k + 1] - jA), Mathf.Max(floor, a.Position.y - depthAt[k + 1]));
                        Vector3 b1 = BL(side * (half - offset[k + 1] - jB), Mathf.Max(floor, b.Position.y - depthAt[k + 1]));
                        Color c = band[k] * (.92f + .08f * Mathf.Sin(d * 1.3f + k)); c.a = 1f;
                        if (side < 0) walls.Quad(a0, b0, b1, a1, c); else walls.Quad(b0, a0, a1, b1, c * .88f);
                    }
                    // Snow tufts and small ledges along the rim keep the drop edge crisp.
                    walls.Beam(AL(side * (half - .1f), a.Position.y - 1.35f), BL(side * (half - .1f), b.Position.y - 1.35f), .5f, .22f, snow);
                    if (Mathf.RoundToInt(d) % 12 == 0)
                        walls.Rock(AL(side * (half - .5f), a.Position.y - 5f), new Vector3(.7f, .6f, 1.1f), upper, Mathf.RoundToInt(d) + (side > 0 ? 1 : 0));
                }
            }
            Render(root, "Layered gorge walls", walls, surface);
            // Two soft cloud layers fill the gorge, so the drop reads as deep and clean from the air.
            if (gorgeMist == null)
            {
                gorgeMist = new Material(Resources.Load<Shader>("WorldShiftMountainMist")) { name = "Gorge mist" };
                gorgeMist.SetFloat("_Mode", 0f); gorgeMist.SetFloat("_Seed", 5.3f);
            }
            for (int layer = 0; layer < 2; layer++)
            {
                var sheet = new Mesh { name = "Gorge cloud layer" };
                var vertices = new System.Collections.Generic.List<Vector3>();
                var uvs = new System.Collections.Generic.List<Vector2>();
                var triangles = new System.Collections.Generic.List<int>();
                float length = jump.End - jump.Ramp, drop = layer == 0 ? 9f : 16f;
                for (float d = jump.Ramp - 6f; d <= jump.End + 6f; d += 4f)
                {
                    var p = route.Sample(d);
                    float width = p.Width * .5f + 2.5f;
                    Vector3 centre = new Vector3(p.Position.x, low - drop, p.Position.z);
                    vertices.Add(centre - p.Right * width); vertices.Add(centre + p.Right * width);
                    float v = Mathf.InverseLerp(jump.Ramp - 6f, jump.End + 6f, d);
                    uvs.Add(new Vector2(0f, v)); uvs.Add(new Vector2(1f, v));
                    int n = vertices.Count;
                    if (n >= 4) { triangles.AddRange(new[] { n - 4, n - 2, n - 3, n - 3, n - 2, n - 1 }); }
                }
                sheet.SetVertices(vertices); sheet.SetUVs(0, uvs); sheet.SetTriangles(triangles, 0); sheet.RecalculateBounds();
                meshes.Add(sheet);
                var obj = new GameObject(layer == 0 ? "Gorge cloud layer (upper)" : "Gorge cloud layer (lower)", typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root, false);
                obj.GetComponent<MeshFilter>().sharedMesh = sheet;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = gorgeMist; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void AnimateGorgeMist()
        {
            if (gorgeMist == null) return;
            gorgeMist.SetFloat("_FlowTime", clock);
            gorgeMist.SetColor("_Color", game.World == ShiftWorld.Echo ? new Color(.30f, .37f, .50f, .78f) : new Color(.95f, .97f, 1f, .82f));
        }
    }
}
