using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WorldShiftGetawayTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private int bank, best;
    private readonly int[] upgradeLevels = new int[3];
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
    {
        bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank", 0); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best", 0);
        for (int i = 0; i < 3; i++)
        {
            string key = "WorldShift.Getaway.Upgrade." + (VehicleUpgrades.Part)i;
            upgradeLevels[i] = PlayerPrefs.GetInt(key, -1);
            PlayerPrefs.DeleteKey(key);
        }
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        for (int i = 0; i < 3; i++)
        {
            string key = "WorldShift.Getaway.Upgrade." + (VehicleUpgrades.Part)i;
            if (upgradeLevels[i] < 0) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetInt(key, upgradeLevels[i]);
        }
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best); PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator TutorialLootPauseBankAndDuplicateSettlement()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Capture("menu");
        // The home screen is now the level select; start the older tutorial-first escape directly.
        Flow("StartGetaway", true);
        yield return null; yield return null;
        var tutorial = Find("WorldShiftCity"); tutorial.enabled = false;
        Assert.That(Get(tutorial, "TutorialLevel"), Is.True);
        Assert.That(Get(tutorial, "GetawayMode"), Is.False);
        Assert.That((float)Get(tutorial, "Speed"), Is.EqualTo(18f), "The lesson keeps its original pace.");
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Running));
        object level = Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), "Tutorial");
        Flow("Next", level); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Get(city, "GetawayMode"), Is.True);
        Assert.That((float)Get(city, "Speed"), Is.EqualTo(25.2f).Within(.01f));
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
        // City continues automatically; the first cash-out side exit is at the end of Desert.
        Pose(city, (float)Get(city, "RunLength") - 5f, 0f);
        Call(city, "StepSimulation", .4f, 0f, false); yield return null; yield return null;
        var desert = Find("DesertShiftTrial"); desert.enabled = false;
        getawayHud = Find("WorldShiftGetaway");
        drivingHud = (GameObject)getawayHud.GetType().GetField("drivingHud", Flags).GetValue(getawayHud);
        Pose(desert, (float)Get(desert, "RunLength") - 5f, -3.2f);
        Call(desert, "StepSimulation", .4f, 0f, false); Call(getawayHud, "Refresh");
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
    public IEnumerator CompleteCityEscapeAutomaticallyKeepsItsCarriedLoot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;

        bool pictured = false;
        for (int i = 0; i < 3400 && Get(game, "State").ToString() == "Running"; i++)
        {
            float d = (float)Get(game, "Distance");
            bool freezeEncounter = d >= 170f && d < 310f || d >= 670f && d < 870f;
            string desired = freezeEncounter || d >= 1040f && d < 1180f ? "Echo" : "Day";
            if (!(bool)Get(game, "IsMerged") && Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            float target = d >= 360f && d < 700f || d > 1460f ? -3.2f : EscapeSafeLane(game, d);
            Call(game, "StepSimulation", .05f, Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            Assert.That(Get(game, "Hits"), Is.EqualTo(0), "Unexpected collision at " + d + " lateral=" + Get(game, "Lateral") + " target=" + target);
            Assert.That(Get(game, "State").ToString(), Is.Not.EqualTo("Lost"), "Escape failed at " + d);
            if (!pictured && d > 1530f) { Capture("full-run-exit"); pictured = true; }
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Running));
        Assert.That(Run.Carried, Is.GreaterThanOrEqualTo(300));
        Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank"), Is.EqualTo(bank));
        Assert.That((float)Get(game, "Duration"), Is.InRange(55f, 65f), "The short ordinary transition removes the old finishing straight.");
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
        Assert.That((float)Get(desert, "CurrentSpeed"), Is.EqualTo(21.6f).Within(.01f));
        Call(desert, "StepSimulation", 3.6f, 0f, false);
        Assert.That(Run.Carried, Is.GreaterThanOrEqualTo(750));
        Capture("desert-loot");
        Pose(desert, (float)Get(desert, "RunLength") - 5f, 3.3f);
        Call(desert, "StepSimulation", .3f, 0f, false);
        yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Ocean_WorldShift"));
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

    [UnityTest]
    public IEnumerator OptionalJumpsWorkInBothEnvironmentsAndGroundRouteCannotCollectAirLoot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 3; leg++)
        {
            var game = Find(leg == 1 ? "DesertShiftTrial" : "WorldShiftCity"); game.enabled = false;
            var getaway = Get(game, "Getaway");
            var extras = Get(getaway, "Extras"); var jump = Get(extras, "Jump");
            float start = (float)jump.GetType().GetField("Start").GetValue(jump);
            float takeoff = (float)jump.GetType().GetField("Takeoff").GetValue(jump);
            float landing = (float)jump.GetType().GetField("Landing").GetValue(jump);
            int before = Run.Carried;
            float lane = leg == 2 ? -3.2f : 3.2f;
            Pose(game, start - 8f, lane);
            Assert.That(Get(game, "IsMerged"), Is.False);
            string desired = "Day";
            if (Get(game, "World").ToString() != desired) Assert.That(Call(game, "TrySwitch"), Is.True);
            Advance(game, start - 1f, lane);
            Assert.That((float)Get(game, game.GetType().Name == "WorldShiftCity" ? "Speed" : "CurrentSpeed"),
                Is.EqualTo(leg == 1 ? 24f : 28f).Within(.01f), "Bonus jump and bypass keep the authored pace.");
            if (leg < 2) Capture(leg == 0 ? "city-bonus-approach" : "desert-bonus-approach");
            Advance(game, takeoff + 12f, lane);
            var player = (Transform)Get(game, "Player");
            object point = Call(Get(game, "Route"), "Sample", Get(game, "Distance"));
            float height = player.position.y - ((Vector3)Get(point, "Position")).y;
            Assert.That(Get(jump, "Airborne"), Is.EqualTo(leg != 2));
            Assert.That(height, leg == 2 ? Is.LessThan(.4f) : Is.GreaterThan(4f));
            if (leg == 0)
            {
                Call(game, "TogglePause"); float distance = (float)Get(game, "Distance");
                Call(game, "StepSimulation", 1f, -1f, false);
                Assert.That(Get(game, "Distance"), Is.EqualTo(distance));
                Assert.That(player.position.y, Is.EqualTo(((Vector3)Get(point, "Position")).y + height));
                Call(game, "TogglePause");
            }
            Capture(leg == 2 ? "ground-bypass" : leg == 0 ? "city-bonus-air" : "desert-bonus-air");
            Advance(game, landing + 18f, lane);
            Assert.That(Get(jump, "Completed"), Is.EqualTo(leg != 2));
            int reward = (int)Math.Round((leg == 2 ? 200 : 600) * Run.LootMultiplier);
            Assert.That(Run.Carried - before, Is.EqualTo(reward), "A ground bypass must only award ground bags.");
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            if (leg < 2) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator ShieldPickupCarriesAcrossScenesBlocksOneHitAndDoesNotSaveAFall()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Pose(city, 35f, -3.2f); Call(city, "StepSimulation", .5f, 0f, false);
        Assert.That(Run.HasShield, Is.True);
        Capture("shield-on");
        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        var desert = Find("DesertShiftTrial"); desert.enabled = false;
        Assert.That(Run.HasShield, Is.True, "An unused shield travels with the haul.");
        float gap = (float)Get(desert, "PursuitGap");
        Call(desert, "Impact", "first-test-rock", "ROCK HIT");
        Assert.That(Run.HasShield, Is.False);
        Assert.That(Get(desert, "PursuitGap"), Is.EqualTo(gap));
        Call(desert, "Impact", "same-field-rock", "ROCK HIT");
        Assert.That(Get(desert, "PursuitGap"), Is.EqualTo(gap), "One overlap must not charge a second hit immediately.");
        Call(desert, "StepSimulation", 1.2f, 0f, false);
        float afterDriving = (float)Get(desert, "PursuitGap");
        Call(desert, "Impact", "second-test-rock", "ROCK HIT");
        Assert.That((float)Get(desert, "PursuitGap"), Is.EqualTo(afterDriving - 22.5f).Within(.02f));

        Flow("StartGetaway", false); yield return null; yield return null;
        city = Find("WorldShiftCity"); city.enabled = false;
        Pose(city, 35f, -3.2f); Call(city, "StepSimulation", .5f, 0f, false);
        Assert.That(Run.HasShield, Is.True);
        float cityGap = (float)Get(city, "PursuitGap");
        Call(city, "TrySwitch"); Pose(city, 1262f, -3.2f); Call(city, "TrySwitch");
        Assert.That(Get(city, "PursuitGap"), Is.EqualTo(cityGap));
        Assert.That(Run.HasShield, Is.False);
        Assert.That(Get(city, "Hits"), Is.EqualTo(0));

        Flow("StartGetaway", false); yield return null; yield return null;
        city = Find("WorldShiftCity"); city.enabled = false;
        Pose(city, 35f, -3.2f); Call(city, "StepSimulation", .5f, 0f, false);
        Pose(city, 1255f, 0f); Call(city, "TrySwitch");
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Running"), "Switching never removes the road in Escape.");
        Assert.That(Run.HasShield, Is.True);
        Call(city, "Fail", "MISSED RELAY LANDING");
        Assert.That(Run.HasShield, Is.False, "A shield cannot protect a failed run.");
    }

    [UnityTest]
    public IEnumerator PowerChoicesRespectWorldPauseExpiryAndPursuitInBothEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var getaway = Find("WorldShiftGetaway"); var extras = Get(getaway, "Extras");
            var markers = new[] { "shieldPickup", "magnetPickup", "boostPickup" }
                .Select(name => (Transform)extras.GetType().GetField(name, Flags).GetValue(extras)).ToArray();
            for (float d = 0f; d < (float)Get(extras, "BoostDistance") + 10f; d += 10f)
            {
                Pose(game, d, 0f); Call(extras, "Present", Get(game, "World"));
                Assert.That(markers.Count(marker => marker.gameObject.activeSelf), Is.LessThanOrEqualTo(1),
                    "Power-ups must have separate approach windows at " + d);
            }
            Pose(game, 142f, 3.2f); Call(game, "TrySwitch");
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "MagnetActive"), Is.False, "No power-ups can be taken in Night.");
            Pose(game, 142f, 3.2f); Call(game, "TrySwitch");
            Capture(environment == 0 ? "city-power-choices" : "desert-power-choices");
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "MagnetActive"), Is.True);
            Assert.That(Run.HasShield, Is.False);
            // The opening location no longer contains a boost.
            Call(extras, "Collect", 42f, 46f, 0f, 0f);
            Assert.That(Get(extras, "BoostActive"), Is.False);
            Call(game, "TogglePause");
            float pausedTime = (float)Get(game, "Duration");
            Call(game, "StepSimulation", 8f, 0f, false);
            Assert.That(Get(game, "Duration"), Is.EqualTo(pausedTime));
            Assert.That(Get(extras, "MagnetActive"), Is.True);
            Call(game, "TogglePause");
            Pose(game, 170f, 3.2f);
            int beforeLoot = Run.Carried;
            Call(game, "StepSimulation", .4f, 0f, false);
            Assert.That(Run.Carried, Is.GreaterThan(beforeLoot), "Magnet must collect the opposite lane's Day bag.");
            Capture(environment == 0 ? "city-magnet" : "desert-magnet");
            int beforeAir = Run.Carried;
            object jump = Get(extras, "Jump");
            float airBag = (float)jump.GetType().GetField("Takeoff").GetValue(jump) + 24f;
            Call(getaway, "Collect", airBag - 1f, airBag + 1f, -3.2f, -3.2f);
            Assert.That(Run.Carried, Is.EqualTo(beforeAir), "Magnet cannot pull elevated loot from below the ramp.");
            Pose(game, 44f, 0f);
            Call(game, "StepSimulation", 6f, 0f, false);
            Assert.That(Get(extras, "MagnetActive"), Is.False);

            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            getaway = Find("WorldShiftGetaway"); extras = Get(getaway, "Extras");
            Pose(game, (float)Get(extras, "BoostDistance") - 8f, -2.8f);
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "BoostActive"), Is.True);
            Assert.That((float)Get(game, environment == 0 ? "Speed" : "CurrentSpeed"),
                Is.EqualTo(environment == 0 ? 30.24f : 25.92f).Within(.01f));
            Assert.That(Get(extras, "SteeringMultiplier"), Is.EqualTo(1f));
            float gap = (float)Get(game, "PursuitGap"), distance = (float)Get(game, "Distance");
            Call(game, "StepSimulation", 1f, 0f, false);
            Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap + 4f), "Police must not inherit the player's boost.");
            Assert.That((float)Get(game, "Distance") - distance, Is.GreaterThan(environment == 0 ? 29f : 25f));
            Capture(environment == 0 ? "city-boost" : "desert-boost");
            Call(game, "TogglePause"); Call(game, "StepSimulation", 10f, 0f, false);
            Assert.That(Get(extras, "BoostActive"), Is.True);
            Call(game, "TogglePause");
            Call(game, "StepSimulation", 3.2f, 0f, false);
            Assert.That(Get(extras, "BoostActive"), Is.False);
            Assert.That(Get(extras, "SteeringMultiplier"), Is.EqualTo(1f));
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            Call(game, "Fail", "CAUGHT");
            Assert.That(Get(extras, "SpeedMultiplier"), Is.EqualTo(1f));
        }
    }

    [UnityTest]
    public IEnumerator EscapeHudShowsOneReadablePromptAwayFromTheCarAndMirror()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        var hud = Find("WorldShiftGetaway");
        var prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        var driving = (GameObject)hud.GetType().GetField("drivingHud", Flags).GetValue(hud);
        Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Collect cash. Bank it at a marked cash-out exit."));
        Assert.That(driving.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("A / D to steer")), Is.False);
        Assert.That(driving.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Chase goal"), Is.False);
        Assert.That(prompt.fontSizeMin, Is.GreaterThanOrEqualTo(24f));
        Assert.That(prompt.fontSizeMax, Is.EqualTo(26f));
        var panel = (RectTransform)prompt.transform.parent;
        var mirror = (GameObject)hud.GetType().GetField("mirrorPanel", Flags).GetValue(hud);
        Assert.That(panel.anchorMin.y, Is.GreaterThanOrEqualTo(.75f));
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(driving.transform, panel);
        float width = ((RectTransform)driving.transform).rect.width;
        Assert.That(bounds.max.x, Is.LessThan(width * (((RectTransform)mirror.transform).anchorMin.x - .5f)));
        prompt.ForceMeshUpdate();
        Assert.That(prompt.isTextOverflowing, Is.False);
        Assert.That(prompt.textInfo.lineCount, Is.EqualTo(1));
        Capture("hud-clean-opening");

        // A shield acknowledgement and the opening objective cannot stack with a road instruction.
        Call(Get(hud, "Extras"), "Collect", 42f, 46f, -3.2f, -3.2f);
        Pose(game, 60f, 0f);
        Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Collect cash. Bank it at a marked cash-out exit."), "A shared obstacle cannot be removed by switching worlds.");
        Assert.That(driving.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "They're close!"), Is.False);
        Capture("hud-clean-road-cue");
        Pose(game, 220f, 0f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Space: clear the Day barrier."));
        prompt.ForceMeshUpdate();
        Assert.That(prompt.isTextOverflowing, Is.False);
        Capture("hud-freeze-timing");
        Call(game, "TogglePause"); Call(hud, "Refresh");
        Assert.That(driving.activeSelf, Is.False);
        Assert.That(prompt.text, Is.Empty);
        Capture("hud-clean-pause");
        Call(game, "TogglePause");
        Pose(game, (float)Get(game, "RunLength") - 90f, -3.2f);
        Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Desert ahead."), "Ordinary environment changes need no route choice.");
        Capture("hud-clean-exit");

        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        game = Find("DesertShiftTrial"); game.enabled = false;
        hud = Find("WorldShiftGetaway");
        Pose(game, 1120f, 0f); Call(hud, "Refresh");
        prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        Assert.That(prompt.text, Is.Empty);
        Capture("hud-clean-desert");
    }

    [UnityTest]
    public IEnumerator GameplayPromptsStayStableAndCriticalActionsPreemptReminders()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        var hud = Find("WorldShiftGetaway"); var combat = Get(game, "Combat");
        var prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        var panel = (RectTransform)prompt.transform.parent;
        void TimeAt(float time) => game.GetType().GetProperty("Duration", Flags).SetValue(game, time);
        Pose(game, 1230f, -3.2f); TimeAt(10f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.Empty, "Brief shooting opportunities do not flash immediately.");
        TimeAt(10.2f); Call(hud, "Refresh"); Assert.That(prompt.text, Is.Empty);
        TimeAt(10.5f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("F / click to shoot the block."));
        float width = panel.sizeDelta.x;
        for (int i = 0; i < 4; i++)
        {
            Pose(game, 1230f, i % 2 == 0 ? 0f : -3.2f);
            TimeAt(10.6f + i * .1f); Call(hud, "Refresh");
            Assert.That(prompt.text, Is.EqualTo("F / click to shoot the block."), "Small aim changes must not flicker the caption.");
            Assert.That(panel.sizeDelta.x, Is.EqualTo(width));
        }
        Pose(game, 1230f, -3.2f); TimeAt(11f); Call(combat, "TryShoot"); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.Empty, "A completed shot clears its hint immediately.");
        Pose(game, 960f, 0f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Drive up the ramp to jump."), "Jump instructions preempt the quiet interval.");
        Pose(game, 1030f, 0f); Call(game, "StepSimulation", .5f, 0f, false); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Space to land in Nightshift."));
        Call(game, "TrySwitch");
        TimeAt((float)Get(game, "Duration") + 12.1f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Stay in Nightshift. Day has no road yet."), "An optional reminder cannot cover a landing instruction.");
        Assert.That(panel.sizeDelta.x, Is.EqualTo(width));
        Pose(game, 1229f, 0f); Call(game, "StepSimulation", .025f, 0f, false); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.Empty, "A quiet interval separates action instructions from informational tips.");
        float now = (float)Get(game, "Duration");
        for (int i = 0; i < 3; i++)
        {
            TimeAt(now + 1.1f + i); Call(hud, "Refresh");
            Assert.That(prompt.text, Is.EqualTo("Fuel still drains. Return to Day for supplies."), "The Nightshift reminder is one stable message.");
            Assert.That(panel.sizeDelta.x, Is.EqualTo(width));
        }
        Capture("quiet-nightshift-reminder");
        Call(game, "TogglePause"); Call(hud, "Refresh"); Assert.That(prompt.text, Is.Empty);
        Call(game, "TogglePause"); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Fuel still drains. Return to Day for supplies."));
        Call(game, "TrySwitch"); Pose(game, 60f, 0f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.Empty, "Old objectives and tips are never replayed after a critical action.");
        Call(Get(hud, "Extras"), "Collect", 148f, 152f, 3.2f, 3.2f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.Empty, "Power-up acknowledgements do not compete with road guidance.");
        Pose(game, (float)Get(game, "RunLength") - 90f, 0f); Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Desert ahead."), "A normal environment ending announces the next course without a forced choice.");
    }

    [UnityTest]
    public IEnumerator PrimitiveLootBoxesTeachCollectionAndGiveSilentPausedFeedback()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false;
        var hud = Find("WorldShiftGetaway");
        var prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        var total = (TMP_Text)hud.GetType().GetField("haul", Flags).GetValue(hud);
        var crate = hud.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Loot Day 35");
        var symbol = crate.GetComponentInChildren<TMP_Text>(true);
        Assert.That(symbol.text, Is.EqualTo("$"));
        Assert.That(symbol.transform.localPosition.z, Is.LessThan(0f), "The dollar belongs on the box face, not floating above it.");
        Assert.That(symbol.transform.localPosition.y, Is.LessThan(1f));
        Assert.That(crate.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True, "A collectible must not act as a road obstacle.");
        Assert.That(crate.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<TMP_Text>() == null)
            .All(r => !r.sharedMaterial.IsKeywordEnabled("_EMISSION")), Is.True);
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None), Is.Empty);
        Call(hud, "Refresh");
        Assert.That(prompt.text, Is.EqualTo("Collect cash. Bank it at a marked cash-out exit."));
        Pose(game, 18f, 0f); Capture("loot-box-approach");
        Pose(game, 0f, 0f);
        Call(game, "StepSimulation", 1.4f, 0f, false); Call(hud, "Refresh");
        Assert.That(Run.Carried, Is.EqualTo(100));
        Assert.That(crate.gameObject.activeSelf, Is.False);
        Assert.That(prompt.text, Is.EqualTo("Collect cash. Bank it at a marked cash-out exit."), "Collecting cash must not flash another message.");
        Assert.That(total.text, Is.EqualTo("<size=14>Carrying</size>\n$100"));
        Assert.That(total.rectTransform.localScale.x, Is.GreaterThan(1f));
        Capture("loot-total-feedback");
        Call(game, "TogglePause"); Call(hud, "Refresh");
        Vector3 pulse = total.rectTransform.localScale;
        Call(game, "StepSimulation", 5f, 0f, false); Call(hud, "Refresh");
        Assert.That(total.rectTransform.localScale, Is.EqualTo(pulse));
        Call(game, "TogglePause"); Pose(game, 80f, 0f); Call(game, "StepSimulation", .5f, 0f, false); Call(hud, "Refresh");
        Assert.That(total.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        game = Find("DesertShiftTrial"); game.enabled = false;
        hud = Find("WorldShiftGetaway"); Call(hud, "Refresh");
        prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        Assert.That(prompt.text, Is.Not.EqualTo("Drive through to collect"));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None), Is.Empty);
        Pose(game, 18f, 0f); Capture("loot-box-desert");
    }

    [UnityTest]
    public IEnumerator PowerupsUsePlainLabelsAndRampsExplainAutomaticJumping()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        for (int environment = 0; environment < 2; environment++)
        {
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var hud = Find("WorldShiftGetaway"); var extras = Get(hud, "Extras");
            foreach (string field in new[] { "shieldPickup", "magnetPickup", "boostPickup" })
            {
                var marker = (Transform)extras.GetType().GetField(field, Flags).GetValue(extras);
                Assert.That(marker.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Pickup block"), Is.True);
                Assert.That(marker.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("$")), Is.False);
                Assert.That(marker.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
            }
            var lootItems = ((System.Collections.IEnumerable)hud.GetType().GetField("loot", Flags).GetValue(hud)).Cast<object>().ToArray();
            foreach (var item in lootItems)
            {
                float at = (float)item.GetType().GetField("At").GetValue(item);
                float x = (float)item.GetType().GetField("X").GetValue(item);
                if (environment == 1)
                {
                    for (int i = 0; i < 10; i++)
                        if (Mathf.Abs(at - (float)Call(game, "FuelDistance", i)) < 1f)
                            Assert.That(Mathf.Abs(x), Is.GreaterThan(2.5f), "Cash boxes must not hide fuel cans.");
                }
                else
                {
                    var fuel = ((System.Collections.IEnumerable)Get(game, "Pickups")).Cast<object>();
                    foreach (var pickup in fuel)
                    {
                        float d = (float)pickup.GetType().GetField("Distance").GetValue(pickup);
                        if (Mathf.Abs(at - d) > 1f) continue;
                        float lane = (int)pickup.GetType().GetField("Lane").GetValue(pickup) * 10f / 3f;
                        Assert.That(Mathf.Abs(x - lane), Is.GreaterThan(2.5f));
                    }
                }
            }
            Pose(game, 18f, 0f); Capture(environment == 0 ? "distinct-pickups-day" : "distinct-pickups-desert");
            Call(game, "TrySwitch"); Capture(environment == 0 ? "distinct-pickups-night" : "distinct-pickups-desert-night");
            Call(game, "TrySwitch");
            float rampApproach = environment == 0 ? 960f : 1930f;
            float takeoff = environment == 0 ? 1040f : 2004f;
            Pose(game, rampApproach, 0f); Call(hud, "Refresh");
            var prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
            Assert.That(prompt.text, Is.EqualTo("Drive up the ramp to jump."));
            Capture(environment == 0 ? "auto-jump-approach" : "auto-jump-desert-approach");
            Advance(game, takeoff + 5f, 0f); Call(hud, "Refresh");
            Assert.That(Get(game, environment == 0 ? "IsRelayAirborne" : "IsAirborne"), Is.True,
                "Driving onto the ramp must jump without pressing Space.");
            Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));
            Assert.That(prompt.text, Is.EqualTo("Space to land in Nightshift."), "The jump requires an airborne switch to the visible landing.");
            Capture(environment == 0 ? "auto-jump-air" : "auto-jump-desert-air");
            if (environment == 0) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator FuelPercentageAndAmmoShareTheSameStripInBothModesAndEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        for (int mode = 0; mode < 4; mode++)
        {
            if (mode == 0) Flow("StartGetaway", false);
            else if (mode == 1) Flow("FinishGetawayLeg", true);
            else Flow("Select", Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), mode == 2 ? "City" : "Desert"), false);
            yield return null; yield return null;
            bool city = mode == 0 || mode == 2;
            var game = Find(city ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var hud = Find(mode < 2 ? "WorldShiftGetaway" : city ? "WorldShiftHud" : "DesertShiftTrialView");
            var strip = hud.GetType().GetField("statusStrip", Flags).GetValue(hud);
            var fuel = (FuelState)game.GetType().GetField("fuel", Flags).GetValue(game);
            fuel.Reset(100f); Call(hud, "Refresh");
            var fuelText = (TMP_Text)Get(strip, "Fuel");
            Assert.That(fuelText.text, Is.EqualTo("Fuel 100%"));
            Assert.That(((TMP_Text)Get(strip, "World")).gameObject.activeInHierarchy, Is.False);
            Assert.That(((GameObject)Get(strip, "MirrorPanel")).activeInHierarchy, Is.False);
            Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Speed km/h"), Is.False, "Driving should not add another numeric counter.");
            Assert.That(fuelText.rectTransform.anchorMin.x, Is.EqualTo(.34f));
            Assert.That(((RectTransform)hud.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Ammo")).anchorMin.x, Is.EqualTo(.67f));
            Assert.That(((RectTransform)fuelText.transform.parent).anchorMin.y, Is.GreaterThan(.85f));
            Assert.That(hud.GetComponentsInChildren<RectTransform>(true).Any(r => new[] { "Fuel track", "RouteProgress", "Exit distance", "Progress" }.Contains(r.name)), Is.False);
            fuel.Reset(12.25f); Call(hud, "Refresh");
            Assert.That(fuelText.text, Is.EqualTo("Fuel 13%"), "Fuel percentage must round consistently in every route.");
            Assert.That(fuelText.color.r, Is.GreaterThan(fuelText.color.g));
            Capture("status-strip-" + (mode < 2 ? "escape-" : "practice-") + (city ? "city" : "desert"));
            Call(game, "TogglePause"); Call(hud, "Refresh");
            float before = fuel.CurrentFuel;
            Call(game, "StepSimulation", 5f, 0f, false);
            Assert.That(fuel.CurrentFuel, Is.EqualTo(before));
            Call(game, "TogglePause"); fuel.Reset(100f);
            if (mode == 0)
            {
                var extras = Get(hud, "Extras");
                float boostAt = (float)Get(extras, "BoostDistance");
                Call(extras, "Collect", boostAt - 2f, boostAt + 2f, -2.8f, -2.8f); Call(hud, "Refresh");
                Assert.That((float)Get(game, "Speed"), Is.EqualTo(30.24f).Within(.01f), "Removing the speed label must preserve boost behavior.");
            }
        }
    }

    private static float EscapeSafeLane(object game, float d)
    {
        // Hold a clear lane through each obstacle cluster, then return for shared fuel.
        float current = Mathf.Round((float)Get(game, "Lateral") / 3.2f) * 3.2f;
        if (Call(game, "FindCollision", Get(game, "World"), d, d + 35f, current, current, false) == null)
        {
            bool cluster = new[] { 0f, -3.2f, 3.2f }.Any(x => Call(game, "FindCollision", Get(game, "World"), d - 12f, d + 40f, x, x, false) != null);
            if (cluster) return current;
        }
        foreach (float x in new[] { 0f, 3.2f, -3.2f })
            if (Call(game, "FindCollision", Get(game, "World"), d, d + 35f, x, x, false) == null) return x;
        foreach (float x in new[] { 0f, -3.2f, 3.2f })
            if (Call(game, "FindCollision", Get(game, "World"), d, d + 28f, x, x, false) == null) return x;
        return (float)Get(game, "Lateral");
    }

    private static void Advance(object game, float target, float lane)
    {
        for (int i = 0; i < 1200 && (float)Get(game, "Distance") < target; i++)
        {
            Call(game, "StepSimulation", .025f, Mathf.Clamp((lane - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        }
        Assert.That((float)Get(game, "Distance"), Is.GreaterThanOrEqualTo(target));
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
