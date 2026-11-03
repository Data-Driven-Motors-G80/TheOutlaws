using UnityEngine;

namespace Outlaws.WorldShift
{
    public static class WorldShiftTutorial
    {
        public const float Length = 900f, FinalLessonStart = 460f, MergeStart = FinalLessonStart;
        public const string NightSuppliesMessage = "No cash, fuel or power-ups in Nightshift.\nReturn to Day.";
        public const string NightLandingMessage = "Stay in Nightshift until the roads rejoin.";
        public static readonly string[] LessonKeys = { "A / D", "SPACE", "SPACE", "F / CLICK", "A / D", "SPACE" };
        public static readonly string[] LessonMessages =
        {
            "Steer around the block.",
            "Enter Nightshift to escape the roadblock.",
            "Return to Day for cash and supplies.",
            "Shoot the marked block.",
            "Steer through the gap.",
            "Take the Day ramp.\nSwitch to Nightshift in the air."
        };
        public static readonly ShiftRelay[] Relays = { new ShiftRelay(1, 650f, 710f, 794f, ShiftWorld.Day) };
        public const float FirstFinalObstacle = 510f, LastFinalObstacle = 558f;
        public static readonly ShiftHazard ShootingObstacle = new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 440f, 446f, 2);
        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 90f, 96f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 174f, 180f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 314f, 320f, 7),
            ShootingObstacle,
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, FirstFinalObstacle, FirstFinalObstacle + 6f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, LastFinalObstacle, LastFinalObstacle + 6f, 6, halfSide: 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 440f, 446f, 1),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 440f, 446f, 4),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.SwitchWall, 168f, 174f, 7)
        };
        public static ShiftHazard[] CreateHazards()
        {
            var result = new ShiftHazard[Hazards.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var h = Hazards[i];
                bool traffic = i == 1 || i == 2;
                result[i] = new ShiftHazard(h.World, traffic ? ShiftHazardKind.Oncoming : h.Kind, i == 1 ? 192f : h.Start, i == 1 ? 198f : h.End,
                    i == 2 ? 1 : traffic ? 2 : h.LaneMask, i == 1 ? 1.5f : i == 2 ? 8f : 0f, h.HalfSide, h.Kind != ShiftHazardKind.SwitchWall, lateralMotion: i == 1);
            }
            return result;
        }
        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "STEER", "A / D: steer."),
            new ShiftCheckpoint(110f, ShiftWorld.Day, 0f, "SWITCH", "Enter Nightshift to escape the roadblock."),
            new ShiftCheckpoint(220f, ShiftWorld.Echo, 0f, "SWITCH BACK", "Return to Day for cash and supplies."),
            new ShiftCheckpoint(330f, ShiftWorld.Day, 0f, "SHOOT", "F / click: shoot the block."),
            new ShiftCheckpoint(FinalLessonStart, ShiftWorld.Day, 2.5f, "YOUR CHOICE", "Steer through the gap."),
            new ShiftCheckpoint(580f, ShiftWorld.Day, 0f, "JUMP", "Take the Day ramp. Switch to Nightshift in the air.")
        };
    }
    // A tiny Day-only trail demonstrates the same reward rule without another HUD.
    public sealed class WorldShiftTutorialRewards : MonoBehaviour
    {
        private WorldShiftCity game;
        private readonly float[] at = { 276f, 296f };
        private readonly Transform[] boxes = new Transform[2];
        private readonly bool[] taken = new bool[2];
        private Transform trafficCue;
        private LineRenderer trafficOutline, motionArrow;
        private Material cueMaterial;
        private LineRenderer Line(Transform parent, string name, Vector3[] points)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = cueMaterial;
            line.startWidth = line.endWidth = .12f;
            line.positionCount = points.Length; line.SetPositions(points);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        public int Collected { get; private set; }
        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            for (int i = 0; i < at.Length; i++)
                boxes[i] = WorldShiftGetaway.CreateLootBox(transform, "Tutorial loot " + at[i], game.Route.Sample(at[i]), 0f,
                    RuntimeMaterials.Coloured(new Color(1f, .77f, .25f)), RuntimeMaterials.Coloured(new Color(.12f, .1f, .04f)));
            cueMaterial = new Material(Shader.Find("Sprites/Default"));
            var outline = new[] { new Vector3(-1f, .2f, -3.5f), new Vector3(-1f, .2f, 3.5f),
                new Vector3(1f, .2f, 3.5f), new Vector3(1f, .2f, -3.5f), new Vector3(-1f, .2f, -3.5f) };
            trafficCue = new GameObject("Tutorial traffic outline").transform;
            trafficCue.SetParent(transform, false);
            trafficOutline = Line(trafficCue, "Hazard outline", outline);
            motionArrow = Line(trafficCue, "Traffic moving arrow", new[] { new Vector3(-1.4f, .25f, -4f), new Vector3(-2f, .25f, -3.4f),
                new Vector3(-1.4f, .25f, -2.8f), new Vector3(-2f, .25f, -3.4f),
                new Vector3(2f, .25f, -3.4f), new Vector3(1.4f, .25f, -4f),
                new Vector3(2f, .25f, -3.4f), new Vector3(1.4f, .25f, -2.8f) });
            Present(game.World);
        }
        public void Step(float from, float to, float a, float b)
        {
            if (game.World == ShiftWorld.Day)
                for (int i = 0; i < at.Length; i++)
                    if (!taken[i] && from <= at[i] + 1.3f && to >= at[i] - 1.3f
                        && Mathf.Abs(Mathf.Lerp(a, b, Mathf.InverseLerp(from, to, at[i]))) < 1.35f)
                    { taken[i] = true; Collected++; }
            Present(game.World);
        }
        public void Restore(float checkpoint)
        {
            Collected = 0;
            for (int i = 0; i < at.Length; i++) { taken[i] = at[i] < checkpoint; if (taken[i]) Collected++; }
        }
        public void Present(ShiftWorld world)
        {
            if (trafficCue != null)
            {
                int lesson = game.TutorialLessonIndex;
                bool active = lesson == 1 || lesson == 2;
                trafficCue.gameObject.SetActive(active && !game.TutorialLessonComplete);
                if (active)
                {
                    var hazard = game.Hazards[lesson == 1 ? 1 : 2];
                    hazard.BoundsAt(game.Distance, out float start, out float end);
                    var point = game.Route.Sample((start + end) * .5f);
                    float x = hazard.LateralMotion ? hazard.LateralOffset : hazard.LaneMask == 1 ? -WorldShiftLevel.RoadWidth / 3f : 0f;
                    trafficCue.SetPositionAndRotation(point.Position + point.Right * x, Quaternion.LookRotation(point.Forward, point.Up));
                    trafficOutline.transform.localScale = new Vector3(hazard.LateralMotion ? 1f : 1.7f, 1f, 1f);
                    trafficOutline.startColor = trafficOutline.endColor = world == ShiftWorld.Day ? Color.white : Color.cyan;
                    motionArrow.gameObject.SetActive(world == ShiftWorld.Day);
                    motionArrow.transform.localPosition = Vector3.zero;
                    motionArrow.transform.localRotation = Quaternion.Euler(0f, hazard.LateralMotion ? 0f : 90f, 0f);
                }
            }
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i] != null) boxes[i].gameObject.SetActive(!taken[i] && world == ShiftWorld.Day && at[i] > game.Distance - 10f && at[i] < game.Distance + 170f);
        }
        private void OnDestroy() { if (cueMaterial != null) Destroy(cueMaterial); }
    }

}
