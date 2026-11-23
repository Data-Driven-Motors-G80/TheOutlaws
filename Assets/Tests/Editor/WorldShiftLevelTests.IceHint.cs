using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// The first time the car reaches ice in a level, a short HUD hint explains it, once.
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator IceHintShowsOnceWhenTheCarFirstReachesIce()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        ClearShownHints();
        StartLevel(SnowLevel); yield return WaitForNavigation();
        var game = Started();
        var hud = Find("WorldShiftLevelHud");
        var ice = (Vector2[])Field(Get(game, "Course"), "IceZones");
        Assert.That(ice, Is.Not.Empty);
        Assert.That(ice[0].x, Is.GreaterThan(50f), "Snow's first ice is after the start, so the hint is not shown at the start.");
        for (int i = 0; i < 5; i++) yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.False, "No hint at the start of the level.");

        // Drive onto the first ice.
        Pose(game, ice[0].x - 3f, 0f);
        for (int i = 0; i < 20 && !(bool)Get(game, "OnIce"); i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That((bool)Get(game, "OnIce"), Is.True);
        yield return null; yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.True, "The hint appears on first contact with ice.");
        Assert.That(HudTextShown(hud, "ICE: the car slides, steer early"), Is.True);
        float shown = Time.realtimeSinceStartup + .7f;
        while (Time.realtimeSinceStartup < shown) yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.True, "Still on screen after a moment.");
        Capture("snow-ice-hint");

        // Gone after a few seconds.
        float until = Time.realtimeSinceStartup + 2.6f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.False, "The hint lasts only a few seconds.");

        // Later ice, and a checkpoint retry onto ice, do not show it again.
        Pose(game, ice[1].x + 5f, 0f); Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That((bool)Get(game, "OnIce"), Is.True);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.False, "Only once per level.");
        StartLevel(SnowLevel); yield return WaitForNavigation();
        game = Started(); hud = Find("WorldShiftLevelHud");
        Pose(game, ice[0].x + 5f, 0f); Call(game, "StepSimulation", .05f, 0f, false);
        for (int i = 0; i < 10; i++) yield return null;
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.False, "Restarting the level does not repeat the hint.");
    }

    private static void ClearShownHints()
    {
        var shown = TypeFor("WorldShiftLevelHud").GetField("hintsShown", Flags).GetValue(null);
        shown.GetType().GetMethod("Clear").Invoke(shown, null);
    }

    // Kept out of the iterator: play mode reloads the domain and drops lambda captures inside test coroutines.
    private static bool HudTextShown(MonoBehaviour hud, string text)
        => hud.GetComponentsInChildren<TMP_Text>(false).Any(t => t.text == text && t.gameObject.activeInHierarchy);

    [UnityTest]
    public IEnumerator LevelsWithoutIceNeverShowTheIceHint()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        var hud = Find("WorldShiftLevelHud");
        Assert.That(hud.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Ice hint"), Is.False);
        Assert.That((bool)Get(hud, "IceHintVisible"), Is.False);
    }
}
