using System;
using System.Linq;
using UnityEngine;

public static class WorldShiftTestCapture
{
    public static void Capture(string name)
    {
        string folder = Environment.GetEnvironmentVariable("OUTLAWS_CAMPAIGN_PREVIEW");
        if (string.IsNullOrEmpty(folder)) folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Outlaws-Campaign-Preview");
        var driver = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetType().Name == "WorldShiftCity");
        if (driver != null) driver.GetType().GetMethod("UpdateCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(driver, new object[] { true });
        Camera camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var planes = canvases.Select(c => c.planeDistance).ToArray();
        int width = name.EndsWith("-1080") ? 1920 : 1280, height = name.EndsWith("-1080") ? 1080 : 720;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture; var active = RenderTexture.active; int mask = camera.cullingMask; float aspect = camera.aspect;
        try
        {
            camera.targetTexture = target; camera.aspect = (float)width / height;
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1f; }
            camera.cullingMask = -1; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; camera.cullingMask = mask; camera.aspect = aspect; RenderTexture.active = active;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = planes[i]; }
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
