using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Mountain "Rockslide Pass": medium difficulty, rolling and crossing boulders, guardrail gaps on bends.
public sealed partial class WorldShiftLevelTests
{
    private static int MountainLevel => Array.IndexOf((Array)Levels.GetField("Environments", Flags).GetValue(null),
        Enum.Parse(TypeFor("ShiftLevel"), "Mountain"));

    private static object MountainCourse() => Static(TypeFor("WorldShiftLevelCourse"), "For", MountainLevel);

    [Test]
    public void MountainIsAMediumRockslideLevel()
    {
        var course = MountainCourse();
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        Assert.That(ramps.Select(r => Field(r, "Source").ToString()), Is.EqualTo(new[] { "Day", "Echo", "Day", "Echo", "Day" }));
        var hazards = ((Array)Field(course, "Hazards")).Cast<object>().ToArray();
        // Medium-hard: clearly busier than City and Ocean, still below Snow and Desert.
        Assert.That(hazards.Length, Is.InRange(62, 68));
        var movers = hazards.Where(h => (bool)Field(h, "SharedAcrossWorlds") && (float)Field(h, "MotionSpeed") > 0f).ToArray();
        Assert.That(movers.Length, Is.GreaterThanOrEqualTo(15), "A rockslide level: most of the challenge moves.");
        foreach (var h in hazards)
        {
            string kind = Field(h, "Kind").ToString();
            Assert.That(kind, Is.Not.EqualTo("SlidingGate"), "No spike platforms or sliding blocks in Mountain.");
            if ((bool)Field(h, "LateralMotion"))
            {
                Assert.That(Field(h, "Rock"), Is.Not.Null, "Sideways movers are goats crossing the road (Rock hazards), not sliders.");
                if ((bool)Get(Field(h, "Rock"), "Airborne"))
                    Assert.That(Field(Field(h, "Rock"), "Flight") != null, Is.True, "Only eagles over jumps fly.");
                Assert.That((bool)Field(h, "MovesWheneverDay"), Is.True, "Rockfall moves whenever it is Day (visual rule 4).");
            }
            if (kind == "Oncoming")
                Assert.That((bool)Field(h, "SharedAcrossWorlds"), Is.True, "No one-world traffic; oncoming hazards are boulders.");
        }
    }

    [Test]
    public void MountainGuardrailGapsSitOnTheValleySideOfBendsBesideForcedRightLanes()
    {
        var course = MountainCourse();
        var gaps = (Vector3[])Field(course, "EdgeGaps");
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        var checkpoints = ((Array)Field(course, "Checkpoints")).Cast<object>().ToArray();
        var hazards = ((Array)Field(course, "Hazards")).Cast<object>().ToArray();
        Assert.That(gaps.Length, Is.GreaterThanOrEqualTo(3));
        foreach (var gap in gaps)
        {
            Assert.That(gap.z, Is.EqualTo(1f), "Gaps open onto the downhill valley (right), never the rock face.");
            Assert.That(gap.y - gap.x, Is.InRange(20f, 30f));
            // Heading falls through the gap: the road bends left, so the valley side is the outside of the bend.
            for (float d = gap.x; d <= gap.y; d += 4f)
                Assert.That((float)Call(course, "Heading", d + 2f) - (float)Call(course, "Heading", d - 2f), Is.LessThan(-.6f),
                    $"Gap at {gap.x} must be on the outside of a real bend (at {d}).");
            foreach (var ramp in ramps)
                Assert.That(gap.y < (float)Field(ramp, "Ramp") - 60f || gap.x > (float)Field(ramp, "End") + 40f, Is.True, $"Gap at {gap.x} near a jump.");
            foreach (var checkpoint in checkpoints)
                Assert.That(gap.x - (float)Field(checkpoint, "Distance"), Is.Not.InRange(0f, 40f), $"Gap at {gap.x} right after a respawn.");
            // Playtest request (deliberately loosened from "no forced edge near a gap"): each gap sits beside an obstacle
            // that forces the right lane, so dodging means passing the drop. The right lane itself stays safe.
            Assert.That(hazards.Any(h => ForcesRightLane(h) && (float)Field(h, "End") > gap.x - 10f && (float)Field(h, "Start") < gap.y + 10f),
                Is.True, $"Gap at {gap.x} sits beside an obstacle that forces the right lane.");
            // Goats never cross right at the drop.
            foreach (var h in hazards.Where(h => Field(h, "Rock") != null))
            {
                float at = (float)Get(Field(h, "Rock"), "Distance");
                Assert.That(at + 1f < gap.x - 15f || at - 1f > gap.y + 15f, Is.True, $"Gap at {gap.x} too close to a goat crossing at {at}.");
            }
        }
        for (int level = 0; level < 6; level++)
            if (level != MountainLevel)
                Assert.That((Vector3[])Field(Static(TypeFor("WorldShiftLevelCourse"), "For", level), "EdgeGaps"), Is.Empty, "level " + level);
    }

