using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>A distance-authored dust bank; deterministic on retries and motionless during pause.</summary>
    public static class WorldShiftLevelSandstorm
    {
        public static float IntensityAt(LevelCourse course, float distance)
        {
            float intensity = 0f;
            foreach (var zone in course.DustZones)
            {
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zone.x, zone.x + 55f, distance));
                float leave = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zone.y - 70f, zone.y, distance));
                intensity = Mathf.Max(intensity, enter * leave);
            }
            return intensity;
        }

        public static void Apply(LevelCourse course, Camera camera, Vector3 player, float distance, float speed,
            ShiftWorld world, Color normalColour, float normalStart, float normalEnd)
        {
            Color dust = world == ShiftWorld.Day ? new Color(.72f, .51f, .29f) : new Color(.27f, .23f, .19f);
            float eyeOffset = camera != null ? Vector3.Distance(camera.transform.position, player) : 14f;
            // Preserve roughly 2.8 seconds of road visibility. Night does not remove the storm.
            DesertSandstorm.ApplyVisibility(camera, IntensityAt(course, distance), speed, eyeOffset,
                normalColour, normalColour, normalStart, normalEnd, dust, 70f, 2.5f);
        }
    }
}
