using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldShiftCombatRoutesTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private int bank, best;
    private static Type FlowType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift.WorldShiftFlow")).First(t => t != null);
    private static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
    private static object Field(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Flow(string name, params object[] args) => FlowType.GetMethod(name).Invoke(null, args);
    private static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == name);
    private static GetawayRunState Run => (GetawayRunState)FlowType.GetProperty("Escape").GetValue(null);
    private static void Pose(MonoBehaviour game, float d, float x)
    {
        game.GetType().GetProperty("Distance", Flags).SetValue(game, d);
        Call(Field(game, "driver"), "RestoreExternalPose", d, x);
        if (game.GetType().Name == "WorldShiftCity") game.GetType().GetProperty("Lateral", Flags).SetValue(game, x);
    }
    private static void Capture(string name) => typeof(WorldShiftGetawayTests).GetMethod("Capture", Flags).Invoke(null, new object[] { name });
    private static void Advance(MonoBehaviour game, float target, float x)
    {
        for (int i = 0; i < 1600 && (float)Get(game, "Distance") < target; i++)
        {
            Call(game, "StepSimulation", .025f, Mathf.Clamp((x - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"), "Unexpected failure at " + Get(game, "Distance"));
        }
        Assert.That((float)Get(game, "Distance"), Is.GreaterThanOrEqualTo(target));
    }
    [SetUp] public void SaveBank() { bank = PlayerPrefs.GetInt("WorldShift.Getaway.Bank"); best = PlayerPrefs.GetInt("WorldShift.Getaway.Best"); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        PlayerPrefs.SetInt("WorldShift.Getaway.Bank", bank); PlayerPrefs.SetInt("WorldShift.Getaway.Best", best); PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator CityShotsClearBothWorldsRespectPauseAmmoAndCurvedRoads()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var game = Find("WorldShiftCity"); game.enabled = false; var combat = Get(game, "Combat");
        var hazards = ((IEnumerable)Get(game, "Hazards")).Cast<object>().ToArray();
        var echoBarrier = hazards.Single(h => Field(h, "Kind").ToString() == "Barrier" && (float)Field(h, "Start") == 440f);
        Pose(game, 415f, 0f); Call(game, "TrySwitch");
        Assert.That(Call(combat, "TryShoot"), Is.True);
        Assert.That(Run.Ammo, Is.EqualTo(2));
        Assert.That(Call(combat, "IsDestroyed", echoBarrier), Is.True);
        Assert.That(Call(game, "FindCollision", Get(game, "World"), 441f, 442f, 0f, 0f, false), Is.Null);
        var root = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Echo Barrier 440");
        Assert.That(root.Find("Solid - collides in this world").gameObject.activeSelf, Is.False);
        Capture("shooting-night-barrier");
        Call(game, "TogglePause"); Call(game, "StepSimulation", 4f, 0f, false);
        Assert.That(Call(combat, "TryShoot"), Is.False); Assert.That(Run.Ammo, Is.EqualTo(2));
        Call(game, "TogglePause"); Call(game, "StepSimulation", .25f, 0f, false);
        Pose(game, 462f, -3.2f); Call(game, "TrySwitch");
        var dayWall = hazards.Single(h => Field(h, "Kind").ToString() == "HalfBarrier" && (float)Field(h, "Start") == 480f);
        Assert.That(Call(combat, "TryShoot"), Is.True);
        Assert.That(Call(combat, "IsDestroyed", dayWall), Is.True); Assert.That(Run.Ammo, Is.EqualTo(1));
        // A real bend: the shot clears the visible Day barrier at 900 m.
        Call(combat, "Step", 779f, 781f, 0f, 0f); Assert.That(Run.Ammo, Is.EqualTo(3));
        Pose(game, 870f, -3.2f); Call(game, "StepSimulation", .2f, 0f, false);
        var curved = hazards.Single(h => Field(h, "Kind").ToString() == "Barrier" && (float)Field(h, "Start") == 900f);
        Assert.That(Call(combat, "TryShoot"), Is.True);
        Assert.That(Call(combat, "IsDestroyed", curved), Is.True);
        Capture("shooting-around-bend");
        Call(game, "Fail", "CAUGHT"); Assert.That(Call(combat, "TryShoot"), Is.False);
        Assert.That(Run.Ammo, Is.Zero);
    }

    [UnityTest]
    public IEnumerator DesertRocksBreakInDayAndNightAndAmmoCarriesBetweenEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Flow("StartGetaway", false); yield return null; yield return null;
        var city = Find("WorldShiftCity"); city.enabled = false;
        Assert.That(Call(Get(city, "Combat"), "TryShoot"), Is.True);
        Flow("FinishGetawayLeg", true); yield return null; yield return null;
        var game = Find("DesertShiftTrial"); game.enabled = false; var combat = Get(game, "Combat");
        Assert.That(Run.Ammo, Is.EqualTo(2));
        var rocks = ((IEnumerable)Field(game, "Rocks")).Cast<object>().ToArray();
        var first = rocks.Single(r => (float)Field(r, "Distance") == 180f);
        Pose(game, 150f, 0f); Call(game, "TrySwitch");
        Assert.That(Call(combat, "TryShoot"), Is.True); Assert.That(Call(combat, "IsDestroyed", first), Is.True);
        float gap = (float)Get(game, "PursuitGap"); Advance(game, 195f, 0f);
        Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap - .3f), "A destroyed rock cannot retain its collision.");
        Capture("shooting-frozen-rock");
        Pose(game, 270f, 0f); Call(game, "TrySwitch");
        var second = rocks.Single(r => (float)Field(r, "Distance") == 300f);
        Pose(game, 270f, (float)Call(game, "RockLateral", second, Get(game, "World")));
        Assert.That(Call(combat, "TryShoot"), Is.True); Assert.That(Call(combat, "IsDestroyed", second), Is.True);
        Assert.That(Run.Ammo, Is.Zero); Assert.That(Call(combat, "TryShoot"), Is.False);
        Call(combat, "Step", 779f, 781f, 0f, 0f); Assert.That(Run.Ammo, Is.EqualTo(3));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None), Is.Empty);
    }

    [UnityTest]
    public IEnumerator BothRealBranchesRejoinAndTradeAmmoForRewardInBothEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int pass = 0; pass < 4; pass++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (pass >= 2) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(pass < 2 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var hud = Find("WorldShiftGetaway"); var routing = Get(hud, "Routing"); var combat = Get(game, "Combat");
            float end = (float)Get(routing, "EnvironmentEnd"); bool right = pass % 2 != 0;
            if (!right)
            {
                Call(combat, "TryShoot"); Call(game, "StepSimulation", .2f, 0f, false); Call(combat, "TryShoot");
                Call(game, "StepSimulation", .2f, 0f, false); Call(combat, "TryShoot");
                Assert.That(Run.Ammo, Is.Zero, "The clear branch must remain usable without ammo.");
            }
            Pose(game, end + 20f, right ? 3.2f : -2.8f);
            float gap = (float)Get(game, "PursuitGap");
            bool pictured = false;
            for (int i = 0; i < 900 && (float)Get(game, "Distance") < end + 324f; i++)
            {
                float d = (float)Get(game, "Distance");
                if (right && ((string)Get(combat, "Hint")).StartsWith("F")) Call(combat, "TryShoot");
                Call(game, "StepSimulation", .025f, Mathf.Clamp(((right ? 3.2f : -2.8f) - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
                Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
                if (!pictured && d > end + 195f)
                {
                    var player = (Transform)Get(game, "Player"); object p = Call(Get(game, "Route"), "Sample", Get(game, "Distance"));
                    Assert.That(Mathf.Abs(Vector3.Dot(player.position - (Vector3)Get(p, "Position"), (Vector3)Get(p, "Right"))), Is.GreaterThan(6f));
                    var hidden = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Shared road " + (end + 160f).ToString("0"));
                    Assert.That(hidden.Find("Road").GetComponent<Renderer>().enabled, Is.False, "There must be no invisible straight-road shortcut between the branches.");
                    Capture("fork-" + (pass < 2 ? "city-" : "desert-") + (right ? "reward" : "safe")); pictured = true;
                }
            }
            Assert.That(Get(routing, "RewardBranch"), Is.EqualTo(right ? 1 : -1));
            Assert.That((float)Get(game, "Distance"), Is.GreaterThan(end + 320f));
            Assert.That(Run.Carried, Is.EqualTo((int)((right ? 600 : 200) * Run.LootMultiplier)));
            Assert.That(Run.Ammo, Is.EqualTo(right ? 1 : 3));
            Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap - .3f), "Both branches must be traversable without an impact.");
            Assert.That((float)Call(routing, "Spread", Get(game, "Distance")), Is.EqualTo(0f));
        }
    }

    [UnityTest]
    public IEnumerator PracticeRetriesRestoreAmmoAndDestroyedObstaclesInBothEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("Select", Enum.Parse(FlowType.Assembly.GetType("Outlaws.WorldShift.ShiftLevel"), environment == 0 ? "City" : "Desert"), false);
            yield return null; yield return null;
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var combat = Get(game, "Combat"); object target;
            if (environment == 0)
            {
                target = ((IEnumerable)Get(game, "Hazards")).Cast<object>().Single(h => Field(h, "Kind").ToString() == "Barrier" && (float)Field(h, "Start") == 440f);
                Pose(game, 415f, 0f);
            }
            else
            {
                target = ((IEnumerable)Field(game, "Rocks")).Cast<object>().Single(r => (float)Field(r, "Distance") == 180f);
                Pose(game, 150f, 0f);
            }
            Call(game, "TrySwitch"); Assert.That(Call(combat, "TryShoot"), Is.True);
            Assert.That(Call(combat, "IsDestroyed", target), Is.True);
            Assert.That(Get(combat, "Ammo"), Is.EqualTo(2));
            Call(game, environment == 0 ? "RestoreCheckpoint" : "Restore");
            Assert.That(Call(combat, "IsDestroyed", target), Is.False);
            Assert.That(Get(combat, "Ammo"), Is.EqualTo(3));
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        }
    }

    [UnityTest]
    public IEnumerator ExitJunctionCommitsBySteeringAndBanksOrLoadsTheNextEnvironment()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int choice = 0; choice < 2; choice++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            var game = Find("WorldShiftCity"); game.enabled = false;
            var hud = Find("WorldShiftGetaway"); var route = Get(hud, "Routing");
            float end = (float)Get(route, "EnvironmentEnd"); Run.Collect(500);
            Pose(game, end + 500f, choice == 0 ? -3.2f : 3.3f);
            Advance(game, end + 690f, choice == 0 ? -3.2f : 3.3f);
            Assert.That(Get(route, "ExitBranch"), Is.EqualTo(choice == 0 ? -1 : 1));
            Capture(choice == 0 ? "junction-cash-out" : "junction-continue");
            // Once the roads separate, steering toward the other road stays within the visible rail.
            Call(game, "StepSimulation", .5f, choice == 0 ? 1f : -1f, false);
            Assert.That((float)Get(game, "Lateral"), choice == 0 ? Is.LessThan(0f) : Is.GreaterThan(0f));
            for (int i = 0; i < 150 && Get(game, "State").ToString() == "Running"; i++) Call(game, "StepSimulation", .05f, 0f, false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
            if (choice == 0)
            {
                Assert.That(Run.Status, Is.EqualTo(GetawayRunState.RunStatus.Banked));
                Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Bank"), Is.EqualTo(bank + 500));
                Assert.That(PlayerPrefs.GetInt("WorldShift.Getaway.Best"), Is.GreaterThanOrEqualTo(500));
                Capture("banked-personal-best");
            }
            else
            {
                yield return null; yield return null;
                Assert.That(Find("DesertShiftTrial"), Is.Not.Null); Assert.That(Run.Carried, Is.EqualTo(500));
            }
        }
    }
}
