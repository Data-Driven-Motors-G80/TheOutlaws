using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldShiftCityTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("Outlaws.WorldShift." + name)).First(t => t != null);
    private static object Field(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Flags).GetValue(value);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);

    [Test]
    public void TwoMinuteLevelHasValidHazardsAndClearCheckpoints()
    {
        Type level = RuntimeType("WorldShiftLevel");
        float length = (float)level.GetField("Length").GetValue(null);
        float speed = (float)level.GetField("Speed").GetValue(null);
        object[] hazards = ((IEnumerable)level.GetField("Hazards").GetValue(null)).Cast<object>().ToArray();
        object[] checkpoints = ((IEnumerable)level.GetField("Checkpoints").GetValue(null)).Cast<object>().ToArray();
        Assert.That(length / speed, Is.EqualTo(120f));
        Assert.That(hazards.Select(h => Field(h, "Kind").ToString()).Distinct(),
            Is.EquivalentTo(new[] { "Fire", "Police", "Gap", "Spikes", "Barrier", "Divider", "Pedestrians", "Oncoming", "HalfBarrier" }));
        foreach (object hazard in hazards)
        {
            Assert.That((float)Field(hazard, "Start"), Is.InRange(0f, length));
            Assert.That((float)Field(hazard, "End"), Is.GreaterThan((float)Field(hazard, "Start")).And.LessThanOrEqualTo(length));
            Assert.That((int)Field(hazard, "LaneMask"), Is.InRange(1, 7));
            if (Field(hazard, "Kind").ToString() == "Gap") Assert.That(Field(hazard, "LaneMask"), Is.EqualTo(7));
            if (Field(hazard, "Kind").ToString() == "Oncoming")
            {
                Assert.That((int)Field(hazard, "LaneMask"), Is.EqualTo(1).Or.EqualTo(2).Or.EqualTo(4));
                float at = (float)Field(hazard, "Start");
                object[] before = { at - 100f, 0f, 0f }, later = { at - 50f, 0f, 0f };
                hazard.GetType().GetMethod("BoundsAt").Invoke(hazard, before);
                hazard.GetType().GetMethod("BoundsAt").Invoke(hazard, later);
                Assert.That((float)later[1], Is.LessThan((float)before[1]), "Oncoming traffic must approach along the road.");
            }
        }
        object[] pickups = ((IEnumerable)level.GetField("FuelPickups").GetValue(null)).Cast<object>().ToArray();
        foreach (object pickup in pickups)
        {
            float at = (float)Field(pickup, "Distance");
            int lane = (int)Field(pickup, "Lane");
            Assert.That(at, Is.InRange(0f, length));
            Assert.That(lane, Is.InRange(-1, 1));
            foreach (object hazard in hazards)
            {
                if (!Field(hazard, "World").Equals(Field(pickup, "World"))) continue;
                // A vehicle's spawn coordinate is not its position when the
                // player reaches this pickup. Check the actual moving bounds.
                object[] bounds = { at, 0f, 0f };
                hazard.GetType().GetMethod("BoundsAt").Invoke(hazard, bounds);
                bool overlaps = (float)bounds[1] < at + 2f && (float)bounds[2] > at - 2f
                    && ((int)Field(hazard, "LaneMask") & (1 << (lane + 1))) != 0;
                Assert.That(overlaps, Is.False, "Fuel at " + at + " needs a clear collection area.");
            }
        }
        Assert.That(checkpoints.Length, Is.GreaterThan(1));
        Assert.That(Field(checkpoints[0], "Distance"), Is.EqualTo(0f));
        float previous = -1f;
        foreach (object checkpoint in checkpoints)
        {
            float distance = (float)Field(checkpoint, "Distance");
            Assert.That(distance, Is.GreaterThan(previous).And.LessThan(length));
            Assert.That((string)Field(checkpoint, "Title"), Is.Not.Empty);
            Assert.That((string)Field(checkpoint, "Hint"), Is.Not.Empty);
            Assert.That(hazards.Any(h => (float)Field(h, "Start") <= distance + 12f
                && (float)Field(h, "End") >= distance - 12f), Is.False, "Checkpoints need a clear restart in both worlds.");
            previous = distance;
        }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CitySwitchPauseFinishAndRetrySmokeTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/City_WorldShift.unity");
        yield return new EnterPlayMode();
        var game = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType().FullName == "Outlaws.WorldShift.WorldShiftCity");
        game.enabled = false; // Only the explicit simulation steps below advance the run.
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Ready"));
        Assert.That(Get(game, "Route"), Is.Not.Null);
        AssertRoadsideSeams(game);
        var player = (Transform)Get(game, "Player");
        Light headlight = player.GetComponentsInChildren<Light>(true).Single(l => l.name == "Player forward cone headlight");
        Assert.That(headlight.enabled, Is.False, "Day does not need the night beam.");
        Renderer haze = headlight.GetComponentsInChildren<Renderer>(true).Single();
        Assert.That(haze.sharedMaterial.shader.isSupported, Is.True);
        Assert.That(haze.gameObject.activeSelf, Is.False);
        var police = (Component)Get(game, "Police");
        var mirror = (Component)Get(game, "RearView");
        Assert.That(police, Is.Not.Null);
        Assert.That((bool)Get(police, "IsFiniteRoute"), Is.True);
        Assert.That(Get(mirror, "Texture"), Is.Not.Null);
        Assert.That(player.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name == "AutoDriveCar"), Is.True);
        CapturePreview(game, "00-ready");
        Call(game, "BeginRun");
        Call(game, "StepSimulation", .05f, 1f, false);
        float distance = (float)Get(game, "Distance"), lateral = (float)Get(game, "Lateral");
        float duration = (float)Get(game, "Duration");
        Vector3 position = player.position;
        Quaternion rotation = player.rotation;
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"));
        Assert.That(headlight.enabled, Is.True);
        Assert.That(haze.gameObject.activeSelf, Is.True);
        Assert.That(headlight.type, Is.EqualTo(LightType.Spot));
        Assert.That(headlight.range, Is.GreaterThanOrEqualTo(60f));
        Assert.That(RenderSettings.fogEndDistance, Is.InRange(120f, 160f));
        Assert.That(Get(game, "Distance"), Is.EqualTo(distance));
        Assert.That(Get(game, "Lateral"), Is.EqualTo(lateral));
        Assert.That(player.position, Is.EqualTo(position));
        Assert.That(player.rotation, Is.EqualTo(rotation));
        Call(game, "TogglePause");
        float fuelBeforePause = (float)Get(game, "FuelFraction");
        float gapBeforePause = (float)Get(game, "PursuitGap");
        Vector3 policeBeforePause = police.transform.position;
        Call(game, "StepSimulation", 1f, -1f, true);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Paused"));
        Assert.That(Get(game, "Distance"), Is.EqualTo(distance));
        Assert.That(Get(game, "Duration"), Is.EqualTo(duration));
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"));
        Assert.That(Get(game, "FuelFraction"), Is.EqualTo(fuelBeforePause));
        Assert.That(Get(game, "PursuitGap"), Is.EqualTo(gapBeforePause));
        Assert.That(police.transform.position, Is.EqualTo(policeBeforePause));
        Assert.That(player.position, Is.EqualTo(position));
        Call(game, "TogglePause");

        bool firePreview = false, gapPreview = false, spikePreview = false;
        bool medianPreview = false, trafficPreview = false, laterCrossingPreview = false, comboPreview = false, gapWarningPreview = false;
        bool riftApproach = false, riftOpening = false, riftJoined = false;
        bool pressureNight = false, pressureJoined = false, readabilityPeople = false, readabilityPit = false;
        bool previews = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OUTLAWS_WORLD_SHIFT_PREVIEW"));
        for (int step = 0; step < 2500 && Get(game, "State").ToString() == "Running"; step++)
        {
            distance = (float)Get(game, "Distance");
            string desiredWorld = SafeWorld(distance);
            if (!(bool)Get(game, "IsMerged") && Get(game, "World").ToString() != desiredWorld) Assert.That(Call(game, "TrySwitch"), Is.True);
            if (!readabilityPeople && distance >= 290f)
            { CapturePreview(game, "readability-01-pedestrians"); readabilityPeople = true; }
            if (!readabilityPit && distance >= 1210f)
            {
                Assert.That(Call(game, "TrySwitch"), Is.True);
                CapturePreview(game, "readability-02-night-pit");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                readabilityPit = true;
            }
            if (!pressureNight && distance >= 1050f)
            {
                Assert.That(game.GetComponentsInChildren<Transform>().Any(t => t.name == "Outline - belongs to other world"), Is.False,
                    "Other-world previews are removed for the entire run.");
                CapturePreview(game, "readability-03-near-preview");
                CapturePreview(game, "pressure-01-night-traffic"); pressureNight = true;
            }
            if (!pressureJoined && distance >= 3020f)
            { CapturePreview(game, "pressure-02-joined-traffic"); pressureJoined = true; }
            if (previews && !spikePreview && distance >= 290f)
            { CapturePreview(game, "01b-spikes-echo"); spikePreview = true; }
            if (previews && !medianPreview && distance >= 850f)
            {
                CapturePreview(game, "01c-night-no-median");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                CapturePreview(game, "01c-day-median");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                medianPreview = true;
            }
            if (previews && !trafficPreview && distance >= 870f)
            { CapturePreview(game, "01d-oncoming-echo"); trafficPreview = true; }
            if (previews && !gapWarningPreview && distance >= 1170f)
            { CapturePreview(game, "02a-pit-approach-echo"); gapWarningPreview = true; }
            if (!comboPreview && distance >= 1818f)
            {
                Assert.That(Get(game, "IsMerged"), Is.False, "No middle-sector joining remains.");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                Assert.That(Call(game, "TrySwitch"), Is.True);
                comboPreview = true;
            }
            if (!riftApproach && distance >= 2750f)
            {
                Assert.That(Get(game, "IsMerged"), Is.False);
                Assert.That(game.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("World rift -")), Is.True);
                CapturePreview(game, "rift-01-approach");
                riftApproach = true;
            }
            if (!riftOpening && distance >= 2807f)
            {
                Assert.That(Get(game, "IsMerged"), Is.True);
                Assert.That((float)Get(game, "JoinedViewSplit"), Is.GreaterThan(.5f).And.LessThan(1f));
                Assert.That(Call(game, "TrySwitch"), Is.False);
                CapturePreview(game, "rift-02-opening");
                riftOpening = true;
            }
            if (!riftJoined && distance >= 2960f)
            {
                Assert.That((float)Get(game, "JoinedViewSplit"), Is.EqualTo(.5f));
                Call(game, "UpdateCamera", true);
                foreach (float ahead in new[] { 0f, 30f, 90f })
                {
                    object point = Call(Get(game, "Route"), "Sample", distance + ahead);
                    Assert.That(Camera.main.WorldToViewportPoint((Vector3)Get(point, "Position")).x,
                        Is.EqualTo(.5f).Within(.002f), "The visual seam must meet the road centre at every depth.");
                }
                CapturePreview(game, "rift-03-joined");
                riftJoined = true;
            }
            if (previews && !laterCrossingPreview && distance >= 2100f)
            {
                CapturePreview(game, "02b-overlap-exit");
                laterCrossingPreview = true;
            }
            if (previews && ((!firePreview && distance >= 70f) || (!gapPreview && distance >= 1210f)))
            {
                string label = distance < 100f ? "01-fire" : "02-bridge";
                CapturePreview(game, label + "-" + Get(game, "World").ToString().ToLowerInvariant());
                Assert.That(Call(game, "TrySwitch"), Is.True);
                CapturePreview(game, label + "-" + Get(game, "World").ToString().ToLowerInvariant());
                Assert.That(Call(game, "TrySwitch"), Is.True);
                if (distance < 100f) firePreview = true; else gapPreview = true;
            }
            float target = SafeLane(distance);
            if (distance >= 2800f)
                Assert.That(Get(game, "World").ToString(), Is.EqualTo((float)Get(game, "Lateral") < 0f ? "Day" : "Echo"));
            if (previews && distance >= 1749f && distance < 1751f) CapturePreview(game, "06-joined-left-block");
            if (previews && distance >= 1869f && distance < 1871f) CapturePreview(game, "07-joined-right-block");
            if (previews && distance >= 2909f && distance < 2911f) CapturePreview(game, "08-final-overlap");
            float steer = Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f);
            Call(game, "StepSimulation", .05f, steer, false);
            Assert.That(Get(game, "Hits"), Is.EqualTo(0), "Safe-route contact near " + distance
                + " m; world " + Get(game, "World") + "; lateral " + Get(game, "Lateral")
                + "; hazards: " + string.Join(", ", ((IEnumerable)Field(game, "hitHazards")).Cast<object>()
                    .Select(h => Field(h, "World") + " " + Field(h, "Kind") + " " + Field(h, "Start"))));
        }
        Assert.That(riftApproach && riftOpening && riftJoined, Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        Assert.That(Get(game, "Hits"), Is.EqualTo(0), "The complete route must remain possible without forced collisions.");
        Assert.That(Get(game, "Progress"), Is.EqualTo(1f));
        Assert.That((int)Get(game, "FuelCollected"), Is.GreaterThanOrEqualTo(6));
        Assert.That((float)Get(game, "FuelFraction"), Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(game.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Outline - belongs to other world").All(t => !t.gameObject.activeSelf), Is.True,
            "The joined finale shows real halves, without other-world ghost overlays.");
        Assert.That((float)Get(game, "Duration"), Is.InRange(112f, 118f));
        Assert.That(Get(game, "RelaysCompleted"), Is.EqualTo(3));
        CapturePreview(game, "03-won");

        // Both halves coexist, regardless of which world the car centre belongs to.
        var worldType = Get(game, "World").GetType();
        foreach (string name in new[] { "Day", "Echo" })
        {
            object w = Enum.Parse(worldType, name);
            Assert.That(Call(game, "FindCollision", w, 2902f, 2903f, -.3f, -.3f, false), Is.Not.Null,
                "A car straddling the seam must still contact the visible left half-wall.");
            Assert.That(Call(game, "FindCollision", w, 2902f, 2903f, 3.2f, 3.2f, false), Is.Null);
            Assert.That(Call(game, "FindCollision", w, 2972f, 2973f, 3.2f, 3.2f, false), Is.Not.Null);
            Assert.That(Call(game, "FindCollision", w, 2972f, 2973f, -3.2f, -3.2f, false), Is.Null);
        }
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Joined road ")), Is.False,
            "The full worlds are rendered; no replacement half-road geometry should be built.");
        var rift = game.GetComponentsInChildren<Transform>(true).Single(t => t.name.StartsWith("World rift -"));
        Assert.That(rift.GetComponentsInChildren<Collider>(true), Is.Empty);

        Call(game, "RestartRun");
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 5);
        Call(game, "RetryCheckpoint");
        Assert.That(Get(game, "Distance"), Is.EqualTo(2800f));
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));
        Assert.That(Call(game, "TrySwitch"), Is.False);
        Call(game, "TogglePause");
        position = player.position;
        Call(game, "StepSimulation", .5f, 1f, true);
        Assert.That(player.position, Is.EqualTo(position));
        Call(game, "TogglePause");
        for (int i = 0; i < 30; i++) Call(game, "StepSimulation", .05f, 1f, false);
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Echo"), "Steering across joins the other world without Space.");
        Call(game, "RetryCheckpoint");
        Assert.That((float)Get(game, "Lateral"), Is.EqualTo(-3.2f));
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));

        // A moving car crosses this sweep before reaching its original authored position.
        var hazards = ((IEnumerable)RuntimeType("WorldShiftLevel").GetField("Hazards").GetValue(null)).Cast<object>().ToArray();
        object moving = hazards.Single(h => Field(h, "Kind").ToString() == "Oncoming" && (float)Field(h, "Start") == 960f);
        object echoWorld = Field(moving, "World");
        Assert.That(Call(game, "FindCollision", echoWorld, 910f, 920f, -3.2f, -3.2f, false), Is.SameAs(moving));

        Call(game, "RestartRun");
        Assert.That(Get(game, "Switches"), Is.EqualTo(0));
        float visualGap = (float)Get(police, "DistanceToPlayer");
        Call(game, "StepSimulation", 5f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That(Get(game, "Hits"), Is.EqualTo(1), "A long obstacle must not drain pursuit repeatedly.");
        Assert.That((float)Get(game, "PursuitGap"), Is.InRange(22f, 22.5f), "One impact costs half the 45 m starting gap, plus normal pursuit.");
        Assert.That((float)Get(game, "FuelFraction"), Is.EqualTo(.9f).Within(.002f), "A collision adds no fuel penalty.");
        Assert.That((float)Get(police, "DistanceToPlayer"), Is.LessThan(visualGap));
        CapturePreview(game, "03b-police-closing");
        Call(game, "StepSimulation", 3f, 0f, false);
        Assert.That(Get(game, "Hits"), Is.EqualTo(2));
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That((float)Get(game, "PursuitGap"), Is.EqualTo(0f), "The second collision must exhaust the actual pursuit distance.");
        Assert.That((string)Get(game, "Message"), Does.StartWith("CAUGHT"));
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That(Get(game, "Distance"), Is.EqualTo(0f));
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));

        // A Day median pushes contact to a side and keeps that side until the world changes.
        for (int step = 0; (float)Get(game, "Distance") < 850f && step < 750; step++)
        {
            distance = (float)Get(game, "Distance");
            string desired = SafeWorld(distance);
            if (Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            Call(game, "StepSimulation", .05f, Mathf.Clamp((SafeLane(distance) - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
        }
        Assert.That(Call(game, "TrySwitch"), Is.True);
        int beforeMedian = (int)Get(game, "Hits");
        Call(game, "StepSimulation", .6f, 0f, false);
        Call(game, "StepSimulation", .35f, -1f, false);
        Assert.That((float)Get(game, "Lateral"), Is.GreaterThanOrEqualTo(2.49f));
        Assert.That((int)Get(game, "Hits"), Is.EqualTo(beforeMedian + 1));
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Call(game, "StepSimulation", .6f, -1f, false);
        Assert.That((float)Get(game, "Lateral"), Is.LessThan(1f), "Switching to Night must remove the centre divider.");
        Call(game, "RetryCheckpoint");

        // Reach the first gap safely, then deliberately drive into its missing-world deck.
        Call(game, "TrySwitch");
        for (int step = 0; (float)Get(game, "Distance") < 1210f && step < 950; step++)
        {
            distance = (float)Get(game, "Distance");
            string desired = SafeWorld(distance);
            if (Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            float target = SafeLane(distance);
            Call(game, "StepSimulation", .05f, Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f), false);
        }
        Assert.That((float)Get(game, "Distance"), Is.InRange(1210f, 1213f));
        Assert.That(Call(game, "TrySwitch"), Is.True);
        Call(game, "StepSimulation", .65f, 0f, false);
        CapturePreview(game, "04a-pit-near-lip");
        Call(game, "StepSimulation", 1.35f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        float beforeFall = player.position.y;
        Call(game, "StepSimulation", .4f, 0f, false);
        Assert.That(player.position.y, Is.LessThan(beforeFall - 2f));
        CapturePreview(game, "04-falling");
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "Distance"), Is.EqualTo(1120f));
        Assert.That((float)Get(game, "FuelFraction"), Is.GreaterThanOrEqualTo(.35f));

        // Crossing signals are scenery; hitting a person launches the figure and
        // applies one ordinary distance penalty, with pause/retry still coherent.
        var signal = game.GetComponentsInChildren<Transform>(true)
            .First(t => t.name == "Overhead crossing traffic signals - decoration");
        Assert.That(signal.GetComponentsInChildren<Collider>(true).Any(c => c.enabled), Is.False);
        Assert.That(game.GetComponentsInChildren<Transform>(true)
            .Any(t => t.name == "Triangular broken bridge warning"), Is.True);
        Call(game, "RestartRun");
        Assert.That(Call(game, "TrySwitch"), Is.True);
        var people = game.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(c => c.GetType().Name == "WorldShiftPedestrian").ToArray();
        Assert.That(people.Length, Is.GreaterThan(0));
        Call(game, "StepSimulation", 11.65f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
        Assert.That(Get(game, "Hits"), Is.EqualTo(1));
        Assert.That((float)Get(game, "PursuitGap"), Is.InRange(21f, 22.5f));
        var launched = people.Where(p => (bool)Get(p, "HasBeenHit")).ToArray();
        Assert.That(launched.Length, Is.InRange(1, 3), "Only figures across the actual car width should fly.");
        Assert.That(launched.Any(p => (bool)Get(p, "IsAirborne")), Is.True);
        CapturePreview(game, "05-pedestrian-impact");
        Vector3 airbornePosition = launched[0].transform.position;
        Call(game, "TogglePause");
        Call(game, "StepSimulation", .5f, 0f, false);
        Assert.That(launched[0].transform.position, Is.EqualTo(airbornePosition));
        Call(game, "TogglePause");
        Call(game, "StepSimulation", .2f, 0f, false);
        Assert.That(Vector3.Distance(launched[0].transform.position, airbornePosition), Is.GreaterThan(.1f));
        Call(game, "RetryCheckpoint");
        Assert.That(people.All(p => !(bool)Get(p, "HasBeenHit")), Is.True);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator OriginalCityDriverStillAdvances()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode();
        var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        var analytics = components.FirstOrDefault(c => c.GetType().Name == "WeeWooAnalytics");
        if (analytics != null) analytics.GetType().GetField("uploadsEnabled", Flags).SetValue(analytics, false);
        var manager = components.Single(c => c.GetType().Name == "OutlawGameManager");
        Call(manager, "SetTutorialSelected", false);
        Call(manager, "BeginGame");
        yield return null;
        var driver = components.Single(c => c.GetType().Name == "AutoDriveCar");
        float before = (float)Get(driver, "RoadPositionDistance");
        for (int frame = 0; frame < 12; frame++) yield return null;
        Assert.That((bool)Get(driver, "IsDriveReady"), Is.True);
        Assert.That((float)Get(driver, "RoadPositionDistance"), Is.GreaterThan(before));
        Assert.That(float.IsNaN(driver.transform.position.y), Is.False);
        yield return new ExitPlayMode();
    }

    private static string SafeWorld(float distance)
    {
        if (distance >= 348f && distance < 394f) return "Echo";
        if (distance >= 568f && distance < 820f) return distance < 672f ? "Day" : "Echo";
        if (distance >= 1708f && distance < 1988f) return distance < 1812f ? "Echo" : "Day";
        if (distance >= 2268f && distance < 2550f) return distance < 2372f ? "Day" : "Echo";
        if (distance >= 952f && distance < 1000f) return "Day";
        if (distance >= 1988f && distance < 2038f) return "Echo";
        if (distance >= 2146f && distance < 2184f) return "Echo";
        return distance < 300f ? "Echo" : distance < 620f ? "Day" : distance < 1180f ? "Echo"
            : distance < 1440f ? "Day" : distance < 2000f ? "Echo" : distance < 2300f ? "Day"
            : distance < 2550f ? "Echo" : "Day";
    }

    private static float SafeLane(float d)
    {
        // Read the oncoming car after each wall before returning to that half.
        if (d >= 2800f)
            return d < 2928f ? 3.2f : d < 2998f ? -3.2f : d < 3068f ? 3.2f
                : d < 3138f ? -3.2f : d < 3208f ? 3.2f : d < 3278f ? -3.2f : 3.2f;
        if (d >= 1700f && d < 1880f) return 3.2f;
        if ((d >= 450f && d < 490f) || (d >= 850f && d < 1000f)
            || (d >= 1038f && d < 1150f)) return 3.2f;
        if ((d >= 1500f && d < 1605f) || (d >= 2118f && d < 2184f)) return -3.2f;
        return d >= 2575f ? -3.2f : 0f;
    }

    private static void AssertRoadsideSeams(Component game)
    {
        var route = (Component)Get(game, "Route");
        MeshFilter[] roads = route.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "Road").ToArray();
        MeshFilter[] sides = game.GetComponentsInChildren<MeshFilter>(true)
            .Where(m => m.name.StartsWith("Continuous city roadside ")).ToArray();
        Assert.That(sides.Length, Is.EqualTo(roads.Length));
        Vector3[] previous = null;
        for (int i = 0; i < roads.Length; i++)
        {
            Vector3[] road = roads[i].sharedMesh.vertices, side = sides[i].sharedMesh.vertices;
            for (int row = 0; row < road.Length / 2; row++)
                for (int edge = 0; edge < 2; edge++)
                    Assert.That(Vector3.Distance(roads[i].transform.TransformPoint(road[row * 2 + edge]),
                        sides[i].transform.TransformPoint(side[row * 6 + 2 + edge])), Is.LessThan(.001f),
                        "The shoulder must meet the actual road edge on every bend and slope.");
            if (previous != null)
                for (int v = 0; v < 6; v++)
                    Assert.That(Vector3.Distance(previous[v], sides[i].transform.TransformPoint(side[v])), Is.LessThan(.01f),
                        "Neighbouring roadside pieces must share their entire boundary.");
            previous = side.Skip(side.Length - 6).Select(sides[i].transform.TransformPoint).ToArray();
        }
    }

    private static void CapturePreview(Component game, string name)
    {
        string directory = Environment.GetEnvironmentVariable("OUTLAWS_READABILITY_PREVIEW");
        if (!string.IsNullOrEmpty(directory) && !name.StartsWith("readability-")) return;
        if (string.IsNullOrEmpty(directory)) directory = Environment.GetEnvironmentVariable("OUTLAWS_CITY_PRESSURE_PREVIEW");
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OUTLAWS_CITY_PRESSURE_PREVIEW")) && !name.StartsWith("pressure-")) return;
        if (string.IsNullOrEmpty(directory)) directory = Environment.GetEnvironmentVariable("OUTLAWS_RELAY_PREVIEW");
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OUTLAWS_RELAY_PREVIEW")) && !name.StartsWith("relay-")) return;
        if (string.IsNullOrEmpty(directory)) directory = Environment.GetEnvironmentVariable("OUTLAWS_FINAL_RIFT_PREVIEW");
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OUTLAWS_FINAL_RIFT_PREVIEW")) && !name.StartsWith("rift-")) return;
        if (string.IsNullOrEmpty(directory)) directory = Environment.GetEnvironmentVariable("OUTLAWS_WORLD_SHIFT_PREVIEW");
        if (string.IsNullOrEmpty(directory)) return;
        Call(game, "UpdateCamera", true);
        var components = game.GetComponents<MonoBehaviour>();
        Call(components.Single(c => c.GetType().Name == "WorldShiftPresentation"), "UpdateVisibility", Get(game, "Distance"));
        Call(components.Single(c => c.GetType().Name == "WorldShiftHud"), "Update");
        object mirror = Get(game, "RearView");
        if (mirror != null) Call(mirror, "RenderPreview");
        Canvas.ForceUpdateCanvases();
        Camera camera = Camera.main;
        Canvas[] canvases = game.GetComponentsInChildren<Canvas>(true);
        RenderMode[] modes = canvases.Select(c => c.renderMode).ToArray();
        Camera[] canvasCameras = canvases.Select(c => c.worldCamera).ToArray();
        float[] planes = canvases.Select(c => c.planeDistance).ToArray();
        int mask = camera.cullingMask;
        float aspect = camera.aspect;
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1280f / 720f;
            Call(components.Single(c => c.GetType().Name == "WorldShiftSplitView"), "RenderViews");
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            camera.cullingMask = -1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = canvasCameras[i];
                canvases[i].planeDistance = planes[i];
            }
            camera.cullingMask = mask;
            camera.aspect = aspect;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
