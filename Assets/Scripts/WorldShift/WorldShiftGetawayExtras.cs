using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Optional traversal and automatic protection, shared by the escape environments.</summary>
    public sealed class WorldShiftGetawayExtras : MonoBehaviour
    {
        private WorldShiftGetaway game;
        private WorldShiftRoute route;
        private Transform shieldPickup, shieldAura;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private float shieldProtectionUntil, feedbackUntil;
        private string feedback = "";
        public const float ShieldDistance = 44f;
        public const float ShieldLane = -3.2f;
        public WorldShiftBonusJump Jump { get; private set; }
        public bool ShieldTaken { get; private set; }
        public string Feedback => game.Duration < feedbackUntil ? feedback : "";
        public string Hint => game.Distance >= Jump.Start - 32f && game.Distance < Jump.Start
            ? "Right for a jump. Left for the road." : Jump.OnRamp ? "The ramp does the jumping." : "";

        public void Bind(WorldShiftGetaway owner, WorldShiftRoute road, Transform player, float environmentEnd, Func<Color, Material> material)
        {
            game = owner; route = road; Jump = new WorldShiftBonusJump(environmentEnd);
            Material gold = material(new Color(1f, .76f, .25f));
            Material deck = material(new Color(.10f, .17f, .22f));
            Material cyan = material(new Color(.25f, .85f, 1f));
            BuildRamp(deck, gold);
            var point = route.Sample(ShieldDistance);
            shieldPickup = Root("One-hit shield pickup", point.Position + point.Right * ShieldLane + Vector3.up * 1.5f, point.Forward);
            Line(shieldPickup, "Shield outline", new[] { new Vector3(0f, 1.3f, 0f), new Vector3(-.65f, 1f, 0f),
                new Vector3(-.52f, .1f, 0f), new Vector3(0f, -.3f, 0f), new Vector3(.52f, .1f, 0f), new Vector3(.65f, 1f, 0f) }, cyan, .12f, true);
            Line(shieldPickup, "Shield check", new[] { new Vector3(-.27f, .45f, 0f), new Vector3(-.03f, .20f, 0f), new Vector3(.34f, .76f, 0f) }, cyan, .09f, false);
            shieldAura = new GameObject("One-hit shield around car").transform;
            shieldAura.SetParent(player, false); shieldAura.localPosition = Vector3.up * .9f;
            for (int ring = 0; ring < 3; ring++)
            {
                var points = new Vector3[48];
                for (int i = 0; i < points.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / points.Length;
                    points[i] = ring == 0 ? new Vector3(Mathf.Cos(a) * 1.25f, 0f, Mathf.Sin(a) * 1.95f)
                        : ring == 1 ? new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a) * 1.95f)
                        : new Vector3(Mathf.Cos(a) * 1.25f, Mathf.Sin(a), 0f);
                }
                Line(shieldAura, "Shield ring", points, cyan, .045f, true);
            }
            Refresh();
        }

        public void BeginStep(float from, float to, float lateral) => Jump.BeginStep(from, to, lateral);
        public void FinishStep(float distance, AutoDriveCar driver)
        {
            Jump.FinishStep(distance);
            // Low rails guide the short approach; steering is fully available in the air.
            if (Jump.OnRamp)
            {
                float clamped = Mathf.Clamp(driver.LateralOffset, 2.5f, 4f);
                if (Mathf.Abs(clamped - driver.LateralOffset) > .001f) driver.ConstrainExternalLateral(clamped);
            }
        }
        public RoadPathPoint Sample(float distance, RoadPathPoint ground) => Jump.Sample(distance, ground);

        public void Collect(float from, float to, float fromX, float toX)
        {
            if (ShieldTaken || game.World != ShiftWorld.Day || WorldShiftFlow.Escape.HasShield
                || to < ShieldDistance - 1.3f || from > ShieldDistance + 1.3f) return;
            float t = Mathf.InverseLerp(from, to, ShieldDistance);
            if (Mathf.Abs(Mathf.Lerp(fromX, toX, t) - ShieldLane) > 1.35f) return;
            if (!WorldShiftFlow.Escape.CollectShield()) return;
            ShieldTaken = true;
            Say("Shield on. One hit's covered.");
            shieldPickup.gameObject.SetActive(false);
            Refresh();
        }

        public bool BlocksImpact(bool collisionProtected)
        {
            if (game.Duration < shieldProtectionUntil) return true;
            if (collisionProtected || !WorldShiftFlow.Escape.AbsorbHit()) return false;
            // One obstacle field should not immediately charge another contact for the same hit.
            shieldProtectionUntil = game.Duration + RiskRunState.CollisionProtectionDuration;
            Say("Shield saved you.");
            Refresh();
            return true;
        }

        public void Present(ShiftWorld displayedWorld)
        {
            if (shieldPickup == null) return;
            shieldPickup.gameObject.SetActive(!ShieldTaken && !WorldShiftFlow.Escape.HasShield
                && displayedWorld == ShiftWorld.Day && game.Distance < ShieldDistance + 10f && game.Distance > ShieldDistance - 180f);
        }
        public void Refresh()
        {
            if (shieldAura != null) shieldAura.gameObject.SetActive(WorldShiftFlow.Escape.HasShield
                && WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Running);
        }
        private void Say(string text) { feedback = text; feedbackUntil = game.Duration + 2f; }

        private void BuildRamp(Material deck, Material gold)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (float d = Jump.Start; d < Jump.Takeoff; d += 2f)
            {
                var a = route.Sample(d); var b = route.Sample(Mathf.Min(d + 2f, Jump.Takeoff));
                float h0 = Jump.HeightAt(d) + .08f, h1 = Jump.HeightAt(Mathf.Min(d + 2f, Jump.Takeoff)) + .08f;
                int n = vertices.Count;
                vertices.Add(a.Position + a.Right * 1.7f + Vector3.up * h0);
                vertices.Add(a.Position + a.Right * 4.7f + Vector3.up * h0);
                vertices.Add(b.Position + b.Right * 1.7f + Vector3.up * h1);
                vertices.Add(b.Position + b.Right * 4.7f + Vector3.up * h1);
                vertices.Add(a.Position + a.Right * 1.7f);
                vertices.Add(a.Position + a.Right * 4.7f);
                vertices.Add(b.Position + b.Right * 1.7f);
                vertices.Add(b.Position + b.Right * 4.7f);
                foreach (int i in new[] { 0, 2, 1, 1, 2, 3, 4, 6, 0, 0, 6, 2, 1, 3, 5, 5, 3, 7, 2, 6, 3, 3, 6, 7 }) triangles.Add(n + i);
            }
            var mesh = new Mesh { name = "Optional loot ramp deck" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var ramp = new GameObject("Optional loot ramp", typeof(MeshFilter), typeof(MeshRenderer));
            ramp.transform.SetParent(transform, true);
            ramp.GetComponent<MeshFilter>().sharedMesh = mesh; ramp.GetComponent<MeshRenderer>().sharedMaterial = deck;
            for (float d = Jump.Start + 3f; d < Jump.Takeoff; d += 6f)
            {
                var p = route.Sample(d); float h = Jump.HeightAt(d) + .12f;
                var marker = Root("Loot ramp chevron", p.Position + Vector3.up * h, p.Forward);
                Line(marker, "Follow the loot", new[] { new Vector3(2.5f, 0f, -.5f), new Vector3(3.2f, 0f, .5f), new Vector3(3.9f, 0f, -.5f) }, gold, .12f, false);
            }
            foreach (float side in new[] { 1.85f, 4.55f })
            {
                var points = new List<Vector3>();
                for (float d = Jump.Start + 2f; d <= Jump.Takeoff; d += 2f)
                { var p = route.Sample(d); points.Add(p.Position + p.Right * side + Vector3.up * (Jump.HeightAt(d) + .22f)); }
                var root = Root("Low ramp rail", Vector3.zero, Vector3.forward);
                Line(root, "Ramp edge", points.ToArray(), gold, .10f, false);
            }
            // A wide, flat road supports every landing, including one that misses the reward trail.
            for (int i = 0; i < 5; i++)
            {
                var p = route.Sample(Jump.Landing + i * 2f);
                var root = Root("Bonus landing marker", p.Position + Vector3.up * .09f, p.Forward);
                Line(root, "Landing strip", new[] { new Vector3(1.8f, 0f, 0f), new Vector3(4.6f, 0f, 0f) }, gold, .12f, false);
            }
        }

        private Transform Root(string name, Vector3 position, Vector3 forward)
        {
            var root = new GameObject(name).transform; root.SetParent(transform, true);
            root.SetPositionAndRotation(position, Quaternion.LookRotation(forward)); return root;
        }
        private static void Line(Transform parent, string name, Vector3[] points, Material material, float width, bool loop)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.useWorldSpace = false; line.loop = loop;
            line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = width;
            line.sharedMaterial = material; line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        }
        private void OnDestroy() { foreach (var mesh in meshes) if (mesh != null) Destroy(mesh); }
    }
}
