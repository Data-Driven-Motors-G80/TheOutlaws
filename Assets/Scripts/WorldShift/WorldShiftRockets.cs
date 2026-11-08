using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Warns, then fires a lane-wide salvo from behind in the launch world.</summary>
    public sealed class WorldShiftRockets : MonoBehaviour
    {
        public const float WarningSeconds = 1.5f;
        private const float LaunchGap = 36f, ClosingSpeed = 30f, HalfLength = 2f;
        private WorldShiftCity game;
        private Camera view;
        // Intervals are measured from one warning to the next in gameplay time.
        private const float MinInterval = 4f, MaxInterval = 7f;
        private float untilNextAttack;
        private int mask;
        private bool active, launched;
        private ShiftWorld owner;
        private float timer, position, velocity;
        private readonly Transform[] bodies = new Transform[3];
        private readonly TMP_Text[] warnings = new TMP_Text[3];
        private GameObject warningPanel;
        private TMP_Text instruction;

        public void Bind(WorldShiftCity controller, Camera camera)
        {
            game = controller;
            view = camera;
            for (int lane = 0; lane < 3; lane++) bodies[lane] = BuildRocket(lane);
            var canvas = WorldShiftMenu.CreateCanvas(transform, "Rocket alerts");
            // Keep pause/finish controls above this non-interactive warning overlay.
            canvas.GetComponent<Canvas>().sortingOrder = 149;
            warningPanel = WorldShiftMenu.Panel(canvas, "Rocket warning", Vector2.zero, Vector2.one, Color.clear).gameObject;
            instruction = WorldShiftMenu.Label(warningPanel.transform, "", .15f, .65f, .85f, .72f, 25f);
            instruction.alignment = TextAlignmentOptions.Center;
            for (int lane = 0; lane < 3; lane++)
            {
                var label = WorldShiftMenu.Label(warningPanel.transform, "!", 0f, 0f, 0f, 0f, 64f);
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.rectTransform.sizeDelta = new Vector2(70f, 90f);
                warnings[lane] = label;
            }
            ResetAttacks();
        }

        public void ResetAttacks()
        {
            active = launched = false;
            untilNextAttack = Random.Range(MinInterval, MaxInterval);
            warningPanel.SetActive(false);
            foreach (var body in bodies) body.gameObject.SetActive(false);
        }

        // Called only by the fixed-size gameplay substeps: pause freezes the salvo too.
        public void Step(float dt, float before, float previousX)
        {
            untilNextAttack -= dt;
            if (!active)
            {
                if (untilNextAttack > 0f) return;
                // Give a complete ground warning; ramps can defer an expired timer.
                foreach (var ramp in game.RelayLayout)
                    if (game.Distance + game.Speed * 3.6f >= ramp.Ramp && game.Distance <= ramp.End + 8f) return;
                if (game.RunLength - game.Distance < game.Speed * 3.6f) return;
                mask = RandomLaneMask();
                untilNextAttack = Random.Range(MinInterval, MaxInterval);
                owner = game.World;
                active = true;
                launched = false;
                timer = 0f;
                return;
            }
            if (!launched)
            {
                timer += dt;
                if (timer < WarningSeconds) return;
                // Lock the world at the warning, so an early switch remains a valid escape.
                launched = true;
                position = game.Distance - LaunchGap;
                velocity = game.Speed + ClosingSpeed;
                Present(game.World);
                return;
            }
            timer += dt;
            float oldPosition = position;
            position += velocity * dt;
            if (game.World == owner && !game.IsRelayAirborne)
            {
                // Clip to the longitudinal overlap before checking lateral movement.
                // This catches fast rockets without reporting hits after a successful dodge.
                float a = oldPosition - before, b = position - game.Distance;
                const float reach = HalfLength + 1.5f;
                float lo = 0f, hi = 1f, movement = b - a;
                if (Mathf.Abs(movement) > .00001f)
                {
                    float t0 = (-reach - a) / movement, t1 = (reach - a) / movement;
                    lo = Mathf.Max(0f, Mathf.Min(t0, t1));
                    hi = Mathf.Min(1f, Mathf.Max(t0, t1));
                }
                else if (Mathf.Abs(a) > reach) hi = -1f;
                if (lo <= hi)
                {
                    float x0 = Mathf.Lerp(previousX, game.Lateral, lo);
                    float x1 = Mathf.Lerp(previousX, game.Lateral, hi);
                    float halfWidth = WorldShiftLevel.RoadWidth / 6f;
                    for (int lane = 0; lane < 3; lane++)
                        if ((mask & (1 << lane)) != 0 && Mathf.Min(x0, x1) - .8f < LaneX(lane) + halfWidth
                            && Mathf.Max(x0, x1) + .8f > LaneX(lane) - halfWidth)
                        { game.HitRocket(); active = false; Present(game.World); return; }
                }
            }
            if (position - game.Distance > 22f) active = false;
            Present(game.World);
        }

        private static int RandomLaneMask()
        {
            // Equal chance of 1, 2, or 3 lanes; every lane combination is possible.
            int count = Random.Range(1, 4);
            if (count == 3) return 7;
            int lane = 1 << Random.Range(0, 3);
            return count == 1 ? lane : 7 ^ lane;
        }

        private static float LaneX(int lane) => (lane - 1) * WorldShiftLevel.RoadWidth / 3f;

        public void Present(ShiftWorld world)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                bool show = active && launched && owner == world && (mask & (1 << lane)) != 0;
                bodies[lane].gameObject.SetActive(show);
                if (!show) continue;
                var point = game.Route.Sample(position);
                bodies[lane].SetPositionAndRotation(point.Position + point.Right * LaneX(lane) + point.Up * .9f,
                    Quaternion.LookRotation(point.Forward, point.Up));
            }
        }

        private void LateUpdate()
        {
            if (game == null) return;
            bool show = active && game.State == ShiftRunState.Running && !game.TrainingLessonPaused;
            warningPanel.SetActive(show);
            if (!show) return;
            string world = owner == ShiftWorld.Day ? "DAY" : "NIGHT";
            instruction.text = "! ROCKETS IN " + world + (mask == 7 ? " - SPACE: SWITCH WORLD" : " - DODGE OR SWITCH");
            // Blue marks a rocket in the other world; danger keeps the original pulse.
            Color colour = game.World == owner
                ? Color.Lerp(new Color(1f, .25f, .08f), Color.yellow, .5f + .5f * Mathf.Sin(timer * 12f))
                : new Color(.15f, .55f, 1f);
            instruction.color = colour;
            for (int lane = 0; lane < 3; lane++)
            {
                warnings[lane].gameObject.SetActive((mask & (1 << lane)) != 0);
                var point = game.Route.Sample(game.Distance + 9f);
                Vector3 screen = view.WorldToViewportPoint(point.Position + point.Right * LaneX(lane) + point.Up * .8f);
                var rect = warnings[lane].rectTransform;
                Vector2 anchor = new Vector2(Mathf.Clamp(screen.x, .08f, .92f), Mathf.Clamp(screen.y, .2f, .6f));
                rect.anchorMin = rect.anchorMax = anchor;
                rect.anchoredPosition = Vector2.zero;
                warnings[lane].color = colour;
            }
        }

        private Transform BuildRocket(int lane)
        {
            var root = new GameObject("Rear rocket lane " + lane).transform;
            root.SetParent(transform, false);
            Material metal = WorldShiftLevelProps.Glow(new Color(.65f, .69f, .74f));
            Material red = WorldShiftLevelProps.Glow(new Color(1f, .13f, .05f), 1f);
            Material flame = WorldShiftLevelProps.Glow(new Color(1f, .6f, .08f), 2f);
            Part(root, PrimitiveType.Capsule, Vector3.zero, new Vector3(.7f, 1.5f, .7f), metal, true);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0f, 1.3f), new Vector3(.72f, .72f, 1.1f), red);
            // Broad fins show the full lane covered by each rocket's hitbox.
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -1f),
                new Vector3(WorldShiftLevel.RoadWidth / 3f, .12f, .85f), red);
            Part(root, PrimitiveType.Cube, new Vector3(0f, .35f, -1f), new Vector3(.12f, 1f, .85f), red);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0f, -2f), new Vector3(.45f, .45f, 1.5f), flame);
            root.gameObject.SetActive(false);
            return root;
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 size, Material material, bool rotate = false)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            if (rotate) part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            part.GetComponent<Collider>().enabled = false;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
