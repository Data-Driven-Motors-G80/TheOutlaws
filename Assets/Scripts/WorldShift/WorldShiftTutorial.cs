using UnityEngine;

namespace Outlaws.WorldShift
{
    public static class WorldShiftTutorial
    {
        public const float Length = 720f, MergeStart = 520f;
        public static readonly ShiftHazard ShootingObstacle = new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 440f, 446f, 7);
        public static readonly ShiftHazard[] Hazards =
        {
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 90f, 96f, 2),
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.Barrier, 174f, 180f, 7),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.Barrier, 314f, 320f, 7),
            ShootingObstacle,
            new ShiftHazard(ShiftWorld.Day, ShiftHazardKind.HalfBarrier, 584f, 590f, 3, halfSide: -1),
            new ShiftHazard(ShiftWorld.Echo, ShiftHazardKind.HalfBarrier, 652f, 658f, 6, halfSide: 1)
        };
        public static ShiftHazard[] CreateHazards()
        {
            var result = new ShiftHazard[Hazards.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var h = Hazards[i];
                bool traffic = i == 1 || i == 2;
                result[i] = new ShiftHazard(h.World, traffic ? ShiftHazardKind.Oncoming : h.Kind, i == 1 ? 220f : h.Start, i == 1 ? 226f : h.End,
                    traffic ? i == 1 ? 1 : 4 : h.LaneMask, traffic ? 8f : 0f, h.HalfSide, true);
            }
            return result;
        }
        public static readonly ShiftCheckpoint[] Checkpoints =
        {
            new ShiftCheckpoint(0f, ShiftWorld.Day, 0f, "STEER", "Use A / D or arrow keys to steer."),
            new ShiftCheckpoint(120f, ShiftWorld.Day, 0f, "SWITCH", "Tap Space to freeze traffic."),
            new ShiftCheckpoint(240f, ShiftWorld.Echo, 0f, "SWITCH BACK", "Tap Space for day and loot."),
            new ShiftCheckpoint(380f, ShiftWorld.Day, 0f, "SHOOT", "Click to shoot the block."),
            new ShiftCheckpoint(520f, ShiftWorld.Day, 2.5f, "YOUR CHOICE", "Steer through the gap.")
        };
    }
    // A tiny Day-only trail demonstrates the same reward rule without another HUD.
    public sealed class WorldShiftTutorialRewards : MonoBehaviour
    {
        private WorldShiftCity game;
        private readonly float[] at = { 276f, 296f };
        private readonly Transform[] boxes = new Transform[2];
        private readonly bool[] taken = new bool[2];
        public int Collected { get; private set; }
        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            for (int i = 0; i < at.Length; i++)
                boxes[i] = WorldShiftGetaway.CreateLootBox(transform, "Tutorial loot " + at[i], game.Route.Sample(at[i]), 0f,
                    RuntimeMaterials.Coloured(new Color(1f, .77f, .25f)), RuntimeMaterials.Coloured(new Color(.12f, .1f, .04f)));
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
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i] != null) boxes[i].gameObject.SetActive(!taken[i] && world == ShiftWorld.Day && at[i] > game.Distance - 10f && at[i] < game.Distance + 170f);
        }
    }

}
