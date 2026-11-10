using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldShiftSplitViewTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FullViewsCropWithoutSqueezingAndSteeringCompletesShortRoute()
    {
        var setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WorldShiftDualViewSetup")).First(t => t != null);
        setup.GetMethod("Create").Invoke(null, null);
        EditorSceneManager.OpenScene("Assets/Scenes/City_DualWorldValidation.unity");
        yield return new EnterPlayMode();
        var game = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType().Name == "WorldShiftCity");
        var split = game.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "WorldShiftSplitView");
        game.enabled = false;
        split.enabled = false;
        Assert.That(Get(game, "SplitViewValidation"), Is.True);
        Assert.That(Get(game, "RunLength"), Is.EqualTo(560f));
        Call(game, "BeginRun");
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Assert.That(Call(game, "TrySwitch"), Is.True);

        Advance(game, 165f);
        Assert.That(Get(game, "IsMerged"), Is.True);
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));
        Assert.That(Call(game, "TrySwitch"), Is.False, "Space rests inside the joining section.");
        CaptureAndCheck(game, split, "01-crossing-from-day");
        // Exercise the ordinary LateUpdate path and the final UI, not just offscreen frames.
        split.enabled = true;
        yield return null;
        CaptureGameView(game);
        split.enabled = false;
        Advance(game, 250f);
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"));
        CaptureAndCheck(game, split, "02-crossing-from-night");
        Advance(game, 455f);
        Assert.That(Get(game, "IsMerged"), Is.False);
        Assert.That(Call(game, "TrySwitch"), Is.True, "Whole-world switching returns at the exit.");
        Advance(game, 560f);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        Assert.That(Get(game, "Hits"), Is.EqualTo(0), "Hidden halves must not leave invisible collisions.");
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(20f).Within(.1f));
        Call(game, "RestartRun");
        Assert.That(Get(game, "Distance"), Is.EqualTo(0f));
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        yield return new ExitPlayMode();
    }

    private static void Advance(object game, float destination)
    {
        for (int frame = 0; (float)Get(game, "Distance") < destination && frame < 1300; frame++)
        {
            float distance = (float)Get(game, "Distance");
            float target = distance < 175f || (distance >= 270f && distance < 375f) ? -3.2f : 3.2f;
            float error = target - (float)Get(game, "Lateral");
            Call(game, "StepSimulation", 1f / 60f, Mathf.Clamp(error * 4f, -1f, 1f), false);
        }
        Assert.That((float)Get(game, "Distance"), Is.GreaterThanOrEqualTo(destination));
    }

    private static void CaptureAndCheck(object game, object split, string label)
    {
        Call(game, "UpdateCamera", true);
        object world = Get(game, "World");
        Vector3 position = ((Transform)Get(game, "Player")).position;
        Call(split, "RenderViews");
        Assert.That(Get(game, "World"), Is.EqualTo(world));
        Assert.That(((Transform)Get(game, "Player")).position, Is.EqualTo(position));
        var day = (RenderTexture)Get(split, "DayFrame");
        var night = (RenderTexture)Get(split, "NightFrame");
        var material = (Material)Get(split, "CompositeMaterial");
        Assert.That(material.shader.isSupported, Is.True);
        var result = new RenderTexture(day.width, day.height, 0, RenderTextureFormat.ARGB32);
        result.Create();
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(day, result, material);
        Texture2D dayImage = Read(day), nightImage = Read(night), joined = Read(result);
        try
        {
            float difference = 0f;
            for (int row = 1; row < 9; row++)
                for (int column = 1; column < 10; column++)
                {
                    int x = day.width * column / 10, y = day.height * row / 10;
                    Color expected = (x < day.width / 2 ? dayImage : nightImage).GetPixel(x, y);
                    Color actual = joined.GetPixel(x, y);
                    Assert.That(Vector3.Distance((Vector4)actual, (Vector4)expected), Is.LessThan(.035f),
                        "Each half must preserve full-view UVs, without scaling or re-lighting.");
                    difference += Vector3.Distance((Vector4)dayImage.GetPixel(x, y), (Vector4)nightImage.GetPixel(x, y));
                }
            Assert.That(difference, Is.GreaterThan(3f), "Day and Night must be visually distinct, not blank copies.");
            string folder = Environment.GetEnvironmentVariable("OUTLAWS_DUAL_VIEW_PREVIEW");
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, label + ".png"), joined.EncodeToPNG());
                if (label.StartsWith("01"))
                {
                    File.WriteAllBytes(Path.Combine(folder, "full-day.png"), dayImage.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(folder, "full-night.png"), nightImage.EncodeToPNG());
                }
            }
        }
        finally
        {
            UnityEngine.Object.Destroy(dayImage);
            UnityEngine.Object.Destroy(nightImage);
            UnityEngine.Object.Destroy(joined);
            RenderTexture.active = previous;
            result.Release();
            UnityEngine.Object.Destroy(result);
        }
    }

    private static void CaptureGameView(object game)
    {
        string folder = Environment.GetEnvironmentVariable("OUTLAWS_DUAL_VIEW_PREVIEW");
        if (string.IsNullOrEmpty(folder)) return;
        var camera = Camera.main;
        Call(game, "UpdateCamera", true);
        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var frame = new RenderTexture(1280, 720, 24);
        frame.Create();
        RenderTexture previousActive = RenderTexture.active, previousTarget = camera.targetTexture;
        int previousMask = camera.cullingMask;
        float previousAspect = camera.aspect;
        try
        {
            camera.targetTexture = frame;
            camera.aspect = 1280f / 720f;
            camera.cullingMask = -1;
            var split = ((Component)game).GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "WorldShiftSplitView");
            Call(split, "RenderViews");
            foreach (Canvas c in canvas)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = camera;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Texture2D image = Read(frame);
            File.WriteAllBytes(Path.Combine(folder, "03-game-with-hud.png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }
        finally
        {
            foreach (Canvas c in canvas) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
            camera.cullingMask = previousMask;
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            frame.Release();
            UnityEngine.Object.Destroy(frame);
        }
    }

    private static Texture2D Read(RenderTexture source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = source;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        return texture;
    }
}
