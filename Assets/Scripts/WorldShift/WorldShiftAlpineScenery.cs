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
        private static readonly Color Powder = new Color(.91f, .96f, 1f);
        private static readonly Color Granite = new Color(.43f, .46f, .48f);

        private void BuildAlpine()
        {
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
            snowfall = new GameObject("Light alpine snowfall").transform;
            snowfall.SetParent(transform, false);
            snowfall.position = route.Sample(22f).Position + Vector3.up * 12f;
            var flakes = new WorldShiftArtMesh();
            flakes.Triangle(new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(0, .7f, 0), Powder, true);
            flakes.Triangle(new Vector3(0, 0, -.5f), new Vector3(0, 0, .5f), new Vector3(0, .7f, 0), Powder, true);
            Mesh flake = flakes.Mesh("Reusable snowflake"); meshes.Add(flake);
            var particles = snowfall.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.playOnAwake = false; main.maxParticles = 180;
            main.startLifetime = 8f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.065f, .14f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 16f, 65f);
            var emission = particles.emission; emission.rateOverTime = 22f;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = .4f; velocity.y = -2.5f; velocity.z = -1.2f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = flake;
            renderer.sharedMaterial = surface; renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.useAutoRandomSeed = false; particles.randomSeed = 347u;
            particles.Simulate(8f, true, true); particles.Play();
        }

        private void LateUpdate()
        {
            if (snowfall != null)
                snowfall.position = route.Sample(distance + 22f).Position + Vector3.up * 12f;
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
            RenderSettings.fogStartDistance = night ? 55f : IsSnow ? 110f : 155f;
            RenderSettings.fogEndDistance = night ? 175f : IsSnow ? 260f : 295f;
            sun.color = night ? new Color(.56f, .74f, 1f)
                : IsSnow ? new Color(.86f, .94f, 1f) : new Color(1f, .91f, .77f);
        }
    }
}
