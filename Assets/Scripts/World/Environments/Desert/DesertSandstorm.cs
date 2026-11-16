using UnityEngine;

/// <summary>Desert sandstorm fog math used by the authored Desert level. No gameplay dependencies.</summary>
public static class DesertSandstorm
{
    // Shared fog math for the authored Desert level. No gameplay dependencies.
    public static void ApplyVisibility(Camera camera, float strength, float speed, float cameraOffset,
        Color baseFog, Color baseSky, float baseStart, float baseEnd, Color dust,
        float minimumVisibility = 50f, float visibleTravelSeconds = 2f)
    {
        float visibility = Mathf.Max(minimumVisibility, speed * visibleTravelSeconds);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(baseFog, dust, strength);
        if (camera != null) camera.backgroundColor = Color.Lerp(baseSky, dust, strength);
        RenderSettings.fogStartDistance = Mathf.Lerp(baseStart, cameraOffset + 5f, strength);
        RenderSettings.fogEndDistance = Mathf.Lerp(baseEnd, cameraOffset + visibility, strength);
    }
}
