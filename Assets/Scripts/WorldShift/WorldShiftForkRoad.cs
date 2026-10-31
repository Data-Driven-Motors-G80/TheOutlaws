using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Two real carriageways with a shared longitudinal coordinate and explicit steering commitment.</summary>
    public sealed class WorldShiftForkRoad : MonoBehaviour
    {
        public sealed class Blocker
        {
            public float At, X;
            public Transform Visual;
            public bool Touched;
        }
        public readonly List<Blocker> Blockers = new List<Blocker>();
        public float EnvironmentEnd { get; private set; }
        public int RewardBranch { get; private set; }
        public int ExitBranch { get; private set; }
        public float ForkStart => EnvironmentEnd + 40f;
        public float ChoiceAt => EnvironmentEnd + 60f;
        public float Rejoin => EnvironmentEnd + 320f;
        public float ExitStart => EnvironmentEnd + 520f;
        public float ExitChoiceAt => EnvironmentEnd + 600f;
        private WorldShiftGetaway game;
        private WorldShiftRoute route;
        private Material deck, ground;
        private readonly List<Mesh> meshes = new List<Mesh>();

        public static bool Owns(float environmentEnd, float distance)
            => distance >= environmentEnd + 40f && distance < environmentEnd + 320f || distance >= environmentEnd + 520f;
        public bool RewardSeparated(float distance) => distance >= ChoiceAt && distance < Rejoin - 4f;
        public bool ExitSeparated(float distance) => distance >= ExitChoiceAt;
        public float Spread(float d)
        {
            if (d >= ExitStart) return 4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ExitChoiceAt, EnvironmentEnd + 680f, d));
            if (d < ForkStart || d >= Rejoin) return 0f;
            return 4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChoiceAt, EnvironmentEnd + 120f, d))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EnvironmentEnd + 240f, Rejoin, d)));
        }
        public int Branch(float distance, float lateral)
            => ExitSeparated(distance) ? ExitBranch != 0 ? ExitBranch : lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1
                : RewardSeparated(distance) ? RewardBranch != 0 ? RewardBranch : lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1 : 0;
        public bool ContinueSelected(float lateral) => ExitBranch != 0 ? ExitBranch > 0 : lateral >= WorldShiftGetaway.ContinueBoundary;
        public string Hint(float d)
            => d >= ForkStart - 70f && d < ChoiceAt ? game.World == ShiftWorld.Day ? "Left: ammo. Right: more loot." : "Left: ammo. Day brings loot." : "";

        public void Bind(WorldShiftGetaway owner, WorldShiftRoute path, float environmentEnd, bool desert, Func<Color, Material> material)
        {
            game = owner; route = path; EnvironmentEnd = environmentEnd;
            deck = material(new Color(.22f, .23f, .24f));
            ground = material(desert ? new Color(.5f, .3f, .14f) : new Color(.32f, .37f, .31f));
            var white = material(new Color(.85f, .89f, .85f));
            var gold = material(new Color(1f, .77f, .25f));
            var green = material(new Color(.4f, 1f, .68f));
            Build(ForkStart, Rejoin, white, gold);
            Build(ExitStart, environmentEnd + WorldShiftGetaway.ExitTail + 80f, green, gold);
            for (int i = 0; i < 2; i++)
            {
                float at = environmentEnd + (i == 0 ? 134f : 224f);
                float x = (WorldShiftFlow.Escape.Leg + i) % 2 == 0 ? 3.2f : 1.8f;
                var p = route.Sample(at);
                var root = new GameObject(desert ? "Breakable fork rock" : "Breakable fork barrier").transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(Position(at, x) + p.Up * .65f, Quaternion.LookRotation(p.Forward, p.Up));
                var body = GameObject.CreatePrimitive(desert ? PrimitiveType.Sphere : PrimitiveType.Cube);
                body.transform.SetParent(root, false); body.transform.localScale = new Vector3(1.2f, 1.3f, 1.4f);
                body.GetComponent<Collider>().enabled = false; body.GetComponent<Renderer>().sharedMaterial = material(new Color(.48f, .43f, .36f));
                WorldShiftCombat.MarkBreakable(root, new Vector3(0f, 0f, -.73f));
                Blockers.Add(new Blocker { At = at, X = x, Visual = root });
            }
            route.AttachForks(this);
        }

        public Vector3 Position(float d, float lateral)
        {
            var p = route.Sample(d);
            return p.Position + p.Right * (lateral + Mathf.Sign(lateral) * Spread(d));
        }
        public RoadPathPoint SampleDriver(float d, RoadPathPoint central, float lateral)
        {
            int side = Branch(d, lateral);
            if (side == 0) return central;
            float slope = (Spread(d + .1f) - Spread(d - .1f)) / .2f * side;
            return new RoadPathPoint(central.Position + central.Right * Spread(d) * side,
                (central.Forward + central.Right * slope).normalized, central.Width);
        }
        public void BeginStep(float from, float to, float lateral)
        {
            if (RewardBranch == 0 && to >= ChoiceAt && from < Rejoin) RewardBranch = lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1;
            if (ExitBranch == 0 && to >= ExitChoiceAt) ExitBranch = lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1;
        }
        public void Constrain(float distance, AutoDriveCar driver)
        {
            int side = Branch(distance, driver.LateralOffset);
            if (side == 0) return;
            float commit = ExitSeparated(distance) ? ExitChoiceAt : ChoiceAt;
            float inner = .9f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(commit, commit + 24f, distance));
            float x = side < 0 ? Mathf.Clamp(driver.LateralOffset, -4.1f, -inner) : Mathf.Clamp(driver.LateralOffset, inner, 4.1f);
            if (Mathf.Abs(x - driver.LateralOffset) > .001f) driver.ConstrainExternalLateral(x);
        }
        public void CollectContacts(float from, float to, float a, float b)
        {
            foreach (var block in Blockers)
            {
                bool destroyed = game.Combat != null && game.Combat.IsDestroyed(block);
                block.Visual.gameObject.SetActive(!destroyed && block.At > game.Distance - 15f && block.At < game.Distance + 190f);
                if (block.Touched || destroyed || RewardBranch <= 0 || to + 2f < block.At || from - 2f > block.At) continue;
                if (Mathf.Max(a, b) < block.X - 1.4f || Mathf.Min(a, b) > block.X + 1.4f) continue;
                block.Touched = true; game.HitForkObstacle(block.At);
            }
        }
        public void Present(ShiftWorld world)
        {
            deck.color = world == ShiftWorld.Day ? new Color(.22f, .23f, .24f) : new Color(.085f, .105f, .14f);
            foreach (var block in Blockers) block.Visual.gameObject.SetActive(!(game.Combat?.IsDestroyed(block) ?? false)
                && block.At > game.Distance - 15f && block.At < game.Distance + 190f);
        }

        private void Build(float start, float end, Material leftColour, Material rightColour)
        {
            var earth = new List<Vector3>(); var road = new List<Vector3>(); var earthTris = new List<int>(); var roadTris = new List<int>();
            for (float d = start; d < end; d += 4f)
            {
                float next = Mathf.Min(end, d + 4f);
                Quad(earth, earthTris, Point(d, -18f, -.14f), Point(d, 18f, -.14f), Point(next, -18f, -.14f), Point(next, 18f, -.14f));
                foreach (int side in new[] { -1, 1 })
                {
                    float min = side < 0 ? -5f : 0f, max = side < 0 ? 0f : 5f;
                    Quad(road, roadTris, Point(d, min + side * Spread(d), .025f), Point(d, max + side * Spread(d), .025f),
                        Point(next, min + side * Spread(next), .025f), Point(next, max + side * Spread(next), .025f));
                }
            }
            MeshObject("Fork ground", earth, earthTris, ground); MeshObject("Two separated carriageways", road, roadTris, deck);
            foreach (int side in new[] { -1, 1 })
            {
                Material colour = side < 0 ? leftColour : rightColour;
                foreach (float edge in side < 0 ? new[] { -4.9f, -.1f } : new[] { .1f, 4.9f })
                {
                    var points = new List<Vector3>();
                    for (float d = start; d <= end; d += 4f) points.Add(Point(d, edge + side * Spread(d), .08f));
                    Line("Branch edge", points, colour, .10f);
                }
                var rail = new List<Vector3>();
                for (float d = start + 20f; d <= end - 8f; d += 4f)
                    if (Spread(d) > .15f) rail.Add(Point(d, side * (Spread(d) + .12f), .45f));
                if (rail.Count > 1) Line("Visible inner branch rail", rail, colour, .13f);
                for (float d = start + 12f; d < end; d += 24f)
                {
                    var p = route.Sample(d); float x = side * (2.5f + Spread(d));
                    Line("Branch direction arrow", new List<Vector3> { p.Position + p.Right * (x - .55f) + p.Up * .09f - p.Forward,
                        p.Position + p.Right * x + p.Up * .09f, p.Position + p.Right * (x + .55f) + p.Up * .09f - p.Forward }, colour, .12f);
                }
            }
        }
        private Vector3 Point(float distance, float x, float y) { var p = route.Sample(distance); return p.Position + p.Right * x + p.Up * y; }
        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        { int n = v.Count; v.AddRange(new[] { a, b, c, d }); t.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }); }
        private void MeshObject(string name, List<Vector3> vertices, List<int> indices, Material material)
        {
            var mesh = new Mesh { name = name }; for (int i = 0; i < vertices.Count; i++) vertices[i] = transform.InverseTransformPoint(vertices[i]);
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = material;
        }
        private void Line(string name, List<Vector3> points, Material material, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(transform, false);
            line.positionCount = points.Count; line.SetPositions(points.ToArray()); line.sharedMaterial = material; line.widthMultiplier = width;
        }
        private void OnDestroy() { foreach (var mesh in meshes) if (mesh != null) Destroy(mesh); }
    }
}
