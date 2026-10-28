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
            Is.EquivalentTo(new[] { "Fire", "Police", "Gap", "Spikes", "Barrier", "Divider", "Pedestrians", "Oncoming" }));
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
            Assert.That(hazards.Any(h => Field(h, "World").Equals(Field(pickup, "World"))
                && (float)Field(h, "Start") < at + 2f && (float)Field(h, "End") > at - 2f
                && ((int)Field(h, "LaneMask") & (1 << (lane + 1))) != 0), Is.False, "Fuel needs a clear collection area.");
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
        Assert.That(RenderSettings.fogEndDistance, Is.LessThanOrEqualTo(100f));
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

        bool fireBlocked = false, gapBlocked = false, firePreview = false, gapPreview = false, spikePreview = false;
        bool medianPreview = false, trafficPreview = false, laterCrossingPreview = false, comboPreview = false, gapWarningPreview = false;
        bool previews = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OUTLAWS_WORLD_SHIFT_PREVIEW"));
        for (int step = 0; step < 2500 && Get(game, "State").ToString() == "Running"; step++)
        {
            distance = (float)Get(game, "Distance");
            string desiredWorld = distance < 300f ? "Echo" : distance < 620f ? "Day"
                : distance < 1180f ? "Echo" : distance < 1440f ? "Day" : distance < 2000f ? "Echo"
                : distance < 2300f ? "Day" : distance < 2550f ? "Echo" : distance < 2870f ? "Day"
                : distance < 3100f ? "Echo" : "Day";
            if (distance >= 1820f && distance < 1855f) desiredWorld = "Day";
            if (Get(game, "World").ToString() != desiredWorld) Assert.That(Call(game, "TrySwitch"), Is.True);
            if (previews && !spikePreview && distance >= 290f)
            { CapturePreview(game, "01b-spikes-echo"); spikePreview = true; }
            if (previews && !medianPreview && distance >= 690f)
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
            if (previews && !comboPreview && distance >= 1818f)
            {
                CapturePreview(game, "02c-night-crossing-combo");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                CapturePreview(game, "02c-day-median-combo");
                Assert.That(Call(game, "TrySwitch"), Is.True);
                comboPreview = true;
            }
            if (previews && !laterCrossingPreview && distance >= 2100f)
            {
                Assert.That(Call(game, "TrySwitch"), Is.True);
                CapturePreview(game, "02b-night-crossing");
                Assert.That(Call(game, "TrySwitch"), Is.True);
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
            if ((!fireBlocked && distance >= 105f && distance < 111f)
                || (!gapBlocked && distance >= 1255f && distance < 1265f))
            {
                object world = Get(game, "World");
                int switches = (int)Get(game, "Switches");
                Assert.That(Call(game, "TrySwitch"), Is.False, "A switch must reject an occupied or missing destination.");
                Assert.That(Get(game, "World"), Is.EqualTo(world));
                Assert.That(Get(game, "Switches"), Is.EqualTo(switches));
                if (distance < 200f) fireBlocked = true;
                else
                {
                    gapBlocked = true;
                    object echo = Enum.Parse(world.GetType(), "Echo");
                    Assert.That(Call(Get(game, "Route"), "HasSurface", distance, echo), Is.False);
                    Assert.That(Call(Get(game, "Route"), "HasSurface", distance, world), Is.True);
                }
            }
            float target = (distance >= 850f && distance < 950f) || (distance >= 1745f && distance < 1880f)
                || distance >= 2940f ? 3.2f : distance >= 2660f ? -3.2f : 0f;
            float steer = Mathf.Clamp((target - (float)Get(game, "Lateral")) * 2f, -1f, 1f);
            Call(game, "StepSimulation", .05f, steer, false);
        }
        Assert.That(fireBlocked && gapBlocked, Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"));
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        Assert.That(Get(game, "Hits"), Is.EqualTo(0), "The complete route must remain possible without forced collisions.");
        Assert.That(Get(game, "Progress"), Is.EqualTo(1f));
        Assert.That((int)Get(game, "FuelCollected"), Is.GreaterThanOrEqualTo(6));
        Assert.That((float)Get(game, "FuelFraction"), Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(game.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Outline - belongs to other world").All(t => !t.gameObject.activeSelf), Is.True,
            "Tutorial previews must disappear after the first sector.");
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(120f).Within(.2f));
        CapturePreview(game, "03-won");

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
        for (int step = 0; (float)Get(game, "Distance") < 690f && step < 600; step++)
        {
            distance = (float)Get(game, "Distance");
            string desired = distance < 300f ? "Echo" : distance < 620f ? "Day" : "Echo";
            if (Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            Call(game, "StepSimulation", .05f, Mathf.Clamp(-(float)Get(game, "Lateral") * 2f, -1f, 1f), false);
        }
        Assert.That(Call(game, "TrySwitch"), Is.True);
        int beforeMedian = (int)Get(game, "Hits");
        Call(game, "StepSimulation", .6f, 0f, false);
        Call(game, "StepSimulation", 1f, -1f, false);
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
            string desired = distance < 300f ? "Echo" : distance < 620f ? "Day" : distance < 1180f ? "Echo" : "Day";
            if (Get(game, "World").ToString() != desired) Call(game, "TrySwitch");
            float target = distance >= 850f && distance < 950f ? 3.2f : 0f;
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
        string directory = Environment.GetEnvironmentVariable("OUTLAWS_WORLD_SHIFT_PREVIEW");
        if (string.IsNullOrEmpty(directory)) return;
        Call(game, "UpdateCamera", true);
        var components = game.GetComponents<MonoBehaviour>();
        Call(components.Single(c => c.GetType().Name == "WorldShiftPresentation"), "UpdateVisibility", Get(game, "Distance"));
        Call(components.Single(c => c.GetType().Name == "WorldShiftHud"), "Update");
        object mirror = Get(game, "RearView");
        if (mirror != null) Call(mirror, "RenderPreview");
        Canvas.ForceUpdateCanvases();
        Camera camera = Camera.main;
        Canvas canvas = game.GetComponentInChildren<Canvas>();
        RenderMode mode = canvas.renderMode;
        Camera canvasCamera = canvas.worldCamera;
        float plane = canvas.planeDistance;
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
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
            canvas.renderMode = mode;
            canvas.worldCamera = canvasCamera;
            canvas.planeDistance = plane;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
