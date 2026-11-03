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
    private static MonoBehaviour Game => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .Single(c => c.GetType().Name == "WorldShiftCity" || c.GetType().Name == "DesertShiftTrial");
    private static GetawayRunState Run => (GetawayRunState)FlowType.GetProperty("Escape").GetValue(null);
    private static void Pose(MonoBehaviour game, float d, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, d);
        Call(game.GetType().GetField("driver", Flags).GetValue(game), "RestoreExternalPose", d, x);
        if (game.GetType().Name == "WorldShiftCity") game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
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

    private static readonly string[] UpgradeKeys = { "WorldShift.Getaway.Upgrade.FuelTank", "WorldShift.Getaway.Upgrade.Armor", "WorldShift.Getaway.Upgrade.Headlights" };
    private readonly int[] upgrades = new int[3];

    [SetUp] public void SaveBank()
    {
        bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank", 0); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best", 0);
        for (int i = 0; i < 3; i++) upgrades[i] = PlayerPrefs.GetInt(UpgradeKeys[i], 0);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best);
        for (int i = 0; i < 3; i++) PlayerPrefs.SetInt(UpgradeKeys[i], upgrades[i]);
        PlayerPrefs.Save();
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
    public IEnumerator BankingAtTheDesertExitWritesTheRunAndBothLegs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", false); yield return null; yield return null;
        // City never offers a cash-out; finishing it carries straight on.
        Flow("FinishGetawayLeg", false);
        Assert.That(Events("ws_gate"), Is.Empty, "Ordinary environment endings are not a gate.");
        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        var desert = Game; desert.enabled = false;
        Assert.That(desert.GetType().Name, Is.EqualTo("DesertShiftTrial"));
        // The opening cash trail sits at 50/75/100 m.
        for (int i = 0; i < 400 && (float)Get(desert, "Distance") < 110f; i++) Call(desert, "StepSimulation", .05f, 0f, false);
        var forks = Get(Get(desert, "Getaway"), "Routing");
        Pose(desert, (float)Get(forks, "ExitChoiceAt") - 10f, -3.2f);
        for (int i = 0; i < 200 && Get(desert, "State").ToString() == "Running"; i++) Call(desert, "StepSimulation", .05f, 0f, false);
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));

        Assert.That(Events("ws_gate").Select(e => e["choice"]), Is.EqualTo(new[] { "through", "exit" }));
        var legs = Sheet("ws_legs").ToList();
        Assert.That(legs.Select(l => l["env"]), Is.EqualTo(new[] { "City", "Desert" }));
        Assert.That(legs.Select(l => l["outcome"]), Is.EqualTo(new[] { "through", "exit" }));
        Assert.That(legs.Select(l => l["cashout_offered"]), Is.EqualTo(new[] { false, true }));
        var leg = legs[1];
        Assert.That((int)leg["cash_taken"], Is.GreaterThanOrEqualTo(3));
        Assert.That(leg["loot_boxes"], Is.EqualTo(leg["cash_taken"]));
        Assert.That(Events("ws_pickup").Count(e => e["kind"].Equals("cash") && (bool)e["taken"]), Is.EqualTo(leg["cash_taken"]));
        var run = Sheet("ws_runs").Single();
        Assert.That(run["outcome"], Is.EqualTo("banked"));
        Assert.That(run["banked"], Is.EqualTo(Run.SettledHaul));
        Assert.That(run["legs_started"], Is.EqualTo(2));
        Assert.That(run["legs_cleared"], Is.EqualTo(2));
        Assert.That(run["end_env"], Is.EqualTo("Desert"));
        Assert.That(run["death_reason"], Is.Empty);
        Assert.That((float)run["end_dist_m"], Is.GreaterThan(3000f));
        Assert.That(run["upgrade_total"], Is.EqualTo((int)run["upgrade_fuel"] + (int)run["upgrade_armor"] + (int)run["upgrade_lights"]));

        // Garage purchases between runs are logged with the new level.
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", 5000);
        PlayerPrefs.SetInt(UpgradeKeys[1], 0);
        object armor = Enum.Parse(typeof(VehicleUpgrades.Part), "Armor");
        Assert.That(FlowType.GetMethod("BuyUpgrade").Invoke(null, new[] { armor }), Is.True);
        var upgrade = Events("ws_upgrade").Single();
        Assert.That(upgrade["part"], Is.EqualTo("Armor"));
        Assert.That(upgrade["level"], Is.EqualTo(1));
        Assert.That(upgrade["price"], Is.EqualTo(500));
        Assert.That(upgrade["banked_total"], Is.EqualTo(4500));
    }

    [UnityTest]
    public IEnumerator PickupsPassedInNightAreMissedAndDayPickupsAreTaken()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        // The opening cash trail (35/55/75 m) and the shield (44 m) pass by in Night.
        Call(city, "TrySwitch");
        Call(city, "StepSimulation", 3.5f, 0f, false);
        Assert.That(Run.Carried, Is.Zero);
        Call(city, "TrySwitch");
        Call(city, "TogglePause"); Call(city, "TogglePause");
        Flow("Menu"); yield return null;

        var leg = Sheet("ws_legs").Single();
        Assert.That(leg["cash_taken"], Is.EqualTo(0));
        Assert.That((int)leg["cash_missed"], Is.GreaterThanOrEqualTo(3));
        Assert.That(leg["cash_missed_night"], Is.EqualTo(leg["cash_missed"]), "Every cash box was passed in Night.");
        Assert.That(leg["shield_missed"], Is.EqualTo(1));
        Assert.That(leg["shield_missed_night"], Is.EqualTo(1));
        Assert.That(leg["pickups_taken_pct"], Is.EqualTo(0f));
        Assert.That(leg["pauses"], Is.EqualTo(1));
        Assert.That(Events("ws_pickup").All(e => (bool)e["taken"] == false), Is.True);
        Assert.That(Events("ws_switch").First()["airborne"], Is.False);
    }

    [UnityTest]
    public IEnumerator ContinuingIntoOceanThenLeavingRecordsEachEnvironment()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture();
        Flow("StartGetaway", false); yield return null; yield return null;
        // Drive off the end of City, then stay on the main road past Desert's cash-out.
        for (int leg = 0; leg < 2; leg++)
        {
            var game = Game; game.enabled = false;
            Pose(game, (float)Get(game, "RunLength") - 5f, 0f);
            Call(game, "StepSimulation", .4f, 0f, false);
            for (int i = 0; i < 5; i++) yield return null;
        }
        var ocean = Game; ocean.enabled = false;
        Call(ocean, "StepSimulation", 2f, 0f, false);
        Flow("Menu"); yield return null;

        Assert.That(Events("ws_gate").Select(e => e["choice"]), Is.EqualTo(new[] { "through", "next" }));
        var legs = Sheet("ws_legs").ToList();
        Assert.That(legs.Select(l => l["env"]), Is.EqualTo(new[] { "City", "Desert", "Ocean" }));
        Assert.That(legs.Select(l => l["outcome"]), Is.EqualTo(new[] { "through", "next", "menu" }));
        Assert.That(legs.Select(l => l["leg"]), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(legs.Select(l => l["cashout_offered"]), Is.EqualTo(new[] { false, true, false }));
        Assert.That(legs.Select(l => l["tier"]), Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(Events("ws_leg_start").Select(e => e["cashout_offered"]), Is.EqualTo(new[] { false, true, false }));
        var run = Sheet("ws_runs").Single();
        Assert.That(run["outcome"], Is.EqualTo("menu"));
        Assert.That(run["legs_started"], Is.EqualTo(3));
        Assert.That(run["legs_cleared"], Is.EqualTo(2));
        Assert.That(run["end_env"], Is.EqualTo("Ocean"));
        Assert.That(run["lost"], Is.EqualTo(run["haul"]));
    }
}
