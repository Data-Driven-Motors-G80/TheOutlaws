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
    public IEnumerator MenuUsesPlainPanelsAndKeepsBothActions()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        var menu = Find("WorldShiftMenu");
        var buttons = menu.GetComponentsInChildren<Button>();
        Assert.That(buttons.Select(b => b.name), Is.EquivalentTo(new[] { "Start run", "Tutorial", "Upgrade car" }));
        Assert.That(menu.GetComponentsInChildren<Image>().All(image => image.sprite == null), Is.True,
            "The playtest menu uses flat panels without decorative sprites.");
        Assert.That(menu.GetComponentsInChildren<Mask>(), Is.Empty);
        Assert.That(menu.GetComponentsInChildren<Transform>().Any(t => t.name == "Road preview" || t.name == "Day and night road"), Is.False);
        Canvas.ForceUpdateCanvases();
        var home = (GameObject)menu.GetType().GetField("home", Flags).GetValue(menu);
        var actions = home.GetComponentsInChildren<Button>();
        Assert.That(actions.Select(b => b.name), Is.EqualTo(new[] { "Start run", "Tutorial", "Upgrade car" }));
        var positions = actions.Select(b => b.transform.position).ToArray();
        Assert.That(positions[0].x, Is.EqualTo(positions[1].x).Within(.1f));
        Assert.That(positions[1].x, Is.EqualTo(positions[2].x).Within(.1f));
        Assert.That(positions[0].y, Is.GreaterThan(positions[1].y));
        Assert.That(positions[1].y, Is.GreaterThan(positions[2].y));
        Assert.That(home.GetComponentsInChildren<TMPro.TMP_Text>().Length, Is.EqualTo(5), "Title, three actions and saved cash; no duplicate descriptions or footer.");
        foreach (var label in menu.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate();
            Assert.That(label.isTextOverflowing, Is.False, label.text);
        }
        Capture("plain-playtest-menu");
    }

    [UnityTest]
    public IEnumerator GarageShowsThreeUpgradesAndAllowsFreeStart()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        var menu = Find("WorldShiftMenu");
        menu.GetComponentsInChildren<Button>().Single(b => b.name == "Upgrade car").onClick.Invoke();
        var buttons = menu.GetComponentsInChildren<Button>();
        Assert.That(buttons.Select(b => b.name), Does.Contain("Upgrade Fuel tank"));
        Assert.That(buttons.Select(b => b.name), Does.Contain("Upgrade Armor"));
        Assert.That(buttons.Select(b => b.name), Does.Contain("Upgrade Headlights"));
        Canvas.ForceUpdateCanvases();
        foreach (var label in menu.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate();
            Assert.That(label.isTextOverflowing, Is.False, label.text);
        }
        Capture("garage-upgrades");
        buttons.Single(b => b.name == "Start next run").onClick.Invoke();
        yield return null; yield return null;
        Assert.That(Get(Find("WorldShiftCity"), "GetawayMode"), Is.True);
    }

    [UnityTest]
    public IEnumerator TutorialWaitsOnlyForUnfinishedActions()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", true); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        var hud = Find("WorldShiftHud");
        var prompt = (TMPro.TMP_Text)hud.GetType().GetField("lessonHint", Flags).GetValue(hud);
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 1);
        Call(game, "RetryCheckpoint");
        var hazards = (Array)Get(game, "Hazards");
        var traffic = hazards.GetValue(1);
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That((float)Get(traffic, "MotionTravel"), Is.GreaterThan(0f));
        Assert.That((float)Get(game, "Speed"), Is.EqualTo(18f));
        Call(hud, "Refresh"); Capture("tutorial-moving-traffic");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TutorialLessonComplete"), Is.False, "Switching alone does not finish the lesson.");
        float frozen = (float)Get(traffic, "MotionTravel");
        float frozenX = (float)Get(traffic, "LateralOffset");
        Assert.That(Mathf.Abs(frozenX), Is.GreaterThan(1f), "Day traffic visibly crosses the road.");
        Call(game, "StepSimulation", .5f, 1f, false);
        Assert.That(Get(traffic, "MotionTravel"), Is.EqualTo(frozen));
        Assert.That(Get(traffic, "LateralOffset"), Is.EqualTo(frozenX));
        Call(hud, "Refresh"); Capture("tutorial-frozen-traffic"); Capture("tutorial-frozen-traffic-1080");
        Call(game, "TogglePause");
        float distance = (float)Get(game, "Distance");
        Call(game, "StepSimulation", 2f, 0f, true);
        Assert.That(Get(game, "Distance"), Is.EqualTo(distance));
        Call(game, "TogglePause");
        for (int i = 0; i < 400 && (int)Get(game, "TutorialLessonIndex") == 1; i++)
        {
            float steer = Mathf.Clamp((3.2f - (float)Get(game, "Lateral")) * 2f, -1f, 1f);
            Call(game, "StepSimulation", .05f, steer, false);
        }
        Assert.That(Get(game, "TutorialLessonIndex"), Is.EqualTo(2));
        Call(hud, "Refresh"); Assert.That(prompt.text, Is.EqualTo("No cash, fuel or power-ups in Nightshift.\nReturn to Day."));
        Capture("tutorial-night-no-loot"); Capture("tutorial-night-no-loot-1080");
        Call(game, "TrySwitch"); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Collect cash in Day to fund your escape."));
        Assert.That(Get(game, "TutorialLessonComplete"), Is.False);
        for (int i = 0; i < 200 && !(bool)Get(game, "TutorialLessonComplete"); i++)
            Call(game, "StepSimulation", .05f, Mathf.Clamp(-(float)Get(game, "Lateral") * 2f, -1f, 1f), false);
        Assert.That((int)Get(Get(game, "TutorialRewards"), "Collected"), Is.GreaterThan(0));
        Assert.That(Get(game, "TutorialLessonComplete"), Is.True);
        Call(hud, "Refresh"); Assert.That(prompt.text, Is.Empty);
        // Missing the action retries this short lesson rather than silently progressing.
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 1);
        Call(game, "RetryCheckpoint"); Pose(game, 217f, 3.2f);
        Call(game, "StepSimulation", .5f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "TutorialLessonIndex"), Is.EqualTo(1));
        Assert.That(Get(game, "Distance"), Is.EqualTo(110f));
        Assert.That((float)Get(traffic, "MotionTravel"), Is.Zero);
        // An early correct switch counts; there is no forced return just to replay motion.
        Call(game, "TrySwitch"); Assert.That(Get(game, "TutorialControlKey"), Is.EqualTo("A / D"));
        Call(game, "TrySwitch");
        // Contact with the frozen block is still a collision and retries locally.
        Call(game, "StepSimulation", .1f, 0f, false); Call(game, "TrySwitch");
        Pose(game, 191f, (float)Get(traffic, "LateralOffset")); Call(game, "StepSimulation", .1f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Call(game, "StepSimulation", 1f, 0f, false);
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 2); Call(game, "RetryCheckpoint");
        Call(game, "TrySwitch");
        Pose(game, 327f, -3.2f); Call(game, "StepSimulation", .5f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Missing both supplies retries the lesson.");
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "Distance"), Is.EqualTo(220f));
        Assert.That(Get(Get(game, "TutorialRewards"), "Collected"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator TutorialMessagesStayStableAcrossRetriesTakeoffLandingAndGapHandoff()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", true); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false; var hud = Find("WorldShiftHud");
        var prompt = (TMPro.TMP_Text)hud.GetType().GetField("lessonHint", Flags).GetValue(hud);
        for (int lesson = 0; lesson < 6; lesson++)
        {
            game.GetType().GetField("checkpointIndex", Flags).SetValue(game, lesson); Call(game, "RetryCheckpoint");
            string instruction = (string)Get(game, "TutorialPrompt"), key = (string)Get(game, "TutorialControlKey");
            if (lesson == 1 || lesson == 2 || lesson == 5) Call(game, "TrySwitch");
            Call(game, "Fail", "TEST RETRY"); Call(hud, "Refresh");
            Assert.That(prompt.text, Is.EqualTo(instruction), "Retry keeps the upcoming lesson, rather than flashing a generic message: " + lesson);
            Assert.That(Get(game, "TutorialControlKey"), Is.EqualTo(key));
            Call(game, "StepSimulation", .8f, 0f, false); Call(hud, "Refresh");
            Assert.That(prompt.text, Is.EqualTo(instruction));
        }
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 3); Call(game, "RetryCheckpoint");
        Assert.That(Get(game, "TutorialControlKey"), Is.EqualTo("F / CLICK"));
        Pose(game, 410f, 0f); Assert.That(Call(Get(game, "Combat"), "TryShoot"), Is.True);
        foreach (float d in new[] { 449f, 450f, 459f, 460f })
        {
            Pose(game, d, 3.2f); Call(game, "StepSimulation", .001f, 0f, false); Call(hud, "Refresh");
            Assert.That(prompt.text, Is.EqualTo("Steer through the gap."), "No blank flash or rewording at the gap handoff.");
        }
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 5); Call(game, "RetryCheckpoint");
        string rampPrompt = (string)Get(game, "TutorialPrompt");
        Pose(game, 709.5f, 0f); Call(game, "StepSimulation", .025f, 0f, false);
        Assert.That(Get(game, "IsRelayAirborne"), Is.True);
        Assert.That(Get(game, "TutorialPrompt"), Is.EqualTo(rampPrompt), "Takeoff does not briefly repeat the same switch instruction.");
        Call(game, "TrySwitch"); string landingPrompt = (string)Get(game, "TutorialPrompt");
        for (int i = 0; i < 200 && (bool)Get(game, "IsRelayAirborne"); i++)
        {
            Call(game, "StepSimulation", .05f, 0f, false);
            Assert.That(Get(game, "TutorialPrompt"), Is.EqualTo(landingPrompt));
        }
        Assert.That(Get(game, "RelaysCompleted"), Is.EqualTo(1));
        Call(hud, "Refresh"); Assert.That(prompt.text, Is.EqualTo(landingPrompt));
    }

    [UnityTest]
    public IEnumerator TutorialNightHasNoLootAndJumpUsesGameplayLandingProtection()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", true); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 2);
        Call(game, "RetryCheckpoint");
        var rewards = Get(game, "TutorialRewards");
        Call(rewards, "Present", Get(game, "World"));
        var boxes = (Transform[])rewards.GetType().GetField("boxes", Flags).GetValue(rewards);
        Assert.That(boxes.All(b => !b.gameObject.activeSelf), Is.True, "Nightshift must show no cash.");
        Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Any(g => g.name == "Tutorial supply outline"), Is.False);
        Call(rewards, "Step", 270f, 300f, 0f, 0f);
        Assert.That(Get(rewards, "Collected"), Is.EqualTo(0), "Crossing cash positions at night gives nothing.");
        Call(game, "TrySwitch");
        Assert.That(boxes.All(b => b.gameObject.activeSelf), Is.True);
        Call(rewards, "Step", 270f, 300f, 0f, 0f);
        Assert.That(Get(rewards, "Collected"), Is.EqualTo(2));

        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 5); Call(game, "RetryCheckpoint");
        // Missing the midair switch retries only the jump.
        for (int i = 0; i < 600 && Get(game, "State").ToString() == "Running"; i++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Call(game, "StepSimulation", 1.1f, 0f, false);
        Assert.That(Get(game, "Distance"), Is.EqualTo(580f));
        for (int i = 0; i < 600 && !(bool)Get(game, "IsRelayAirborne"); i++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "IsRelayAirborne"), Is.True);
        Call(game, "TrySwitch");
        Call(Find("WorldShiftHud"), "Refresh"); Capture("tutorial-night-jump");
        for (int i = 0; i < 200 && (bool)Get(game, "IsRelayAirborne"); i++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "RelaysCompleted"), Is.EqualTo(1));
        Assert.That(Call(game, "TrySwitch"), Is.False, "Early return switches are ignored on the one-world landing.");
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"));
        for (int i = 0; i < 200 && (float)Get(game, "Distance") < 851f; i++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "TutorialLessonComplete"), Is.True);
        Assert.That(Call(game, "TrySwitch"), Is.True, "Both roads are safe after rejoining.");
    }

    [UnityTest]
    public IEnumerator MenuTutorialCityDesertAndCollisionSmoke()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        var menuButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        Assert.That(menuButtons.Select(b => b.name), Is.EquivalentTo(new[] { "Start run", "Tutorial", "Upgrade car" }));
        Capture("01-menu");
        menuButtons.Single(b => b.name == "Start run").onClick.Invoke();
        yield return null; yield return null;
        var escape = Find("WorldShiftCity"); escape.enabled = false;
        Assert.That(Get(escape, "TutorialLevel"), Is.False, "Escape must start directly, even in a fresh session.");
        Assert.That(Get(escape, "GetawayMode"), Is.True);
        Assert.That(Get(escape, "State").ToString(), Is.EqualTo("Running"));
        Flow("Select", Level("City"), true);
        yield return null; yield return null;
        var tutorial = Find("WorldShiftCity"); tutorial.enabled = false;
        Assert.That(Get(tutorial, "TutorialLevel"), Is.EqualTo(true));
        Assert.That(Get(tutorial, "State").ToString(), Is.EqualTo("Running"));
        Call(Find("WorldShiftHud"), "Refresh");
        Capture("tutorial-one-prompt");
        bool learnedShooting = false, testedSwitchRetry = false;
        for (int i = 0; i < 1600 && Get(tutorial, "State").ToString() == "Running"; i++)
        {
            float d = (float)Get(tutorial, "Distance");
            if (!learnedShooting && d >= 410f && d < 480f)
            {
                float stoppedAt = d, gap = (float)Get(tutorial, "PursuitGap");
                Call(tutorial, "StepSimulation", 3f, 0f, false);
                Assert.That((float)Get(tutorial, "Distance"), Is.EqualTo(stoppedAt).Within(.01f), "The lesson waits safely for the player's shot.");
                Assert.That((float)Get(tutorial, "PursuitGap"), Is.EqualTo(gap).Within(.01f));
                Assert.That(Get(tutorial, "SectionHint"), Is.EqualTo("F / click: shoot the block."));
                Call(tutorial, "PresentWorldForCamera", Get(tutorial, "World"));
                Call(Find("WorldShiftHud"), "Refresh");
                Capture("tutorial-shoot");
                var combat = Get(tutorial, "Combat");
                Call(tutorial, "TogglePause"); Assert.That(Call(combat, "TryShoot"), Is.False);
                Call(tutorial, "TogglePause"); Assert.That(Call(combat, "TryShoot"), Is.True);
                Assert.That(Get(tutorial, "SectionHint"), Is.EqualTo("Nice shot!"));
                var shotBlocks = ((Array)Get(tutorial, "Hazards")).Cast<object>()
                    .Where(h => (float)h.GetType().GetField("Start").GetValue(h) == 440f).ToArray();
                Assert.That(shotBlocks.Length, Is.EqualTo(3));
                Assert.That(shotBlocks.Count(h => (bool)Call(combat, "IsDestroyed", h)), Is.EqualTo(1),
                    "One shot opens one lane, leaving the other two blocks solid.");
                Assert.That((float)Get(tutorial, "Speed"), Is.EqualTo(18f));
                learnedShooting = true;
            }
            string desired = d >= 130f && d < 260f || d >= 710f ? "Echo" : "Day";
            if (Get(tutorial, "World").ToString() != desired)
            {
                Call(tutorial, "TrySwitch");
                if (d >= 120f && d < 380f)
                {
                    Assert.That(Get(tutorial, "TutorialLessonComplete"), Is.False, "The player must use the frozen route.");
                    if (desired == "Echo" && !testedSwitchRetry)
                    {
                        testedSwitchRetry = true;
                        Call(tutorial, "TrySwitch");
                        Assert.That(Get(tutorial, "TutorialLessonComplete"), Is.False);
                        Call(tutorial, "RetryCheckpoint");
                        Assert.That(Get(tutorial, "SectionHint"), Is.EqualTo("Enter Nightshift to escape the roadblock."), "Retrying the lesson restores its instruction.");
                        Call(tutorial, "TrySwitch");
                        Assert.That(Get(tutorial, "TutorialLessonComplete"), Is.False);
                    }
                }
            }
            float target = d >= 580f ? 0f : d < 240f || d < 534f && d >= 400f ? 3.2f : d >= 534f ? -3.2f : 0f;
            Call(tutorial, "StepSimulation", .05f, Mathf.Clamp((target - (float)Get(tutorial, "Lateral")) * 2f, -1f, 1f), false);
            if (d < 100f && Mathf.Abs((float)Get(tutorial, "Lateral")) >= .8f)
                Assert.That(Get(tutorial, "SectionHint"), Is.Empty, "Steering clears the steering instruction.");
            if (learnedShooting && d > 435f && d < 458f)
                Assert.That(Get(tutorial, "SectionHint"), Is.Empty, "The shot acknowledgement is brief.");
        }
        Assert.That(Get(tutorial, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(learnedShooting, Is.True);
        Assert.That(Get(tutorial, "Deaths"), Is.EqualTo(0));
        Assert.That(Get(tutorial, "Hits"), Is.EqualTo(0), "Hit hazards: " + string.Join(", ", ((System.Collections.IEnumerable)tutorial.GetType().GetField("hitHazards", Flags).GetValue(tutorial)).Cast<object>().Select(h => h.GetType().GetField("Start").GetValue(h))));
        Call(Find("WorldShiftHud"), "Refresh");
        Capture("tutorial-ready");
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
        AdvanceDesert(desert, 1174f, 0f);
        Assert.That((float)Get(desert, "PursuitGap"), Is.LessThan(23f), "Staying in Night must collide with the wall between groups.");
        Call(desert, "Restart");
        desert.GetType().GetProperty("Stage").SetValue(desert, 5);
        Call(desert, "Retry");
        Assert.That(Get(desert, "Distance"), Is.EqualTo(2400f));
        Assert.That(Get(desert, "IsMerged"), Is.False);
        Assert.That(Get(desert, "World").ToString(), Is.EqualTo("Day"));
        Call(desert, "StepSimulation", .3f, -1f, false);
        Assert.That(Get(desert, "World").ToString(), Is.EqualTo("Day"), "Steering never changes worlds.");
        Assert.That(Call(desert, "TrySwitch"), Is.EqualTo(true));
        float dayClock = (float)Get(rocks.GetValue(0), "DaySeconds");
        float dayCar = (float)Get(traffic.GetValue(8), "Travel");
        Call(desert, "StepSimulation", .5f, 0f, false);
        Assert.That(Get(rocks.GetValue(0), "DaySeconds"), Is.EqualTo(dayClock), "Night freezes the finale too.");
        Assert.That(Get(traffic.GetValue(8), "Travel"), Is.EqualTo(dayCar));
        Call(desert, "TrySwitch");
        Call(desert, "StepSimulation", .3f, 0f, false);
        Assert.That((float)Get(rocks.GetValue(0), "DaySeconds"), Is.GreaterThan(dayClock));
        Assert.That((float)Get(traffic.GetValue(8), "Travel"), Is.GreaterThan(dayCar));
        Call(desert, "TogglePause");
        dayClock = (float)Get(rocks.GetValue(0), "DaySeconds");
        Call(desert, "StepSimulation", 1f, 1f, false);
        Assert.That(Get(rocks.GetValue(0), "DaySeconds"), Is.EqualTo(dayClock));
        Call(desert, "TogglePause");
        Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(c => c.GetType().Name == "WorldShiftSplitView" || c.GetType().Name == "WorldShiftRift"), Is.False);
        Call(Find("DesertShiftTrialView"), "Refresh");
        Capture("02-desert-single-world-finale");
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
        var driver = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetType().Name == "WorldShiftCity");
        if (driver != null) Call(driver, "UpdateCamera", true);
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
