using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Desert obstacle rules (Docs/OBSTACLE_VISUAL_RULES.md): no text on obstacles, one sideways arrow on every
// sideways mover, sideways movers always moving in Day however far ahead they are, and desert-themed art.
// Other levels are listed in that doc for their owners; add their level number to CheckObstacleRules once fixed.
public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator DesertObstaclesFollowTheSharedVisualRules()
    {
        // Enter play mode here: nested enumerators cannot enter play mode.
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        yield return CheckObstacleRules(DesertLevel);
    }

    private IEnumerator CheckObstacleRules(int level)
    {
        StartLevel(level); yield return WaitForNavigation();
        var game = Started(); var presentation = Field(game, "presentation");
        foreach (object view in (IEnumerable)Field(presentation, "hazards"))
        {
            var data = Field(view, "Data"); var root = (Transform)Field(view, "Root");
            string kind = Field(data, "Kind").ToString(); float start = (float)Field(data, "Start");
            Assert.That(root.GetComponentsInChildren<TMP_Text>(true), Is.Empty, $"Level {level}: no text on obstacles ({kind} at {start}).");
            if ((bool)Field(data, "LateralMotion"))
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Sideways mover arrow"), Is.True,
                    $"Level {level}: every sideways mover uses the shared arrow ({kind} at {start}).");
            if (kind == "Oncoming" && !(bool)Field(data, "SharedAcrossWorlds"))
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "World badge on traffic nose"
                    || t.name == "Snowplough blade and world panel"), Is.True, $"Level {level}: one-world traffic shows sun/moon ({start}).");
        }
        // Sideways movers keep moving in Day while still far ahead of the car.
        var movers = ((System.Array)Get(game, "Hazards")).Cast<object>().Where(h => (bool)Field(h, "LateralMotion")).ToArray();
        foreach (var mover in movers) Call(mover, "Advance", .5f, -1000f, World("Day"));
        foreach (var mover in movers)
            Assert.That(Mathf.Abs((float)Get(mover, "MotionTravel")), Is.GreaterThan(0f), $"Level {level}: sideways movers never wait for the car.");
        foreach (var mover in movers) Call(mover, "ResetMotion");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator DesertUsesDesertObstaclesWithFlappingVultures()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(DesertLevel); yield return WaitForNavigation();
        var game = Started(); var presentation = Field(game, "presentation");
        var all = game.GetComponentsInChildren<Transform>(true);
        var hazards = ((System.Array)Get(game, "Hazards")).Cast<object>().ToArray();
        int airRocks = hazards.Count(h => Field(h, "Rock") != null && (bool)Get(Field(h, "Rock"), "Airborne"));
        int groundRocks = hazards.Count(h => Field(h, "Rock") != null) - airRocks;
        int airGates = hazards.Count(h => Field(h, "Gate") != null && (bool)Get(Field(h, "Gate"), "Airborne"));
        int groundGates = hazards.Count(h => Field(h, "Gate") != null) - airGates;
        int sliders = hazards.Count(h => (bool)Field(h, "LateralMotion") && Field(h, "Gate") == null && Field(h, "Rock") == null);
        Assert.That(all.Count(t => t.name == "Desert vulture"), Is.EqualTo(airRocks), "Floating things are living, flying animals.");
        Assert.That(all.Count(t => t.name == "Vulture flock"), Is.EqualTo(airGates));
        Assert.That(all.Count(t => t.name == "Camel caravan"), Is.EqualTo(groundGates), "Long sideways blocks are camel caravans.");
        Assert.That(all.Count(t => t.name == "Crossing camel"), Is.EqualTo(sliders), "Single sliders are camels crossing the road.");
        Assert.That(all.Count(t => t.name == "Desert tumbleweed"), Is.EqualTo(groundRocks));
        Assert.That(all.Any(t => t.name == "Tumbleweed ground shadow"), Is.False, "No circles under obstacles.");
        Assert.That(all.Any(t => t.name == "Sliding police spikes"), Is.False);
        int hoodoos = all.Count(t => t.name.EndsWith("Sandstone hoodoos")), saguaros = all.Count(t => t.name.EndsWith("Saguaro cacti"));
        Assert.That(hoodoos, Is.GreaterThan(0));
        Assert.That(saguaros, Is.GreaterThan(0), "Cactus stands vary the Day/Night closures.");
        Assert.That(all.Any(t => t.name == "STORM road marking"), Is.False);
        foreach (var mover in game.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.GetType().Name == "WorldShiftDesertMover"))
            Assert.That(mover.GetComponentsInChildren<Collider>(true), Is.Empty);
        var views = ((IEnumerable)Field(presentation, "hazards")).Cast<object>().ToArray();
        foreach (var view in views)
        {
            var data = Field(view, "Data");
            bool flying = Field(data, "Rock") != null && (bool)Get(Field(data, "Rock"), "Airborne")
                || Field(data, "Gate") != null && (bool)Get(Field(data, "Gate"), "Airborne");
            if (flying) Assert.That(((Transform)Field(view, "Root")).GetComponentsInChildren<Transform>(true).Any(t => t.name == "Frozen ice base"),
                Is.False, "No floating ice ring under flying animals.");
        }

        // Animals move on the Day clock only, so Night freezes them mid-flap / mid-step.
        object vultureView = views.First(v => Field(Field(v, "Data"), "Rock") != null && ((IList)Field(v, "Movers")).Count > 0);
        var vultureMover = (Component)((IList)Field(vultureView, "Movers"))[0];
        var wing = vultureMover.GetComponentsInChildren<Transform>(true).First(t => t.name == "Vulture right wing");
        var rock = Field(Field(vultureView, "Data"), "Rock");
        Call(game, "ApplyWorld", World("Echo"));
        Call(Field(rock, "Motion"), "Restore", .1f);
        Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Quaternion frozen = wing.localRotation;
        Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Assert.That(Quaternion.Angle(frozen, wing.localRotation), Is.LessThan(.01f));
        Call(Field(rock, "Motion"), "Restore", .3f);
        Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Assert.That(Quaternion.Angle(frozen, wing.localRotation), Is.GreaterThan(5f), "Day time moves the wings.");

        object caravanView = views.First(v => Field(Field(v, "Data"), "Gate") != null && !(bool)Get(Field(Field(v, "Data"), "Gate"), "Airborne"));
        var gateHazard = Field(caravanView, "Data");
        var leg = ((Component)((IList)Field(caravanView, "Movers"))[0]).GetComponentsInChildren<Transform>(true).First(t => t.name == "Camel left leg");
        Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Quaternion still = leg.localRotation;
        Call(gateHazard, "Advance", .4f, 0f, World("Echo")); Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Assert.That(Quaternion.Angle(still, leg.localRotation), Is.LessThan(.01f), "Night freezes the caravan mid-step.");
        Call(gateHazard, "Advance", .2f, 0f, World("Day")); Call(presentation, "UpdateVisibility", Get(game, "Distance"));
        Assert.That(Quaternion.Angle(still, leg.localRotation), Is.GreaterThan(2f), "Camels walk in Day.");
        float travel = (float)TypeFor("WorldShiftSlidingGate").GetField("Travel").GetValue(null);
        Assert.That(travel, Is.LessThanOrEqualTo(4.5f), "Sliding blocks stay close to the road.");
        Call(gateHazard, "ResetMotion");

        var ramps = (System.Array)Field(Get(game, "Course"), "Ramps");
        Call(game, "ApplyWorld", World("Day"));
        Pose(game, (float)Field(ramps.GetValue(0), "Takeoff") + 8f, 0f); Call(game, "PresentWorldForCamera", World("Day")); Capture("desert-vultures-day");
        Pose(game, 750f, 0f); Call(game, "PresentWorldForCamera", World("Day")); Capture("desert-caravan-day");
        Pose(game, 270f, 0f); Call(game, "PresentWorldForCamera", World("Day")); Capture("desert-camel-day");
        var nightFlight = ramps.GetValue(1);
        Pose(game, (float)Field(nightFlight, "Takeoff") + 20f, 0f); Call(game, "PresentWorldForCamera", World("Day")); Capture("desert-flock-day");
        Pose(game, 2180f, 0f); Call(game, "PresentWorldForCamera", World("Echo")); Capture("desert-hoodoo-night");
        object cactusView = views.First(v => ((Transform)Field(v, "Root")).GetComponentsInChildren<Transform>(true).Any(t => t.name.EndsWith("Saguaro cacti")));
        var cactusData = Field(cactusView, "Data");
        string cactusWorld = Field(cactusData, "World").ToString();
        Call(game, "ApplyWorld", World(cactusWorld));
        Pose(game, (float)Field(cactusData, "Start") - 28f, 0f); Call(game, "PresentWorldForCamera", World(cactusWorld)); Capture("desert-saguaros");
        LogAssert.NoUnexpectedReceived();
    }
}
