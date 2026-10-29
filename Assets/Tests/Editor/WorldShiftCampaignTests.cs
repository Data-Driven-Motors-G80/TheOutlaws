using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class WorldShiftCampaignTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static object Level(string name) => Enum.Parse(TypeFor("Outlaws.WorldShift.ShiftLevel"), name);
    private static void Flow(string method, params object[] args) => TypeFor("Outlaws.WorldShift.WorldShiftFlow").GetMethod(method).Invoke(null, args);
    private static void Pose(MonoBehaviour game, float distance, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, distance);
        object driver = game.GetType().GetField("driver", Flags).GetValue(game);
        Call(driver, "RestoreExternalPose", distance, x);
        if (game.GetType().Name == "WorldShiftCity") game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
    }
    [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [UnityTest]
    public IEnumerator MenuTutorialCityDesertAndCollisionSmoke()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length, Is.EqualTo(5));
        Capture("01-menu");
        Flow("Select", Level("City"), true);
        yield return null; yield return null;
        var tutorial = Find("WorldShiftCity"); tutorial.enabled = false;
        Assert.That(Get(tutorial, "TutorialLevel"), Is.EqualTo(true));
        Assert.That(Get(tutorial, "State").ToString(), Is.EqualTo("Running"));
        for (int i = 0; i < 850 && Get(tutorial, "State").ToString() == "Running"; i++)
        {
            float d = (float)Get(tutorial, "Distance");
            string desired = d >= 130f && d < 260f ? "Echo" : "Day";
            if (d < 520f && Get(tutorial, "World").ToString() != desired) Call(tutorial, "TrySwitch");
            float target = d < 112f || d < 610f && d >= 400f ? 3.2f : d >= 610f ? -3.2f : 0f;
            Call(tutorial, "StepSimulation", .05f, Mathf.Clamp((target - (float)Get(tutorial, "Lateral")) * 2f, -1f, 1f), false);
        }
        Assert.That(Get(tutorial, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Get(tutorial, "Deaths"), Is.EqualTo(0));
        Assert.That(Get(tutorial, "Hits"), Is.EqualTo(0));
        Flow("Next", Level("Tutorial"));
        yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("City_WorldShift"));
        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Running"));
        Call(city, "TrySwitch"); // Night, then enter an occupied Day position.
        Pose(city, 105f, 0f);
        Assert.That(Call(city, "TrySwitch"), Is.EqualTo(true));
        Assert.That(Get(city, "World").ToString(), Is.EqualTo("Day"));
        Assert.That(Get(city, "Hits"), Is.EqualTo(1));
        Assert.That((float)Get(city, "PursuitGap"), Is.InRange(22f, 22.5f));
        Assert.That(city.GetComponentsInChildren<Transform>().Any(t => t.name == "Other-world outline"), Is.False);
        Call(city, "RestartRun");
        Pose(city, 1255f, 0f); // Night has a missing deck here.
        Assert.That(Call(city, "TrySwitch"), Is.EqualTo(true));
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Retrying"));
        Flow("Next", Level("City"));
        yield return null; yield return null;
        var desert = Find("DesertShiftTrial"); desert.enabled = false;
        Assert.That(Get(desert, "FullLevel"), Is.EqualTo(true));
        Assert.That(Get(desert, "RunLength"), Is.EqualTo(2880f));
        var rocks = (Array)desert.GetType().GetField("Rocks").GetValue(desert);
        Assert.That(rocks.Length, Is.EqualTo(58));
        var traffic = (Array)Get(desert, "Traffic");
        Assert.That(traffic.Length, Is.EqualTo(14));
        Call(desert, "StepSimulation", 1f, 0f, false);
        float clock = (float)Get(rocks.GetValue(0), "DaySeconds");
        Call(desert, "TrySwitch"); Call(desert, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(rocks.GetValue(0), "DaySeconds"), Is.EqualTo(clock));
        Pose(desert, 134f, 0f);
        Assert.That(Call(desert, "TrySwitch"), Is.EqualTo(true));
        Assert.That((float)Get(desert, "PursuitGap"), Is.InRange(22f, 22.5f));
        // Freeze the first four-rock group, then verify the intervening Night wall.
        Call(desert, "Restart");
        desert.GetType().GetProperty("Stage").SetValue(desert, 2);
        Call(desert, "Retry"); Pose(desert, 1080f, 0f);
        Call(desert, "TrySwitch");
        for (int i = 0; i < 57; i++)
        {
            Call(desert, "StepSimulation", .05f, Mathf.Clamp(-(float)Get(desert, "Lateral") * 2f, -1f, 1f), false);
            if (i == 25) Capture("04-rock-field");
        }
        Assert.That(Get(desert, "State").ToString(), Is.EqualTo("Running"));
        Assert.That(Get(desert, "Retries"), Is.EqualTo(1));
        Assert.That((float)Get(desert, "PursuitGap"), Is.GreaterThan(44f), "The frozen centre route must be passable without a forced hit.");
        foreach (object car in traffic) Assert.That(Get(car, "Travel"), Is.EqualTo(0f), "Night freezes cars too.");
        Call(desert, "StepSimulation", 1.2f, 0f, false);
        Assert.That((float)Get(desert, "PursuitGap"), Is.LessThan(23f), "Staying in Night must collide with the wall between groups.");
        Call(desert, "Restart");
        desert.GetType().GetProperty("Stage").SetValue(desert, 5);
        Call(desert, "Retry");
        Assert.That(Get(desert, "Distance"), Is.EqualTo(2400f));
        Assert.That(Call(desert, "TrySwitch"), Is.EqualTo(false));
        Call(desert, "StepSimulation", 2f, -1f, false);
        Assert.That(Get(desert, "World").ToString(), Is.EqualTo("Day"));
        Call(desert, "StepSimulation", .8f, 1f, false);
        Assert.That(Get(desert, "World").ToString(), Is.EqualTo("Echo"));
        var echo = Enum.Parse(TypeFor("Outlaws.WorldShift.ShiftWorld"), "Echo");
        float nightRock = (float)Call(desert, "RockLateral", rocks.GetValue(rocks.Length - 1), echo);
        float dayClock = (float)Get(rocks.GetValue(0), "DaySeconds");
        float nightCar = (float)Get(traffic.GetValue(8), "NightTravel");
        float dayCar = (float)Get(traffic.GetValue(8), "Travel");
        Call(desert, "StepSimulation", .5f, 0f, false);
        Assert.That((float)Get(rocks.GetValue(0), "DaySeconds"), Is.GreaterThan(dayClock), "Day continues even while the player is on the Night half.");
        Assert.That(Call(desert, "RockLateral", rocks.GetValue(rocks.Length - 1), echo), Is.EqualTo(nightRock));
        Assert.That((float)Get(traffic.GetValue(8), "Travel"), Is.GreaterThan(dayCar));
        Assert.That(Get(traffic.GetValue(8), "NightTravel"), Is.EqualTo(nightCar));
        Call(desert, "TogglePause");
        dayClock = (float)Get(rocks.GetValue(0), "DaySeconds");
        Call(desert, "StepSimulation", 1f, 1f, false);
        Assert.That(Get(rocks.GetValue(0), "DaySeconds"), Is.EqualTo(dayClock));
        Call(desert, "TogglePause");
        Call(Find("DesertShiftTrialView"), "Refresh");
        var compositor = Find("WorldShiftSplitView"); Call(compositor, "RenderViews");
        Assert.That(Get(compositor, "DayFrame"), Is.Not.Null);
        Assert.That(Get(compositor, "NightFrame"), Is.Not.Null);
        Capture("02-desert-rift");
        Pose(desert, 2879f, 0f); Call(desert, "StepSimulation", .1f, 0f, false);
        Assert.That(Get(desert, "State").ToString(), Is.EqualTo("Won"));
        Call(Find("DesertShiftTrialView"), "Refresh"); Capture("03-desert-complete");
        Flow("Next", Level("Desert")); yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"));
        // The same optional tutorial must return to Desert when that is the selection.
        Flow("Select", Level("Desert"), true); yield return null; yield return null;
        Assert.That(Get(Find("WorldShiftCity"), "TutorialLevel"), Is.EqualTo(true));
        Flow("Next", Level("Tutorial")); yield return null; yield return null;
        Assert.That(Get(Find("DesertShiftTrial"), "FullLevel"), Is.EqualTo(true));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DesertAirRelayFreezesAndRequiresNightLanding()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Desert_WorldShift.unity");
        yield return new EnterPlayMode();
        var game = Find("DesertShiftTrial"); game.enabled = false;
        Call(game, "Begin");
        game.GetType().GetProperty("Stage").SetValue(game, 4);
        Call(game, "Retry");
        Pose(game, 1950f, 0f); Call(game, "TrySwitch");
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Night has no ramp to drive across.");
        Call(game, "Retry");
        AdvanceDesert(game, 1988f, 4.05f);
        var relay = Get(game, "Relay");
        Assert.That(Call(relay, "SpeedAt", 1988f), Is.EqualTo(42f));
        var route = Get(game, "Route");
        float bottom = ((Vector3)Get(Call(route, "Sample", 1944f), "Position")).y;
        float top = ((Vector3)Get(Call(route, "Sample", 2004f), "Position")).y;
        Assert.That(top - bottom, Is.GreaterThan(10f));
        Capture("05-desert-boost");
        AdvanceDesert(game, 2006f, 4.05f);
        Assert.That(Get(game, "IsAirborne"), Is.True);
        var police = (Component)Get(game, "Police"); Vector3 policeBefore = police.transform.position;
        Call(game, "TrySwitch");
        var rocks = (Array)game.GetType().GetField("Rocks").GetValue(game);
        float clock = (float)Get(rocks.GetValue(0), "DaySeconds");
        AdvanceDesert(game, 2088f, 4.05f);
        Assert.That(Get(rocks.GetValue(0), "DaySeconds"), Is.EqualTo(clock), "Night freezes the aerial obstacles, not the flight.");
        Assert.That(Vector3.Distance(policeBefore, police.transform.position), Is.GreaterThan(70f));
        Capture("06-desert-air-night");
        AdvanceDesert(game, 2190f, 4.05f);
        Assert.That(Get(game, "IsAirborne"), Is.False);
        Assert.That(Get(relay, "Completed"), Is.EqualTo(1));
        Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(44f), "A reachable frozen aerial route must be clear without damage.");
        Capture("07-desert-night-landing");
        Call(game, "Retry");
        var day = Enum.Parse(TypeFor("Outlaws.WorldShift.ShiftWorld"), "Day");
        Call(relay, "BeginStep", 2003f, 2005f, day);
        Pose(game, 2171f, 0f); Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That(Get(game, "Message").ToString(), Does.Contain("MISSED LANDING"));
        Call(game, "Retry");
        Assert.That(Get(game, "Distance"), Is.EqualTo(1920f));
        Assert.That(Get(game, "IsAirborne"), Is.False);
        yield return new ExitPlayMode();
    }

    private static void AdvanceDesert(object game, float target, float lane)
    {
        for (int i = 0; i < 600 && (float)Get(game, "Distance") < target; i++)
        {
            Call(game, "StepSimulation", .025f, Mathf.Clamp((lane - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"), "Failed at " + Get(game, "Distance") + ": " + Get(game, "Message"));
        }
        Assert.That((float)Get(game, "Distance"), Is.GreaterThanOrEqualTo(target));
    }

    private static void Capture(string name)
    {
        string folder = Environment.GetEnvironmentVariable("OUTLAWS_CAMPAIGN_PREVIEW");
        if (string.IsNullOrEmpty(folder)) folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Outlaws-Campaign-Preview");
        Camera camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var planes = canvases.Select(c => c.planeDistance).ToArray();
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture; var active = RenderTexture.active; int mask = camera.cullingMask; float aspect = camera.aspect;
        try
        {
            camera.targetTexture = target; camera.aspect = 1280f / 720f;
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1f; }
            camera.cullingMask = -1; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
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
