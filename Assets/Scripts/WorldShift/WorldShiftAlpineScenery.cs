using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Snow forest and rocky mountain-pass art for the shared gameplay scene.</summary>
    public sealed partial class WorldShiftEnvironmentScenery
    {
        private bool IsAlpine => route.Course.Environment == ShiftLevel.Snow
            || route.Course.Environment == ShiftLevel.Mountain;
        private bool IsSnow => route.Course.Environment == ShiftLevel.Snow;
        private Transform snowfall;
        private ParticleSystem snowParticles;
        private WorldShiftCity snowGame;
        private Material snowflakeFinish;
        private float previousSnowDistance;
        public float SnowfallStrength { get; private set; }
        private static readonly Color Powder = new Color(.91f, .96f, 1f);
        private static readonly Color Granite = new Color(.43f, .46f, .48f);

        private void BuildAlpine()
        {
            if (!IsSnow) { BuildMountainPass(); return; }
            // Every placement checks the entire path, so bends cannot enter the scenery.
            int seed = 0;
            for (float at = -35f; at < route.Course.Length + 70f; at += IsSnow ? 19f : 29f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    int n = seed++;
                    float along = at + (side > 0 ? 9f : 0f);
                    float lateral = side * (15f + n % 3 * 3.3f);
                    if (!NearJump(along, 22f) && Clear(along, lateral, 5f))
                    {
                        var root = Root(IsSnow ? "Snow-laden spruce grove" : "Alpine fir and scree", along, lateral);
                        var mesh = new WorldShiftArtMesh();
                        Fir(mesh, new Vector3(0, -.35f, 0), 7f + n % 4 * 1.3f, IsSnow, n);
                        if (IsSnow)
                        {
                            Fir(mesh, new Vector3(side * 2.1f, -.4f, 2.4f), 5.3f, true, n + 3);
                            mesh.Rock(new Vector3(0, -.2f, -2.5f), new Vector3(2.5f, .85f, 1.7f), Powder, n);
                        }
                        else
                        {
                            for (int i = 0; i < 6; i++)
                                mesh.Rock(new Vector3(Mathf.Sin(i * 2.4f) * 2.9f, -.15f, Mathf.Cos(i * 2.4f) * 2.9f),
                                    new Vector3(.5f + i * .09f, .4f + i * .12f, .6f), Granite, n + i);
                        }
                        Render(root, "Batched firs and ground cover", mesh, surface);
                    }
                }
            }

            // Large silhouettes alternate sides; all footprints stay beyond the road.
            for (float at = -30f; at < route.Course.Length + 100f; at += 62f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    int n = seed++;
                    float along = at + (side > 0 ? 28f : 0f);
                    float radius = 21f + n % 3 * 3f;
                    float lateral = side * (IsSnow ? 62f : 54f);
                    if (!Clear(along, lateral, radius + 2f)) continue;
                    var root = Root(IsSnow ? "Distant snowy ridge" : "Jagged mountain massif", along, lateral);
                    var mesh = new WorldShiftArtMesh();
                    Peak(mesh, radius, (IsSnow ? 32f : 44f) + n % 4 * 7f, n, IsSnow);
                    Render(root, "Faceted rock and snowcap", mesh, surface);
                }
            }

            for (float at = 55f; at < route.Course.Length; at += IsSnow ? 125f : 64f)
            {
                int n = seed++;
                float side = n % 2 == 0 ? -1f : 1f;
                float lateral = side * (IsSnow ? 28f : 25f);
                if (NearJump(at, 35f) || !Clear(at, lateral, 12f)) continue;
                var root = Root(IsSnow ? "Snowbound timber lodge" : "Layered roadside crag", at, lateral);
                var mesh = new WorldShiftArtMesh();
                if (IsSnow) Lodge(root, mesh);
                else
                {
                    // Offset shelves break up the cliff rather than making a solid wall.
                    for (int layer = 0; layer < 5; layer++)
                    {
                        Color rock = layer % 2 == 0 ? Granite : new Color(.53f, .50f, .43f);
                        mesh.Rock(new Vector3(side * layer * .8f, layer * 2.7f - 1f, layer % 2),
                            new Vector3(6.8f - layer * .65f, 3.7f, 8f - layer * .6f), rock, n + layer);
                    }
                    mesh.Rock(new Vector3(side * 3f, 12.8f, 0), new Vector3(3.7f, .55f, 3.9f), Powder, n);
                }
                Render(root, IsSnow ? "Lodge timber and snowy roof" : "Stratified granite outcrop", mesh, surface);
            }
            route.VisitRoadPieces(AlpineEdges);
            if (IsSnow) BuildSnowfall();
        }

        private void BuildMountainPass()
        {
            // Three overlapping ranges give the pass a foreground, middle distance and skyline.
            // Keep their roots alive; perspective, fog and Unity frustum culling handle visibility.
            for (int tier = 0; tier < 3; tier++)
            {
                float step = tier == 0 ? 98f : tier == 1 ? 152f : 215f;
                float width = tier == 0 ? 66f : tier == 1 ? 110f : 158f;
                float span = tier == 0 ? 148f : tier == 1 ? 226f : 310f;
                float radius = Mathf.Sqrt(width * width + span * span) * .5f + 3f;
                for (float at = -140f; at < route.Course.Length + 180f; at += step)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        int seed = Mathf.RoundToInt((at + 140f) / step) + tier * 37;
                        float along = at + (side > 0 ? step * .43f : 0f);
                        float across = side * (tier == 0 ? 116f : tier == 1 ? 212f : 322f);
                        // Move a massif outwards if another part of the road bends towards it.
                        bool clear = false;
                        for (int attempt = 0; attempt < 4; attempt++)
                        {
                            if (Clear(along, across, radius)) { clear = true; break; }
                            across += side * 38f;
                        }
                        if (!clear) continue;
                        var root = Root(tier == 0 ? "Towering granite mountain flank"
                            : tier == 1 ? "High snow-capped range" : "Distant alpine skyline", along, across);
                        var mesh = new WorldShiftArtMesh();
                        float height = (tier == 0 ? 116f : tier == 1 ? 184f : 255f) + seed % 4 * 15f;
                        MountainRidge(mesh, width, span, height, seed + (side > 0 ? 7 : 0));
                        Render(root, "Crags, gullies and upper snowfields", mesh, surface);
                    }
            }

            // A rock face on the uphill side, with open views across the downhill side.
            for (float at = 8f; at < route.Course.Length + 20f; at += 31f)
            {
                int seed = Mathf.RoundToInt(at / 31f);
                if (NearJump(at, 38f)) continue;
                float cuttingOffset = -28f;
                if (!Clear(at, cuttingOffset, 22f)) cuttingOffset = -36f;
                if (!Clear(at, cuttingOffset, 22f)) continue;
                var root = Root("Uphill rock cutting", at, cuttingOffset);
                var mesh = new WorldShiftArtMesh();
                MountainRidge(mesh, 17f, 38f, 34f + seed % 4 * 4f, seed + 23);
                // Broken scree connects the cliff foot to the rough shoulder.
                for (int i = 0; i < 9; i++)
                {
                    float z = -12f + i * 3f;
                    mesh.Rock(new Vector3(9f + .5f * Mathf.Sin(i + seed), -.3f, z),
                        new Vector3(1.1f + i % 3 * .4f, .7f + i % 4 * .35f, 1.6f),
                        i % 2 == 0 ? Granite : new Color(.53f, .49f, .40f), seed + i);
                }
                Render(root, "Fractured rock cutting and scree apron", mesh, surface);
            }

            // Sparse wind-shaped fir clusters leave the large rock forms readable.
            for (float at = 18f; at < route.Course.Length; at += 43f)
            {
                int seed = Mathf.RoundToInt(at / 43f);
                float side = seed % 3 == 0 ? -1f : 1f;
                float lateral = side * (16f + seed % 3 * 2f);
                if (NearJump(at, 27f) || !Clear(at, lateral, 5.5f)) continue;
                var root = Root("Wind-swept alpine firs", at, lateral);
                var mesh = new WorldShiftArtMesh();
                // Downhill roots follow the lowered visual terrain, not the road height.
                float ground = MountainGroundHeight(Mathf.Abs(lateral) - WorldShiftLevel.RoadWidth * .5f, side);
                Fir(mesh, new Vector3(0, ground - .3f, 0), 6.5f + seed % 4, false, seed);
                Fir(mesh, new Vector3(side * 2f, ground - .7f, 2.5f), 4.4f + seed % 2, false, seed + 1);
                for (int i = 0; i < 4; i++)
                    mesh.Rock(new Vector3(i - 1.5f, ground - .3f, -2f),
                        new Vector3(.65f, .45f + i * .12f, .85f), Granite, seed + i);
                Render(root, "Alpine tree island", mesh, surface);
            }
            route.VisitRoadPieces(AlpineEdges);
        }

        // Same profile as the mountain roadside: a narrow shelf above a deep valley.
        private static float MountainGroundHeight(float offsetFromEdge, float side)
        {
            float t = Mathf.Clamp01((offsetFromEdge - 2.6f) / (55f - 2.6f));
            return (side > 0 ? -38f : 16f) * t;
        }

        private static void MountainRidge(WorldShiftArtMesh mesh, float width, float length, float height, int seed)
        {
            const int rows = 13, columns = 6;
            var points = new Vector3[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1);
                // Broad saddles and offset spires interrupt both silhouette and faces.
                // Mathf.Sin(Mathf.PI) can be slightly negative: clamp before fractional powers.
                // A NaN here corrupts mesh bounds and can make an entire ridge disappear.
                float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), .34f);
                float crest = height * envelope * (.68f + .25f * Mathf.PerlinNoise(row * .61f, seed * .37f)
                    + .10f * Mathf.Sin(row * 1.91f + seed));
                float shift = Mathf.Sin(row * 1.3f + seed) * .10f;
                for (int col = 0; col < columns; col++)
                {
                    float across = col / (float)(columns - 1);
                    float x = (across - .5f) * width;
                    float ridge = Mathf.Clamp01(1f - Mathf.Abs(across - (.47f + shift)) * 2f);
                    float baseY = width > 30f ? -60f : -8f;
                    float y = baseY + (crest - baseY) * Mathf.Pow(ridge, .72f);
                    y += col == 0 || col == columns - 1 ? 0f : Mathf.Sin(row * 3f + col * 4f + seed) * height * .045f;
                    points[row, col] = new Vector3(x, y, (t - .5f) * length);
                }
            }
            for (int row = 0; row < rows - 1; row++)
                for (int col = 0; col < columns - 1; col++)
                {
                    Vector3 a = points[row, col], b = points[row + 1, col];
                    Vector3 c = points[row + 1, col + 1], d = points[row, col + 1];
                    Color rock = Color.Lerp(new Color(.29f, .34f, .36f), new Color(.59f, .55f, .46f),
                        .25f + .65f * Mathf.Abs(Mathf.Sin(seed + row * 1.7f + col * .8f)));
                    // Split every face at an uneven snow line, following the rock facets.
                    float snowline = height * (.62f + .07f * Mathf.Sin(row * 1.8f + seed));
                    SnowFace(mesh, a, b, c, rock, snowline);
                    SnowFace(mesh, a, c, d, rock * .93f, snowline);
                }
            // Close range ends down to the valley floor; no hollow mountain silhouettes.
            float floor = width > 30f ? -60f : -8f;
            for (int col = 0; col < columns - 1; col++)
                foreach (int row in new[] { 0, rows - 1 })
                {
                    Vector3 a = points[row, col], b = points[row, col + 1];
                    Vector3 lowA = new Vector3(a.x, floor, a.z), lowB = new Vector3(b.x, floor, b.z);
                    if (row == 0) mesh.Quad(lowB, lowA, a, b, Granite);
                    else mesh.Quad(lowA, lowB, b, a, Granite);
                }
        }

        private static void SnowFace(WorldShiftArtMesh mesh, Vector3 a, Vector3 b, Vector3 c, Color rock, float line)
        {
            // Clip the triangle into rock and snow so the boundary crosses the face naturally.
            var triangle = new[] { a, b, c };
            for (int region = 0; region < 2; region++)
            {
                var polygon = new System.Collections.Generic.List<Vector3>(4);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = triangle[i], q = triangle[(i + 1) % 3];
                    bool inside = region == 0 ? p.y <= line : p.y >= line;
                    bool nextInside = region == 0 ? q.y <= line : q.y >= line;
                    if (inside) polygon.Add(p);
                    if (inside != nextInside)
                        polygon.Add(Vector3.Lerp(p, q, (line - p.y) / (q.y - p.y)));
                }
                Color colour = region == 0 ? rock : Powder;
                colour.a = 1f;
                for (int i = 1; i + 1 < polygon.Count; i++)
                    mesh.Triangle(polygon[0], polygon[i], polygon[i + 1], colour);
            }
        }

        private static void Fir(WorldShiftArtMesh mesh, Vector3 foot, float height, bool snowy, int seed)
        {
            mesh.Cylinder(foot, .24f, .12f, height * .85f, new Color(.30f, .23f, .18f), 7);
            for (int tier = 0; tier < 4; tier++)
            {
                float y = height * (.17f + tier * .17f);
                float radius = height * (.29f - tier * .052f);
                float crown = height * .43f;
                mesh.Cylinder(foot + Vector3.up * y, radius, 0f, crown,
                    tier % 2 == 0 ? new Color(.13f, .28f, .25f) : new Color(.18f, .35f, .28f), 9);
                if (snowy)
                    mesh.Cylinder(foot + Vector3.up * (y + crown * .18f), radius * .84f, 0f,
                        crown * .83f, tier % 2 == 0 ? Powder : new Color(.76f, .87f, .92f), 9);
            }
        }

        private static void Peak(WorldShiftArtMesh mesh, float radius, float height, int seed, bool snowy)
        {
            const int sides = 11;
            Vector3 summit = new Vector3(radius * .16f * Mathf.Sin(seed), height, radius * .13f);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 Base(float angle) => new Vector3(Mathf.Cos(angle) * radius * (.87f + .09f * Mathf.Sin(angle * 3 + seed)),
                    -3f, Mathf.Sin(angle) * radius * .84f);
                Vector3 x = Base(a), y = Base(b);
                float lineA = (snowy ? .38f : .70f) + .07f * Mathf.Sin(i * 2f + seed);
                float lineB = (snowy ? .38f : .70f) + .07f * Mathf.Sin((i + 1) * 2f + seed);
                Vector3 sx = Vector3.Lerp(x, summit, lineA), sy = Vector3.Lerp(y, summit, lineB);
                Color rock = i % 3 == 0 ? new Color(.37f, .42f, .45f) : i % 3 == 1 ? Granite : new Color(.55f, .54f, .49f);
                mesh.Quad(y, x, sx, sy, rock);
                mesh.Triangle(sy, sx, summit, i % 2 == 0 ? Powder : new Color(.69f, .80f, .87f));
            }
        }

        private void Lodge(Transform root, WorldShiftArtMesh mesh)
        {
            var timber = new Color(.38f, .23f, .15f);
            mesh.Box(new Vector3(0, 1.5f, 0), new Vector3(7f, 3.4f, 6f), timber);
            for (int i = 0; i < 8; i++)
                mesh.Box(new Vector3(0, .1f + i * .41f, -3.04f), new Vector3(7.1f, .10f, .12f), new Color(.24f, .16f, .12f));
            // A pitched roof with deep eaves, rather than a white cube.
            Vector3 a = new Vector3(-4.2f, 3.1f, -3.7f), b = new Vector3(-4.2f, 3.1f, 3.7f);
            Vector3 c = new Vector3(4.2f, 3.1f, -3.7f), d = new Vector3(4.2f, 3.1f, 3.7f);
            Vector3 peakA = new Vector3(0, 5.5f, -3.7f), peakB = new Vector3(0, 5.5f, 3.7f);
            mesh.Quad(a, b, peakB, peakA, Powder);
            mesh.Quad(peakA, peakB, d, c, Powder);
            mesh.Triangle(a, peakA, c, timber, true);
            mesh.Triangle(b, d, peakB, timber, true);
            mesh.Box(new Vector3(2.1f, 4.7f, 1f), new Vector3(.7f, 2.8f, .8f), Granite);
            mesh.Box(new Vector3(0, 1.1f, -3.1f), new Vector3(1.25f, 2.3f, .15f), new Color(.20f, .15f, .12f));
            var panes = new WorldShiftArtMesh();
            foreach (float x in new[] { -2.15f, 2.15f })
            {
                mesh.Box(new Vector3(x, 1.95f, -3.12f), new Vector3(1.55f, 1.3f, .15f), new Color(.18f, .15f, .12f));
                panes.Box(new Vector3(x, 1.95f, -3.22f), new Vector3(1.22f, 1.02f, .04f), new Color(1f, .70f, .32f));
                mesh.Box(new Vector3(x, 1.95f, -3.26f), new Vector3(.08f, 1.1f, .04f), timber);
            }
            Render(root, "Warm lodge windows", panes, windows);
            mesh.Rock(new Vector3(0, -.15f, -3.7f), new Vector3(4.9f, .55f, 1.2f), Powder, 7);
        }

        private void AlpineEdges(RoadSegment segment, float start, float end)
        {
            // Reuse exact road edge samples, including slope and gap boundaries.
            var filter = segment.transform.Find("Road").GetComponent<MeshFilter>();
            var vertices = filter.sharedMesh.vertices;
            int rows = vertices.Length / 2;
            var mesh = new WorldShiftArtMesh();
            Vector3 Local(Vector3 p) => transform.InverseTransformPoint(p);
            for (int i = 0; i < rows - 1; i++)
            {
                float at = Mathf.Lerp(start, end, (i + .5f) / (rows - 1));
                if (NearJump(at, 15f)) continue;
                Vector3 left = filter.transform.TransformPoint(vertices[i * 2]);
                Vector3 right = filter.transform.TransformPoint(vertices[i * 2 + 1]);
                Vector3 nextLeft = filter.transform.TransformPoint(vertices[(i + 1) * 2]);
                Vector3 nextRight = filter.transform.TransformPoint(vertices[(i + 1) * 2 + 1]);
                Vector3 across = (right - left).normalized, nextAcross = (nextRight - nextLeft).normalized;
                Vector3 up = segment.Sample(segment.Length * i / (rows - 1)).Up;
                Vector3 nextUp = segment.Sample(segment.Length * (i + 1) / (rows - 1)).Up;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 a = side < 0 ? left : right, b = side < 0 ? nextLeft : nextRight;
                    if (IsSnow)
                    {
                        // Raised snow banks start outside the playable carriageway.
                        Vector3 x = a + across * side * .65f, y = b + nextAcross * side * .65f;
                        Vector3 ridgeA = a + across * side * 2f + up * (.65f + .15f * Mathf.Sin(at * .15f));
                        Vector3 ridgeB = b + nextAcross * side * 2f + nextUp * (.65f + .15f * Mathf.Sin(at * .15f + .3f));
                        mesh.Triangle(Local(x), Local(y), Local(ridgeB), Powder, true);
                        mesh.Triangle(Local(x), Local(ridgeB), Local(ridgeA), Powder, true);
                        Vector3 outerA = a + across * side * 3.6f, outerB = b + nextAcross * side * 3.6f;
                        mesh.Triangle(Local(ridgeA), Local(ridgeB), Local(outerB), Powder, true);
                        mesh.Triangle(Local(ridgeA), Local(outerB), Local(outerA), Powder, true);
                    }
                    else
                    {
                        Vector3 x = a + across * side * 1.1f, y = b + nextAcross * side * 1.1f;
                        var steel = new Color(.57f, .63f, .63f);
                        mesh.Beam(Local(x + up * .85f), Local(y + nextUp * .85f), .13f, .26f, steel);
                        if (i % 3 == 0)
                        {
                            mesh.Beam(Local(x), Local(x + up * 1.05f), .14f, .14f, steel);
                            mesh.Box(Local(x + up * 1.1f), new Vector3(.19f, .18f, .19f), new Color(.87f, .84f, .67f));
                        }
                    }
                }
            }
            var root = new GameObject(IsSnow ? "Road-following snow banks" : "Mountain pass guardrails").transform;
            root.SetParent(transform, false);
            Render(root, root.name, mesh, surface);
            chunks.Add(new Chunk { At = (start + end) * .5f, Root = root.gameObject, Deck = true });
        }

        private void BuildSnowfall()
        {
            snowfall = new GameObject("Alpine snowfall - heavier over black ice").transform;
            snowfall.SetParent(transform, false);
            snowfall.position = route.Sample(22f).Position + Vector3.up * 12f;
            var particles = snowfall.gameObject.AddComponent<ParticleSystem>();
            snowParticles=particles;snowGame=GetComponentInParent<WorldShiftCity>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = 1100;
            main.startLifetime = 8f; main.startSpeed = 0f;
            main.startColor=Color.white;
            main.startSize = new ParticleSystem.MinMaxCurve(.065f, .14f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 16f, 65f);
            var emission = particles.emission; emission.rateOverTime = 22f;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = .4f; velocity.y = -2.5f; velocity.z = -1.2f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            snowflakeFinish=new Material(Resources.Load<Shader>("WorldShiftSnowfall")){name="Soft white Snow flakes"};
            materials.Add(snowflakeFinish);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = snowflakeFinish; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            particles.useAutoRandomSeed = false; particles.randomSeed = 347u;
            particles.Simulate(8f, true, true); particles.Play();
        }

        private void LateUpdate()
        {
            if (snowfall != null)
            {
                snowfall.position = route.Sample(distance + 22f).Position + Vector3.up * 12f;
                RefreshSnowfall();
            }
        }

        private void RefreshSnowfall()
        {
            if(snowParticles==null) return;
            if(snowGame!=null && (snowGame.State!=ShiftRunState.Running || snowGame.StoryActive || WorldShiftSceneLoading.Active))
            {
                if(snowParticles.isPlaying) snowParticles.Pause();
                return;
            }
            // Feather the weather at each ice boundary. Fade early enough for the dry ramp approach.
            float strength=0;
            foreach(var zone in route.Course.IceZones)
            {
                float entry=Mathf.SmoothStep(0,1,Mathf.InverseLerp(zone.x,zone.x+18f,distance));
                float exit=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(zone.y-45f,zone.y,distance));
                strength=Mathf.Max(strength,entry*exit);
            }
            if(snowGame!=null && snowGame.IsRelayAirborne) strength=0;
            SnowfallStrength=strength;
            snowflakeFinish.SetFloat("_Night",night?1f:0f);
            // Retries must not carry a dense cloud from the ice back onto a clear checkpoint.
            if(Mathf.Abs(distance-previousSnowDistance)>80f) snowParticles.Clear();
            previousSnowDistance=distance;
            var emission=snowParticles.emission;emission.rateOverTime=Mathf.Lerp(22f,165f,strength);
            var main=snowParticles.main;
            main.startLifetime=Mathf.Lerp(8f,4f,strength);
            main.startSize=new ParticleSystem.MinMaxCurve(Mathf.Lerp(.065f,.085f,strength),Mathf.Lerp(.14f,.18f,strength));
            var velocity=snowParticles.velocityOverLifetime;
            velocity.x=Mathf.Lerp(.4f,1.2f,strength);velocity.y=Mathf.Lerp(-2.5f,-4.2f,strength);
            velocity.z=Mathf.Lerp(-1.2f,-1.9f,strength);
            if(!snowParticles.isPlaying) snowParticles.Play();
        }

        private void ApplyAlpineAtmosphere(Camera camera, Light sun)
        {
            Color horizon = night ? new Color(.065f, .11f, .19f)
                : IsSnow ? new Color(.74f, .85f, .92f) : new Color(.67f, .77f, .82f);
            sky.SetFloat("_Night", night ? 1f : 0f);
            sky.SetColor("_Horizon", horizon);
            sky.SetColor("_Zenith", night ? new Color(.012f, .026f, .07f)
                : IsSnow ? new Color(.35f, .57f, .75f) : new Color(.20f, .43f, .65f));
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = IsSnow ? (night ? 55f : 110f) : (night ? 120f : 240f);
            RenderSettings.fogEndDistance = IsSnow ? (night ? 175f : 260f) : (night ? 780f : 1150f);
            if (!IsSnow) camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1400f);
            sun.color = night ? new Color(.56f, .74f, 1f)
                : IsSnow ? new Color(.86f, .94f, 1f) : new Color(1f, .91f, .77f);
        }
    }
}
