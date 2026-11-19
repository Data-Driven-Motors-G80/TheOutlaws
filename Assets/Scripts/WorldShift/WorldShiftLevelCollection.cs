using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Swept collection. Ocean ammo is optional, Day-only and banked at actual checkpoints.</summary>
    public sealed class WorldShiftLevelCollection
    {
        public readonly Vector2[] Items; // x = road distance, y = lateral metres
        private readonly bool[] taken;
        private readonly bool[] saved;
        public readonly bool Optional;
        public const float Radius = 1.35f;
        public const float MissDistance = 5f;
        public int Count { get; private set; }

        public WorldShiftLevelCollection(Vector2[] items, bool optional = false)
        {
            Items = items;
            taken = new bool[items.Length];
            saved = new bool[items.Length]; Optional = optional;
        }

        public bool IsTaken(int index) => taken[index];

        public bool Step(float from, float to, float fromLateral, float toLateral, bool canCollect = true)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (taken[i]) continue;
                var item = Items[i];
                // Swept sampling catches items even when a frame crosses their whole depth.
                if (canCollect && from <= item.x && to >= item.x)
                {
                    float x = Mathf.Lerp(fromLateral, toLateral, Mathf.InverseLerp(from, to, item.x));
                    if (Mathf.Abs(x - item.y) <= Radius) { taken[i] = true; Count++; }
                }
                if (!Optional && !taken[i] && to > item.x + MissDistance) return false;
            }
            return true;
        }

        public void SaveCheckpoint() => System.Array.Copy(taken, saved, taken.Length);

        public void Restore(float checkpoint)
        {
            Count = 0;
            for (int i = 0; i < Items.Length; i++)
            {
                // Only earlier, completed sections keep their items on retry.
                if (Optional && checkpoint <= 0f) saved[i] = false;
                taken[i] = Optional ? saved[i] && Items[i].x < checkpoint : Items[i].x < checkpoint;
                if (taken[i]) Count++;
            }
        }
    }
}
