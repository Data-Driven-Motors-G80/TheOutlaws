using UnityEngine;

/// <summary>Predetermined crossing phases; no player steering or backpack state is used.</summary>
public static class DesertCrossingPattern
{
    // Left, centre, right, centre (or its mirror). Never jump between both edge lanes.
    public static int LaneAt(int index, bool mirrored)
    {
        int phase = index % 4;
        int lane = phase == 0 ? -1 : phase == 2 ? 1 : 0;
        return mirrored ? -lane : lane;
    }

    public static float TravelSeconds(float width, float span, float duration, int direction, int lane)
    {
        float lateral = Mathf.Clamp(lane, -1, 1) * width / 3f;
        return duration * Mathf.Clamp01((span + direction * lateral) / (2f * span));
    }

    public static int OrdinaryCount(int minimum, int maximum, bool preferLargerGroups, double roll)
    {
        minimum = Mathf.Clamp(minimum, 1, 3);
        maximum = Mathf.Clamp(maximum, minimum, 3);
        int total = 0;
        for (int count = minimum; count <= maximum; count++)
            total += preferLargerGroups && count > 1 ? 2 : 1;
        double choice = System.Math.Max(0d, System.Math.Min(0.999999d, roll)) * total;
        for (int count = minimum; count <= maximum; count++)
        {
            choice -= preferLargerGroups && count > 1 ? 2 : 1;
            if (choice < 0d) return count;
        }
        return maximum;
    }
}
