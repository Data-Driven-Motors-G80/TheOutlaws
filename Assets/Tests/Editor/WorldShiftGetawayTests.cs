using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WorldShiftGetawayTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private int bank, best;
    private static Type FlowType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift.WorldShiftFlow")).First(t => t != null);
    private static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Flow(string name, params object[] args) => FlowType.GetMethod(name).Invoke(null, args);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static GetawayRunState Run => (GetawayRunState)FlowType.GetProperty("Escape").GetValue(null);
    private static void Pose(MonoBehaviour game, float d, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, d);
        Call(game.GetType().GetField("driver", Flags).GetValue(game), "RestoreExternalPose", d, x);
        if (game.GetType().Name == "WorldShiftCity") game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
    }

    [SetUp] public void SaveBank()
    { bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank", 0); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best", 0); }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best); PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator TutorialLootPauseBankAndDuplicateSettlement()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture("menu");
        Flow("StartGetaway", true); yield return null; yield return null;
        var tutorial = Find("WorldShiftCity"); tutorial.enabled = false;
        Assert.That(Get(tutorial, "TutorialLevel"), Is.True);
        Assert.That(Get(tutorial, "GetawayMode"), Is.False);
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Running));
        object level = Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), "Tutorial");
        Flow("Next", level); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Get(city, "GetawayMode"), Is.True);
        Call(city, "StepSimulation", 3f, 0f, false);
        Assert.That(Run.Carried, Is.EqualTo(300));
        Call(city, "TogglePause"); float d = (float)Get(city, "Distance");
        var getawayHud = Find("WorldShiftGetaway");
        Call(getawayHud, "Refresh");
        var drivingHud = (GameObject)getawayHud.GetType().GetField("drivingHud", Flags).GetValue(getawayHud);
        Assert.That(drivingHud.activeSelf, Is.False, "Pause must hide the driving UI.");
        Capture("paused");
        Call(city, "StepSimulation", 2f, 1f, true);
        Assert.That(Get(city, "Distance"), Is.EqualTo(d));
        Assert.That(Run.Carried, Is.EqualTo(300));
        Call(city, "RetryCheckpoint"); Assert.That(Get(city, "State").ToString(), Is.EqualTo("Paused"));
        Call(city, "TogglePause");
        Capture("city-loot");
        float end = (float)Get(city, "RunLength");
        Pose(city, end - 90f, -3.2f); yield return null;
        Capture("exit-choice");
        Call(city, "StepSimulation", 4f, 0f, false); yield return null;
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));
        int total = PlayerPrefs.GetInt("WorldShift.Getaway.Bank");
        Assert.That(total, Is.EqualTo(bank + 300));
        Flow("FinishGetawayLeg", false);
        Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank"), Is.EqualTo(total));
        Assert.That(Run.Carried, Is.Zero);
        Capture("banked");
        Assert.That(drivingHud.activeSelf, Is.False, "Results must hide the driving UI.");
    }

    [UnityTest]
    public IEnumerator CompleteCityEscapeWithLootAndBankThroughTheExitLane()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        var safeWorld = typeof(WorldShiftCityTests).GetMethod("SafeWorld", Flags);
        var safeLane = typeof(WorldShiftCityTests).GetMethod("SafeLane", Flags);
        bool pictured = false;
        for (int i = 0; i < 2600 && Get(game, "State").ToString() == "Running"; i++)
        {
            float d = (float)Get(game, "Distance");
            string desired = d < 82f ? "Day" : (string)safeWorld.Invoke(null, new object[] { d });
            if (!(bool)Get(game, "IsMerged") && Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            float target = d > 3380f ? -3.2f : (float)safeLane.Invoke(null, new object[] { d });
            Call(game, "StepSimulation", .05f, Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            Assert.That(Get(game, "Hits"), Is.EqualTo(0), "Unexpected collision at " + d);
            Assert.That(Get(game, "State").ToString(), Is.Not.EqualTo("Lost"), "Escape failed at " + d);
            if (!pictured && d > 3450f) { Capture("full-run-exit"); pictured = true; }
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));
        Assert.That(Run.SettledHaul, Is.GreaterThanOrEqualTo(300));
        Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank"), Is.EqualTo(bank + Run.SettledHaul));
        Assert.That((float)Get(game, "Duration"), Is.GreaterThan(115f));
    }

    [UnityTest]
    public IEnumerator ContinueAcrossBothEnvironmentsAndLoseWithoutCheckpointRefund()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Call(city, "StepSimulation", 3f, 0f, false);
        Pose(city, (float)Get(city, "RunLength") - 5f, 3.3f);
        Call(city, "StepSimulation", .3f, 0f, false);
        yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Desert_WorldShift"));
        Assert.That(Run.Leg, Is.EqualTo(1));
        Assert.That(Run.Carried, Is.EqualTo(300));
        var desert = Find("DesertShiftTrial"); desert.enabled = false;
        Call(desert, "StepSimulation", 3.2f, 0f, false);
        Assert.That(Run.Carried, Is.GreaterThanOrEqualTo(750));
        Capture("desert-loot");
        Pose(desert, (float)Get(desert, "RunLength") - 5f, 3.3f);
        Call(desert, "StepSimulation", .3f, 0f, false);
        yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("City_WorldShift"));
        Assert.That(Run.Leg, Is.EqualTo(2));
        int haul = Run.Carried;
        city = Find("WorldShiftCity"); city.enabled = false;
        Call(city, "Fail", "CAUGHT");
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Lost"));
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Lost));
        Assert.That(Run.SettledHaul, Is.EqualTo(haul));
        Call(city, "RetryCheckpoint"); Call(city, "StepSimulation", 3f, 0f, false);
        Assert.That(Run.Carried, Is.Zero);
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Lost"));
        Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank", 0), Is.EqualTo(bank));
        yield return null; Capture("caught");
    }

    private static void Capture(string name)
    {
        // Simulations are stepped manually; refresh camera and presentation before capturing.
        var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        var city = components.FirstOrDefault(c => c.GetType().Name == "WorldShiftCity");
        if (city != null) Call(city, "UpdateCamera", true);
        var desertView = components.FirstOrDefault(c => c.GetType().Name == "DesertShiftTrialView");
        if (desertView != null) Call(desertView, "Refresh");
        var escape = components.FirstOrDefault(c => c.GetType().Name == "WorldShiftGetaway");
        if (escape != null) Call(escape, "Refresh");
        var split = components.FirstOrDefault(c => c.GetType().Name == "WorldShiftSplitView");
        if (split != null) Call(split, "RenderViews");
        // Use the existing camera+overlay capture helper so batchmode includes native UI.
        typeof(WorldShiftCampaignTests).GetMethod("Capture", Flags).Invoke(null, new object[] { "getaway-" + name });
    }
}
