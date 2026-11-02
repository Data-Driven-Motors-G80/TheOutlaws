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
    public IEnumerator EscapeJumpsHaveRealGapsAndRequireTheOtherWorldToLand()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int pass = 0; pass < 8; pass++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            int jump = pass % 4; bool desert = jump == 3, switchInAir = pass < 4;
            if (desert) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(desert ? "DesertShiftTrial" : "WorldShiftCity"); game.enabled = false;
            float ramp = new[] { 600f, 1740f, 2300f, 1944f }[jump];
            float takeoff = new[] { 660f, 1800f, 2360f, 2004f }[jump];
            float landing = new[] { 744f, 1896f, 2468f, 2172f }[jump];
            if (jump == 1) Call(game, "TrySwitch");
            object source = Get(game, "World");
            object target = Enum.Parse(source.GetType(), jump == 1 ? "Day" : "Echo");
            var route = Get(game, "Route");
            Assert.That(Call(route, "HasSurface", ramp + 10f, source), Is.True);
            Assert.That(Call(route, "HasSurface", ramp + 10f, target), Is.False);
            Assert.That(Call(route, "HasSurface", takeoff + 8f, source), Is.False);
            Assert.That(Call(route, "HasSurface", takeoff + 8f, target), Is.False);
            Assert.That(Call(route, "HasSurface", landing + 4f, source), Is.False);
            Assert.That(Call(route, "HasSurface", landing + 4f, target), Is.True);
            float lane = desert ? -3.2f : 0f;
            Pose(game, takeoff - 28f, lane);
            Advance(game, takeoff + 5f, lane);
            Assert.That(Get(game, desert ? "IsAirborne" : "IsRelayAirborne"), Is.True);
            string hint = (string)Get(game, desert ? "Hint" : "SectionHint");
            Assert.That(hint, Does.Contain("Space to land in"));
            var visual = game.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "Relay " + (desert ? 1 : jump + 1) + " - launch and landing");
            Assert.That(visual.Find("Launch world missing landing").gameObject.activeSelf, Is.True);
            Assert.That(visual.Find("Other-world landing outline - no road here yet").gameObject.activeSelf, Is.True);
            Capture("escape-jump-gap-" + jump);
            if (switchInAir)
            {
                Call(game, "TrySwitch");
                Assert.That(visual.Find("Solid landing markers").gameObject.activeSelf, Is.True);
                Advance(game, landing + 6f, lane);
                Assert.That(Get(game, desert ? "IsAirborne" : "IsRelayAirborne"), Is.False);
                Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
                Capture("escape-jump-landed-" + jump);
            }
            else
            {
                // Isolate the landing rule from the desert's separate airborne-rock challenge.
                Pose(game, landing - .5f, lane);
                Call(game, "StepSimulation", .025f, 0f, false);
                Assert.That(Get(game, "State").ToString(), Is.EqualTo("Lost"), "Remaining in the launch world must miss the landing.");
                Assert.That(Run.Failure, Is.EqualTo("MISSED LANDING"));
            }
        }
    }

    [UnityTest]
    public IEnumerator DayEarnsAndNightFreezesWithoutRemovingHazardsOrResourcesInBothEnvironments()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var combat = Get(game, "Combat"); var hud = Get(game, "Getaway"); var extras = Get(hud, "Extras");
            var fuel = (FuelState)Field(game, "fuel"); fuel.Reset(50f);
            Call(game, "TrySwitch"); Call(combat, "TryShoot");
            Call(game, "StepSimulation", 2.2f, 0f, false);
            Assert.That(Run.Carried, Is.Zero, "Night cannot award loot.");
            Assert.That(Get(extras, "BoostActive"), Is.False); Assert.That(Run.HasShield, Is.False);
            var boxes = ((IEnumerable)Field(hud, "loot")).Cast<object>().ToArray();
            Assert.That(boxes.All(b => !((Transform)Field(b, "Visual")).gameObject.activeSelf), Is.True);
            foreach (string name in new[] { "shieldPickup", "boostPickup", "magnetPickup" })
                Assert.That(((Transform)Field(extras, name)).gameObject.activeSelf, Is.False);
            if (environment == 0) { Pose(game, 260f, 0f); Call(game, "StepSimulation", .6f, 0f, false); }
            Assert.That(fuel.CurrentFuel, Is.LessThan(50f), "Night burns fuel without collecting it.");
            Assert.That(Run.Ammo, Is.EqualTo(2));
            Call(combat, "Step", (float)Get(combat, "RefillAt") - 1f, (float)Get(combat, "RefillAt") + 1f, 0f, 0f);
            Assert.That(Run.Ammo, Is.EqualTo(3), "Ammo remains collectible in Night.");

            Call(game, "TrySwitch");
            float fuelBeforeDay = fuel.CurrentFuel;
            float fuelAt = environment == 0 ? 270f : 24f;
            Pose(game, fuelAt - 8f, 0f); Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(fuel.CurrentFuel, Is.GreaterThan(fuelBeforeDay), "Day restores fuel collection.");
            Pose(game, 30f, 0f); Call(game, "StepSimulation", .5f, 0f, false);
            Pose(game, (float)Get(extras, "BoostDistance") - 8f, -2.8f); Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(Run.Carried, Is.GreaterThan(0), "Returning to Day restores earning.");
            Assert.That(Get(extras, "BoostActive"), Is.True, "The clear branch offers boost in Day.");
            Pose(game, (float)Get(Get(hud, "Routing"), "EnvironmentEnd") + 21f, -2.8f);
            Call(game, "StepSimulation", 4.2f, 0f, false);
            Assert.That(Get(extras, "BoostActive"), Is.False);
            object mover;
            string clock;
            if (environment == 0)
            {
                mover = ((IEnumerable)Get(game, "Hazards")).Cast<object>().Single(h => (float)Field(h, "Start") == 300f);
                clock = "MotionTravel"; Pose(game, 150f, 0f);
            }
            else
            {
                mover = ((IEnumerable)Get(game, "Traffic")).Cast<object>().First();
                clock = "Travel"; Pose(game, 450f, 0f);
            }
            float initial = (float)Get(mover, clock);
            Call(game, "StepSimulation", .25f, 0f, false);
            Assert.That((float)Get(mover, clock), Is.GreaterThan(initial));
            Call(game, "TrySwitch"); float frozen = (float)Get(mover, clock);
            float gap = (float)Get(game, "PursuitGap");
            Call(game, "StepSimulation", .4f, 0f, false);
            Assert.That((float)Get(mover, clock), Is.EqualTo(frozen));
            Assert.That((float)Get(game, "PursuitGap"), Is.LessThan(gap), "The chase continues in Night.");
            Call(game, "TrySwitch");
            Assert.That((float)Get(mover, clock), Is.EqualTo(frozen), "Switching must not teleport an obstacle.");
            Call(game, "StepSimulation", .2f, 0f, false);
            Assert.That((float)Get(mover, clock), Is.GreaterThan(frozen));
            var route = Get(game, "Route"); object day = Get(game, "World"); Call(game, "TrySwitch"); object night = Get(game, "World");
            foreach (float distance in new[] { 1255f, 1520f, 2800f })
            { Assert.That(Call(route, "HasSurface", distance, day), Is.True); Assert.That(Call(route, "HasSurface", distance, night), Is.True); }
            if (environment == 0)
                Assert.That(Call(game, "FindCollision", night, 105f, 106f, -3.2f, -3.2f, false), Is.Not.Null, "Frozen hazards remain solid.");
            Capture(environment == 0 ? "day-night-city" : "day-night-desert");
        }
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
        Call(game, "StepSimulation", .001f, 0f, false);
        Assert.That(Call(combat, "TryShoot"), Is.True);
        var bullet = game.GetComponentsInChildren<Transform>().Single(t => t.name == "Forward Shot");
        Assert.That(bullet.localScale, Is.EqualTo(Vector3.one * .28f));
        Assert.That(Vector4.Distance(bullet.GetComponent<Renderer>().sharedMaterial.color, new Vector4(1f, .75f, .05f, 1f)), Is.LessThan(.0001f));
        Assert.That(bullet.GetComponent<Collider>().enabled, Is.False, "The original visual must not introduce a second collision system.");
        Vector3 muzzle = bullet.position;
        Call(game, "StepSimulation", .08f, 0f, false); Call(combat, "Present");
        Assert.That(Vector3.Distance(muzzle, bullet.position), Is.EqualTo(5.6f).Within(.15f), "The visible bullet travels at the original 70 m/s.");
        Assert.That(Run.Ammo, Is.EqualTo(2));
        Assert.That(Call(combat, "IsDestroyed", echoBarrier), Is.True);
        Assert.That(Call(game, "FindCollision", Get(game, "World"), 441f, 442f, 0f, 0f, false), Is.Null);
        var root = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Day Barrier 440");
        Assert.That(root.Find("Solid - collides in this world").gameObject.activeSelf, Is.False);
        Capture("shooting-night-barrier");
        Call(game, "TogglePause"); Call(game, "StepSimulation", 4f, 0f, false);
        Vector3 pausedShot = bullet.position; Call(combat, "Present");
        Assert.That(bullet.position, Is.EqualTo(pausedShot));
        Assert.That(Call(combat, "TryShoot"), Is.False); Assert.That(Run.Ammo, Is.EqualTo(2));
        Call(game, "TogglePause"); Call(game, "StepSimulation", .25f, 0f, false);
        Pose(game, 1020f, 0f); Call(game, "TrySwitch");
        var dayWall = hazards.Single(h => Field(h, "Kind").ToString() == "Barrier" && (float)Field(h, "Start") == 1050f);
        Assert.That(Call(combat, "TryShoot"), Is.True);
        Assert.That(Call(combat, "IsDestroyed", dayWall), Is.True); Assert.That(Run.Ammo, Is.EqualTo(1));
        // A real bend: the shot clears the visible Day barrier at 900 m.
        Call(combat, "Step", (float)Get(combat, "RefillAt") - 1f, (float)Get(combat, "RefillAt") + 1f, 0f, 0f); Assert.That(Run.Ammo, Is.EqualTo(3));
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
        Call(combat, "Step", (float)Get(combat, "RefillAt") - 1f, (float)Get(combat, "RefillAt") + 1f, 0f, 0f); Assert.That(Run.Ammo, Is.EqualTo(3));
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
            float start = (float)Get(routing, "ForkStart"), rejoin = (float)Get(routing, "Rejoin"); bool right = pass % 2 != 0;
            Assert.That(rejoin, Is.LessThan((float)Get(routing, "EnvironmentEnd")), "The choice belongs in the main chase.");
            float rightLane = (float)Get(routing, "RightLootLane");
            object curveA = Call(Get(game, "Route"), "Sample", pass < 2 ? 820f : 200f);
            object curveB = Call(Get(game, "Route"), "Sample", pass < 2 ? 1000f : 420f);
            Assert.That(Vector3.Angle((Vector3)Get(curveA, "Forward"), (Vector3)Get(curveB, "Forward")), Is.InRange(8f, 18f), "Environment bends should be visible and gentle.");
            object forkA = Call(Get(game, "Route"), "Sample", start + 80f), forkB = Call(Get(game, "Route"), "Sample", start + 200f);
            Assert.That(Vector3.Angle((Vector3)Get(forkA, "Forward"), (Vector3)Get(forkB, "Forward")), Is.LessThan(2f), "The fork must not coincide with a sharp bend.");
            if (!right)
            {
                Call(combat, "TryShoot"); Call(game, "StepSimulation", .2f, 0f, false); Call(combat, "TryShoot");
                Call(game, "StepSimulation", .2f, 0f, false); Call(combat, "TryShoot");
                Assert.That(Run.Ammo, Is.Zero, "The clear branch must remain usable without ammo.");
            }
            Pose(game, start - 20f, right ? rightLane : -2.8f);
            float gap = (float)Get(game, "PursuitGap");
            bool pictured = false;
            for (int i = 0; i < 900 && (float)Get(game, "Distance") < rejoin + 4f; i++)
            {
                float d = (float)Get(game, "Distance");
                if (right && ((string)Get(combat, "Hint")).StartsWith("Click")) Call(combat, "TryShoot");
                Call(game, "StepSimulation", .025f, Mathf.Clamp(((right ? rightLane : -2.8f) - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
                Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
                if (!pictured && d > start + 155f)
                {
                    var player = (Transform)Get(game, "Player"); object p = Call(Get(game, "Route"), "Sample", Get(game, "Distance"));
                    Assert.That(Mathf.Abs(Vector3.Dot(player.position - (Vector3)Get(p, "Position"), (Vector3)Get(p, "Right"))), Is.GreaterThan(6f));
                    var hidden = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Shared road " + (start + 120f).ToString("0"));
                    Assert.That(hidden.Find("Road").GetComponent<Renderer>().enabled, Is.False, "There must be no invisible straight-road shortcut between the branches.");
                    Assert.That((float)Call(routing, "Width", d, -1), Is.EqualTo((float)Call(routing, "Width", d, 1)));
                    Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == (pass < 2 ? "Side-street boundary" : "Canyon boundary")), Is.True);
                    var beforeSwitch = player.position; int branch = (int)Get(routing, "RewardBranch");
                    Call(game, "TrySwitch");
                    Assert.That(player.position, Is.EqualTo(beforeSwitch)); Assert.That(Get(routing, "RewardBranch"), Is.EqualTo(branch));
                    Capture("fork-night-" + pass);
                    Call(game, "TrySwitch");
                    Capture("fork-" + (pass < 2 ? "city-" : "desert-") + (right ? "reward" : "safe")); pictured = true;
                }
            }
            Assert.That(Get(routing, "RewardBranch"), Is.EqualTo(right ? 1 : -1));
            Assert.That((float)Get(game, "Distance"), Is.GreaterThan(rejoin));
            Assert.That(Run.Carried, Is.EqualTo((int)((right ? 600 : 200) * Run.LootMultiplier)));
            Assert.That(Run.Ammo, Is.EqualTo(right ? 1 : 3));
            Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap - 3f), "Both branches must be traversable without an impact.");
            Assert.That((float)Call(routing, "Spread", Get(game, "Distance")), Is.EqualTo(0f));
        }
    }

    [UnityTest]
    public IEnumerator BothModesKeepEqualCruiseSpeedAndFuelBurn()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var fuel = (FuelState)Field(game, "fuel");
            string speed = environment == 0 ? "Speed" : "CurrentSpeed";
            float daySpeed = (float)Get(game, speed);
            Pose(game, 60f, 0f); fuel.Reset(50f);
            Call(game, "StepSimulation", 1f, 0f, false);
            float dayBurn = 50f - fuel.CurrentFuel;
            Call(game, "TrySwitch");
            Assert.That((float)Get(game, speed), Is.EqualTo(daySpeed));
            Pose(game, 60f, 0f); fuel.Reset(50f);
            Call(game, "StepSimulation", 1f, 0f, false);
            Assert.That(50f - fuel.CurrentFuel, Is.EqualTo(dayBurn).Within(.001f),
                "Night's cost comes from missed supplies, with no extra fuel penalty.");
            Assert.That((float)Get(game, "Distance"), Is.EqualTo(60f + daySpeed).Within(.01f));
        }
    }

    [UnityTest]
    public IEnumerator FreezeTimingPreservesEitherABlockedLaneOrAClearOpening()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int timing = 0; timing < 2; timing++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            Flow("FinishGetawayLeg", true); yield return null; yield return null;
            var game = Find("DesertShiftTrial"); game.enabled = false;
            var block = ((IEnumerable)Field(game, "Rocks")).Cast<object>().Single(r => (float)Field(r, "Distance") == 180f);
            // The same crossing block starts in the middle and moves aside during Day.
            Call(block, "Restore", 0f);
            var day = Get(game, "World");
            Call(block, "Advance", (float)timing, day);
            float frozenX = (float)Get(block, "Lateral");
            Call(game, "TrySwitch");
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That((float)Call(game, "RockLateral", block, Get(game, "World")), Is.EqualTo(frozenX));
            Pose(game, 180f, 0f);
            float gap = (float)Get(game, "PursuitGap");
            Call(game, "CheckRockContacts", 180f, 0f, 0f);
            float loss = gap - (float)Get(game, "PursuitGap");
            Assert.That(loss, timing == 0 ? Is.GreaterThan(20f) : Is.EqualTo(0f),
                "Switching must freeze the player's chosen moment, keeping blocked lanes solid and open lanes usable.");
        }
    }

    [UnityTest]
    public IEnumerator NightHidesFuelDisablesPowersAndRemindsWithoutAdvancingDuringPause()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var hud = Get(game, "Getaway"); var extras = Get(hud, "Extras");
            Call(extras, "Collect", 42f, 46f, -3.2f, -3.2f);
            Call(extras, "Collect", 148f, 152f, 3.2f, 3.2f);
            float boostAt = (float)Get(extras, "BoostDistance");
            Call(extras, "Collect", boostAt - 2f, boostAt + 2f, -2.8f, -2.8f);
            Assert.That(Get(extras, "BoostActive"), Is.True);
            Assert.That(Get(extras, "MagnetActive"), Is.True);
            Call(game, "TrySwitch"); Call(hud, "Refresh");
            var strip = Field(hud, "statusStrip");
            Assert.That(((TMPro.TMP_Text)Get(strip, "World")).text, Is.EqualTo("Nightshift"));
            Assert.That(Get(extras, "BoostActive"), Is.False);
            Assert.That(Get(extras, "MagnetActive"), Is.False);
            Assert.That(Call(extras, "BlocksImpact", false), Is.False);
            Assert.That(Run.HasShield, Is.True, "Night must not consume the shield stored for Day.");
            Assert.That(((Transform)Field(extras, "shieldAura")).gameObject.activeSelf, Is.False);
            var fuelModels = environment == 0
                ? (IEnumerable)Field(Find("WorldShiftPresentation"), "fuelModels")
                : (IEnumerable)Field(Find("DesertShiftTrialView"), "fuelViews");
            Assert.That(fuelModels.Cast<GameObject>().All(model => !model.activeSelf), Is.True);
            Call(game, "TrySwitch");
            Assert.That(Get(extras, "BoostActive"), Is.True, "Returning promptly to Day restores unexpired powers.");
            Call(game, "TrySwitch");
            Assert.That(Get(hud, "NightReminder"), Is.EqualTo(""));
            Call(game, "StepSimulation", .1f, 0f, false);
            game.GetType().GetProperty("Duration", Flags).SetValue(game, (float)Get(game, "Duration") + 12f);
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            Call(game, "TogglePause");
            float duration = (float)Get(game, "Duration");
            Call(game, "StepSimulation", 10f, 0f, false);
            Assert.That(Get(game, "Duration"), Is.EqualTo(duration));
            Call(game, "TogglePause");
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            // A render of the other world must not reset the player's Night stay.
            Call(game, "PresentWorldForCamera", Enum.Parse(Get(game, "World").GetType(), "Day"));
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            Call(game, "PresentWorldForCamera", Get(game, "World"));
            game.GetType().GetProperty("Duration", Flags).SetValue(game, duration + 2f);
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            Capture("night-reminder-" + environment);
            game.GetType().GetProperty("Duration", Flags).SetValue(game, duration + 4f);
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            game.GetType().GetProperty("Duration", Flags).SetValue(game, duration + 7f);
            Assert.That(Get(hud, "NightReminder"), Is.EqualTo(""));
            game.GetType().GetProperty("Duration", Flags).SetValue(game, duration + 45f);
            Assert.That((string)Get(hud, "NightReminder"), Does.Contain("Return to Day for supplies"));
            Call(game, "TrySwitch");
            Assert.That(Get(hud, "NightReminder"), Is.EqualTo(""));
            Call(game, "TrySwitch");
            Assert.That(Get(hud, "NightReminder"), Is.EqualTo(""), "A new Night visit gets its own grace period.");
        }
    }

    [UnityTest]
    public IEnumerator SolidHazardsShareReadableXBlocksInBothWorlds()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var blocks = game.GetComponentsInChildren<Transform>(true).Where(t => t.name == "X obstacle").ToArray();
            Assert.That(blocks.Length, Is.GreaterThan(10));
            foreach (var block in blocks)
            {
                Assert.That(block.Find("Obstacle block"), Is.Not.Null);
                Assert.That(block.GetComponentsInChildren<Renderer>(true).Count(r => r.name == "Obstacle X"), Is.EqualTo(6));
                Assert.That(block.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True,
                    "Visual blocks must keep the existing swept collision rules.");
            }
            if (environment == 0)
            {
                foreach (var hazard in ((IEnumerable)Get(game, "Hazards")).Cast<object>())
                {
                    if (Field(hazard, "Kind").ToString() == "Gap") continue;
                    string rootName = Field(hazard, "World") + " " + Field(hazard, "Kind") + " " + Field(hazard, "Start");
                    var root = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == rootName);
                    Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "X obstacle"), Is.True);
                }
            }
            Pose(game, environment == 0 ? 65f : 145f, 0f);
            Capture("x-obstacles-day-" + environment);
            Call(game, "TrySwitch");
            Capture("x-obstacles-night-" + environment);
            Assert.That(blocks.All(b => b.localRotation == Quaternion.identity), Is.True,
                "Frozen and moving blocks keep the X upright.");
        }
    }

    [UnityTest]
    public IEnumerator RewardBranchHasASteeringBypassWithoutAmmoOrShield()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        for (int environment = 0; environment < 2; environment++)
        {
            Flow("StartGetaway", false); yield return null; yield return null;
            if (environment == 1) { Flow("FinishGetawayLeg", true); yield return null; yield return null; }
            var game = Find(environment == 0 ? "WorldShiftCity" : "DesertShiftTrial"); game.enabled = false;
            var routing = Get(Get(game, "Getaway"), "Routing");
            while (Run.Ammo > 0) Run.SpendShot();
            float start = (float)Get(routing, "ForkStart");
            Pose(game, start - 10f, 3.2f);
            float gap = (float)Get(game, "PursuitGap");
            Advance(game, start + 85f, 5.5f);
            Assert.That((float)Call(routing, "Width", start + 100f, 1), Is.EqualTo(10f));
            Advance(game, (float)Get(routing, "Rejoin") + 5f, 5.5f);
            Assert.That((float)Get(game, "PursuitGap"), Is.GreaterThan(gap - 3f));
            Assert.That(Run.HasShield, Is.False);
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
            Pose(game, end + 180f, choice == 0 ? -3.2f : 3.3f);
            Advance(game, (float)Get(route, "ExitChoiceAt") + 90f, choice == 0 ? -3.2f : 3.3f);
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
