using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DesertShiftTrialTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static object Get(object obj, string name)
    {
        Assert.That(obj, Is.Not.Null, "Missing receiver for " + name);
        var property = obj.GetType().GetProperty(name, Flags);
        Assert.That(property, Is.Not.Null, obj.GetType().FullName + " missing property " + name);
        return property.GetValue(obj);
    }
    private static object Call(object obj, string name, params object[] args)
    {
        Assert.That(obj, Is.Not.Null, "Missing receiver for " + name);
        var method = obj.GetType().GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, obj.GetType().FullName + " missing method " + name);
        return method.Invoke(obj, args);
    }
    private static object[] Rocks(object game) => ((IEnumerable)game.GetType().GetField("Rocks").GetValue(game)).Cast<object>().ToArray();

    [UnityTearDown]
    public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [UnityTest]
    public IEnumerator RollFreezeResumeAndCompleteThreeExercises()
    {
        Type setup = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DesertShiftTrialSetup")).First(t => t != null);
        setup.GetMethod("Create").Invoke(null, null);
        EditorSceneManager.OpenScene("Assets/Scenes/Desert_WorldShiftTrial.unity");
        yield return new EnterPlayMode();
        var game = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType().Name == "DesertShiftTrial");
        game.enabled = false;
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Ready"));
        Call(game, "Begin");
        float distantRockBefore = (float)Get(Rocks(game)[4], "Lateral");
        Call(game, "StepSimulation", 2f, 0f, false);
        object rock = Rocks(game)[0];
        float movingX = (float)Get(rock, "Lateral");
        Assert.That(movingX, Is.GreaterThan(2f));
        Assert.That((float)Get(Rocks(game)[4], "DaySeconds"), Is.EqualTo((float)Get(rock, "DaySeconds")),
            "Even the distant final rock moves from the start; there is no proximity arming.");
        Assert.That((float)Get(Rocks(game)[4], "Lateral"), Is.Not.EqualTo(distantRockBefore));
        Assert.That(game.GetComponentsInChildren<MonoBehaviour>().Any(c => c.GetType().Name == "DesertRoadsideSand"), Is.True);
        Assert.That(game.GetComponentsInChildren<Transform>().Any(t => t.name == "Original desert obstacle prefab"), Is.True);
        object firstPoint = Call(Get(game, "Route"), "Sample", 0f), bendPoint = Call(Get(game, "Route"), "Sample", 400f);
        Assert.That(Vector3.Angle((Vector3)Get(firstPoint, "Forward"), (Vector3)Get(bendPoint, "Forward")), Is.GreaterThan(20f));
        Assert.That(((Vector3)Get(Call(Get(game, "Route"), "Sample", 280f), "Position")).y,
            Is.GreaterThan(((Vector3)Get(firstPoint, "Position")).y + .5f));
        Call(game, "TogglePause");
        float pausedDistance = (float)Get(game, "Distance");
        Call(game, "StepSimulation", 5f, 1f, true);
        Assert.That(Get(game, "Distance"), Is.EqualTo(pausedDistance));
        Assert.That(Get(rock, "Lateral"), Is.EqualTo(movingX));
        Call(game, "TogglePause");
        var player = (Transform)Get(game, "Player"); Vector3 beforeSwitch = player.position;
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Assert.That(player.position, Is.EqualTo(beforeSwitch));
        Call(game, "StepSimulation", .3f, 0f, false);
        Assert.That(Get(rock, "Lateral"), Is.EqualTo(movingX), "Night must freeze the same rock without removing it.");
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Call(game, "StepSimulation", .2f, 0f, false);
        Assert.That((float)Get(rock, "Lateral"), Is.GreaterThan(movingX), "Day resumes from the frozen position.");
        bool pictured = false, dayPictured = false;
        float checkpointClock = -1f;
        for (int n = 0; n < 1000 && Get(game, "State").ToString() == "Running"; n++)
        {
            float d = (float)Get(game, "Distance");
            bool night = (d >= 60f && d < 240f) || (d >= 420f && d < 540f)
                || (d >= 660f && d < 744f) || d >= 798f;
            if (Get(game, "World").ToString() != (night ? "Echo" : "Day")) Assert.That(Call(game, "TrySwitch"), Is.True);
            if (!pictured && d >= 140f) { Capture(game, "desert-01-frozen-opening"); pictured = true; }
            if (!dayPictured && d >= 400f) { Capture(game, "desert-02-original-road-day"); dayPictured = true; }
            float steer = Mathf.Clamp(-(float)Get(game, "Lateral") * 2f, -1f, 1f);
            Call(game, "StepSimulation", .05f, steer, false);
            if ((int)Get(game, "Stage") == 2 && checkpointClock < 0f)
                checkpointClock = (float)Get(Rocks(game)[0], "DaySeconds");
            Assert.That(Get(game, "Retries"), Is.EqualTo(0), "The authored route must be reachable at " + d);
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(40f).Within(.1f));
        Assert.That(Get(game, "Stage"), Is.EqualTo(2));
        Call(game, "Retry");
        Assert.That(Get(game, "Distance"), Is.EqualTo(600f));
        Assert.That(checkpointClock, Is.GreaterThan(0f));
        // Do not capture checkpointClock in a lambda: EnterPlayMode's domain
        // reload does not preserve the compiler-generated closure instance.
        foreach (object restoredRock in Rocks(game))
            Assert.That((float)Get(restoredRock, "DaySeconds"), Is.EqualTo(checkpointClock).Within(.051f),
                "Retry must restore the checkpoint's continuous rock clock.");
        // Night is not invincibility. Freezing the first rock at its starting
        // centre position must still collide and retry the first checkpoint.
        Call(game, "Restart"); Call(game, "TrySwitch");
        Call(game, "StepSimulation", 8f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That((string)Get(game, "Message"), Does.StartWith("ROCK CONTACT"));
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "Distance"), Is.EqualTo(0f));
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));
        Assert.That((float)Get(Rocks(game)[0], "DaySeconds"), Is.EqualTo(0f));
        yield return new ExitPlayMode();
    }

    private static void Capture(Component game, string name)
    {
        string directory = Environment.GetEnvironmentVariable("OUTLAWS_READABILITY_PREVIEW");
        if (string.IsNullOrEmpty(directory)) return;
        var camera = Camera.main;
        var canvas = game.GetComponentInChildren<Canvas>();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
        var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
