using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class WorldShiftLevelTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string BeatenKey = "WorldShift.Levels.HighestBeaten", UnlockAllKey = "WorldShift.Levels.UnlockAll";
    private int savedBeaten, savedUnlockAll;
    private readonly List<(string sheet, string name, Dictionary<string, object> data)> analytics = new List<(string, string, Dictionary<string, object>)>();

    private static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift." + name)).First(t => t != null);
    private static Type Levels => TypeFor("WorldShiftLevels");
    private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
    private static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    private static object Static(Type t, string n, params object[] args) => t.GetMethod(n, Flags).Invoke(null, args);
    private static object StaticGet(Type t, string n) => t.GetProperty(n, Flags).GetValue(null);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static object World(string name) => Enum.Parse(TypeFor("ShiftWorld"), name);
    private static void Capture(string name)
    {
        // The simulation is stepped manually; bring the HUD up to date before the picture.
        var hud = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(c => c.GetType().Name == "WorldShiftLevelHud");
        if (hud != null) Call(hud, "Refresh");
        typeof(WorldShiftCampaignTests).GetMethod("Capture", Flags).Invoke(null, new object[] { "level-" + name });
    }
    private static void Pose(MonoBehaviour game, float distance, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, distance);
        game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
        Call(Field(game, "driver"), "RestoreExternalPose", distance, x);
        Call(game, "UpdatePolice", 0f);
    }
    private static void Step(MonoBehaviour game, float seconds, float steer = 0f)
    {
        for (float t = 0f; t < seconds; t += .05f) Call(game, "StepSimulation", .05f, steer, false);
    }

    [SetUp]
    public void Save()
    {
        savedBeaten = PlayerPrefs.GetInt(BeatenKey, -1);
        savedUnlockAll = PlayerPrefs.GetInt(UnlockAllKey, 0);
        analytics.Clear();
        Action<string, string, Dictionary<string, object>> sink = (sheet, name, data) => analytics.Add((sheet, name, data));
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, null);
        Static(Levels, "Leave");
        PlayerPrefs.SetInt(BeatenKey, savedBeaten);
        PlayerPrefs.SetInt(UnlockAllKey, savedUnlockAll);
        PlayerPrefs.Save();
    }

    // Call after EnterPlayMode (nested enumerators cannot enter play mode), then wait two frames.
    private void StartLevel(int level)
    {
        // Play mode resets session statics, so attach the analytics sink again.
        Action<string, string, Dictionary<string, object>> sink = (sheet, name, data) => analytics.Add((sheet, name, data));
        TypeFor("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
        PlayerPrefs.SetInt(UnlockAllKey, 1);
        Assert.That(Static(Levels, "Play", level, "menu"), Is.True);
    }

    private static MonoBehaviour Started()
    {
        var game = Find("WorldShiftCity");
        game.enabled = false;
        return game;
    }

    [Test]
    public void EveryCourseEndsSectionsOnRampsAndLeavesTimeToSwitch()
    {
        var builder = TypeFor("WorldShiftLevelCourse");
        for (int level = 0; level < 6; level++)
        {
            object course = Static(builder, "For", level);
            var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
            var hazards = ((Array)Field(course, "Hazards")).Cast<object>().ToArray();
            var checkpoints = ((Array)Field(course, "Checkpoints")).Cast<object>().ToArray();
            float length = (float)Field(course, "Length");
            Assert.That(ramps.Length, Is.EqualTo(level == 0 ? 1 : 5), "level " + level);
            Assert.That(checkpoints.Length, Is.EqualTo(5), "Four mid-level checkpoints plus the start, level " + level);
            for (int i = 1; i < checkpoints.Length; i++)
                Assert.That((float)Field(checkpoints[i], "Distance"), Is.GreaterThan((float)Field(checkpoints[i - 1], "Distance")));
            foreach (var ramp in ramps)
            {
                float start = (float)Field(ramp, "Ramp") - 30f, end = (float)Field(ramp, "End") + 10f;
                foreach (var h in hazards.Where(h => (float)Field(h, "MotionSpeed") == 0f))
                    Assert.That((float)Field(h, "End") < start || (float)Field(h, "Start") > end, Is.True,
                        $"Level {level}: ramp approach and landing at {start + 30f} stay clear of obstacles.");
            }
            var walls = hazards.Where(h => (int)Field(h, "LaneMask") == 7 && (float)Field(h, "MotionSpeed") == 0f).ToArray();
            foreach (var a in walls)
                foreach (var b in walls)
                    if (!Field(a, "World").Equals(Field(b, "World")))
                        Assert.That(Mathf.Abs((float)Field(a, "Start") - (float)Field(b, "Start")), Is.GreaterThanOrEqualTo(35f),
                            $"Level {level}: opposite full walls at {Field(a, "Start")} and {Field(b, "Start")} need a switch window.");
            foreach (var checkpoint in checkpoints)
            {
                float at = (float)Field(checkpoint, "Distance");
                foreach (var wall in walls.Where(w => Field(w, "World").Equals(Field(checkpoint, "World"))))
                    Assert.That((float)Field(wall, "Start") - at, Is.Not.InRange(0f, 45f), $"Level {level}: respawn at {at} is not right in front of a wall.");
            }
            foreach (var h in hazards) Assert.That((float)Field(h, "End"), Is.LessThan(length));
        }
    }

    [Test]
    public void LevelsUnlockInOrderAndTheTestSwitchOpensEverything()
    {
        Static(Levels, "ResetProgress");
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        Assert.That(Static(Levels, "IsUnlocked", 0), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.False);
        Static(Levels, "Complete", 0);
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 2), Is.False);
        Static(Levels, "Complete", 0);
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0), "Replaying never lowers progress.");
        Levels.GetProperty("UnlockAll").SetValue(null, true);
        Assert.That(Static(Levels, "IsUnlocked", 5), Is.True);
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0), "Unlock All does not change saved progress.");
    }

    [UnityTest]
    public IEnumerator HomeShowsSixLevelsWithLocksUntilBeaten()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        Static(Levels, "ResetProgress");
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        yield return new EnterPlayMode();
        var menu = Find("WorldShiftMenu");
        var tiles = menu.GetComponentsInChildren<Button>().Select(b => b.name).ToArray();
        Assert.That(tiles, Is.EqualTo(new[] { "Level 0", "Level 1", "Level 2", "Level 3", "Level 4", "Level 5" }));
        Assert.That(menu.GetComponentsInChildren<Transform>().Count(t => t.name == "Lock badge"), Is.EqualTo(5));
        Assert.That(menu.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text), Does.Not.Contain("Upgrade car"));
        Capture("menu-locked");
        menu.GetComponentsInChildren<Button>().Single(b => b.name == "Level 3").onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"), "Locked levels do not start.");
        Levels.GetProperty("UnlockAll").SetValue(null, true);
        Call(menu, "BuildLevels");
        yield return null;
        Assert.That(menu.GetComponentsInChildren<Transform>().Count(t => t.name == "Lock badge"), Is.EqualTo(0));
        Capture("menu-unlock-all");
    }

    [UnityTest]
    public IEnumerator OneHitCrashLetsPoliceCatchUpThenRestartsAtTheCheckpoint()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return null; yield return null;
        var game = Started();
        Assert.That(Get(game, "LevelMode"), Is.True);
        Assert.That(Get(game, "Combat"), Is.Null, "No shooting in level play.");
        Assert.That(Get(game, "Getaway"), Is.Null, "No cash, fuel pickups, power-ups or forks.");
        Assert.That(Get(game, "Environment").ToString(), Is.EqualTo("City"));
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Capture("city-start");
        for (int i = 0; i < 200 && Get(game, "State").ToString() == "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "The first block in the middle lane ends the attempt.");
        Assert.That(Get(game, "Capturing"), Is.True);
        Assert.That(Get(game, "Deaths"), Is.EqualTo(1));
        var police = (Component)Get(game, "Police");
        var player = (Transform)Get(game, "Player");
        float before = Vector3.Distance(police.transform.position, player.position);
        Step(game, .8f);
        Assert.That(Vector3.Distance(police.transform.position, player.position), Is.LessThan(before - 2f), "The police close in after a crash.");
        Capture("police-catch");
        for (int i = 0; i < 40 && Get(game, "State").ToString() != "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(0f).Within(.01f));
        var death = analytics.Single(a => a.name == "wl_death");
        Assert.That(death.data["cause"], Is.EqualTo("crash"));
        Assert.That(death.data["hazard_world"], Is.EqualTo("day"));
    }

    [UnityTest]
    public IEnumerator OtherWorldObstaclesShowAsDashesAndSwitchingIntoOneCrashes()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(1); yield return null; yield return null;
        var game = Started();
        // City section 1: a Night block covers the left and middle lanes at 150 m.
        Pose(game, 132f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        var ghost = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Echo Barrier 150").Find("Outline - belongs to other world");
        Assert.That(ghost.gameObject.activeInHierarchy, Is.True, "Night's obstacle is visible as dashes from Day.");
        Capture("day-sees-night-dashes");
        Pose(game, 151f, 0f);
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Switching inside the dashes fills them in.");
        Assert.That(analytics.Single(a => a.name == "wl_death").data["cause"], Is.EqualTo("switched_into"));
        Capture("switched-into-dashes");
    }

    [UnityTest]
    public IEnumerator DesertSlidersMoveInDayAndFreezeInNight()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(2); yield return null; yield return null;
        var game = Started();
        Assert.That(Get(game, "Environment").ToString(), Is.EqualTo("Desert"));
        var slider = ((Array)Get(game, "Hazards")).Cast<object>().First(h => (bool)Field(h, "LateralMotion"));
        Step(game, .6f);
        float moved = (float)Get(slider, "MotionTravel");
        Assert.That(moved, Is.GreaterThan(0f));
        Capture("desert-day");
        Call(game, "TrySwitch");
        Step(game, .6f);
        Assert.That((float)Get(slider, "MotionTravel"), Is.EqualTo(moved).Within(.0001f), "Sliders freeze in Night.");
        Capture("desert-night");
    }

    [UnityTest]
    public IEnumerator TrainingUsesSignsAndFinishingUnlocksLevelOne()
    {
        Static(Levels, "ResetProgress");
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return null; yield return null;
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        var game = Started();
        Assert.That(game.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Highway sign")), Is.EqualTo(5));
        Pose(game, 20f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-steer-sign");
        object course = Get(game, "Course");
        var ramp = ((Array)Field(course, "Ramps")).GetValue(0);
        Pose(game, (float)Field(ramp, "Ramp") - 50f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-ramp");
        for (int i = 0; i < 400 && Get(game, "State").ToString() == "Running"; i++)
        {
            Call(game, "StepSimulation", .05f, 0f, false);
            if ((bool)Get(game, "IsRelayAirborne") && Get(game, "World").ToString() == "Day") Call(game, "TrySwitch");
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"), "Launch in Day, switch midair, land in Night, reach the finish.");
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0));
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True);
        Assert.That(Static(Levels, "IsUnlocked", 2), Is.False);
        yield return null;
        Capture("training-complete");
        var row = analytics.Single(a => a.sheet == "ws_level_attempts").data;
        Assert.That(row["outcome"], Is.EqualTo("complete"));
        Assert.That(row["level"], Is.EqualTo(0));
    }
}
