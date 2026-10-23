using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DesertIntegrationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Component Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
        .First(c => c.GetType().Name == name);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);

    private static void DisableUploads()
    {
        var analytics = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetType().Name == "WeeWooAnalytics");
        if (analytics != null) Set(analytics, "uploadsEnabled", false);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CrossingPausesAndCanBeShotWithoutBlockingPickups()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        var spawner = Find("DesertObstacleSpawner");
        ((Behaviour)spawner).enabled = false;
        var controller = Find("DesertCrossingController");
        Set(controller, "minimumOrdinaryRocks", 1);
        Set(controller, "maximumOrdinaryRocks", 1);
        Set(controller, "timer", 0f);
        yield return null;
        var rock = Find("DesertRollingRock");
        Assert.That((bool)Get(controller, "CrossingActive"), Is.True);
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.False, "A pickup/portal cannot overlap the crossing.");
        // Let the car approach naturally so the preview and movement test exercise the warning timer.
        float until = Time.time + 7f;
        Vector3 waiting = rock.transform.position;
        while (rock.transform.position == waiting && Time.time < until)
            yield return null;
        Assert.That(rock.transform.position, Is.Not.EqualTo(waiting));
        Vector3 before = rock.transform.position;
        yield return null; yield return null;
        Assert.That(Vector3.Distance(before, rock.transform.position), Is.GreaterThan(0f));
        if (System.Environment.GetEnvironmentVariable("OUTLAWS_DESERT_PREVIEW") == "1")
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outlaws-desert-preview.png"), image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active;
            target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
        }
        Call(game, "Pause", "test"); before = rock.transform.position;
        yield return null; yield return null;
        Assert.That(rock.transform.position, Is.EqualTo(before));
        Call(game, "Resume");
        Assert.That(Call(spawner, "TryApplyShot", rock.GetComponent<Collider>()), Is.True);
        Assert.That(rock.gameObject.activeSelf, Is.False);
        until = Time.time + 3f;
        while ((bool)Get(controller, "CrossingActive") && Time.time < until) yield return null;
        Assert.That((bool)Get(controller, "CrossingActive"), Is.False);
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True, "Crossing must release the shared reservation.");
        Call(spawner, "ReleaseSafeGap", game);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RockCollisionUsesExistingPenaltyAndNeverDrainsFuel()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        ((Behaviour)Find("DesertObstacleSpawner")).enabled = false;
        var controller = Find("DesertCrossingController"); Set(controller, "timer", 0f);
        yield return null;
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        var rock = Find("DesertRollingRock");
        var state = effects.RunState;
        float fuel = effects.CurrentFuel;
        var meter = Find("FuelMeter");
        float meterFuel = (float)Get(meter, "CurrentFuel");
        float gap = state.PursuitGap;
        Call(Find("HandleCrash"), "OnTriggerEnter", rock.GetComponent<Collider>());
        Assert.That(effects.CurrentFuel, Is.EqualTo(fuel));
        Assert.That((float)Get(meter, "CurrentFuel"), Is.EqualTo(meterFuel));
        Assert.That(gap - state.PursuitGap, Is.EqualTo(15f).Within(0.01f));
        Call(rock, "Advance", 0f);
        Assert.That(rock.GetComponent<Collider>().enabled, Is.False, "A moving rock must only charge one impact.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DesertPortalRoundTripPreservesBackpackAndPursuit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        var storm = Find("DesertSandstorm");
        // Begin a real storm, then leave while it is active.
        Call(storm, "SetStorm", true);
        yield return null; yield return null;
        Assert.That((float)Get(storm, "Intensity"), Is.GreaterThan(0f));
        effects.Backpack.Restore(RandomPickupOutcome.Gun, 2);
        float initialGap = effects.RunState.PursuitGap;
        var session = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RunSession")).First(t => t != null);
        foreach (string destination in new[] { "Env_City", "Env_Desert" })
        {
            session.GetMethod("SetPending").Invoke(null, new[] { Call(game, "CaptureRun") });
            UnityEngine.SceneManagement.SceneManager.LoadScene(destination);
            yield return null; yield return null;
            game = Find("OutlawGameManager");
            effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
            Assert.That(effects.Backpack.Shots, Is.EqualTo(2));
            Assert.That(effects.RunState.PursuitGap, Is.EqualTo(initialGap).Within(2f));
            Assert.That((bool)Get(game, "IsTutorial"), Is.False);
            if (destination == "Env_City") Assert.That(RenderSettings.fog, Is.False, "Desert fog must not leak into city.");
            else Assert.That((float)Get(Find("DesertSandstorm"), "Intensity"), Is.Zero);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TwelveRockFieldBuildsStrongerFogPausesAndReleasesItsReservation()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        var spawner = Find("DesertObstacleSpawner"); ((Behaviour)spawner).enabled = false;
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        effects.RunState.MinimumPursuitGap = 6f; // Observe the entire field without steering a test car.
        var meter = Find("FuelMeter");
        var drainMode = meter.GetType().GetProperty("DrainMode");
        drainMode.SetValue(meter, Enum.Parse(drainMode.PropertyType, "Frozen"));
        var controller = Find("DesertCrossingController");
        var storm = Find("DesertSandstorm");
        float originalEnd = RenderSettings.fogEndDistance;
        Color originalSky = Camera.main.backgroundColor;
        Set(controller, "nextIsStorm", true);
        Set(controller, "minimumRegionRocks", 12); // Exercise the longest configured region.
        Set(controller, "maximumRegionRocks", 12);
        Set(controller, "timer", 0f);
        yield return null;
        Assert.That((bool)Get(controller, "StormRegionActive"), Is.True);
        int maxRocks = (int)Get(controller, "ActiveRockCount");
        float peak = 0f;
        bool paused = false;
        float until = Time.time + 40f;
        while ((bool)Get(controller, "CrossingActive") && Time.time < until)
        {
            maxRocks = Mathf.Max(maxRocks, (int)Get(controller, "ActiveRockCount"));
            float intensity = (float)Get(storm, "Intensity"); peak = Mathf.Max(peak, intensity);
            if (!paused && intensity >= 1f)
            {
                paused = true;
                Assert.That(RenderSettings.fogEndDistance, Is.LessThan(originalEnd));
                var player = Find("AutoDriveCar");
                float offset = Vector3.Distance(Camera.main.transform.position, player.transform.position);
                float speed = (float)Get(player, "ForwardSpeed") * (float)Get(player, "EnvironmentSpeedMultiplier") * effects.ForwardSpeedMultiplier;
                float oldVisibility = Mathf.Max(65f, speed * 2.5f);
                Assert.That(RenderSettings.fogEndDistance, Is.LessThan(offset + oldVisibility), "The new storm should be denser even than the old peak gust.");
                Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.False);
                Call(game, "Pause", "storm test");
                float fogEnd = RenderSettings.fogEndDistance;
                yield return null; yield return null;
                Assert.That((float)Get(storm, "Intensity"), Is.EqualTo(intensity));
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(fogEnd));
                Call(game, "Resume");
                if (System.Environment.GetEnvironmentVariable("OUTLAWS_DESERT_PREVIEW") == "1") CaptureStormPreview();
            }
            yield return null;
        }
        Assert.That((int)Get(controller, "RocksSpawnedInRegion"), Is.EqualTo(12));
        Assert.That(maxRocks, Is.GreaterThanOrEqualTo(3));
        Assert.That(peak, Is.GreaterThanOrEqualTo(0.95f));
        Assert.That((bool)Get(controller, "CrossingActive"), Is.False);
        Assert.That((bool)Get(storm, "IsClear"), Is.True);
        Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(originalEnd));
        Assert.That(Camera.main.backgroundColor, Is.EqualTo(originalSky));
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True);
        Call(spawner, "ReleaseSafeGap", game);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator OrdinaryOneTwoThreeRockGroupsStayClearAndLeadIntoStorm()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        var spawner = Find("DesertObstacleSpawner"); ((Behaviour)spawner).enabled = false;
        UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>().RunState.MinimumPursuitGap = 6f;
        var meter = Find("FuelMeter");
        var drainMode = meter.GetType().GetProperty("DrainMode");
        drainMode.SetValue(meter, Enum.Parse(drainMode.PropertyType, "Frozen"));
        var controller = Find("DesertCrossingController");
        var storm = Find("DesertSandstorm");
        float originalEnd = RenderSettings.fogEndDistance;
        for (int count = 1; count <= 3; count++)
        {
            Set(controller, "nextIsStorm", false);
            Set(controller, "minimumOrdinaryRocks", count);
            Set(controller, "maximumOrdinaryRocks", count);
            Set(controller, "timer", 0f);
            yield return null;
            Assert.That((bool)Get(controller, "CrossingActive"), Is.True);
            Assert.That((bool)Get(controller, "StormRegionActive"), Is.False);
            Assert.That((int)Get(controller, "TargetRockCount"), Is.EqualTo(count));
            float until = Time.time + 12f;
            while ((bool)Get(controller, "CrossingActive") && Time.time < until)
            {
                Assert.That((float)Get(storm, "Intensity"), Is.Zero);
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(originalEnd));
                yield return null;
            }
            Assert.That((bool)Get(controller, "CrossingActive"), Is.False);
            Assert.That((int)Get(controller, "RocksSpawnedInRegion"), Is.EqualTo(count));
            Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True);
            Call(spawner, "ReleaseSafeGap", game);
        }
        // Do not force the next mode: verify the real scheduler goes from ordinary to storm.
        float nextDeadline = Time.time + 10f;
        while (!(bool)Get(controller, "CrossingActive") && Time.time < nextDeadline) yield return null;
        Assert.That((bool)Get(controller, "StormRegionActive"), Is.True);
        Assert.That((int)Get(controller, "TargetRockCount"), Is.InRange(10, 12));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator OrdinaryCrossingsRespectPickupAndPortalReservations()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        var spawner = Find("DesertObstacleSpawner"); ((Behaviour)spawner).enabled = false;
        var controller = Find("DesertCrossingController");
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True);
        Set(controller, "timer", 0f);
        yield return null; yield return null;
        Assert.That((bool)Get(controller, "CrossingActive"), Is.False);
        Assert.That((int)Get(controller, "ActiveRockCount"), Is.Zero);
        Call(spawner, "ReleaseSafeGap", game);
        yield return null;
        Assert.That((bool)Get(controller, "CrossingActive"), Is.True);
        Assert.That((bool)Get(controller, "StormRegionActive"), Is.False);
        Assert.That((int)Get(controller, "TargetRockCount"), Is.InRange(1, 3));
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.False);
        ((Behaviour)controller).enabled = false;
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True, "Disabling the controller must not strand the reservation.");
        Call(spawner, "ReleaseSafeGap", game);
        yield return new ExitPlayMode();
    }

    private static void CaptureStormPreview()
    {
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outlaws-desert-storm.png"), image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
    }
}
