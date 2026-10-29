using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    public static class DesertShiftLevel
    {
        public const float Length = 2880f, MergeStart = 2400f, CheckpointSpacing = 480f;
        public const float RockSpeed = 3f, FieldRockSpeed = 3.8f;
        public static readonly Vector2[] RockFields = { new Vector2(1100f, 1356f), new Vector2(1580f, 1836f) };
        public static readonly ShiftRelay[] Relays = { new ShiftRelay(1, 1944f, 2004f, 2172f, ShiftWorld.Day) };
        public static bool InRockField(float distance)
        {
            foreach (var field in RockFields) if (distance >= field.x - 45f && distance <= field.y + 10f) return true;
            return false;
        }
        public static DesertTrialRock[] CreateRocks()
        {
            var rocks = new List<DesertTrialRock>
            {
                new DesertTrialRock(180f, 0f, 1f, RockSpeed), new DesertTrialRock(300f, 4f, -1f, RockSpeed),
                new DesertTrialRock(376f, 7.5f, 1f, RockSpeed), new DesertTrialRock(410f, 11f, -1f, RockSpeed)
            };
            float[] offsets = { 92f, 118f, 146f, 210f, 238f, 266f, 294f, 350f, 376f, 402f, 430f };
            for (int section = 1; section < 5; section++)
            {
                if (section != 1) continue;
                for (int i = 0; i < offsets.Length; i++)
                    rocks.Add(new DesertTrialRock(section * 480f + offsets[i],
                        Mathf.Repeat(section * 3.7f + i * 4.1f, 16.4f), (i + section) % 2 == 0 ? 1f : -1f, RockSpeed));
            }
            // Three four-rock groups, with clear release windows around Night walls.
            foreach (var field in RockFields)
                for (int i = 0; i < 12; i++)
                    rocks.Add(new DesertTrialRock(field.x + i / 4 * 110f + i % 4 * 12f, 4.2f,
                        i % 2 == 0 ? 1f : -1f, FieldRockSpeed, false, 0, false, 1.1f));
            // Two rows between each pair of half-walls; none coincides with a wall.
            for (int i = 0; i < 5; i++)
                foreach (float offset in new[] { 30f, 50f })
                    rocks.Add(new DesertTrialRock(2480f + i * 72f + offset, i * 3.5f + offset * .1f,
                        i % 2 == 0 ? 1f : -1f, RockSpeed, false,
                        (i % 2 == 0 ? -1 : 1) * (offset == 30f ? 1 : -1)));
            // Shared airborne rocks move in Day and retain their exact positions in Night.
            for (int i = 0; i < 6; i++)
                rocks.Add(new DesertTrialRock(2028f + i * 24f, 2.8f + i % 2 * .2f,
                    i % 2 == 0 ? 1f : -1f, 5f, false, 0, true));
            foreach (float at in new[] { 2288f, 2316f, 2368f })
                rocks.Add(new DesertTrialRock(at, at % 16.4f, 1f, RockSpeed));
            rocks.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return rocks.ToArray();
        }
        public static DesertTrialGate[] CreateGates()
        {
            var gates = new List<DesertTrialGate>
            {
                new DesertTrialGate(130f, ShiftWorld.Day), new DesertTrialGate(240f, ShiftWorld.Echo),
                new DesertTrialGate(340f, ShiftWorld.Day)
            };
            for (int section = 1; section < 5; section++)
            {
                float at = section * 480f;
                if (section == 4)
                {
                    gates.Add(new DesertTrialGate(2264f, ShiftWorld.Echo));
                    gates.Add(new DesertTrialGate(2332f, ShiftWorld.Day));
                    continue;
                }
                gates.Add(new DesertTrialGate(at + 60f, ShiftWorld.Day));
                if (section == 2 || section == 3)
                {
                    foreach (float offset in new[] { 70f, 180f, 296f })
                        gates.Add(new DesertTrialGate(at + 140f + offset, ShiftWorld.Echo));
                    continue;
                }
                gates.Add(new DesertTrialGate(at + 180f, ShiftWorld.Echo));
                gates.Add(new DesertTrialGate(at + 320f, ShiftWorld.Day));
            }
            for (int i = 0; i < 6; i++)
                gates.Add(new DesertTrialGate(2480f + i * 72f, i % 2 == 0 ? ShiftWorld.Day : ShiftWorld.Echo, i % 2 == 0 ? -1 : 1));
            return gates.ToArray();
        }
        public static readonly string[] Titles =
        { "ROLL / FREEZE", "ROCKS + TRAFFIC", "ROCK FIELD I", "ROCK FIELD II", "BOOST / FREEZE / LAND", "THE DESERT RIFT" };

        public static DesertShiftTraffic[] CreateTraffic() => new[]
        {
            new DesertShiftTraffic(600f, 1), new DesertShiftTraffic(730f, -1), new DesertShiftTraffic(870f, 1),
            new DesertShiftTraffic(1118f, 1), new DesertShiftTraffic(1288f, -1),
            new DesertShiftTraffic(1598f, 1), new DesertShiftTraffic(1768f, -1),
            new DesertShiftTraffic(2348f, 1),
            new DesertShiftTraffic(2520f, -1), new DesertShiftTraffic(2574f, 1),
            new DesertShiftTraffic(2664f, -1), new DesertShiftTraffic(2718f, 1),
            new DesertShiftTraffic(2808f, -1), new DesertShiftTraffic(2840f, 1)
        };
    }
}
