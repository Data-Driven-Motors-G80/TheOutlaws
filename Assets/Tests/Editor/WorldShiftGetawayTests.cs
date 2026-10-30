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
        Assert.That((float)Get(tutorial, "Speed"), Is.EqualTo(18f), "The lesson keeps its original pace.");
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Running));
        object level = Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), "Tutorial");
        Flow("Next", level); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Get(city, "GetawayMode"), Is.True);
        Assert.That((float)Get(city, "Speed"), Is.EqualTo(30.8f).Within(.01f));
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
        Assert.That((float)Get(desert, "CurrentSpeed"), Is.EqualTo(26.4f).Within(.01f));
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
            string desired = leg < 2 ? "Echo" : "Day";
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
        Call(city, "TrySwitch"); Pose(city, 105f, 0f); Call(city, "TrySwitch");
        Assert.That(Get(city, "PursuitGap"), Is.EqualTo(cityGap));
        Assert.That(Run.HasShield, Is.False);
        Assert.That(Get(city, "Hits"), Is.EqualTo(0));

        Flow("StartGetaway", false); yield return null; yield return null;
        city = Find("WorldShiftCity"); city.enabled = false;
        Pose(city, 35f, -3.2f); Call(city, "StepSimulation", .5f, 0f, false);
        Pose(city, 1255f, 0f); Call(city, "TrySwitch");
        Assert.That(Get(city, "State").ToString(), Is.EqualTo("Lost"));
        Assert.That(Run.HasShield, Is.False, "A shield protects impacts, not missing road.");
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
            Pose(game, 35f, 3.2f);
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "MagnetActive"), Is.False, "Night powers cannot be taken in Day.");
            Pose(game, 35f, 3.2f); Call(game, "TrySwitch");
            Capture(environment == 0 ? "city-power-choices" : "desert-power-choices");
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "MagnetActive"), Is.True);
            Assert.That(Run.HasShield, Is.False);
            // Crossing the other choice cannot stack it onto this leg's selected power.
            Call(extras, "Collect", 42f, 46f, 0f, 0f);
            Assert.That(Get(extras, "BoostActive"), Is.False);
            Call(game, "TogglePause");
            float pausedTime = (float)Get(game, "Duration");
            Call(game, "StepSimulation", 8f, 0f, false);
            Assert.That(Get(game, "Duration"), Is.EqualTo(pausedTime));
            Assert.That(Get(extras, "MagnetActive"), Is.True);
            Call(game, "TogglePause");
            Pose(game, 60f, 3.2f);
            int beforeLoot = Run.Carried;
            Call(game, "StepSimulation", .4f, 0f, false);
            Assert.That(Run.Carried, Is.GreaterThan(beforeLoot), "Magnet must collect the opposite lane's Night bag.");
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
            Pose(game, 35f, 0f); Call(game, "TrySwitch");
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Get(extras, "BoostActive"), Is.True);
            Assert.That((float)Get(game, environment == 0 ? "Speed" : "CurrentSpeed"),
                Is.EqualTo(environment == 0 ? 36.96f : 31.68f).Within(.01f));
            Assert.That(Get(extras, "SteeringMultiplier"), Is.EqualTo(.8f));
            float gap = (float)Get(game, "PursuitGap"), distance = (float)Get(game, "Distance");
            Call(game, "StepSimulation", 1f, 0f, false);
            Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap + 4f), "Police must not inherit the player's boost.");
            Assert.That((float)Get(game, "Distance") - distance, Is.GreaterThan(environment == 0 ? 32f : 28f));
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
        Assert.That(prompt.text, Is.EqualTo("Drive through to collect"));
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
        Assert.That(prompt.text, Is.EqualTo("Space to switch."));
        Assert.That(driving.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "They're close!"), Is.False);
        Capture("hud-clean-road-cue");
        Call(game, "TogglePause"); Call(hud, "Refresh");
        Assert.That(driving.activeSelf, Is.False);
        Assert.That(prompt.text, Is.Empty);
        Capture("hud-clean-pause");
        Call(game, "TogglePause");
        Pose(game, (float)Get(game, "RunLength") - 90f, -3.2f);
        Call(hud, "Refresh");
        Assert.That(prompt.gameObject.activeInHierarchy, Is.False, "Exit choices replace the guidance prompt.");
        Capture("hud-clean-exit");

        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        game = Find("DesertShiftTrial"); game.enabled = false;
        hud = Find("WorldShiftGetaway");
        Pose(game, 1120f, 0f); Call(hud, "Refresh");
        prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        Assert.That(prompt.text, Is.EqualTo("Space to freeze a gap."));
        Capture("hud-clean-desert");
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
        Assert.That(prompt.text, Is.EqualTo("Drive through to collect"));
        Pose(game, 18f, 0f); Capture("loot-box-approach");
        Pose(game, 0f, 0f);
        Call(game, "StepSimulation", 1.2f, 0f, false); Call(hud, "Refresh");
        Assert.That(Run.Carried, Is.EqualTo(100));
        Assert.That(crate.gameObject.activeSelf, Is.False);
        Assert.That(prompt.text, Is.Not.EqualTo("Drive through to collect"));
        Assert.That(total.text, Is.EqualTo("$100"));
        Assert.That(total.rectTransform.localScale.x, Is.GreaterThan(1f));
        Capture("loot-total-feedback");
        Call(game, "TogglePause"); Call(hud, "Refresh");
        Vector3 pulse = total.rectTransform.localScale;
        Call(game, "StepSimulation", 5f, 0f, false); Call(hud, "Refresh");
        Assert.That(total.rectTransform.localScale, Is.EqualTo(pulse));
        Call(game, "TogglePause"); Call(game, "StepSimulation", .5f, 0f, false); Call(hud, "Refresh");
        Assert.That(total.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        game = Find("DesertShiftTrial"); game.enabled = false;
        hud = Find("WorldShiftGetaway"); Call(hud, "Refresh");
        prompt = (TMP_Text)hud.GetType().GetField("guidance", Flags).GetValue(hud);
        Assert.That(prompt.text, Is.Not.EqualTo("Drive through to collect"));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None), Is.Empty);
        Pose(game, 18f, 0f); Capture("loot-box-desert");
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