    // Blocks the left and middle lanes (a LeftMiddle wall or a left half barrier) in its world.
    private static bool ForcesRightLane(object h)
    {
        string kind = Field(h, "Kind").ToString();
        if (kind == "HalfBarrier") return (int)Field(h, "HalfSide") < 0;
        return kind == "Barrier" && (int)Field(h, "LaneMask") == 3;
    }

    [UnityTest]
    public IEnumerator DrivingOffAGuardrailGapFallsToTheCheckpointButHoldingTheLaneIsSafe()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        // Section 2's gap: its rolling boulders are still well ahead when the car is placed here.
        var gap = ((Vector3[])Field(MountainCourse(), "EdgeGaps"))[1];
        Assert.That(GameObject.Find("Torn guardrail and chevron post"), Is.Not.Null, "Both ends of a gap are marked.");

        // Holding the right lane through the gap, with normal corrections, is safe.
        Pose(game, gap.x - 8f, 3.4f);
        float lastX = 3.4f;
        while ((float)Get(game, "Distance") < gap.y + 4f)
        {
            float x = (float)Get(game, "Lateral"), error = Mathf.Clamp((3.4f - x) * 2.2f, -8f, 8f) - (x - lastX) / .02f;
            lastX = x;
            Call(game, "StepSimulation", .02f, error > .5f ? 1f : error < -.5f ? -1f : 0f, false);
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"), $"Lane holding fell at {Get(game, "Distance")}: {Field(game, "lastFailure")}");
        }

        // Outside a gap the rail still holds the car on the road.
        Pose(game, gap.x - 30f, 3.4f);
        for (int n = 0; n < 40 && (float)Get(game, "Distance") < gap.x - 4f; n++) Call(game, "StepSimulation", .02f, 1f, false);
        Assert.That((float)Get(game, "Lateral"), Is.LessThanOrEqualTo(4.01f));

        // Steering into the gap goes over the edge: OFF THE ROAD, no police capture, then the section checkpoint.
        Call(game, "RetryCheckpoint");
        Pose(game, gap.x + 2f, 3.4f);
        for (int n = 0; n < 60 && Get(game, "State").ToString() == "Running"; n++) Call(game, "StepSimulation", .02f, 1f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That(Get(game, "FailureReason"), Is.EqualTo("NO ROAD"));
        Assert.That((bool)Get(game, "Capturing"), Is.False);
        Assert.That((float)Get(game, "Lateral"), Is.GreaterThan(WorldShiftLevelRoadHalfWidth));
        Assert.That((float)Get(game, "Distance"), Is.InRange(gap.x, gap.y));
        // The camera watches the car drop into the valley before the OFF THE ROAD card appears.
        Assert.That((bool)Get(game, "GapFall"), Is.True);
        Assert.That((bool)Get(game, "WatchingGapFall"), Is.True);
        for (int n = 0; n < 30; n++) Call(game, "StepSimulation", .02f, 0f, false);
        yield return null;
        Assert.That(Find("WorldShiftLevelHud").transform.GetComponentsInChildren<Transform>(false).Any(t => t.name == "Checkpoint retry notice"),
            Is.False, "No card while the camera follows the fall.");
        Capture("mountain-guardrail-gap-fall-camera");
        for (int n = 0; n < 40; n++) Call(game, "StepSimulation", .02f, 0f, false);
        Assert.That((bool)Get(game, "WatchingGapFall"), Is.False);
        yield return null;
        Capture("mountain-guardrail-gap-fall");
        float checkpoint = (float)Field(((IEnumerable)Get(game, "Checkpoints")).Cast<object>().ElementAt((int)Get(game, "CheckpointIndex")), "Distance");
        for (int n = 0; n < 200 && Get(game, "State").ToString() != "Running"; n++) Call(game, "StepSimulation", .02f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(checkpoint).Within(.5f));
        Assert.That(Mathf.Abs((float)Get(game, "Lateral")), Is.LessThan(4.01f));
    }

