using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WorldShiftEnvironmentFlowTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private int bank, best;
    private static Type Flow => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift.WorldShiftFlow")).First(t => t != null);
    private static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
    private static object Field(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Navigate(string name, params object[] args) => Flow.GetMethod(name).Invoke(null, args);
    private static GetawayRunState Run => (GetawayRunState)Flow.GetProperty("Escape").GetValue(null);
    private static MonoBehaviour Game => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .Single(c => c.GetType().Name == "WorldShiftCity" || c.GetType().Name == "DesertShiftTrial");
    private static void Pose(MonoBehaviour game, float d, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, d);
        Call(Field(game, "driver"), "RestoreExternalPose", d, x);
        if (game.GetType().Name == "WorldShiftCity") game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
    }
    private static void Capture(string name) => typeof(WorldShiftGetawayTests).GetMethod("Capture", Flags).Invoke(null, new object[] { name });
    [SetUp] public void Save() { bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank"); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best"); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best); PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator FiveEnvironmentsAdvanceAutomaticallyAndCarrySuppliesIntoAHarderLap()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        Game.enabled = false; Run.Collect(500); Run.CollectShield(); Run.SpendShot();
        float initialMotion = (float)Field(((IEnumerable)Get(Game, "Hazards")).Cast<object>().First(), "MotionSpeed");
        string[] scenes = { "City", "Desert", "Ocean", "Snow", "Mountain", "City" };
        for (int leg = 0; leg < scenes.Length; leg++)
        {
            var game = Game; game.enabled = false;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scenes[leg] + "_WorldShift"));
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            Assert.That(Run.Leg, Is.EqualTo(leg)); Assert.That(Run.Carried, Is.EqualTo(500));
            Assert.That(Run.Ammo, Is.EqualTo(2)); Assert.That(Run.HasShield, Is.True);
            var hud = Get(game, "Getaway");
            Assert.That(Get(hud, "CashOutAvailable"), Is.EqualTo(leg % 2 == 1));
            float end = Convert.ToSingle(Field(hud, "environmentEnd"));
            float tail = (float)Get(game, "RunLength") - end;
            Assert.That(tail, Is.EqualTo(leg % 2 == 1 ? 440f : 60f));
            if (leg % 2 == 0)
            {
                Pose(game, end, 0f);
                Assert.That(Get(hud, "InExit"), Is.True, "Announce the next environment at the short transition.");
                Assert.That(tail / (float)Get(game, "Speed"), Is.LessThan(3f));
            }
            if (leg % 2 == 0)
            {
                Navigate("FinishGetawayLeg", false);
                Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Running), "Ordinary environment endings cannot bank a run.");
                Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Exit branch gate"), Is.False);
            }
            if (leg == 5)
            {
                Assert.That((float)Field(((IEnumerable)Get(game, "Hazards")).Cast<object>().First(), "MotionSpeed"), Is.GreaterThan(initialMotion));
                Assert.That(Run.ExtraPursuitSpeed, Is.GreaterThan(0f));
                break;
            }
            // No right-lane choice: staying in the centre continues even at a cash-out opportunity.
            Pose(game, (float)Get(game, "RunLength") - 5f, 0f);
            Call(game, "StepSimulation", .4f, 0f, false);
            yield return null; yield return null;
        }
    }

    [UnityTest]
    public IEnumerator CashingOutIsAnExplicitSideExitAndOffersUpgradesBeforeANewRun()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        Navigate("FinishGetawayLeg", true); yield return null; yield return null;
        var game = Game; game.enabled = false; Run.Collect(500); int deposit = Run.Carried;
        var hud = Get(game, "Getaway"); var forks = Get(hud, "Routing");
        Pose(game, (float)Get(forks, "ExitChoiceAt") - 10f, -3.2f);
        for (int i = 0; i < 200 && Get(game, "State").ToString() == "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));
        Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank"), Is.EqualTo(bank + deposit));
        Call(hud, "Refresh"); Capture("cash-out-upgrade-car");
        var upgrade = game.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Upgrade car");
        upgrade.onClick.Invoke(); yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WorldShift_Menu"));
        Assert.That(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Any(b => b.name == "Start next run"), Is.True);
    }

    [UnityTest]
    public IEnumerator EveryEnvironmentHasAnIntentionalDayBarrierAndDistinctRoadCharacter()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; Run.CollectAmmo();
            bool desert = leg == 1;
            var hud = Get(game, "Getaway"); var route = Get(game, "Route");
            object gate = desert ? ((IEnumerable)Get(game, "ActiveGates")).Cast<object>().First(g => (bool)Field(g, "SwitchOnly"))
                : ((IEnumerable)Get(game, "Hazards")).Cast<object>().First(h => Field(h, "Kind").ToString() == "SwitchWall");
            var switchWalls = desert ? ((IEnumerable)Get(game, "ActiveGates")).Cast<object>().Where(g => (bool)Field(g, "SwitchOnly")).ToArray()
                : ((IEnumerable)Get(game, "Hazards")).Cast<object>().Where(h => Field(h, "Kind").ToString() == "SwitchWall").ToArray();
            Assert.That(switchWalls.Length, Is.EqualTo(3), "Each environment has three intentional switch encounters.");
            float at = Convert.ToSingle(Field(gate, "Start"));
            Pose(game, at - 80f, 3.2f); Call(game, "StepSimulation", .001f, 0f, false); Call(hud, "Refresh");
            Assert.That(Get(hud, "SwitchWallHint"), Is.EqualTo("Space: clear the Day barrier."));
            var day = Get(game, "World");
            if (!desert)
                Assert.That(Call(game, "FindCollision", day, at, at + .1f, 3.2f, 3.2f, false), Is.EqualTo(gate));
            var wall = game.GetComponentsInChildren<Transform>(true).Where(t => t.name == "X obstacle")
                .OrderBy(t => Vector3.Distance(t.transform.position, (Vector3)Get(Call(route, "Sample", at), "Position"))).First();
            Assert.That(wall.gameObject.activeInHierarchy, Is.True);
            Assert.That(game.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.name == "Switch wall instruction"), Is.False);
            if (leg == 0)
            {
                Pose(game, at - 35f, 3.2f); Call(game, "StepSimulation", .001f, 0f, false);
                Capture("day-switch-wall");
            }
            Assert.That(Call(game, "TrySwitch"), Is.True);
            var night = Get(game, "World");
            Assert.That(wall.gameObject.activeInHierarchy, Is.False, "The Day-only X wall vanishes in Nightshift.");
            Assert.That(Call(route, "HasSurface", at, night), Is.True, "The wall vanishes; the road remains.");
            if (!desert)
                Assert.That(Call(game, "FindCollision", night, at, at + .1f, 3.2f, 3.2f, false), Is.Null);
            else
            {
                Pose(game, at, 3.2f); float gap = (float)Get(game, "PursuitGap");
                Call(game, "CheckGates", at, 3.2f);
                Assert.That(Get(game, "PursuitGap"), Is.EqualTo(gap));
            }
            Assert.That(Run.Ammo, Is.EqualTo(3), "Switching clears the wall without spending ammo.");
            // Clear the field in Night, then return to Day for the supply stretch.
            Pose(game, at - 20f, -3.2f); Call(game, "TrySwitch");
            var combat = Get(game, "Combat");
            Assert.That(Call(combat, "TryShoot"), Is.True);
            Assert.That(Call(combat, "IsDestroyed", gate), Is.True, "Ammo remains an alternative to shifting.");

            float courseEnd = Convert.ToSingle(Field(hud, "environmentEnd")), maxTurn = 0f;
            for (float d = 80f; d < courseEnd - 20f; d += 20f)
                maxTurn = Mathf.Max(maxTurn, Vector3.Angle(PlanarForward(route, d), PlanarForward(route, d + 20f)));
            float[] minimumTurns = { 6f, 4f, 8f, 5f, 12f };
            Assert.That(maxTurn, Is.GreaterThan(minimumTurns[leg]), "The actual road should contain a pronounced bend.");
            var routing = Get(hud, "Routing");
            Assert.That(Vector3.Angle(PlanarForward(route, (float)Get(routing, "ForkStart") + 20f),
                PlanarForward(route, (float)Get(routing, "Rejoin") - 20f)), Is.LessThan(.1f), "Forks stay straight.");
            var jump = leg == 1 ? ((Array)Flow.Assembly.GetType("Outlaws.WorldShift.DesertShiftLevel").GetField("Relays").GetValue(null)).GetValue(0)
                : ((Array)Get(game, "RelayLayout")).GetValue(0);
            float ramp = Convert.ToSingle(Field(jump, "Ramp")), takeoff = Convert.ToSingle(Field(jump, "Takeoff"));
            Assert.That(Vector3.Angle(PlanarForward(route, ramp - 40f), PlanarForward(route, takeoff - 1f)),
                Is.LessThan(.1f), "Ramp approaches stay straight despite harder bends.");

            // Terrain and handling identify the environment, without changing the mode's base speed.
            var low = Call(route, "Sample", 100f); var high = Call(route, "Sample", 400f);
            float rise = ((Vector3)Get(high, "Position")).y - ((Vector3)Get(low, "Position")).y;
            if (leg == 0) Assert.That(Mathf.Abs(rise), Is.LessThan(.05f), "City is flat outside its ramp.");
            if (leg == 3)
            {
                Assert.That(rise, Is.LessThan(-5f), "Snow has a gradual descent.");
                Assert.That(Get(Field(game, "driver"), "EnvironmentSteeringGrip"), Is.EqualTo(.85f));
                Call(game, "TrySwitch");
                Assert.That(Get(Field(game, "driver"), "EnvironmentSteeringGrip"), Is.EqualTo(.85f), "Night freezes hazards, not the car's handling.");
            }
            if (leg == 4)
            {
                Assert.That(rise, Is.GreaterThan(15f), "Mountain climbs before the switchbacks.");
                var descent = Call(route, "Sample", 1450f);
                Assert.That(((Vector3)Get(descent, "Forward")).y, Is.LessThan(-.06f));
                Assert.That(((IEnumerable)Get(game, "Hazards")).Cast<object>().Count(h => Field(h, "Kind").ToString() == "Oncoming"), Is.GreaterThanOrEqualTo(3));
            }
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator LaterCoursesOfferDayRewardsBetweenNightEncounters()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        var profile = Flow.Assembly.GetType("Outlaws.WorldShift.WorldShiftEscapeProfile");
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; var hud = Get(game, "Getaway");
            var windows = (Vector2[])profile.GetMethod("PacingRewards").Invoke(null, new[] { Get(hud, "Environment") });
            Assert.That(windows.Length, Is.GreaterThan(0));
            var boxes = ((IEnumerable)Field(hud, "loot")).Cast<object>().ToArray();
            foreach (var window in windows)
                Assert.That(boxes.Any(b => Convert.ToSingle(Field(b, "At")) == window.x && Convert.ToSingle(Field(b, "X")) == window.y), Is.True);
            Call(game, "TrySwitch"); int before = Run.Carried;
            foreach (var window in windows) Call(hud, "Collect", window.x - 2f, window.x + 2f, window.y, window.y);
            Assert.That(Run.Carried, Is.EqualTo(before), "Staying in Nightshift misses the return windows.");
            Call(game, "TrySwitch");
            foreach (var window in windows) Call(hud, "Collect", window.x - 2f, window.x + 2f, window.y, window.y);
            Assert.That(Run.Carried, Is.GreaterThanOrEqualTo(before + 100 * windows.Length), "Returning to Day makes the encounter rewards available.");
            if (leg == 0)
                Assert.That(windows.Select(w => w.x), Is.EqualTo(new[] { 760f, 930f, 1300f }), "Preserve the approved City pacing.");
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator SuppliesHaveSeparatePickupWindowsAcrossAllEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; var hud = Get(game, "Getaway"); var extras = Get(hud, "Extras");
            var items = new List<(string Name, float At, float X, bool Cash)>();
            foreach (var ammo in ((IEnumerable)Field(Get(game, "Combat"), "pickups")).Cast<object>())
                items.Add(("Ammo", Convert.ToSingle(Field(ammo, "At")), Convert.ToSingle(Field(ammo, "X")), false));
            items.Add(("Shield", 44f, -3.2f, false)); items.Add(("Magnet", 150f, 3.2f, false));
            items.Add(("Boost", (float)Get(extras, "BoostDistance"), -2.8f, false));
            if (leg == 1)
                for (int i = 0; i < (int)Get(game, "FuelCount"); i++) items.Add(("Fuel", (float)Call(game, "FuelDistance", i), 0f, false));
            else foreach (var fuel in ((IEnumerable)Get(game, "Pickups")).Cast<object>())
                items.Add(("Fuel", Convert.ToSingle(Field(fuel, "Distance")), 0f, false));
            foreach (var cash in ((IEnumerable)Field(hud, "loot")).Cast<object>())
                if (!(bool)Field(cash, "Bonus")) items.Add(("Cash", Convert.ToSingle(Field(cash, "At")), Convert.ToSingle(Field(cash, "X")), true));
            for (int a = 0; a < items.Count; a++) for (int b = a + 1; b < items.Count; b++)
            {
                if (items[a].Cash && items[b].Cash || Mathf.Abs(items[a].X - items[b].X) >= 2.4f) continue;
                Assert.That(Mathf.Abs(items[a].At - items[b].At), Is.GreaterThanOrEqualTo(20f),
                    "Supplies must not overlap or share a pickup window: leg " + leg + " " + items[a] + " / " + items[b]);
            }
            if (leg == 0)
            {
                Pose(game, (float)Get(Get(hud, "Routing"), "ForkStart") + 50f, -2.8f);
                Call(game, "StepSimulation", .001f, 0f, false); Capture("separated-fork-supplies");
            }
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator CityLinksNightEncountersToDayRewardsAndRequiresFinalLaneChanges()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        var game = Game; game.enabled = false; var hud = Get(game, "Getaway");
        var hazards = ((IEnumerable)Get(game, "Hazards")).Cast<object>().ToArray();
        var first = hazards.Single(h => Convert.ToSingle(Field(h, "Start")) == 125f);
        Pose(game, 95f, 3.2f); Call(game, "StepSimulation", .2f, 0f, false);
        Call(game, "TrySwitch"); float frozen = (float)Get(first, "MotionTravel");
        for (int i = 0; i < 150 && (float)Get(game, "Distance") < 206f; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(first, "MotionTravel"), Is.EqualTo(frozen));
        Assert.That(Get(game, "Hits"), Is.EqualTo(0));
        int before = Run.Carried; Call(game, "TrySwitch");
        for (int i = 0; i < 80 && (float)Get(game, "Distance") < 253f; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Run.Carried - before, Is.GreaterThanOrEqualTo(200), "Clear the opening in Nightshift, then return to Day for cash.");
        var rejoinMover = hazards.Single(h => Convert.ToSingle(Field(h, "Start")) == 710f);
        Assert.That(Field(rejoinMover, "LateralMotion"), Is.True);
        Assert.That(Convert.ToSingle(Field(rejoinMover, "Start")) - (float)Get(Get(hud, "Routing"), "Rejoin"), Is.InRange(25f, 60f));
        Pose(game, 724f, 3.2f); before = Run.Carried;
        for (int i = 0; i < 80 && (float)Get(game, "Distance") < 762f; i++) Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Run.Carried, Is.GreaterThan(before), "A Day reward breaks up the moving-block sequence.");
        Assert.That(Get(game, "Hits"), Is.EqualTo(0));
        foreach (float at in new[] { 1260f, 1340f })
            Assert.That(Call(game, "FindCollision", Get(game, "World"), at, at + 1f, 0f, 0f, false), Is.Not.Null,
                "Holding the centre lane cannot bypass the final slalom.");
    }

    [UnityTest]
    public IEnumerator ShortEnvironmentTransitionsWaitForBonusRampLandingAndRecovery()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; var hud = Get(game, "Getaway"); var jump = Get(Get(hud, "Extras"), "Jump");
            float start = Convert.ToSingle(Field(jump, "Start")), takeoff = Convert.ToSingle(Field(jump, "Takeoff"));
            float landing = Convert.ToSingle(Field(jump, "Landing")), finish = (float)Get(game, "RunLength");
            Assert.That(finish - landing, Is.GreaterThanOrEqualTo(24f), "Every ramp lands before the environment ends.");
            if (leg % 2 == 0)
            {
                var route = Get(game, "Route");
                Assert.That(Vector3.Angle(PlanarForward(route, start - 20f), PlanarForward(route, landing)), Is.LessThan(.1f));
                Assert.That(Mathf.Abs(((Vector3)Get(Call(route, "Sample", start - 20f), "Forward")).y), Is.LessThan(.001f));
            }
            Pose(game, start - 8f, 3.2f);
            for (int frame = 0; frame < 300 && (float)Get(game, "Distance") < landing + 18f; frame++)
            {
                Call(game, "StepSimulation", .025f, 0f, false);
                Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"), "No transition during the ramp or landing recovery.");
            }
            Assert.That(Get(jump, "Completed"), Is.True);
            Assert.That(Get(jump, "Traversing"), Is.False);
            var player = (Transform)Get(game, "Player");
            var point = Call(Get(game, "Route"), "Sample", Get(game, "Distance"));
            Assert.That(Mathf.Abs(player.position.y - ((Vector3)Get(point, "Position")).y), Is.LessThan(.4f), "The car has settled onto the road.");
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator ForkBannersLabelBothRoadsWithoutAHudRouteInstruction()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; var hud = Get(game, "Getaway"); var routing = Get(hud, "Routing");
            var signs = game.GetComponentsInChildren<TMPro.TextMeshPro>(true).Where(t => t.name.StartsWith("Fork ")).ToArray();
            Assert.That(signs.Length, Is.EqualTo(2));
            foreach (var sign in signs)
            {
                Assert.That(sign.text, Does.Not.Contain("DAY ONLY"));
                Assert.That(sign.transform.localPosition.y, Is.GreaterThan(4f));
                Assert.That(sign.transform.localPosition.x * (sign.name == "Fork left route" ? -1f : 1f), Is.GreaterThan(0f));
                sign.ForceMeshUpdate(); Assert.That(sign.isTextOverflowing, Is.False);
            }
            Assert.That(signs.Single(t => t.name == "Fork left route").text, Does.Contain("AMMO + BOOST"));
            Assert.That(signs.Single(t => t.name == "Fork right route").text, Does.Contain("CASH"));
            Pose(game, (float)Get(routing, "ForkStart") - 30f, 0f); Call(game, "StepSimulation", .001f, 0f, false); Call(hud, "Refresh");
            Assert.That(((TMPro.TMP_Text)Field(hud, "guidance")).text, Is.Empty);
            if (leg == 0) Capture("fork-banners-day");
            Call(game, "TrySwitch"); Call(hud, "Refresh");
            Assert.That(signs.All(t => t.gameObject.activeInHierarchy), Is.True, "Route signs stay readable at night.");
            Assert.That(((TMPro.TMP_Text)Field(hud, "guidance")).text, Is.Empty);
            if (leg == 0) Capture("fork-banners-night");
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    private static Vector3 PlanarForward(object route, float distance)
        => Vector3.ProjectOnPlane((Vector3)Get(Call(route, "Sample", distance), "Forward"), Vector3.up).normalized;

    [UnityTest]
    public IEnumerator ExtraSwitchWallsLeadToDayOnlyRewardWindows()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 5; leg++)
        {
            var game = Game; game.enabled = false; var hud = Get(game, "Getaway");
            var walls = leg == 1 ? ((IEnumerable)Get(game, "ActiveGates")).Cast<object>().Where(g => (bool)Field(g, "SwitchOnly"))
                : ((IEnumerable)Get(game, "Hazards")).Cast<object>().Where(h => Field(h, "Kind").ToString() == "SwitchWall");
            float at = Convert.ToSingle(Field(walls.Last(), "Start"));
            var expected = (Vector2[])Flow.Assembly.GetType("Outlaws.WorldShift.WorldShiftEscapeProfile")
                .GetMethod("SwitchRewards").Invoke(null, new[] { Get(hud, "Environment") });
            var rewards = ((IEnumerable)Field(hud, "loot")).Cast<object>()
                .Where(r => expected.Any(p => Mathf.Abs(Convert.ToSingle(Field(r, "At")) - p.x) < .01f
                    && Mathf.Abs(Convert.ToSingle(Field(r, "X")) - p.y) < .01f)).ToArray();
            Assert.That(rewards.Length, Is.GreaterThanOrEqualTo(2));
            var forks = Get(hud, "Routing");
            Assert.That(at < (float)Get(forks, "ForkStart") - 50f || at > (float)Get(forks, "Rejoin") + 50f, Is.True);
            Pose(game, at - 30f, 3.2f); Call(game, "TrySwitch");
            Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"));
            Call(game, "StepSimulation", .05f, 0f, false);
            foreach (var reward in rewards) Assert.That(((Transform)Field(reward, "Visual")).gameObject.activeSelf, Is.False);
            float x = Convert.ToSingle(Field(rewards[0], "X"));
            Pose(game, at + 40f, x); Call(game, "TrySwitch");
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"), "Returning after the wall is safe.");
            int before = Run.Carried;
            Call(hud, "Collect", at + 45f, at + 85f, x, x);
            Assert.That(Run.Carried, Is.GreaterThan(before), "Returning to Day earns the reward; staying at night misses it.");
            if (leg < 4) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        }
    }

    [UnityTest]
    public IEnumerator SnowSlideIsRecoverableInBothModesAndClearsJumpApproaches()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Navigate("StartGetaway", false); yield return null; yield return null;
        for (int leg = 0; leg < 3; leg++) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
        var game = Game; game.enabled = false; var driver = Field(game, "driver");
        float dayCoast = 0f;
        for (int mode = 0; mode < 2; mode++)
        {
            if (mode == 1) Call(game, "TrySwitch");
            Pose(game, 100f, 0f); Call(game, "StepSimulation", .01f, 0f, false);
            Assert.That(Get(driver, "EnvironmentReleaseGrip"), Is.EqualTo(.6f));
            // Run the same steering pulse on the same road, with and without Snow coasting.
            float normal = Coast(driver, 1f, false);
            float slide = Coast(driver, .6f, false);
            float caught = Coast(driver, .6f, true);
            Assert.That(slide - normal, Is.InRange(.1f, 1.2f), "Snow adds a small bounded slide after releasing steering.");
            Assert.That(caught, Is.LessThan(slide), "Countersteering must shorten the slide.");
            if (mode == 0) dayCoast = slide;
            else Assert.That(slide, Is.EqualTo(dayCoast).Within(.001f), "Nightshift does not change road grip.");
        }
        foreach (float at in new[] { 500f, 1080f, 1250f, 1310f, 1540f, 1620f })
        {
            Pose(game, at, 0f); Call(game, "StepSimulation", .001f, 0f, false);
            Assert.That(Get(driver, "EnvironmentReleaseGrip"), Is.EqualTo(1f), "No extra slide at forks, ramps or landing recovery: " + at);
        }
    }

    private static float Coast(object driver, float releaseGrip, bool countersteer)
    {
        Call(driver, "RestoreExternalPose", 100f, 0f);
        driver.GetType().GetProperty("EnvironmentReleaseGrip").SetValue(driver, releaseGrip);
        for (int i = 0; i < 10; i++) Call(driver, "StepExternal", .02f, 1f, 0f);
        float from = (float)Get(driver, "LateralOffset");
        for (int i = 0; i < 25; i++)
        {
            Call(driver, "StepExternal", .02f, countersteer ? -1f : 0f, 0f);
            if (countersteer && Convert.ToSingle(Field(driver, "lateralSpeed")) <= 0f) break;
        }
        Assert.That(Mathf.Abs(Convert.ToSingle(Field(driver, "lateralSpeed"))), Is.LessThan(.8f), "The car settles or reverses within half a second.");
        return (float)Get(driver, "LateralOffset") - from;
    }

    [UnityTest]
    public IEnumerator OceanSnowAndMountainCoursesCanBeDrivenThroughBothModesWithoutHits()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int course = 2; course < 5; course++)
        {
            Navigate("StartGetaway", false); yield return null; yield return null;
            for (int leg = 0; leg < course; leg++) { Navigate("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Game; game.enabled = false;
            var hud = Get(game, "Getaway"); var forks = Get(hud, "Routing");
            var hazards = ((IEnumerable)Get(game, "Hazards")).Cast<object>().ToArray();
            var relay = ((Array)Get(game, "RelayLayout")).GetValue(0);
            float takeoff = Convert.ToSingle(Field(relay, "Takeoff")), landingEnd = Convert.ToSingle(Field(relay, "End"));
            float fork = (float)Get(forks, "ForkStart"), rejoin = (float)Get(forks, "Rejoin");
            bool pictured = false;
            for (int frame = 0; frame < 2400 && Get(game, "State").ToString() == "Running"; frame++)
            {
                float d = (float)Get(game, "Distance");
                bool freeze = hazards.Any(h => Convert.ToSingle(Field(h, "MotionSpeed")) != 0f && d >= Convert.ToSingle(Field(h, "Start")) - 110f && d < Convert.ToSingle(Field(h, "End")) + 35f);
                bool phaseWall = hazards.Any(h => Field(h, "Kind").ToString() == "SwitchWall" && d >= Convert.ToSingle(Field(h, "Start")) - 100f && d < Convert.ToSingle(Field(h, "End")) + 4f);
                string desired = freeze || phaseWall || d >= takeoff && d < landingEnd ? "Echo" : "Day";
                if (Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
                float target = 0f, current = (float)Get(game, "Lateral");
                if (d >= fork - 50f && d < rejoin + 20f) target = -3.2f;
                else
                {
                    float[] lanes = { 0f, 3.2f, -3.2f };
                    // Choose a clear lane centre and hold it until the whole car has cleared the encounter.
                    // Holding an arbitrary current offset can hug a wall edge and drift into it on a bend.
                    target = lanes.FirstOrDefault(x => Call(game, "FindCollision", Get(game, "World"), d - 8f, d + 65f, x, x, false) == null);
                }
                Call(game, "StepSimulation", .05f, Mathf.Clamp((target - current) * 2f, -1f, 1f), false);
                Assert.That(Get(game, "Hits"), Is.EqualTo(0), "Collision at " + d + " in course " + course + " lateral=" + Get(game, "Lateral") + " target=" + target);
                Assert.That(Get(game, "State").ToString(), Is.Not.EqualTo("Lost"), "Failed at " + d + " in course " + course);
                if (!pictured && d > 150f) { Capture("automatic-course-" + course); pictured = true; }
            }
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
            Assert.That((int)Get(game, "Switches"), Is.GreaterThanOrEqualTo(4));
        }
    }
}
