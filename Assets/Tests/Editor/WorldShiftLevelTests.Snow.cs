using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Snow: black ice stretches and a harder five-section layout.
public sealed partial class WorldShiftLevelTests
{
    // Found by environment so a campaign reorder never points these tests at another level.
    private static int SnowLevel => Array.IndexOf((Array)Levels.GetField("Environments", Flags).GetValue(null),
        Enum.Parse(TypeFor("ShiftLevel"), "Snow"));

    private static object SnowCourse() => Static(TypeFor("WorldShiftLevelCourse"), "For", SnowLevel);
    private static float SectionStart(object[] ramps, int section) => section == 0 ? 0f : (float)Field(ramps[section - 1], "End");

    [Test]
    public void SnowIceStaysOffEveryJumpAndOnlyAppearsInSnow()
    {
        var course = SnowCourse();
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        var ice = (Vector2[])Field(course, "IceZones");
        Assert.That(ramps.Select(r => Field(r, "Source").ToString()), Is.EqualTo(new[] { "Day", "Echo", "Day", "Echo", "Day" }));
        Assert.That(ice.Length, Is.EqualTo(5), "One ice stretch per section.");
        for (int i = 0; i < ice.Length; i++)
        {
            float start = SectionStart(ramps, i), ramp = (float)Field(ramps[i], "Ramp");
            Assert.That(ice[i].x, Is.GreaterThanOrEqualTo(start + 40f), $"Ice {i} starts clear of the landing.");
            Assert.That(ice[i].y, Is.LessThanOrEqualTo(ramp - 70f), $"Ice {i} ends well before the ramp approach.");
            Assert.That(ice[i].y - ice[i].x, Is.GreaterThanOrEqualTo(120f), "Ice stretches are long enough to matter.");
            Assert.That((bool)Call(course, "IceAt", (ice[i].x + ice[i].y) * .5f), Is.True);
            Assert.That((bool)Call(course, "IceAt", ice[i].x - 1f), Is.False);
            Assert.That((bool)Call(course, "IceAt", ice[i].y + 1f), Is.False);
        }
        var hazards = ((Array)Field(course, "Hazards")).Cast<object>().ToArray();
        Assert.That(hazards.Length, Is.GreaterThanOrEqualTo(85), "Snow is a dense, late-campaign level.");
        Assert.That(hazards.Where(h => (bool)Field(h, "LateralMotion") || Field(h, "Rock") != null || Field(h, "Gate") != null
            || (Field(h, "Kind").ToString() == "Oncoming" && (bool)Field(h, "SharedAcrossWorlds"))), Is.Empty,
            "No obstacles that only move in Day: everything is steering or Space timing.");
        // A spot belongs to Day or to Night, never both: switching always reveals a different layout.
        // One-world traffic counts where it meets the player.
        float Spot(object h)
        {
            float start = (float)Field(h, "Start"), speed = (float)Field(h, "MotionSpeed") / 28f;
            return Field(h, "Kind").ToString() == "Oncoming" ? start - 150f * speed / (1f + speed) : start;
        }
        var oneWorld = hazards.Where(h => !(bool)Field(h, "SharedAcrossWorlds")).ToArray();
        foreach (var day in oneWorld.Where(h => Field(h, "World").ToString() == "Day"))
            foreach (var night in oneWorld.Where(h => Field(h, "World").ToString() == "Echo"))
                Assert.That(Mathf.Abs(Spot(day) - Spot(night)), Is.GreaterThanOrEqualTo(20f),
                    $"Day obstacle at {Spot(day)} and Night obstacle at {Spot(night)} share a spot.");
        for (int level = 0; level < 6; level++)
            if (level != SnowLevel)
                Assert.That((Vector2[])Field(Static(TypeFor("WorldShiftLevelCourse"), "For", level), "IceZones"), Is.Empty, "level " + level);
    }

    [UnityTest]
    public IEnumerator SnowIceKeepsSlidesGoingUntilYouCountersteer()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(SnowLevel); yield return WaitForNavigation();
        var game = Started();
        var ramps = ((Array)Field(SnowCourse(), "Ramps")).Cast<object>().ToArray();
        Assert.That(GameObject.Find("Snow black ice"), Is.Not.Null, "Ice is drawn on the road.");

