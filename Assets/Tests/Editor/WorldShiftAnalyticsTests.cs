using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldShiftAnalyticsTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private sealed class Captured { public string Sheet, Event; public Dictionary<string, object> Data; }
    private readonly List<Captured> rows = new List<Captured>();
    private int bank, best;
    private static Type FlowType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift.WorldShiftFlow")).First(t => t != null);
    private static Type AnalyticsType => FlowType.Assembly.GetType("Outlaws.WorldShift.WorldShiftAnalytics");
    private static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Flow(string name, params object[] args) => FlowType.GetMethod(name).Invoke(null, args);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static GetawayRunState Run => (GetawayRunState)FlowType.GetProperty("Escape").GetValue(null);
    private static void Pose(MonoBehaviour game, float d, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, d);
        Call(game.GetType().GetField("driver", Flags).GetValue(game), "RestoreExternalPose", d, x);
        game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
    }
    private IEnumerable<Dictionary<string, object>> Sheet(string sheet) => rows.Where(r => r.Sheet == sheet).Select(r => r.Data);
    private IEnumerable<Dictionary<string, object>> Events(string name) => rows.Where(r => r.Event == name).Select(r => r.Data);

    // Entering play mode resets the tracker's statics, so capture is installed afterwards.
    // EnterPlayMode must be yielded by the test itself; the runner ignores it inside nested coroutines.
    private void Capture()
    {
        rows.Clear();
        Action<string, string, Dictionary<string, object>> sink = (sheet, ev, data) => rows.Add(new Captured { Sheet = sheet, Event = ev, Data = data });
        AnalyticsType.GetField("Sink", Flags).SetValue(null, sink);
    }

    [SetUp] public void SaveBank()
    { bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank", 0); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best", 0); }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best); PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator SkippedTutorialThenCaughtInCityRecordsSwitchesAndDeath()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", true); yield return null; yield return null;
        var tutorial = Find("WorldShiftCity"); tutorial.enabled = false;
        Call(tutorial, "StepSimulation", 2f, 0f, false);
        object level = Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), "Tutorial");
        Flow("Next", level); yield return null; yield return null;

        var tutorialRow = Sheet("ws_tutorials").Single();
        Assert.That(tutorialRow["status"], Is.EqualTo("skipped"));
        Assert.That(tutorialRow["then_escape"], Is.True);
        Assert.That((float)tutorialRow["s_0_steer"], Is.GreaterThan(0f));

        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Events("ws_leg_start").Count(), Is.EqualTo(1));
        Call(city, "StepSimulation", 1f, 0f, false);
        Call(city, "TrySwitch");
        Call(city, "StepSimulation", 1f, 0f, false);
        Call(city, "TrySwitch");
        // Driving straight without steering eventually hits enough hazards to be caught.
        for (int i = 0; i < 4000 && Get(city, "State").ToString() == "Running"; i++)
            Call(city, "StepSimulation", .05f, 0f, false);
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Lost));

        var leg = Sheet("ws_legs").Single();
        Assert.That(leg["env"], Is.EqualTo("City"));
        Assert.That(leg["outcome"], Is.EqualTo("lost"));
        Assert.That(leg["switches"], Is.EqualTo(2));
        Assert.That((float)leg["night_s"], Is.EqualTo(1f).Within(.11f));
        Assert.That((int)leg["impacts"], Is.GreaterThan(0));
        Assert.That(leg["death_reason"], Is.Not.Empty);
        Assert.That((float)leg["dist_m"], Is.GreaterThan(0f));

        var run = Sheet("ws_runs").Single();
        Assert.That(run["outcome"], Is.EqualTo("lost"));
        Assert.That(run["failure"], Is.EqualTo(Run.Failure));
        Assert.That(run["tutorial_first"], Is.True);
        Assert.That(run["legs_started"], Is.EqualTo(1));
        Assert.That(run["legs_cleared"], Is.EqualTo(0));
        Assert.That(run["switches"], Is.EqualTo(2));
        Assert.That(run["haul"], Is.EqualTo(Run.SettledHaul));
        Assert.That(Events("ws_switch").Select(e => e["to"]), Is.EqualTo(new[] { "night", "day" }));
        Assert.That(Events("ws_impact").Count(), Is.EqualTo((int)leg["impacts"]));
        Assert.That(Events("ws_death").Count(), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator BankingAtTheExitWritesOneRunAndLeg()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Call(city, "StepSimulation", 3f, 0f, false);
        Pose(city, (float)Get(city, "RunLength") - 90f, -3.2f); yield return null;
        Call(city, "StepSimulation", 4f, 0f, false); yield return null;
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));
        Flow("FinishGetawayLeg", false);

        Assert.That(Events("ws_gate").Single()["choice"], Is.EqualTo("exit"));
        var leg = Sheet("ws_legs").Single();
        Assert.That(leg["outcome"], Is.EqualTo("exit"));
        Assert.That((int)leg["loot_boxes"], Is.EqualTo(3));
        var run = Sheet("ws_runs").Single();
        Assert.That(run["outcome"], Is.EqualTo("banked"));
        Assert.That(run["banked"], Is.EqualTo(Run.SettledHaul));
        Assert.That(run["legs_cleared"], Is.EqualTo(1));
        Assert.That(run["death_reason"], Is.Empty);
        Assert.That((float)run["end_dist_m"], Is.GreaterThan(3000f));
    }

    [UnityTest]
    public IEnumerator ContinuingToDesertThenLeavingRecordsBothLegs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Pose(city, (float)Get(city, "RunLength") - 90f, 3.2f); yield return null;
        Call(city, "StepSimulation", 4f, 0f, false);
        for (int i = 0; i < 5; i++) yield return null;
        var desert = Find("DesertShiftTrial"); desert.enabled = false;
        Call(desert, "StepSimulation", 2f, 0f, false);
        Flow("Menu"); yield return null;

        Assert.That(Events("ws_gate").Single()["choice"], Is.EqualTo("next"));
        var legs = Sheet("ws_legs").ToList();
        Assert.That(legs.Select(l => l["env"]), Is.EqualTo(new[] { "City", "Desert" }));
        Assert.That(legs.Select(l => l["outcome"]), Is.EqualTo(new[] { "next", "menu" }));
        Assert.That(legs[1]["leg"], Is.EqualTo(1));
        var run = Sheet("ws_runs").Single();
        Assert.That(run["outcome"], Is.EqualTo("menu"));
        Assert.That(run["legs_started"], Is.EqualTo(2));
        Assert.That(run["legs_cleared"], Is.EqualTo(1));
        Assert.That(run["end_env"], Is.EqualTo("Desert"));
        Assert.That(run["lost"], Is.EqualTo(run["haul"]));
    }
}
