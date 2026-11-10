using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DesertLevelCourseTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Progress = "WorldShift.Levels.HighestBeaten", Unlock = "WorldShift.Levels.UnlockAll";
    private int savedProgress, savedUnlock;
    private static Type T(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift." + n)).First(t => t != null);
    private static object P(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
    private static object F(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Flags).Invoke(o, a);
    private static object World(string n) => Enum.Parse(T("ShiftWorld"), n);
    private static object[] Items(object a) => ((IEnumerable)a).Cast<object>().ToArray();
    private static float Float(object o, string n) => (float)P(o, n);
    private static MonoBehaviour Game() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(c => c.GetType().Name == "WorldShiftCity");
    private static void Pose(object g, float d, float x)
    {
        g.GetType().GetProperty("Distance", Flags).SetValue(g, d);
        g.GetType().GetProperty("Lateral", Flags).SetValue(g, x);
        Call(F(g, "driver"), "RestoreExternalPose", d, x);
    }
    private static object Course() => T("WorldShiftLevelCourse").GetMethod("For").Invoke(null, new object[] { 2 });
    private static bool Air(object h) => F(h, "Rock") != null && (bool)P(F(h, "Rock"), "Airborne");
    private static float EscapeLane(System.Collections.Generic.List<object> rocks, int first, int count)
    {
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int n = first; n < first + count; n++)
        { min = Mathf.Min(min, Float(rocks[n], "Lateral")); max = Mathf.Max(max, Float(rocks[n], "Lateral")); }
        return min + 3.5f > 3.5f - max ? -3.5f : 3.5f;
    }
    private static void RestoreAt(object game, float at)
    {
        var checkpoints = Items(P(game, "Checkpoints")); int index = 0;
        for (int i = 0; i < checkpoints.Length; i++) if ((float)F(checkpoints[i], "Distance") <= at) index = i;
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, index);
        Call(game, "RestoreCheckpoint");
    }
    private static void Capture(string n)
    {
        var hud = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(c => c.GetType().Name == "WorldShiftLevelHud");
        if (hud != null) Call(hud, "Refresh");
        WorldShiftTestCapture.Capture("desert-level-" + n);
    }

    [SetUp] public void Save() { savedProgress = PlayerPrefs.GetInt(Progress, -1); savedUnlock = PlayerPrefs.GetInt(Unlock); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        T("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, null);
        T("WorldShiftLevels").GetMethod("Leave").Invoke(null, null);
        PlayerPrefs.SetInt(Progress, savedProgress); PlayerPrefs.SetInt(Unlock, savedUnlock); PlayerPrefs.Save();
    }

    [Test]
    public void DesertBendsAlternateWhileEveryJumpHasAStraightApproachAndRecovery()
    {
        var course = Course(); var ramps = Items(F(course, "Ramps"));
        float maxLeft = 0f, maxRight = 0f;
        for (float d = 0f; d <= (float)F(course, "Length"); d += 5f)
        {
            float heading = (float)Call(course, "Heading", d);
            maxLeft = Mathf.Min(maxLeft, heading); maxRight = Mathf.Max(maxRight, heading);
            Assert.That(Mathf.Abs(heading), Is.LessThanOrEqualTo(35f), "Keep the original desert's heading limit.");
        }
        Assert.That(maxLeft, Is.LessThan(-25f)); Assert.That(maxRight, Is.GreaterThan(25f));
        foreach (var ramp in ramps)
            for (float d = (float)F(ramp, "Ramp") - 80f; d <= (float)F(ramp, "End") + 40f; d += 4f)
                Assert.That((float)Call(course, "Heading", d), Is.Zero.Within(.001f), "Jump approaches and landings stay straight at " + d);
    }

    [Test]
    public void SixAerialFieldsKeepClearLandingsFreshMotionAndExactCheckpoints()
    {
        var course = Course(); var hazards = Items(F(course, "Hazards")); var ramps = Items(F(course, "Ramps"));
        Assert.That(F(course, "Length"), Is.EqualTo(3516f));
        Assert.That(hazards.Length, Is.InRange(85, 95));
        Assert.That(Items(F(course, "Checkpoints")).Select(c => (float)F(c, "Distance")),
            Is.EqualTo(new[] { 0f, 810f, 1350f, 1890f, 2160f, 2590f, 3150f }));
        Assert.That(ramps.Select(r => F(r, "Source").ToString()), Is.EqualTo(new[] { "Day", "Echo", "Day", "Echo", "Day", "Day" }));
        int[] expected = { 2, 0, 3, 2, 4, 10 };
        for (int i = 0; i < ramps.Length; i++)
        {
            var balls = hazards.Where(h => Air(h) && ReferenceEquals(F(F(h, "Rock"), "Flight"), ramps[i])).ToArray();
            Assert.That(balls.Length, Is.EqualTo(expected[i]));
            foreach (var h in balls)
            {
                var rock = F(h, "Rock");
                Assert.That(Float(rock, "Distance"), Is.GreaterThan((float)F(ramps[i], "Takeoff") + 20f)
                    .And.LessThanOrEqualTo((float)F(ramps[i], "Landing") - 20f));
                Assert.That(F(h, "SharedAcrossWorlds"), Is.True);
            }
            for (int a = 0; a < balls.Length; a++)
                for (int b = a + 1; b < balls.Length; b++)
                    Assert.That(Mathf.Abs(Float(F(balls[a], "Rock"), "Distance") - Float(F(balls[b], "Rock"), "Distance")),
                        Is.GreaterThanOrEqualTo(6f), "Depth spacing prevents overlap at every motion/freeze phase.");
            if (F(ramps[i], "Source").ToString() == "Day")
            {
                Assert.That(balls.Select(h => Float(F(h, "Rock"), "Lateral")).Distinct().Count(), Is.EqualTo(balls.Length),
                    "Airborne spheres do not spawn in one longitudinal column.");
                Assert.That(balls.Select(h => (float)F(F(F(h, "Rock"), "Motion"), "Direction")).Distinct().Count(), Is.EqualTo(2));
            }
            if (i > 0)
            {
                var retry = Items(F(course, "Checkpoints")).Last(c => (float)F(c, "Distance") < (float)F(ramps[i], "Ramp"));
                Assert.That((float)F(ramps[i], "Ramp") - (float)F(retry, "Distance"), Is.EqualTo(70f));
            }
            if (i == ramps.Length - 1)
            {
                for (int n = 1; n < balls.Length; n++)
                    if (n != 5)
                        Assert.That(Mathf.Abs(Float(F(balls[n], "Rock"), "Lateral") - Float(F(balls[n - 1], "Rock"), "Lateral")),
                            Is.GreaterThan(1f), "Neighbours start visibly staggered, not in a shallow curve.");
                foreach (var h in balls)
                {
                    var rock = F(h, "Rock"); float min = 5f, max = -5f;
                    for (float seconds = 0f; seconds <= 2f; seconds += .04f)
                    {
                        Call(F(rock, "Motion"), "Restore", seconds);
                        min = Mathf.Min(min, Float(rock, "Lateral")); max = Mathf.Max(max, Float(rock, "Lateral"));
                    }
                    Assert.That(min, Is.LessThan(-3.3f)); Assert.That(max, Is.GreaterThan(3.3f), "Each final sphere crosses all three lanes.");
                    Call(F(rock, "Motion"), "Restore", 0f);
                }
                // Each frozen wave leaves an outside passage, including awkward mid-cycle freezes.
                for (float seconds = 0f; seconds <= 2f; seconds += .04f)
                    for (int wave = 0; wave < 2; wave++)
                    {
                        float min = 5f, max = -5f;
                        for (int n = wave * 5; n < wave * 5 + 5; n++)
                        {
                            var rock = F(balls[n], "Rock"); Call(F(rock, "Motion"), "Restore", seconds);
                            min = Mathf.Min(min, Float(rock, "Lateral")); max = Mathf.Max(max, Float(rock, "Lateral"));
                        }
                        Assert.That(min + 3.5f > 1.9f || 3.5f - max > 1.9f, Is.True);
                    }
                foreach (var h in balls) Call(h, "ResetMotion");
            }
        }
        Assert.That(Items(F(course, "Signs")), Is.Empty, "Instruction signs are reserved for Training.");
        var gates = hazards.Where(h => F(h, "Gate") != null).ToArray();
        Assert.That(gates.Length, Is.EqualTo(8));
        Assert.That(gates.Count(h => (bool)P(F(h, "Gate"), "Airborne")), Is.EqualTo(2));
        foreach (var hazard in gates)
        {
            var gate = F(hazard, "Gate"); float gateAt = (float)F(gate, "Distance");
            var flight = F(gate, "Flight"); float activeAt = flight == null ? gateAt - 40f : (float)F(flight, "Takeoff") + 10f;
            if (flight != null) Assert.That(gateAt - (float)F(flight, "Takeoff"), Is.EqualTo(56f));
            else
            {
                Call(hazard, "Advance", 3f, gateAt - 60f, World("Day"));
                Assert.That(Float(gate, "DaySeconds"), Is.Zero, "A distant ground block must not open before the approach.");
            }
            Call(hazard, "Advance", 1f, activeAt, World("Echo"));
            Assert.That(Float(gate, "Lateral"), Is.Zero);
            for (float xLane = -4f; xLane <= 4f; xLane += .25f)
                Assert.That(Call(gate, "Touches", gateAt, gateAt, xLane, xLane, 0f, 0f, 0f, 1.1f), Is.True,
                    "A frozen gate initially closes the road.");
            Call(hazard, "Advance", 1f, activeAt, World("Day"));
            float opened = Float(gate, "Lateral"), exitX = (float)F(gate, "Direction") > 0f ? -3.3f : 3.3f;
            Assert.That(Call(gate, "Touches", gateAt, gateAt, exitX, exitX, 0f, 0f, opened, 1.1f), Is.False);
            Call(hazard, "Advance", 1f, activeAt, World("Echo"));
            Assert.That(Float(gate, "Lateral"), Is.EqualTo(opened), "Night holds an opened block in place.");
            float toEdge = 11f / (float)F(gate, "Speed");
            Call(hazard, "Advance", toEdge - 1f, activeAt, World("Day"));
            Assert.That(Mathf.Abs(Float(gate, "Lateral")), Is.EqualTo(11f).Within(.001f));
            Call(hazard, "Advance", .5f, activeAt, World("Day"));
            Assert.That(Mathf.Abs(Float(gate, "Lateral")), Is.LessThan(11f), "The block returns from the roadside.");
            Call(hazard, "Advance", toEdge - .5f, activeAt, World("Day"));
            Assert.That(Float(gate, "Lateral"), Is.EqualTo(0f).Within(.001f), "It completes a repeatable out-and-back cycle.");
            Call(hazard, "ResetMotion");
            Assert.That(Float(gate, "Lateral"), Is.Zero);
        }
        Assert.That(hazards.Count(h => F(h, "Rock") != null && !Air(h)
            && Float(F(h, "Rock"), "Distance") >= 2200f && Float(F(h, "Rock"), "Distance") < 2595f), Is.EqualTo(19));
        Assert.That(gates.Count(h => (float)F(F(h, "Gate"), "Distance") >= 2200f
            && (float)F(F(h, "Gate"), "Distance") < 2595f), Is.EqualTo(2));
        var halfBlocks = hazards.Where(h => F(h, "Kind").ToString() == "HalfBarrier"
            && (float)F(h, "Start") >= 2200f && (float)F(h, "Start") < 2595f).ToArray();
        Assert.That(halfBlocks.Select(h => (int)F(h, "HalfSide")), Is.EqualTo(new[] { 1, -1, 1 }));
        Assert.That(halfBlocks.All(h => F(h, "World").ToString() == "Echo" && (int)F(h, "LaneMask") != 7), Is.True);
        var fresh = Items(Call(course, "CreateHazards")); var original = hazards.First(h => F(h, "Rock") != null);
        var copy = fresh.First(h => F(h, "Rock") != null);
        Call(copy, "Advance", .5f, (float)F(copy, "Start") - 30f, World("Day"));
        Assert.That(Float(F(F(original, "Rock"), "Motion"), "DaySeconds"), Is.Zero);
        var ground = F(copy, "Rock");
        float x = Float(ground, "Lateral"), at = Float(ground, "Distance");
        Assert.That(Call(ground, "Touches", at - 1f, at + 1f, x, x, 0f, 0f, x, 1f), Is.True);
        Assert.That(Call(ground, "Touches", at - 1f, at + 1f, x, x, 10f, 10f, x, 1f), Is.False, "A ground rock cannot hit a high-flying car.");
    }

    [UnityTest]
    public IEnumerator RockFreezeAirContactsAndAllSixLandingsUseTheSharedLevelRules()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Action<string, string, System.Collections.Generic.Dictionary<string, object>> sink = (sheet, ev, row) => { };
        T("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
        T("WorldShiftLevels").GetProperty("UnlockAll").SetValue(null, true);
        T("WorldShiftLevels").GetMethod("Play").Invoke(null, new object[] { 2, "menu" });
        yield return null; yield return null;
        var game = Game(); game.enabled = false;
        var hazards = Items(P(game, "Hazards"));
        Call(game, "StepSimulation", .4f, 0f, false);
        var ground = F(hazards.First(h => F(h, "Rock") != null), "Rock");
        float clock = Float(F(ground, "Motion"), "DaySeconds"); Assert.That(clock, Is.GreaterThan(0f));
        Call(game, "TrySwitch"); Call(game, "StepSimulation", .4f, 0f, false);
        Assert.That(Float(F(ground, "Motion"), "DaySeconds"), Is.EqualTo(clock));
        Call(game, "TogglePause"); Call(game, "StepSimulation", 2f, 1f, true);
        Assert.That(Float(F(ground, "Motion"), "DaySeconds"), Is.EqualTo(clock)); Call(game, "TogglePause");
        Pose(game, 45f, -3.3f); Call(game, "PresentWorldForCamera", World("Echo")); Capture("ground-night");
        var ramps = Items(F(P(game, "Course"), "Ramps"));
        var route = P(game, "Route");
        foreach (var ramp in ramps)
        {
            Vector3 launch = Vector3.ProjectOnPlane((Vector3)P(Call(route, "Sample", (float)F(ramp, "Takeoff")), "Forward"), Vector3.up);
            for (float at = (float)F(ramp, "Ramp") - 75f; at <= (float)F(ramp, "End") + 40f; at += 5f)
            {
                Vector3 forward = Vector3.ProjectOnPlane((Vector3)P(Call(route, "Sample", at), "Forward"), Vector3.up);
                Assert.That(Vector3.Angle(launch, forward), Is.LessThan(.1f), "Rendered road is straight through jump at " + at);
            }
        }
        Pose(game, 90f, 0f); Call(game, "ApplyWorld", World("Day")); Capture("ground-bends");
        for (int i = 0; i < ramps.Length; i++)
        {
            RestoreAt(game, (float)F(ramps[i], "Ramp"));
            bool day = F(ramps[i], "Source").ToString() == "Day";
            var flightRocks = new System.Collections.Generic.List<object>();
            foreach (var hazard in hazards)
                if (Air(hazard) && ReferenceEquals(F(F(hazard, "Rock"), "Flight"), ramps[i])) flightRocks.Add(F(hazard, "Rock"));
            // Different Day exposure before each ramp must still leave a readable frozen bypass.
            if (day) foreach (var rock in flightRocks) Call(F(rock, "Motion"), "Restore", .7f + i * .4f);
            if (!P(game, "World").Equals(F(ramps[i], "Source"))) Call(game, "TrySwitch");
            float dayLane = i == 0 ? 3.3f : i == 5 ? EscapeLane(flightRocks, 0, 5) : 1.7f;
            Pose(game, (float)F(ramps[i], "Takeoff") - .4f, day ? dayLane : i == 3 ? 3.3f : -3.3f);
            Call(game, "StepSimulation", .02f, 0f, false);
            Assert.That(P(game, "IsRelayAirborne"), Is.True);
            // A Night launch now gives time to read the wall instead of demanding a same-frame switch.
            if (!day)
            {
                Call(game, "StepSimulation", .45f, 0f, false);
                if (i == 1) Capture("night-sliding-wall");
            }
            Call(game, "TrySwitch"); // Day launches freeze; Night launches release the blocked row.
            float secondLane = i == 5 ? EscapeLane(flightRocks, 5, 5) : -dayLane;
            for (int n = 0; n < 320 && Float(game, "Distance") < (float)F(ramps[i], "End") + 1f
                && P(game, "State").ToString() == "Running"; n++)
            {
                float turnAt = i == 2 ? 44f : i == 4 ? 32f : 68f;
                float target = day && i > 0 && Float(game, "Distance") > (float)F(ramps[i], "Takeoff") + turnAt
                    ? secondLane : day ? dayLane : i == 3 ? 3.3f : -3.3f;
                float steer = Mathf.Clamp((target - Float(game, "Lateral")) * 2f, -1f, 1f);
                Call(game, "StepSimulation", .02f, steer, false);
                if (n == 28 && i == 0) Capture("air-night");
                if (n == 55 && i == 5) Capture("final-ten-balls");
            }
            Assert.That(P(game, "State").ToString(), Is.EqualTo(i == ramps.Length - 1 ? "Won" : "Running"),
                "Flight " + (i + 1) + " at " + Float(game, "Distance") + ", lateral " + Float(game, "Lateral") + ": " + F(game, "lastFailure"));
            Assert.That(P(game, "IsRelayAirborne"), Is.False);
        }
        // Contact with the visible sphere while airborne is fatal, even in frozen Night.
        RestoreAt(game, 0f);
        Pose(game, (float)F(ramps[0], "Takeoff") - .4f, -3.3f); Call(game, "StepSimulation", .02f, 0f, false); Call(game, "TrySwitch");
        var airborne = F(hazards.First(Air), "Rock");
        Pose(game, Float(airborne, "Distance"), Float(airborne, "Lateral"));
        Call(game, "StepSimulation", .01f, 0f, false);
        Assert.That(P(game, "State").ToString(), Is.EqualTo("Retrying"));
        for (int i = 0; i < 100 && P(game, "State").ToString() != "Running"; i++) Call(game, "StepSimulation", .03f, 0f, false);
        Assert.That(Float(game, "Distance"), Is.EqualTo(0f));
        Assert.That(Float(F(ground, "Motion"), "DaySeconds"), Is.Zero);
        Pose(game, 255f, 0f); Call(game, "PresentWorldForCamera", World("Day")); Capture("first-freeze-sign");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator NightLaunchNeedsDayAndDustFadesOutOnCheckpointRetry()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        Action<string, string, System.Collections.Generic.Dictionary<string, object>> sink = (sheet, ev, row) => { };
        T("WorldShiftLevelAnalytics").GetField("Sink", Flags).SetValue(null, sink);
        T("WorldShiftLevels").GetProperty("UnlockAll").SetValue(null, true);
        T("WorldShiftLevels").GetMethod("Play").Invoke(null, new object[] { 2, "menu" });
        yield return null; yield return null;
        var game = Game(); game.enabled = false;
        var ramps = Items(F(P(game, "Course"), "Ramps"));
        RestoreAt(game, 810f);
        Pose(game, (float)F(ramps[1], "Takeoff") - .4f, -3.3f);
        for (int i = 0; i < 100 && P(game, "State").ToString() == "Running"; i++) Call(game, "StepSimulation", .02f, 0f, false);
        Assert.That(P(game, "State").ToString(), Is.EqualTo("Retrying"), "Staying frozen cannot bypass the sliding wall.");
        for (int i = 0; i < 100 && P(game, "State").ToString() != "Running"; i++) Call(game, "StepSimulation", .03f, 0f, false);
        Assert.That(Float(game, "Distance"), Is.EqualTo(810f));
        RestoreAt(game, 2160f);
        var dunes = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("Prototype desert dune ")).ToArray();
        Assert.That(dunes.Length, Is.EqualTo(0), "No decorative scenery: the desert has no dunes.");
        var labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int movingBlocks = Items(P(game, "Hazards")).Count(h => (bool)F(h, "LateralMotion") && F(h, "Rock") == null);
        Assert.That(labels.Count(t => t.text == "DAY TO MOVE"), Is.EqualTo(movingBlocks), "Every moving block uses the same cue.");
        var stormPaint = labels.Single(t => t.text == "STORM");
        Assert.That(Vector3.Dot(-stormPaint.transform.forward, Vector3.up), Is.GreaterThan(.99f), "STORM is painted facing upward on the road.");
        Pose(game, 110f, 0f); Call(game, "ApplyWorld", World("Day")); Capture("desert-plain-day");
        Pose(game, 2186f, 0f); Call(game, "ApplyWorld", World("Day")); Capture("desert-storm-entry");
        Pose(game, 2265f, 3.3f); Call(game, "ApplyWorld", World("Echo")); Capture("storm-night-half");
        Pose(game, 2390f, -3.3f); Call(game, "UpdateCamera", true); Call(game, "ApplyWorld", World("Day"));
        Assert.That(RenderSettings.fogEndDistance, Is.InRange(70f, 90f)); Capture("sandstorm-day");
        Call(game, "ApplyWorld", World("Echo"));
        Assert.That(RenderSettings.fogEndDistance, Is.InRange(70f, 90f)); Capture("sandstorm-night");
        float fog = RenderSettings.fogEndDistance, distance = Float(game, "Distance");
        Call(game, "TogglePause"); Call(game, "StepSimulation", 2f, 1f, true);
        Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(fog)); Assert.That(Float(game, "Distance"), Is.EqualTo(distance));
        Call(game, "TogglePause");
        foreach (var checkpoint in Items(P(game, "Checkpoints")))
        {
            RestoreAt(game, (float)F(checkpoint, "Distance"));
            Call(game, "StepSimulation", .5f, 0f, false);
            Assert.That(P(game, "State").ToString(), Is.EqualTo("Running"),
                "Respawn has immediate reaction space at " + F(checkpoint, "Distance"));
        }
        RestoreAt(game, 2160f);
        Call(game, "RestoreCheckpoint");
        Assert.That(Float(game, "Distance"), Is.EqualTo(2160f));
        Assert.That(RenderSettings.fogEndDistance, Is.GreaterThan(140f), "Retry starts before the dust fade-in.");
        Assert.That((float)T("WorldShiftLevelSandstorm").GetMethod("IntensityAt").Invoke(null,
            new object[] { P(game, "Course"), 2650f }), Is.Zero, "Dust is clear before the ramp.");
        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(t => t.name == "Checkpoint gate"), Is.EqualTo(6));
        Pose(game, 2589f, 0f); Call(game, "StepSimulation", .08f, 0f, false);
        Assert.That(Float(game, "CheckpointNoticeRemaining"), Is.GreaterThan(2f), "Passing a real checkpoint shows its confirmation.");
        Capture("checkpoint-reached");
        var notice = UnityEngine.Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None).Single(c => c.name == "Checkpoint notice");
        Assert.That(notice.alpha, Is.EqualTo(1f));
        Call(game, "TogglePause"); float remaining = Float(game, "CheckpointNoticeRemaining");
        Call(game, "StepSimulation", 3f, 0f, false);
        Assert.That(Float(game, "CheckpointNoticeRemaining"), Is.EqualTo(remaining));
        Call(game, "TogglePause"); Call(game, "StepSimulation", 2.3f, 0f, false);
        Assert.That(Float(game, "CheckpointNoticeRemaining"), Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }
}