        // Same inputs on clear stretches of section 3 (Day): dry road between zipper gates, then ice.
        float Slide(float at, object world, out bool onIce, int countersteer)
        {
            Pose(game, at, -3.5f); Call(game, "ApplyWorld", world);
            for (int n = 0; n < 30; n++) Call(game, "StepSimulation", .02f, 1f, false);
            onIce = (bool)Get(game, "OnIce");
            for (int n = 0; n < 20; n++) Call(game, "StepSimulation", .02f, -countersteer, false);
            float before = (float)Get(game, "Lateral");
            for (int n = 0; n < 5; n++) Call(game, "StepSimulation", .02f, -countersteer, false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            return ((float)Get(game, "Lateral") - before) / .1f;
        }
        float dryDrift = Slide(SectionStart(ramps, 2) + 77f, World("Day"), out bool dryIce, 0);
        float iceDrift = Slide(SectionStart(ramps, 2) + 264f, World("Day"), out bool onIce, 0);
        float iceBraked = Slide(SectionStart(ramps, 2) + 264f, World("Day"), out _, 1);
        Assert.That(dryIce, Is.False); Assert.That(onIce, Is.True);
        // Bends add a little outward drift on any surface, so compare ice against dry road.
        Assert.That(iceDrift, Is.GreaterThan(dryDrift + 1.5f), "Ice keeps sliding after you let go; dry road stops.");
        Assert.That(iceBraked, Is.LessThan(iceDrift - 1.5f), "Countersteering brakes a slide on ice.");

        // Approaching ice in each world, and driving on it (OUTLAWS_CAMPAIGN_PREVIEW folder).
        var ice = (Vector2[])Field(SnowCourse(), "IceZones");
        // A frame between shots lets the ice pick up the world's look in LateUpdate.
        Pose(game, ice[4].x - 24f, 3.3f); Call(game, "ApplyWorld", World("Day")); yield return null; Capture("snow-ice-approach-day");
        Pose(game, ice[1].x - 24f, 0f); Call(game, "ApplyWorld", World("Echo")); yield return null; Capture("snow-ice-approach-night");
        Pose(game, ice[3].x + 120f, -3.3f); Call(game, "ApplyWorld", World("Echo")); yield return null; Capture("snow-ice-slalom-night");
        Call(game, "ApplyWorld", World("Day")); yield return null; Capture("snow-ice-slalom-day");
    }

    // A scripted driver using only left/right and Space clears the whole level, so nothing is impossible.
    private enum SnowAction { Lane, World, FreezeSide, WorldBySide }
    private readonly struct SnowStep
    {
        public readonly float At; public readonly SnowAction Action; public readonly float Value;
        public SnowStep(float at, SnowAction action, float value = 0f) { At = at; Action = action; Value = value; }
    }
    private static SnowStep L(float at, float x) => new SnowStep(at, SnowAction.Lane, x);
    private static SnowStep Day(float at) => new SnowStep(at, SnowAction.World, 0f);
    private static SnowStep Night(float at) => new SnowStep(at, SnowAction.World, 1f);
    private static SnowStep Freeze(float at) => new SnowStep(at, SnowAction.FreezeSide);
    private static SnowStep BySide(float at) => new SnowStep(at, SnowAction.WorldBySide);

    private static readonly SnowStep[][] SnowLine =
    {
        new[] { Day(0f), L(0f, 3.5f), Night(60f), Day(84f), Night(108f), L(129f, -3.9f), Day(180f), Night(210f),
            L(205f, 3.9f), Day(250f), L(262f, -3.9f), Night(325f), L(323f, .2f), Day(350f), L(364f, 3.4f) },
        new[] { Day(45f), L(30f, 0f), Night(122f), L(118f, 3.5f), Day(200f), L(198f, 0f), Night(226f),
            L(248f, -3.9f), Day(295f), L(308f, 3.9f), Night(372f) },
        new[] { Day(0f), L(0f, 3.5f), Night(55f), Day(82f), L(77f, 0f), Night(120f), Day(150f), L(142f, -3.9f),
            Night(205f), L(194f, 0f), Day(240f), Night(288f), L(308f, -3.9f), Day(365f), L(361f, 0f) },
        new[] { Night(0f), L(20f, 3.5f), Day(110f), L(128f, -3.9f), Night(150f), Day(190f), Night(228f),
            Day(258f), L(253f, 3.9f), Night(318f) },
        new[] { Day(0f), L(0f, 3.5f), Night(100f), Day(124f), L(146f, -3.9f), Night(152f), Day(205f), L(203f, 0f),
            Night(285f), L(278f, -3.9f), Day(340f), Night(385f), L(408f, 3.9f), Day(420f) },
    };

