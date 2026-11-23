using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain finishing touches, all visual only (no colliders, never on the road):
    /// valley mist below the road on the drop side, waterfalls on a few uphill cliff faces, picture-only falling-rock
    /// signs, timber chalets with lit windows, and painted stone road markers.
    /// </summary>
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private readonly List<Material> flowMaterials = new List<Material>();
        private readonly List<Material> mistMaterials = new List<Material>();
        private Material waterfallMaterial;

        private void BuildMountainDressing()
        {
            BuildValleyMist();
            BuildFallingRockSigns();
            BuildChalets();
            BuildStoneMarkers();
        }

        // ---------- Valley mist ----------

        private Material MistMaterial(float seed)
        {
            var material = new Material(Resources.Load<Shader>("WorldShiftMountainMist")) { name = "Valley mist " + seed };
            material.SetFloat("_Mode", 0f);
            material.SetFloat("_Seed", seed);
            materials.Add(material); mistMaterials.Add(material); flowMaterials.Add(material);
            return material;
        }

        private void BuildValleyMist()
        {
            var shared = new[] { MistMaterial(1.3f), MistMaterial(7.9f), MistMaterial(13.1f) };
            int index = 0;
            for (float at = -40f; at < route.Course.Length + 120f; at += 62f, index++)
                for (int layer = 0; layer < 2; layer++)
                {
                    float lateral = 78f + layer * 34f + (index % 3) * 9f;
                    float height = -24f - layer * 9f - (index % 2) * 3f;
                    float width = 120f + layer * 40f, depth = 86f + layer * 20f;
                    // Mist only ever sits below the road; skip a bank wherever another arm of the road dips under it.
                    if (!MistClear(at, lateral, height, width * .5f, depth * .5f)) continue;
                    var root = Root("Valley mist bank", at, lateral);
                    var bank = Sheet("Soft valley mist", new Vector3(0f, height, 0f), width, depth, Quaternion.identity, root);
                    bank.GetComponent<MeshRenderer>().sharedMaterial = shared[(index + layer) % shared.Length];
                }
        }

        private bool MistClear(float at, float lateral, float height, float halfWidth, float halfDepth)
        {
            var p = route.Sample(at);
            Vector3 centre = p.Position + p.Right * lateral + Vector3.up * height;
            Vector3 right = Vector3.ProjectOnPlane(p.Right, Vector3.up).normalized, forward = Vector3.Cross(right, Vector3.up);
            foreach (var road in roadPoints)
            {
                Vector3 offset = road - centre;
                if (Mathf.Abs(Vector3.Dot(offset, right)) > halfWidth + 8f || Mathf.Abs(Vector3.Dot(offset, forward)) > halfDepth + 8f) continue;
                if (road.y < centre.y + 6f) return false;
            }
            return true;
        }

        // ---------- Waterfalls ----------

        /// <summary>Adds a waterfall down the road-facing slope of an uphill rock cutting (called for a few cuttings).</summary>
        private void AddWaterfall(Transform cutting, int seed)
        {
            if (waterfallMaterial == null)
            {
                waterfallMaterial = new Material(Resources.Load<Shader>("WorldShiftMountainMist")) { name = "Falling water" };
                waterfallMaterial.SetFloat("_Mode", 1f);
                waterfallMaterial.SetFloat("_Seed", 3.7f);
                materials.Add(waterfallMaterial); flowMaterials.Add(waterfallMaterial);
            }
            // The cutting's road-facing slope runs from its crest (x≈1.5) down to its foot (x≈8.5); hang the water
            // just in front of it.
            Vector3 top = new Vector3(2.4f, 24f + seed % 3 * 2f, 0f), foot = new Vector3(9.4f, 1.5f, 0f);
            Vector3 down = foot - top;
            var rotation = Quaternion.LookRotation(Vector3.Cross(down.normalized, Vector3.forward), -down.normalized);
            var fall = Sheet("Waterfall", (top + foot) * .5f + Vector3.right * .5f, 2.6f + seed % 2, down.magnitude, rotation, cutting, true);
            fall.GetComponent<MeshRenderer>().sharedMaterial = waterfallMaterial;
            // Spray at the foot and a small plunge pool ringed with stones.
            var spray = Sheet("Waterfall spray", foot + new Vector3(1.4f, 1.4f, 0f), 6f, 3.4f,
                Quaternion.LookRotation(Vector3.right, Vector3.up), cutting, true);
            var sprayMaterial = MistMaterial(seed * 1.7f);
            spray.GetComponent<MeshRenderer>().sharedMaterial = sprayMaterial;
            var pool = new WorldShiftArtMesh();
            pool.Cylinder(foot + new Vector3(1.2f, -.35f, 0f), 2.2f, 2.3f, .25f, new Color(.32f, .55f, .66f), 12);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                pool.Rock(foot + new Vector3(1.2f + Mathf.Cos(a) * 2.4f, -.4f, Mathf.Sin(a) * 2.4f), new Vector3(.45f, .5f, .45f), Granite, seed + i);
            }
            Render(cutting, "Plunge pool and stones", pool, surface);
        }

        /// <summary>A flat quad with UVs (x across, y along), lying in local XZ unless <paramref name="upright"/>.</summary>
        private GameObject Sheet(string label, Vector3 centre, float width, float length, Quaternion rotation, Transform parent, bool upright = false)
        {
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = centre;
            obj.transform.localRotation = rotation;
            var mesh = new Mesh { name = label };
            float w = width * .5f, l = length * .5f;
            mesh.vertices = upright
                ? new[] { new Vector3(-w, -l, 0), new Vector3(w, -l, 0), new Vector3(w, l, 0), new Vector3(-w, l, 0) }
                : new[] { new Vector3(-w, 0, -l), new Vector3(w, 0, -l), new Vector3(w, 0, l), new Vector3(-w, 0, l) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return obj;
        }

        // ---------- Roadside dressing ----------

        // Horizontal distance from the road centre line, over the whole route.
        private bool FarFromRoad(Vector3 point, float minimum)
        {
            foreach (var road in roadPoints)
                if (new Vector2(point.x - road.x, point.z - road.z).sqrMagnitude < minimum * minimum) return false;
            return true;
        }

        private bool NearGap(float at, float margin)
        {
            foreach (var gap in route.Course.EdgeGaps) if (at > gap.x - margin && at < gap.y + margin) return true;
            return false;
        }

        // Yellow diamond with a black falling-rocks pictogram: a cliff wedge and three tumbling rocks. No text.
        private void BuildFallingRockSigns()
        {
            int index = 0;
            for (float at = 70f; at < route.Course.Length - 40f; at += 245f, index++)
            {
                if (NearJump(at, 40f)) continue;
                const float lateral = -8.4f;
                var p = route.Sample(at);
                if (!FarFromRoad(p.Position + p.Right * lateral, 7.9f)) continue;
                var root = Root("Falling rocks warning sign", at, lateral);
                var art = new WorldShiftArtMesh();
                Color yellow = new Color(1f, .78f, .12f), black = new Color(.06f, .06f, .07f), grey = new Color(.62f, .64f, .66f);
                art.Box(new Vector3(0f, 1.1f, 0f), new Vector3(.1f, 2.2f, .1f), grey);
                Vector3 c = new Vector3(0f, 2.55f, -.07f);
                float r = .72f;
                Vector3 D(float x, float y, float z = 0f) => c + new Vector3(x, y, z);
                // Black rim, then the yellow face, facing oncoming drivers (-Z).
                art.Quad(D(0, r + .06f, .01f), D(r + .06f, 0, .01f), D(0, -r - .06f, .01f), D(-r - .06f, 0, .01f), black);
                art.Quad(D(0, r, -.005f), D(r, 0, -.005f), D(0, -r, -.005f), D(-r, 0, -.005f), yellow);
                art.Quad(D(-r, 0, .02f), D(0, -r, .02f), D(r, 0, .02f), D(0, r, .02f), yellow);
                // Cliff wedge on the left and falling rocks on the right.
                art.Triangle(D(-.42f, -.32f, -.015f), D(-.42f, .36f, -.015f), D(-.08f, -.32f, -.015f), black, true);
                art.Box(D(-.05f, -.36f, -.015f), new Vector3(.62f, .05f, .01f), black);
                foreach (var rock in new[] { new Vector3(.05f, .22f, .11f), new Vector3(.2f, .02f, .09f), new Vector3(.12f, -.2f, .07f) })
                    art.Quad(D(rock.x - rock.z, rock.y, -.016f), D(rock.x, rock.y + rock.z, -.016f),
                        D(rock.x + rock.z, rock.y, -.016f), D(rock.x, rock.y - rock.z, -.016f), black);
                Render(root, "Sign face and post", art, surface);
            }
        }

        // Timber chalets perched on stone plinths on the valley side, windows lit at Night.
        private void BuildChalets()
        {
            int index = 0;
            for (float at = 230f; at < route.Course.Length; at += 520f, index++)
            {
                float lateral = 25f + index % 2 * 4f;
                if (NearJump(at, 30f) || NearGap(at, 40f)) continue;
                var p = route.Sample(at);
                if (!FarFromRoad(p.Position + p.Right * lateral, 15f)) continue;
                var root = Root("Timber chalet with lit windows", at, lateral);
                float ground = MountainGroundHeight(lateral - WorldShiftLevel.RoadWidth * .5f, 1f);
                var body = new WorldShiftArtMesh();
                var glass = new WorldShiftArtMesh();
                Color stone = new Color(.52f, .5f, .46f), timber = new Color(.50f, .31f, .17f), timberDark = new Color(.33f, .2f, .11f);
                Color roof = new Color(.30f, .24f, .21f), snow = new Color(.92f, .95f, .98f), shutter = new Color(.62f, .14f, .12f);
                // Stone plinth reaching down the slope, timber storey, steep roof with snow on the ridge.
                body.ChamferBox(new Vector3(0f, ground + 1.2f, 0f), new Vector3(8.4f, 7f, 7.4f), .2f, stone);
                body.ChamferBox(new Vector3(0f, ground + 6.2f, 0f), new Vector3(8f, 3f, 7f), .08f, timber);
                for (int i = 0; i < 5; i++)
                    body.Box(new Vector3(0f, ground + 4.9f + i * .62f, -3.52f), new Vector3(8.02f, .06f, .04f), timberDark);
                float eave = ground + 7.7f, ridge = ground + 11f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(side * 5f, eave, -4.4f), b = new Vector3(side * 5f, eave, 4.4f);
                    Vector3 c = new Vector3(0f, ridge, 4.4f), d = new Vector3(0f, ridge, -4.4f);
                    if (side > 0) { body.Quad(a, d, c, b, roof); body.Quad(b, c, d, a, roof); }
                    else { body.Quad(b, c, d, a, roof); body.Quad(a, d, c, b, roof); }
                }
                foreach (float z in new[] { -3.5f, 3.5f })
                    body.Triangle(new Vector3(-4f, eave, z), new Vector3(0f, ridge - .25f, z), new Vector3(4f, eave, z), timber, true);
                body.Box(new Vector3(0f, ridge + .05f, 0f), new Vector3(.9f, .25f, 8.9f), snow);
                // Balcony facing the road, windows with red shutters, a chimney.
                body.Box(new Vector3(-4.6f, ground + 5.1f, 0f), new Vector3(1.1f, .12f, 6.6f), timberDark);
                for (float z = -3.2f; z <= 3.2f; z += .55f) body.Box(new Vector3(-5.1f, ground + 5.55f, z), new Vector3(.06f, .8f, .06f), timber);
                body.Box(new Vector3(-5.1f, ground + 5.95f, 0f), new Vector3(.1f, .1f, 6.6f), timberDark);
                foreach (float z in new[] { -1.8f, 1.8f })
                {
                    glass.Box(new Vector3(-4.02f, ground + 6.4f, z), new Vector3(.06f, 1.1f, .9f), new Color(1f, .82f, .5f));
                    foreach (float s in new[] { -1f, 1f })
                        body.Box(new Vector3(-4.04f, ground + 6.4f, z + s * .72f), new Vector3(.05f, 1.15f, .45f), shutter);
                }
                glass.Box(new Vector3(0f, ground + 6.4f, -3.52f), new Vector3(1f, 1f, .06f), new Color(1f, .82f, .5f));
                body.Box(new Vector3(2f, ridge - .6f, 1.6f), new Vector3(.8f, 2.4f, .8f), stone);
                Render(root, "Chalet stone, timber and roof", body, surface);
                Render(root, "Chalet lit windows", glass, windows);
            }
        }

        // Red-and-white topped stone road markers just beyond the uphill guardrail.
        private void BuildStoneMarkers()
        {
            for (float at = 25f; at < route.Course.Length; at += 48f)
            {
                if (NearJump(at, 20f)) continue;
                const float lateral = -7.1f;
                var p = route.Sample(at);
                if (!FarFromRoad(p.Position + p.Right * lateral, 6.8f)) continue;
                var root = Root("Painted stone road marker", at, lateral);
                var art = new WorldShiftArtMesh();
                art.ChamferBox(new Vector3(0f, .42f, 0f), new Vector3(.32f, .84f, .26f), .06f, new Color(.80f, .79f, .76f));
                art.ChamferBox(new Vector3(0f, .76f, 0f), new Vector3(.34f, .18f, .28f), .05f, new Color(.78f, .14f, .12f));
                art.Rock(new Vector3(.25f, 0f, .15f), new Vector3(.22f, .2f, .2f), Granite, Mathf.RoundToInt(at));
                Render(root, "Marker stone", art, surface);
            }
        }

        private void AnimateMountainDressing(float clock)
        {
            if (flowMaterials.Count == 0) return;
            Color mist = night ? new Color(.30f, .37f, .50f, .55f) : new Color(.95f, .97f, 1f, .62f);
            Color water = night ? new Color(.45f, .58f, .72f, .78f) : new Color(.78f, .9f, .97f, .85f);
            foreach (var material in flowMaterials)
            {
                material.SetFloat("_FlowTime", clock);
                material.SetColor("_Color", material == waterfallMaterial ? water : mist);
            }
        }
    }
}
