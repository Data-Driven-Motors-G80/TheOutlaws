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
        public static float MainForkStart(float environmentEnd) => environmentEnd == WorldShiftLevel.Length ? 1120f : 720f;
        public static bool InMainFork(float environmentEnd, float d, float padding = 0f)
            => d >= MainForkStart(environmentEnd) - padding && d < MainForkStart(environmentEnd) + 280f + padding;
        public float ForkStart => MainForkStart(EnvironmentEnd);
        public float ChoiceAt => ForkStart + 20f;
        public float Rejoin => ForkStart + 280f;
        public float ExitStart => EnvironmentEnd + 200f;
        public float ExitChoiceAt => EnvironmentEnd + 280f;
        private WorldShiftGetaway game;
        private WorldShiftRoute route;
        private Material deck, ground;
        private bool desertRoute;
        public float RightLootLane => 2.4f;
        private readonly List<Mesh> meshes = new List<Mesh>();

        public static bool Owns(float environmentEnd, float distance)
            => InMainFork(environmentEnd, distance) || distance >= environmentEnd + 200f;
        public bool RewardSeparated(float distance) => distance >= ChoiceAt && distance < Rejoin - 4f;
        public bool ExitSeparated(float distance) => distance >= ExitChoiceAt;
        public float Spread(float d)
        {
            if (d >= ExitStart) return 4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ExitChoiceAt, EnvironmentEnd + 360f, d));
            if (d < ForkStart || d >= Rejoin) return 0f;
            return 4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChoiceAt, ForkStart + 80f, d))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ForkStart + 200f, Rejoin, d)));
        }
        public int Branch(float distance, float lateral)
            => ExitSeparated(distance) ? ExitBranch != 0 ? ExitBranch : lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1
                : RewardSeparated(distance) ? RewardBranch != 0 ? RewardBranch : lateral >= .4f ? 1 : -1 : 0;
        public bool ContinueSelected(float lateral) => ExitBranch != 0 ? ExitBranch > 0 : lateral >= WorldShiftGetaway.ContinueBoundary;
        public string Hint(float d)
            => d >= ForkStart - 70f && d < ChoiceAt ? game.World == ShiftWorld.Day ? desertRoute ? "Open road left: ammo + boost. Loot in the canyon." : "Main road left: ammo + boost. Loot on the right." : "Left for ammo. Day brings loot." : "";

        public void Bind(WorldShiftGetaway owner, WorldShiftRoute path, float environmentEnd, bool desert, Func<Color, Material> material)
        {
            game = owner; route = path; EnvironmentEnd = environmentEnd; desertRoute = desert;
            deck = material(new Color(.22f, .23f, .24f));
            ground = material(desert ? new Color(.5f, .3f, .14f) : new Color(.32f, .37f, .31f));
            var white = material(new Color(.85f, .89f, .85f));
            var gold = material(new Color(1f, .77f, .25f));
            var green = material(new Color(.4f, 1f, .68f));
            Build(ForkStart, Rejoin, white, gold);
            Build(ExitStart, environmentEnd + WorldShiftGetaway.ExitTail + 80f, green, gold);
            for (int i = 0; i < 2; i++)
            {
                float at = ForkStart + (i == 0 ? 94f : 184f);
                float x = (WorldShiftFlow.Escape.Leg + i) % 2 == 0 ? 2.4f : 1.6f;
                var p = route.Sample(at);
                var root = new GameObject(desert ? "Breakable fork rock" : "Breakable fork barrier").transform;
                root.SetParent(transform, false);
                root.SetPositionAndRotation(Position(at, x) + p.Up * .65f, Quaternion.LookRotation(p.Forward, p.Up));
                WorldShiftObstacleVisual.Build(root, Vector3.zero, new Vector3(1.2f, 1.3f, 1.4f));
                Blockers.Add(new Blocker { At = at, X = x, Visual = root });
            }
            BuildBoundaries(material);
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
                (central.Forward + central.Right * slope).normalized, Mathf.Max(central.Width, Width(d, side) * 2f));
        }
        public void BeginStep(float from, float to, float lateral)
        {
            if (RewardBranch == 0 && to >= ChoiceAt && from < Rejoin) RewardBranch = lateral >= .4f ? 1 : -1;
            if (ExitBranch == 0 && to >= ExitChoiceAt) ExitBranch = lateral >= WorldShiftGetaway.ContinueBoundary ? 1 : -1;
        }
        public void Constrain(float distance, AutoDriveCar driver)
        {
            int side = Branch(distance, driver.LateralOffset);
            if (side == 0) return;
            float commit = ExitSeparated(distance) ? ExitChoiceAt : ChoiceAt;
            float inner = .9f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(commit, commit + 24f, distance));
            float x = side < 0 ? Mathf.Clamp(driver.LateralOffset, -Width(distance, -1) + .9f, -inner) : Mathf.Clamp(driver.LateralOffset, inner, Width(distance, 1) - .9f);
            if (Mathf.Abs(x - driver.LateralOffset) > .001f) driver.ConstrainExternalLateral(x);
        }
        public void CollectContacts(float from, float to, float a, float b)
        {
            foreach (var block in Blockers)
            {
                bool active = WorldShiftCombat.IsActive(game.Combat, block);
                block.Visual.gameObject.SetActive(active && block.At > game.Distance - 15f && block.At < game.Distance + 190f);
                if (block.Touched || !active || RewardBranch <= 0 || to + 2f < block.At || from - 2f > block.At) continue;
                if (Mathf.Max(a, b) < block.X - 1.4f || Mathf.Min(a, b) > block.X + 1.4f) continue;
                block.Touched = true; game.HitForkObstacle(block.At);
            }
        }
        public void Present(ShiftWorld world)
        {
            deck.color = world == ShiftWorld.Day ? new Color(.22f, .23f, .24f) : new Color(.085f, .105f, .14f);
            foreach (var block in Blockers) block.Visual.gameObject.SetActive(WorldShiftCombat.IsActive(game.Combat, block)
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
                    float min = side < 0 ? -Width(d, side) : 0f, max = side < 0 ? 0f : Width(d, side);
                    float nextMin = side < 0 ? -Width(next, side) : 0f, nextMax = side < 0 ? 0f : Width(next, side);
                    Quad(road, roadTris, Point(d, min + side * Spread(d), .025f), Point(d, max + side * Spread(d), .025f),
                        Point(next, nextMin + side * Spread(next), .025f), Point(next, nextMax + side * Spread(next), .025f));
                }
            }
            MeshObject("Fork ground", earth, earthTris, ground); MeshObject("Two separated carriageways", road, roadTris, deck);
            foreach (int side in new[] { -1, 1 })
            {
                Material colour = side < 0 ? leftColour : rightColour;
                foreach (bool outer in new[] { false, true })
                {
                    var points = new List<Vector3>();
                    for (float d = start; d <= end; d += 4f) points.Add(Point(d, side * (outer ? Width(d, side) - .1f : .1f) + side * Spread(d), .08f));
                    Line("Branch edge", points, colour, .10f);
                }
                var rail = new List<Vector3>();
                for (float d = start + 20f; d <= end - 8f; d += 4f)
                    if (Spread(d) > .15f) rail.Add(Point(d, side * (Spread(d) + .12f), .45f));
                if (rail.Count > 1) Line("Visible inner branch rail", rail, colour, .13f);
                for (float d = start + 12f; d < end; d += 24f)
                {
                    var p = route.Sample(d); float x = side * (Width(d, side) * .5f + Spread(d));
                    Line("Branch direction arrow", new List<Vector3> { p.Position + p.Right * (x - .55f) + p.Up * .09f - p.Forward,
                        p.Position + p.Right * x + p.Up * .09f, p.Position + p.Right * (x + .55f) + p.Up * .09f - p.Forward }, colour, .12f);
                }
            }
        }
        public float Width(float d, int side)
            => Mathf.Lerp(WorldShiftLevel.RoadWidth * .5f, WorldShiftLevel.RoadWidth, Spread(d) / 4f);

        private void BuildBoundaries(Func<Color, Material> material)
        {
            var surface = material(desertRoute ? new Color(.46f, .32f, .21f) : new Color(.4f, .43f, .46f));
            // Boundaries follow the widened carriageway, leaving a steering bypass beside every blocker.
            for (float d = ChoiceAt + 60f; d < Rejoin - 90f; d += 22f)
                foreach (bool inner in new[] { true, false })
                {
                    var p = route.Sample(d);
                    float x = Spread(d) + (inner ? -.8f : Width(d, 1) + .8f);
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = desertRoute ? "Canyon boundary" : "Side-street boundary";
                    wall.transform.SetParent(transform, false);
                    wall.transform.SetPositionAndRotation(p.Position + p.Right * x + p.Up * 2.1f, Quaternion.LookRotation(p.Forward, p.Up));
                    wall.transform.localScale = new Vector3(1.6f, 4.2f, 19f);
                    wall.GetComponent<Collider>().enabled = false;
                    wall.GetComponent<Renderer>().sharedMaterial = surface;
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
