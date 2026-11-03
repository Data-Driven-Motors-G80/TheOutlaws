using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Optional traversal and automatic power-ups, shared by the escape environments.</summary>
    public sealed class WorldShiftGetawayExtras : MonoBehaviour
    {
        private WorldShiftGetaway game;
        private WorldShiftRoute route;
        private Transform shieldPickup, shieldAura, magnetPickup, boostPickup, magnetAura, boostAura;
        private bool shieldTaken, magnetTaken, boostTaken;
        private bool shieldPassed, magnetPassed, boostPassed;
        private float magnetUntil, boostUntil;
        public const float MagnetDistance = 150f;
        public float BoostDistance { get; private set; }
        private const float PreviewDistance = 80f;
        public const float MagnetLane = 3.2f;
        public const float BoostLane = -2.8f;
        public const float MagnetDuration = 6f, BoostDuration = 4f;
        public bool MagnetActive => ActiveInDay && game.Duration < magnetUntil;
        public bool BoostActive => ActiveInDay && game.Duration < boostUntil;
        private bool Active => game != null && WorldShiftFlow.Escape.Status == GetawayRunState.RunStatus.Running;
        private bool ActiveInDay => Active && game.World == ShiftWorld.Day;
        public float SpeedMultiplier => BoostActive ? 1.2f : 1f;
        public float SteeringMultiplier => 1f;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private float shieldProtectionUntil, feedbackUntil;
        private string feedback = "";
        public const float ShieldDistance = 44f;
        public const float ShieldLane = -3.2f;
        public WorldShiftBonusJump Jump { get; private set; }
        public bool ShieldTaken { get; private set; }
        public string Feedback => game.Duration < feedbackUntil ? feedback : "";
        public string Hint => game.World == ShiftWorld.Day && !shieldTaken && !WorldShiftFlow.Escape.HasShield && Approaching(ShieldDistance)
            ? "Shield left. Protection for the next obstacle."
            : game.World == ShiftWorld.Day && !magnetTaken && Approaching(MagnetDistance)
            ? "Magnet right. Pull in the upcoming loot."
            : game.World == ShiftWorld.Day && !boostTaken && Approaching(BoostDistance)
            ? "Boost left. Gain distance on the open branch."
            : game.Distance >= Jump.Start - 56f && game.Distance < Jump.Start
            ? "Drive up the ramp to jump." : Jump.OnRamp ? "Drive up the ramp to jump." : "";
        private bool Approaching(float at) => game.Distance >= at - PreviewDistance && game.Distance < at - 2f;

        public void Bind(WorldShiftGetaway owner, WorldShiftRoute road, Transform player, float environmentEnd, Func<Color, Material> material)
        {
            game = owner; route = road; Jump = new WorldShiftBonusJump(environmentEnd);
            // One of each per leg: protection early, magnet before loot, boost on the clear fork branch.
            BoostDistance = road.Forks.ForkStart + 100f;
            Material gold = material(new Color(1f, .76f, .25f));
            Material deck = material(new Color(.10f, .17f, .22f));
            Material cyan = material(new Color(.25f, .85f, 1f));
            Material orange = material(new Color(1f, .38f, .12f));
            BuildRamp(deck, gold);
            var point = route.Sample(ShieldDistance);
            shieldPickup = Root("One-hit shield pickup", point.Position + point.Right * ShieldLane + Vector3.up * 1.5f, point.Forward);
            WorldShiftPickupVisual.Build(shieldPickup, "SHIELD", cyan);
            shieldAura = new GameObject("One-hit shield around car").transform;
            shieldAura.SetParent(player, false); shieldAura.localPosition = Vector3.up * .15f;
            Line(shieldAura, "Active shield square", new[] { new Vector3(-1.1f, 0f, -1.8f), new Vector3(1.1f, 0f, -1.8f),
                new Vector3(1.1f, 0f, 1.8f), new Vector3(-1.1f, 0f, 1.8f) }, cyan, .06f, true);
            Material purple = material(new Color(.85f, .45f, 1f));
            point = route.Sample(MagnetDistance);
            magnetPickup = Root("Loot magnet pickup", point.Position + point.Right * MagnetLane + Vector3.up * 1.5f, point.Forward);
            WorldShiftPickupVisual.Build(magnetPickup, "MAGNET", purple);
            point = route.Sample(BoostDistance);
            boostPickup = Root("Getaway boost pickup", road.Forks.Position(BoostDistance, BoostLane) + Vector3.up * 1.5f, point.Forward);
            WorldShiftPickupVisual.Build(boostPickup, "BOOST", orange);
            magnetAura = new GameObject("Active magnet indicator").transform;
            magnetAura.SetParent(player, false); magnetAura.localPosition = new Vector3(0f, 1.2f, -1.1f);
            Line(magnetAura, "Active magnet symbol", new[] { new Vector3(-.35f, .4f, 0f), new Vector3(-.35f, -.2f, 0f),
                new Vector3(0f, -.4f, 0f), new Vector3(.35f, -.2f, 0f), new Vector3(.35f, .4f, 0f) }, purple, .1f, false);
            boostAura = new GameObject("Active boost indicator").transform;
            boostAura.SetParent(player, false); boostAura.localPosition = new Vector3(0f, 1.2f, -1.1f);
            Line(boostAura, "Active boost symbol", new[] { new Vector3(.2f, .5f, 0f), new Vector3(-.3f, 0f, 0f),
                new Vector3(.2f, 0f, 0f), new Vector3(-.2f, -.5f, 0f) }, orange, .1f, false);
            Refresh();
        }

        public bool IsJumpSequence(float distance) => Jump != null && distance >= Jump.Start - 32f && distance < Jump.End;

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
            if (!Active) return;
            RecordPassed(from);
            if (game.World != ShiftWorld.Day) return;
            bool Crosses(float at, float lane)
            {
                if (to < at - 1.3f || from > at + 1.3f) return false;
                float entry = Mathf.InverseLerp(from, to, at - 1.3f);
                float exit = Mathf.InverseLerp(from, to, at + 1.3f);
                float a = Mathf.Lerp(fromX, toX, entry), b = Mathf.Lerp(fromX, toX, exit);
                return Mathf.Max(a, b) >= lane - 1.35f && Mathf.Min(a, b) <= lane + 1.35f;
            }
            if (!shieldTaken && Crosses(ShieldDistance, ShieldLane) && WorldShiftFlow.Escape.CollectShield())
            {
                shieldTaken = ShieldTaken = true;
                Say("Shield on.");
                WorldShiftAnalytics.PowerUp("shield");
            }
            if (!magnetTaken && Crosses(MagnetDistance, MagnetLane))
            {
                magnetTaken = true;
                magnetUntil = game.Duration + MagnetDuration;
                Say("Magnet on!");
                WorldShiftAnalytics.PowerUp("magnet");
            }
            if (!boostTaken && Crosses(BoostDistance, BoostLane))
            {
                boostTaken = true;
                boostUntil = game.Duration + BoostDuration;
                Say("Boost!");
                WorldShiftAnalytics.PowerUp("boost");
            }
            Refresh();
        }

        // A power-up the player drove past (in either world) counts as missed; a shield already held is not on offer,
        // and the boost on the left fork branch is not missed by players who took the right.
        private void RecordPassed(float from)
        {
            if (!shieldPassed && from > ShieldDistance + 1.3f)
            {
                shieldPassed = true;
                if (!shieldTaken && !WorldShiftFlow.Escape.HasShield) WorldShiftAnalytics.Missed("shield");
            }
            if (!magnetPassed && from > MagnetDistance + 1.3f)
            {
                magnetPassed = true;
                if (!magnetTaken) WorldShiftAnalytics.Missed("magnet");
            }
            if (!boostPassed && from > BoostDistance + 1.3f)
            {
                boostPassed = true;
                if (!boostTaken && !(route.Forks.RewardSeparated(BoostDistance) && route.Forks.RewardBranch > 0)) WorldShiftAnalytics.Missed("boost");
            }
        }

        public bool BlocksImpact(bool collisionProtected)
        {
            if (!ActiveInDay) return false;
            if (game.Duration < shieldProtectionUntil) return true;
            if (collisionProtected || !WorldShiftFlow.Escape.AbsorbHit()) return false;
            // One obstacle field should not immediately charge another contact for the same hit.
            shieldProtectionUntil = game.Duration + RiskRunState.CollisionProtectionDuration;
            Say("Shield saved you.");
            WorldShiftAnalytics.ShieldSaved();
            Refresh();
            return true;
        }

        public void Present(ShiftWorld displayedWorld)
        {
            if (shieldPickup == null) return;
            bool Visible(float at) => Active && displayedWorld == ShiftWorld.Day
                && game.Distance >= at - PreviewDistance && game.Distance <= at + 1.3f;
            shieldPickup.gameObject.SetActive(Visible(ShieldDistance) && !shieldTaken && !WorldShiftFlow.Escape.HasShield);
            magnetPickup.gameObject.SetActive(Visible(MagnetDistance) && !magnetTaken);
            boostPickup.gameObject.SetActive(Visible(BoostDistance) && !boostTaken);
        }

        public void Refresh()
        {
            if (shieldAura != null) shieldAura.gameObject.SetActive(ActiveInDay && WorldShiftFlow.Escape.HasShield);
            if (magnetAura != null)
            {
                magnetAura.gameObject.SetActive(MagnetActive);
                magnetAura.localScale = Vector3.one * Mathf.Clamp01((magnetUntil - game.Duration) / MagnetDuration);
            }
            if (boostAura != null)
            {
                boostAura.gameObject.SetActive(BoostActive);
                boostAura.localScale = Vector3.one * Mathf.Clamp01((boostUntil - game.Duration) / BoostDuration);
            }
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
