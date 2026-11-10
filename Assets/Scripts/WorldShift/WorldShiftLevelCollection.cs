using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Required items, shared by Day and Night. A miss retries the section.</summary>
    public sealed class WorldShiftLevelCollection
    {
        public readonly Vector2[] Items; // x = road distance, y = lateral metres
        private readonly bool[] taken;
        public const float Radius = 1.35f;
        public const float MissDistance = 5f;
        public int Count { get; private set; }

        public WorldShiftLevelCollection(Vector2[] items)
        {
            Items = items;
            taken = new bool[items.Length];
        }

        public bool IsTaken(int index) => taken[index];

        public bool Step(float from, float to, float fromLateral, float toLateral)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (taken[i]) continue;
                var item = Items[i];
                // Swept sampling catches items even when a frame crosses their whole depth.
                if (from <= item.x && to >= item.x)
                {
                    float x = Mathf.Lerp(fromLateral, toLateral, Mathf.InverseLerp(from, to, item.x));
                    if (Mathf.Abs(x - item.y) <= Radius) { taken[i] = true; Count++; }
                }
                if (!taken[i] && to > item.x + MissDistance) return false;
            }
            return true;
        }

        public void Restore(float checkpoint)
        {
            Count = 0;
            for (int i = 0; i < Items.Length; i++)
            {
                // Only earlier, completed sections keep their items on retry.
                taken[i] = Items[i].x < checkpoint;
                if (taken[i]) Count++;
            }
        }
    }
}