    [UnityTest]
    public IEnumerator ScriptedDriverClearsEverySnowSectionWithKeyboardInputs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(SnowLevel); yield return WaitForNavigation();
        var game = Started();
        var course = Get(game, "Course");
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        var hazards = ((IEnumerable)Get(game, "Hazards")).Cast<object>().ToArray();
        float length = (float)Field(course, "Length");
        float lastX = (float)Get(game, "Lateral");
        int iceSteps = 0;
        // Decisions are made once (like a player committing), then held.
        var decided = new Dictionary<(int, int), float>();
        for (int n = 0; n < 20000 && Get(game, "State").ToString() == "Running"; n++)
        {
            float d = (float)Get(game, "Distance"), x = (float)Get(game, "Lateral");
            int section = 0;
            while (section < ramps.Length - 1 && d >= (float)Field(ramps[section], "End")) section++;
            float offset = d - SectionStart(ramps, section);
            string world = Get(game, "World").ToString();
            bool airborne = (bool)Get(game, "IsRelayAirborne");
            float target = 0f; string wanted = null;
            for (int s = 0; s < SnowLine[section].Length; s++)
            {
                var step = SnowLine[section][s];
                if (offset < step.At) continue;
                if (step.Action == SnowAction.Lane) target = step.Value;
                else if (step.Action == SnowAction.World) wanted = step.Value > 0f ? "Echo" : "Day";
                else if (step.Action == SnowAction.WorldBySide)
                {
                    if (!decided.TryGetValue((section, s), out float side)) decided[(section, s)] = side = x;
                    wanted = side > 0f ? "Day" : "Echo";
                }
                else if (decided.TryGetValue((section, s), out float held)) target = held;
                else
                {
                    // Hug the wall on the side the frozen slider leaves open.
                    var slider = hazards.Where(h => (bool)Field(h, "LateralMotion") && Field(h, "Gate") == null && Field(h, "Rock") == null
                        && (float)Field(h, "End") > d - 2f && (float)Field(h, "Start") < d + 90f)
                        .OrderBy(h => (float)Field(h, "Start")).FirstOrDefault();
                    if (slider != null && world == "Echo")
                        decided[(section, s)] = target = (float)Get(slider, "LateralOffset") > 0f ? -3.75f : 3.75f;
                }
            }
            var ramp = ramps[section];
            if (airborne && world == Field(ramp, "Source").ToString() && d > (float)Field(ramp, "Takeoff") + 8f) Call(game, "TrySwitch");
            else if (!airborne && wanted != null && world != wanted && d < (float)Field(ramp, "Ramp") - 5f) Call(game, "TrySwitch");
            // Keyboard steering only (-1, 0, +1): aim for a lateral speed, brake by countersteering.
            float speed = (x - lastX) / .02f;
            float aim = Mathf.Abs(target) >= 3.8f ? target * 1.4f : target;
            float error = Mathf.Clamp((aim - x) * 2.2f, -8f, 8f) - speed;
            float steer = error > .5f ? 1f : error < -.5f ? -1f : 0f;
            lastX = x;
            Call(game, "StepSimulation", .02f, steer, false);
            if ((bool)Get(game, "OnIce")) iceSteps++;
            string state = Get(game, "State").ToString();
            if (state != "Running" && state != "Won")
                Assert.Fail($"Crashed ({Field(game, "lastFailure")}) in section {section + 1} at offset {offset:0.0} m, x={x:0.00}, world={world}.");
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"), $"Reached {Get(game, "Distance")} of {length}.");
        Assert.That(iceSteps * .02f, Is.GreaterThan(25f), "A large part of the level is driven on ice.");
    }
}
