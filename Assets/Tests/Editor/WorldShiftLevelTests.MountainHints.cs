using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Mountain one-time hints: the first goat crossing and the first guardrail gap each get a short HUD card, once.
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator MountainShowsGoatAndGapHintsOnce()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        ClearShownHints();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        var hud = Find("WorldShiftLevelHud");
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That((bool)Get(hud, "HintVisible"), Is.False, "No hint at the start of the level.");
        var gaps = (Vector3[])Field(MountainCourse(), "EdgeGaps");
        Pose(game, gaps[0].x - 30f, 3.4f);
        for (int i = 0; i < 4; i++) yield return null;
        Assert.That(Get(hud, "HintText"), Is.EqualTo("GAPS: no guardrail here, don't steer off the edge"));
        float shown = Time.realtimeSinceStartup + .6f;
        while (Time.realtimeSinceStartup < shown) yield return null;
        Capture("mountain-gap-hint");
        float until = Time.realtimeSinceStartup + 2.6f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That((bool)Get(hud, "HintVisible"), Is.False);
        Pose(game, FirstGoatCrossing(game) - 30f, 3.4f);
        for (int i = 0; i < 4; i++) yield return null;
        Assert.That(Get(hud, "HintText"), Is.EqualTo("GOATS: they walk in Day, freeze them in Night"));
        until = Time.realtimeSinceStartup + 3.2f;
        while (Time.realtimeSinceStartup < until) yield return null;
        // The next gap does not repeat the hint.
        Pose(game, gaps[1].x - 30f, 3.4f);
        for (int i = 0; i < 6; i++) yield return null;
        Assert.That((bool)Get(hud, "HintVisible"), Is.False, "Each hint shows once per level.");
    }

    [UnityTest]
    public IEnumerator MountainEscapeDrivesIntoATunnelAndTheGateDrops()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        WinMountainForEscape(game);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        var escape = (MonoBehaviour)Get(game, "Escape");
        Call(escape, "EnsureStarted");
        Assert.That(GameObject.Find("Mountain tunnel escape"), Is.Not.Null, "Mountain ends in its own tunnel scene.");
        Call(escape, "Advance", 2.0f); yield return null;
        Capture("mountain-escape-into-tunnel");
        Call(escape, "Advance", 2.4f); yield return null;
        Assert.That(((Transform)Field(escape, "shutter")).localPosition.y, Is.EqualTo(3f).Within(.05f), "The avalanche gate closes behind the car.");
        Capture("mountain-escape-gate-closed");
    }

    private static void WinMountainForEscape(MonoBehaviour game)
    {
        float end = (float)Get(game, "RunLength");
        var ramps = (Array)Field(Get(game, "Course"), "Ramps"); var last = ramps.GetValue(ramps.Length - 1);
        Pose(game, end - .5f, 0f); Call(game, "ApplyWorld", Get(last, "Destination")); Step(game, .1f);
    }

    [UnityTest]
    public IEnumerator MountainJumpsAreSkiJumpsOverAMistyGorge()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        CheckSkiJumps(game);
    }

    private static void CheckSkiJumps(MonoBehaviour game)
    {
        var visuals = Find("WorldShiftRelayVisuals");
        int jumps = ((Array)Get(game, "RelayLayout")).Length;
        var all = visuals.GetComponentsInChildren<Transform>(true);
        Assert.That(all.Count(t => t.name.StartsWith("Ski jump timber walls")), Is.EqualTo(jumps), "Every Mountain ramp is dressed as a ski jump.");
        Assert.That(all.Count(t => t.name == "Gorge cloud layer (upper)"), Is.EqualTo(jumps), "Every jump crosses a misty gorge.");
        Assert.That(all.Any(t => t.name == "Flowing water below jump"), Is.False, "No stream channel under Mountain jumps any more.");
        Assert.That(all.Where(t => t.name.StartsWith("Ski jump") || t.name.StartsWith("Gorge") || t.name == "Layered gorge walls")
            .SelectMany(t => t.GetComponentsInChildren<Collider>(true)), Is.Empty);
        // The ski jump dressing belongs to the launch world's ramp only.
        foreach (var dressing in all.Where(t => t.name.StartsWith("Ski jump timber walls")))
            Assert.That(dressing.parent.name, Is.EqualTo("Green boost arrows on the actual uphill deck"));
    }
}