    private const float WorldShiftLevelRoadHalfWidth = 5f;

    [UnityTest]
    public IEnumerator HittingAMountainEagleSendsTheCarBackToTheCheckpoint()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        yield return FlyIntoMountainEagle();
    }

    private IEnumerator FlyIntoMountainEagle()
    {
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        // The section 2 jump takes off in Night: its eagle hangs frozen, so flying straight at it must crash.
        var jump = ((Array)Field(Get(game, "Course"), "Ramps")).GetValue(1);
        var eagle = ((Array)Get(game, "Hazards")).Cast<object>().Select(h => Field(h, "Rock"))
            .First(r => r != null && (bool)Get(r, "Airborne") && Field(r, "Flight") == jump);
        float eagleAt = (float)Get(eagle, "Distance");
        Pose(game, (float)Field(jump, "Ramp") - 30f, 0f); Call(game, "ApplyWorld", Field(jump, "Source"));
        for (int n = 0; n < 800 && Get(game, "State").ToString() == "Running"; n++)
        {
            float error = (float)Get(eagle, "Lateral") - (float)Get(game, "Lateral");
            Call(game, "StepSimulation", .02f, Mathf.Clamp(error * 2f, -1f, 1f), false);
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Flying into the eagle is a crash.");
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(eagleAt).Within(4f), "The crash happens at the eagle.");
        Assert.That((bool)Get(game, "IsRelayAirborne"), Is.True, "Hit in the air, not a missed landing.");
        float checkpoint = (float)Field(((IEnumerable)Get(game, "Checkpoints")).Cast<object>().ElementAt((int)Get(game, "CheckpointIndex")), "Distance");
        Assert.That(checkpoint, Is.LessThan((float)Field(jump, "Ramp")));
        for (int n = 0; n < 300 && Get(game, "State").ToString() != "Running"; n++) Call(game, "StepSimulation", .02f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(checkpoint).Within(.5f), "Back at the section checkpoint.");
        Assert.That((bool)Get(game, "IsRelayAirborne"), Is.False);
    }

    [UnityTest]
    public IEnumerator MountainRockslideBouldersRollAndFreeze()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        Assert.That(GameObject.Find("Rolling mountain boulder"), Is.Not.Null, "Rockslides are boulders, not vans.");
        var hazards = ((IEnumerable)Get(game, "Hazards")).Cast<object>().ToArray();
        var boulder = hazards.First(h => Field(h, "Kind").ToString() == "Oncoming");
        float start = (float)Field(boulder, "Start");
        Pose(game, start - 120f, 3.4f); Call(game, "ApplyWorld", World("Day"));
        Step(game, .4f);
        float rolled = (float)Get(boulder, "MotionTravel");
        Assert.That(rolled, Is.GreaterThan(1f), "Day rolls the boulder toward you.");
        yield return null; Capture("mountain-rockslide-day");
        Call(game, "ApplyWorld", World("Echo"));
        Step(game, .4f);
        Assert.That((float)Get(boulder, "MotionTravel"), Is.EqualTo(rolled).Within(.001f), "Night freezes it.");
        yield return null; Capture("mountain-rockslide-night");
    }

    // A scripted keyboard-only driver clears the whole level, so nothing is unfair or impossible.
    // Steps: lane target, world, or Dodge (nearest position clear of the next frozen crossing goat).
    private enum MountainAction { Lane, World, Dodge }
    private readonly struct MountainStep
    {
        public readonly float At; public readonly MountainAction Action; public readonly float Value;
        public MountainStep(float at, MountainAction action, float value) { At = at; Action = action; Value = value; }
    }
    private static MountainStep ML(float at, float x) => new MountainStep(at, MountainAction.Lane, x);
    private static MountainStep MDay(float at) => new MountainStep(at, MountainAction.World, 0f);
    private static MountainStep MNight(float at) => new MountainStep(at, MountainAction.World, 1f);
    private static MountainStep MDodge(float at, float look = 30f) => new MountainStep(at, MountainAction.Dodge, look);

    private static readonly MountainStep[][] MountainLine =
    {
        new[] { MDay(0f), ML(0f, 3.4f), ML(62f, -3.4f), ML(132f, 3.4f), ML(173f, -1f), MNight(205f), MDodge(205f), ML(245f, 3.4f), MDay(266f), MNight(285f), MDay(318f), MDodge(420f, 100f) },
        new[] { ML(0f, 0f), ML(96f, 3.4f), ML(113f, 0f), ML(136f, -3.4f), MDay(143f), MNight(215f), MDodge(215f), ML(262f, -3.4f), MDay(300f), MNight(340f), MDodge(440f, 100f) },
        new[] { MNight(65f), MDodge(65f), MDay(150f), ML(150f, 3.4f), ML(205f, -3.4f), MNight(250f), MDodge(250f), MDay(300f), MNight(340f), MDay(368f), MDodge(440f, 100f) },
        new[] { ML(0f, 3.4f), ML(62f, -3.4f), MDay(105f), ML(178f, 3.4f), MNight(190f), MDodge(205f, 60f), ML(267f, -3.4f), MDay(342f), MNight(372f), MDodge(440f, 100f) },
        new[] { ML(0f, 0f), ML(124f, 3.4f), MNight(132f), MDodge(147f), ML(198f, -3.4f), MDay(215f), ML(270f, 3.4f), MNight(310f), MDodge(310f, 32f), MDay(350f), MNight(388f), MDay(416f), MDodge(480f, 100f) },
    };

    [UnityTest]
    public IEnumerator ScriptedDriverClearsEveryMountainSectionWithKeyboardInputs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        // Play mode reloads the domain; lambdas below must be created after that, in their own coroutine.
        yield return DriveMountainLine();
    }

    private IEnumerator DriveMountainLine()
    {
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        var course = Get(game, "Course");
        var ramps = ((Array)Field(course, "Ramps")).Cast<object>().ToArray();
        var crossings = ((IEnumerable)Get(game, "Hazards")).Cast<object>().Where(h => Field(h, "Rock") != null)
            .Select(h => Field(h, "Rock")).ToArray();
        float length = (float)Field(course, "Length");
        float lastX = (float)Get(game, "Lateral");
        for (int n = 0; n < 20000 && Get(game, "State").ToString() == "Running"; n++)
        {
            float d = (float)Get(game, "Distance"), x = (float)Get(game, "Lateral");
            int section = 0;
            while (section < ramps.Length - 1 && d >= (float)Field(ramps[section], "End")) section++;
            float offset = d - SectionStart(ramps, section);
            string world = Get(game, "World").ToString();
            bool airborne = (bool)Get(game, "IsRelayAirborne");
            float speedNow = Mathf.Max(1f, (float)Get(game, "Speed"));
            float target = 0f; string wanted = null;
            foreach (var step in MountainLine[section])
            {
                if (offset < step.At) continue;
                if (step.Action == MountainAction.Lane) target = step.Value;
                else if (step.Action == MountainAction.World) wanted = step.Value > 0f ? "Echo" : "Day";
                else
                {
                    var ahead = crossings.Where(r => (float)Get(r, "Distance") > d - 2f && (float)Get(r, "Distance") < d + step.Value).ToArray();
                    if (ahead.Length == 0) continue;
                    // Nearest position clear of every goat in the next 14 m: a herd counts as one obstacle.
                    float first = ahead.Min(r => (float)Get(r, "Distance")), here = x;
                    var group = ahead.Where(r => (float)Get(r, "Distance") < first + 14f).Select(r => PredictedLateral(r, d, ramps[section], world, speedNow)).ToArray();
                    var options = Enumerable.Range(-19, 39).Select(i => i * .2f).Where(c => group.All(g => Mathf.Abs(c - g) >= 2.65f)).ToArray();
                    // Weak air steering: line up one position that clears every eagle on this jump, if there is one.
                    var flock = ahead.Where(r => (bool)Get(r, "Airborne")).Select(r => PredictedLateral(r, d, ramps[section], world, speedNow)).ToArray();
                    if (flock.Length > 1)
                    {
                        var all = Enumerable.Range(-19, 39).Select(i => i * .2f).Where(c => flock.All(g => Mathf.Abs(c - g) >= 2.65f)).ToArray();
                        if (all.Length > 0) options = all;
                    }
                    if (options.Length > 0) target = options.OrderBy(c => Mathf.Abs(c - here)).First();
                }
            }
            var ramp = ramps[section];
            if (airborne && world == Field(ramp, "Source").ToString() && d > AirSwitchPoint(ramp, crossings)) Call(game, "TrySwitch");
            else if (!airborne && wanted != null && world != wanted && d < (float)Field(ramp, "Ramp") - 5f) Call(game, "TrySwitch");
            float speed = (x - lastX) / .02f;
            float error = Mathf.Clamp((target - x) * 2.2f, -8f, 8f) - speed;
            float steer = error > .5f ? 1f : error < -.5f ? -1f : 0f;
            lastX = x;
            Call(game, "StepSimulation", .02f, steer, false);
            string state = Get(game, "State").ToString();
            if (state != "Running" && state != "Won")
                Assert.Fail($"Crashed ({Field(game, "lastFailure")}) in section {section + 1} at offset {offset:0.0} m, x={x:0.00}, world={world}.");
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"), $"Reached {Get(game, "Distance")} of {length}.");
    }

    [UnityTest]
    public IEnumerator MountainCrossingsAreGoatsTrottingAcrossTheRoad()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started(); var presentation = Field(game, "presentation");
        var views = ((IEnumerable)Field(presentation, "hazards")).Cast<object>()
            .Where(v => Field(Field(v, "Data"), "Rock") != null && !(bool)Get(Field(Field(v, "Data"), "Rock"), "Airborne")).ToArray();
        Assert.That(views, Is.Not.Empty);
        var all = game.GetComponentsInChildren<Transform>(true);
        Assert.That(all.Count(t => t.name.EndsWith("crossing the road")), Is.EqualTo(views.Length), "One goat per crossing.");
        Assert.That(all.Any(t => t.name == "Mountain goat crossing the road"), Is.True);
        Assert.That(all.Any(t => t.name == "Alpine ibex crossing the road"), Is.True);
        Assert.That(all.Any(t => t.name == "Teal-banded desert boulder"), Is.False, "No rolling crossing boulders left in Mountain.");
        foreach (var view in views)
        {
            var root = (Transform)Field(view, "Root");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "Goats are visual only; the sphere still decides contact.");
            Assert.That(((IEnumerable)Field(view, "Arrows")).Cast<object>().Count(), Is.EqualTo(1), "Each goat carries the direction arrow.");
        }
        // Trot one goat in Day: it walks, turns around at the road edge and faces its direction of travel.
        var goatView = views[0];
        var data = Field(goatView, "Data");
        var goat = ((IEnumerable)Field(goatView, "Goats")).Cast<MonoBehaviour>().Single();
        var leg = goat.transform.Find("Goat body/Goat left leg");
        float at = (float)Field(data, "Start");
        Pose(game, at - 13f, 3.4f);
        var seen = new HashSet<float>();
        Quaternion previousLeg = leg.localRotation;
        bool trotted = false;
        for (int i = 0; i < 400 && seen.Count < 2; i++)
        {
            Call(data, "Advance", .05f, at, World("Day"));
            Call(presentation, "UpdateVisibility", at - 13f);
            float heading = (float)Static(presentation.GetType(), "SidewaysHeading", data);
            trotted |= Quaternion.Angle(previousLeg, leg.localRotation) > .5f;
            previousLeg = leg.localRotation;
            // Once it has finished turning, the goat's nose points the way it walks.
            if (Vector3.Dot(goat.transform.right, ((Transform)Field(goatView, "Root")).right) * heading > .99f && seen.Add(heading))
            {
                yield return null;
                Capture("mountain-goat-" + (heading > 0f ? "right" : "left"));
            }
        }
        Assert.That(trotted, Is.True, "Legs swing while the goat walks in Day.");
        Assert.That(seen, Is.EquivalentTo(new[] { -1f, 1f }), "The goat turns around and faces each way it walks.");
        // Night: frozen mid-stride.
        Quaternion frozen = leg.localRotation;
        float lateral = (float)Get(data, "LateralOffset");
        for (int i = 0; i < 20; i++)
        {
            Call(data, "Advance", .05f, at, World("Echo"));
            Call(presentation, "UpdateVisibility", at - 13f);
        }
        Assert.That((float)Get(data, "LateralOffset"), Is.EqualTo(lateral).Within(1e-4f), "Night freezes the goat in place.");
        Assert.That(Quaternion.Angle(frozen, leg.localRotation), Is.LessThan(.01f), "Night freezes the goat mid-stride.");
        Call(game, "ApplyWorld", World("Echo"));
        yield return null; Capture("mountain-goat-frozen-night");
    }

    [UnityTest]
    public IEnumerator MountainSkyHasCableCarsAndSoaringEagles()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null).ToArray();
        var ropeways = all.Where(t => t.name == "Aerial cable car over the valley").ToArray();
        var eagles = all.Where(t => t.name == "Eagle").ToArray();
        Assert.That(ropeways.Length, Is.GreaterThanOrEqualTo(3), "Cable cars cross the valley along the pass.");
        Assert.That(all.Count(t => t.name == "Cable car cabin"), Is.EqualTo(ropeways.Length * 6));
        Assert.That(eagles.Length, Is.GreaterThanOrEqualTo(10), "Eagles soar over the pass.");
        foreach (var t in ropeways.Concat(eagles))
            Assert.That(t.GetComponentsInChildren<Collider>(true), Is.Empty, "Background life never touches the car.");
        var cabins = all.Where(t => t.name == "Cable car cabin").ToArray();
        Vector3 cabin = cabins[0].position, eagle = eagles[0].position;
        for (int i = 0; i < 30; i++) yield return null;
        Assert.That(Vector3.Distance(cabin, cabins[0].position), Is.GreaterThan(.05f), "Cable cars ride the cable.");
        Assert.That(Vector3.Distance(eagle, eagles[0].position), Is.GreaterThan(.05f), "Eagles circle.");
        float ahead = CheckSkyLifeAboveRoad(game, cabins.Concat(eagles).ToArray(), ropeways[0]);
        Pose(game, ahead - 70f, 0f);
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-sky-cable-cars");
        Call(game, "ApplyWorld", World("Echo"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-sky-cable-cars-night");
    }

    // Every cabin and eagle over the road stays high above it; returns the road distance under the first ropeway.
    private static float CheckSkyLifeAboveRoad(MonoBehaviour game, Transform[] flyers, Transform ropeway)
    {
        var route = Get(game, "Route");
        float length = (float)Field(Get(route, "Course"), "Length");
        var road = new List<(float at, Vector3 p)>();
        for (float at = 0f; at < length; at += 3f) road.Add((at, (Vector3)Get(Call(route, "Sample", at), "Position")));
        float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        foreach (var flyer in flyers)
        {
            var below = road.OrderBy(r => Flat(r.p, flyer.position)).First();
            if (Flat(below.p, flyer.position) < 8f)
                Assert.That(flyer.position.y - below.p.y, Is.GreaterThan(12f), $"{flyer.name} stays high above the road.");
        }
        return road.OrderBy(r => Flat(r.p, ropeway.position)).First().at;
    }

    [UnityTest]
    public IEnumerator MountainBarriersAreRockslidePilesWithWorldPanels()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        float wall = CheckRockslidePiles(game);
        Pose(game, wall - 26f, 0f); Call(game, "ApplyWorld", World("Day"));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-rockslide-pile-day");
        Pose(game, wall - 26f, 0f); Call(game, "ApplyWorld", World("Echo"));
        StepBarriers(game);
        for (int i = 0; i < 3; i++) yield return null;
        Capture("mountain-rockslide-pile-lowered-night");
    }

    private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < .06f;

    // Every Mountain Day/Night closure is a rockslide pile with the sun/moon panel; returns the first full-width Day wall.
    private static float CheckRockslidePiles(MonoBehaviour game)
    {
        var barriers = game.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c.GetType().Name == "WorldShiftRetractableBarrier").ToArray();
        Assert.That(barriers, Is.Not.Empty);
        foreach (var barrier in barriers)
        {
            Assert.That(barrier.name, Does.EndWith("Rockslide pile"));
            var colours = ((Mesh)Field(barrier, "bodyMesh")).colors;
            Assert.That(colours.Any(c => Near(c, new Color(1f, .48f, .12f)) || Near(c, new Color(.76f, .67f, .95f))), Is.True,
                "Each pile carries the world-coloured sun/moon panel.");
            Assert.That(barrier.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        var walls = ((Array)Get(game, "Hazards")).Cast<object>()
            .Where(h => Field(h, "Kind").ToString() == "Barrier" && (int)Field(h, "LaneMask") == 7 && Field(h, "World").ToString() == "Day");
        return (float)Field(walls.First(), "Start");
    }

    [UnityTest]
    public IEnumerator MountainCarSlowsClimbingAndSpeedsUpDescending()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        CheckGradeSpeed(game);
    }

    private static void CheckGradeSpeed(MonoBehaviour game)
    {
        var course = Get(game, "Course"); var route = Get(game, "Route");
        float cruise = 28f * .9f, steepUp = 0f, steepDown = 0f, upSlope = 0f, downSlope = 0f;
        for (float d = 0f; d < (float)Field(course, "Length"); d += 5f)
        {
            float slope = (float)Call(course, "TerrainSlope", d);
            if (slope > upSlope) { upSlope = slope; steepUp = d; }
            if (slope < downSlope) { downSlope = slope; steepDown = d; }
        }
        Assert.That(upSlope, Is.GreaterThan(.06f), "Mountain has real climbs.");
        float y0 = ((Vector3)Get(Call(route, "Sample", steepUp - 10f), "Position")).y, y1 = ((Vector3)Get(Call(route, "Sample", steepUp + 10f), "Position")).y;
        Assert.That(y1, Is.GreaterThan(y0), "A positive slope climbs.");
        Pose(game, steepUp, 0f);
        Assert.That((float)Get(game, "Speed"), Is.LessThan(cruise * .92f), "The car slows on a climb.");
        Assert.That((bool)Get(game, "IsBoosting"), Is.False);
        Pose(game, steepDown, 0f);
        Assert.That((float)Get(game, "Speed"), Is.GreaterThan(cruise * 1.08f), "The car speeds up on a descent.");
        Assert.That((bool)Get(game, "IsBoosting"), Is.False, "Descending is not a jump boost.");
        // Jump approaches stay at the normal cruise speed so every ramp plays the same.
        var ramp = ((Array)Field(course, "Ramps")).GetValue(0);
        Pose(game, (float)Field(ramp, "Ramp") - 45f, 0f);
        Assert.That((float)Get(game, "Speed"), Is.EqualTo(cruise).Within(.05f));
    }

    // How a player handles the eagles: off a Day ramp, switch to Night at once and the eagles freeze where they are;
    // off a Night ramp they are already frozen, so stay in Night until past the last one, then switch before landing.
    private static float AirSwitchPoint(object ramp, object[] rocks)
    {
        float takeoff = (float)Field(ramp, "Takeoff"), landing = (float)Field(ramp, "Landing");
        if (Field(ramp, "Source").ToString() == "Day") return takeoff + 1f;
        var eagles = rocks.Where(r => (bool)Get(r, "Airborne")).Select(r => (float)Get(r, "Distance")).Where(at => at > takeoff && at < landing).ToArray();
        return eagles.Length > 0 ? eagles.Max() + 4f : takeoff + 8f;
    }

    // Judge an eagle where it will be when it freezes: off a Day ramp it glides until the switch just after takeoff
    // (a player reads that from its arrow); off a Night ramp, or once in Night, it is frozen where it is.
    private static float PredictedLateral(object rock, float d, object ramp, string world, float speed)
    {
        if (!(bool)Get(rock, "Airborne") || Field(ramp, "Source").ToString() == "Echo" || world == "Echo")
            return (float)Get(rock, "Lateral");
        float seconds = (float)Get(Field(rock, "Motion"), "DaySeconds") + Mathf.Max(0f, ((float)Field(ramp, "Takeoff") + 1f - d) / speed);
        return (float)Call(rock, "LateralAt", seconds);
    }

    [UnityTest]
    public IEnumerator MountainEaglesSoarLevelOverEveryJump()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        yield return PictureMountainEagle();
    }

    private IEnumerator PictureMountainEagle()
    {
        StartLevel(MountainLevel); yield return WaitForNavigation();
        var game = Started();
        float eagleAt = CheckMountainEagles(game);
        // Picture one from the air: the car has switched to Day and the eagle is gliding across the gorge.
        var jump = ((Array)Field(Get(game, "Course"), "Ramps")).GetValue(1);
        var eagle = ((Array)Get(game, "Hazards")).Cast<object>().Select(h => Field(h, "Rock"))
            .First(r => r != null && (bool)Get(r, "Airborne") && Field(r, "Flight") == jump);
        eagleAt = (float)Get(eagle, "Distance");
        Pose(game, (float)Field(jump, "Ramp") - 30f, 0f); Call(game, "ApplyWorld", Field(jump, "Source"));
        for (int i = 0; i < 800 && (float)Get(game, "Distance") < eagleAt - 24f; i++)
        {
            // Line up beside the frozen eagle, then switch to Day just before it so it is gliding in the picture.
            float eagleX = (float)Get(eagle, "Lateral"), target = eagleX > 0f ? eagleX - 3.4f : eagleX + 3.4f;
            Call(game, "StepSimulation", .02f, Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
            if ((bool)Get(game, "IsRelayAirborne") && Get(game, "World").ToString() != "Day"
                && (float)Get(game, "Distance") > eagleAt - 34f) Call(game, "TrySwitch");
        }
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        yield return null;
        Capture("mountain-eagle-over-jump");
    }

    // An eagle over every jump (two on the summit jump), like Desert's flyers; big, level wings, the arrow, no ring.
    private static float CheckMountainEagles(MonoBehaviour game)
    {
        var hazards = ((Array)Get(game, "Hazards")).Cast<object>().Where(h => Field(h, "Rock") != null && (bool)Get(Field(h, "Rock"), "Airborne")).ToArray();
        var ramps = ((Array)Field(Get(game, "Course"), "Ramps")).Cast<object>().ToArray();
        Assert.That(hazards.Length, Is.EqualTo(6), "One eagle per jump and two on the summit keep Mountain medium.");
        foreach (var ramp in ramps)
            Assert.That(hazards.Count(h => Field(Field(h, "Rock"), "Flight") == ramp), Is.EqualTo(ramp == ramps.Last() ? 2 : 1), $"Eagles over jump {Field(ramp, "Number")}.");
        foreach (var h in hazards)
        {
            var flight = Field(Field(h, "Rock"), "Flight");
            Assert.That((float)Field(h, "Start"), Is.InRange((float)Field(flight, "Takeoff") + 25f, (float)Field(flight, "Landing") - 10f), "Inside the flight.");
            Assert.That((bool)Field(h, "MovesWheneverDay"), Is.True, "Glides in Day, frozen in Night.");
        }
        var eagles = game.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Gliding eagle over the jump").ToArray();
        Assert.That(eagles.Length, Is.EqualTo(6));
        foreach (var eagle in eagles)
        {
            var bounds = new Bounds(eagle.position, Vector3.zero);
            foreach (var r in eagle.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.GreaterThan(8f), "About a 10 m wingspan, big enough to see from the ramp.");
            Assert.That(eagle.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(Vector3.Angle(eagle.up, eagle.parent.up), Is.LessThan(12f), "Wings level (horizontal), not flared upright.");
            Assert.That(eagle.parent.parent.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Frozen ice base"), Is.False, "No ring under a flying animal.");
        }
        return (float)Field(hazards[0], "Start");
    }

    [Test]
    public void MountainPlaysCominRoundTheMountain()
    {
        var library = TypeFor("WorldShiftMusicLibrary");
        object mountain = Enum.Parse(TypeFor("ShiftLevel"), "Mountain");
        Assert.That(Static(library, "Title", mountain), Is.EqualTo("Comin Round the Mountain"));
        Assert.That((string)Static(library, "SourceUrl", mountain), Does.Contain("USUAN1100817"));
        var credits = Resources.Load<TextAsset>("WorldShiftMusic/CREDITS").text;
        Assert.That(credits, Does.Contain("Comin Round the Mountain"));
        Assert.That(credits, Does.Not.Contain("Hall of the Mountain King"));
        Assert.That(credits, Does.Not.Contain("Mountain Emperor"));
        var song = Resources.Load<AudioClip>("WorldShiftMusic/Mountain");
        Assert.That(song, Is.Not.Null);
        Assert.That(song.length, Is.EqualTo(174f).Within(1f));
    }
}
