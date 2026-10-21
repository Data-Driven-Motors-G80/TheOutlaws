using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BackpackIntegrationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Component Find(string type) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(x => x.GetType().Name == type);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static object Get(object target, string property) => target.GetType().GetProperty(property, Flags).GetValue(target);

    [UnityTest]
    public IEnumerator BackpackUsesAndRowCollectionWorkInSavedScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode();
        yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        var gun = Find("OutlawShooting");
        Assert.That(effects.Backpack.Item, Is.EqualTo(RandomPickupOutcome.None));
        effects.TryApply(PickupEffectType.Gun);
        Assert.That(effects.UseBackpack(), Is.False, "Must not use a reward on its collection frame.");
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            yield return null; Call(gun,"TryShoot"); Assert.That(effects.Backpack.Shots, Is.EqualTo(remaining));
        }
        effects.TryApply(PickupEffectType.Shield); Assert.That(effects.ShieldReady, Is.False);
        yield return null; Call(gun,"TryShoot"); Assert.That(effects.ShieldReady, Is.True);
        effects.TryApply(PickupEffectType.Gun); Assert.That(effects.ShieldReady, Is.True);
        effects.TryApply(PickupEffectType.ReverseSteering); Assert.That(effects.Backpack.Shots, Is.EqualTo(3));
        effects.TryApply(PickupEffectType.ProximityRecovery); Assert.That(effects.Backpack.Shots, Is.Zero);
        var spawner = Find("PickupSpawner");
        Call(spawner,"RestoreSchedule",(float)Get(game,"ElapsedSeconds")); Call(spawner,"Update");
        var boxes = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        Assert.That(boxes.Length, Is.EqualTo(3));
        int count = effects.PickupCount;
        var body = effects.GetComponentInChildren<Collider>();
        foreach(var box in boxes) Call(box,"OnTriggerEnter",body);
        Assert.That(effects.PickupCount, Is.EqualTo(count+1));
        Assert.That((float)Get(spawner,"NextRowAt"), Is.GreaterThanOrEqualTo((float)Get(game,"ElapsedSeconds")+19.9f));
        effects.Backpack.Restore(RandomPickupOutcome.Gun,2);
        object snapshot = Call(game,"CaptureRun");
        var sessionType = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RunSession")).First(t=>t!=null);
        sessionType.GetMethod("SetPending").Invoke(null,new[]{snapshot});
        UnityEngine.SceneManagement.SceneManager.LoadScene("Env_Snow");
        yield return null; yield return null;
        effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        Assert.That(effects.Backpack.Shots, Is.EqualTo(2)); Assert.That(effects.ShieldReady, Is.True);
        Assert.That(effects.ReverseCountdownRemaining > 0 || effects.ActiveEffect == PickupEffectType.ReverseSteering, Is.True);
        game=Find("OutlawGameManager"); snapshot=Call(game,"CaptureRun");
        sessionType.GetMethod("SetPending").Invoke(null,new[]{snapshot});
        UnityEngine.SceneManagement.SceneManager.LoadScene("Env_City");
        yield return null; yield return null;
        effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>(); Assert.That(effects.Backpack.Shots, Is.EqualTo(2));
        game=Find("OutlawGameManager"); Call(game,"Pause","test"); Assert.That(Time.timeScale, Is.Zero); Call(game,"Resume");
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator TutorialStoresRewardsAndPreservesActivatedNitroOnHandoff()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        var game=Find("OutlawGameManager"); Call(game,"SetTutorialSelected",true); Call(game,"BeginGame");
        yield return null;
        var tutorial=Find("TutorialDirector");
        var stepType=tutorial.GetType().GetProperty("Step").PropertyType;
        var effects=UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        Call(tutorial,"EnterStep",Enum.Parse(stepType,"Ammo"));
        var box=UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).First();
        Call(box,"OnTriggerEnter",effects.GetComponentInChildren<Collider>());
        Assert.That(effects.Backpack.Shots,Is.EqualTo(3));
        yield return null;
        foreach(var lesson in new[]{"Shield","Recovery","Nitro"})
        {
            Call(tutorial,"EnterStep",Enum.Parse(stepType,lesson));
            yield return null;
            box=UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).First();
            Call(box,"OnTriggerEnter",effects.GetComponentInChildren<Collider>());
            Assert.That(effects.Backpack.Item,Is.Not.EqualTo(RandomPickupOutcome.None));
            Assert.That((string)Get(tutorial,"Title"),Does.Contain("SPACE"));
            if(lesson=="Shield") Assert.That(effects.ShieldReady,Is.False);
            if(lesson=="Nitro") Assert.That(effects.RunState.HasNitro,Is.False);
            yield return null;
            Call(Find("OutlawShooting"),"TryShoot");
            Assert.That(effects.Backpack.Item,Is.EqualTo(RandomPickupOutcome.None));
            if(lesson!="Nitro") Assert.That((bool)tutorial.GetType().GetField("lessonCollected",Flags).GetValue(tutorial),Is.True);
            yield return null;
        }
        Assert.That(effects.RunState.HasNitro,Is.True);
        Call(tutorial,"Finish");
        Assert.That((bool)Get(game,"IsTutorial"),Is.False);
        Assert.That(effects.Backpack.Item,Is.EqualTo(RandomPickupOutcome.None));
        Assert.That(effects.RunState.HasNitro,Is.True);
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator SafeGapsPreserveCityObstacles() => CheckSafeGaps("Env_City");

    [UnityTest]
    public IEnumerator SafeGapsPreserveSnowObstacles() => CheckSafeGaps("Env_Snow");

    private IEnumerator CheckSafeGaps(string scene)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
        yield return new EnterPlayMode(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var spawner = Find("PickupSpawner");
        var obstacles = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .First(x => x.GetType().GetMethod("TryReserveSafeGap", Flags) != null);
        var portal = Find("EnvironmentSwitcher");
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        Call(spawner, "ClearAndSuppress");
        spawner.GetType().GetProperty("Suppressed").SetValue(spawner, false);
        var wall = (Component)Call(obstacles, "SpawnTutorialWall", 95f, TrafficObstacleState.Green);
        Call(spawner, "RestoreSchedule", (float)Get(game, "ElapsedSeconds"));
        Call(spawner, "Update");
        Assert.That(wall.gameObject.activeInHierarchy, Is.True, "Waiting must not delete the blocking obstacle.");
        Assert.That(UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Length, Is.Zero);
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.True);
        Assert.That((bool)Call(obstacles, "TryReserveSafeGap", portal), Is.False);
        // Simulate the existing obstacle having passed the proposed row.
        wall.transform.position = effects.transform.position;
        Call(spawner, "Update");
        Assert.That(UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
        Call(spawner, "Update");
        Assert.That(wall.gameObject.activeInHierarchy, Is.True);
        Call(spawner, "ClearAndSuppress");
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.False);

        wall = (Component)Call(obstacles, "SpawnTutorialWall", 95f, TrafficObstacleState.Green);
        var session = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RunSession")).First(t => t != null);
        session.GetField("NextPortalAt").SetValue(null, 0f);
        Call(portal, "UpdatePortal");
        Assert.That(wall.gameObject.activeInHierarchy, Is.True);
        Assert.That(portal.GetType().GetField("portal", Flags).GetValue(portal), Is.Null);
        wall.transform.position = effects.transform.position;
        Call(portal, "UpdatePortal");
        Assert.That(portal.GetType().GetField("portal", Flags).GetValue(portal), Is.Not.Null);
        Assert.That(wall.gameObject.activeInHierarchy, Is.True);
        ((Behaviour)portal).enabled = false;
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.False);
        // Releasing a gap must not release the tutorial's independent suppression.
        Call(obstacles, "TryReserveSafeGap", spawner);
        obstacles.GetType().GetProperty("SuppressAutoSpawn").SetValue(obstacles, true);
        Call(obstacles, "ReleaseSafeGap", spawner);
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.True);
        Call(obstacles, "BeginRealRun");
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.False);
        yield return new ExitPlayMode();
    }
    private static Type TransitionType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("SceneTransition");
            if (type != null) return type;
        }
        throw new InvalidOperationException("SceneTransition type missing");
    }

    [UnityTest]
    public IEnumerator PortalRoundTripsWithFogRestoreTheRunAndRemoveOverlay()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return PlayPortalRoundTrips();
        yield return new ExitPlayMode();
    }

    private IEnumerator PlayPortalRoundTrips()
    {
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var transitionType = TransitionType();
        foreach (var target in new[] { "Env_Snow", "Env_City", "Env_Snow", "Env_City" })
        {
            var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
            effects.Backpack.Restore(RandomPickupOutcome.Gun, 2);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Env_Snow")
            {
                var fog = Find("SnowFogObstacle");
                Assert.That((bool)Call(fog, "TrySpawn"), Is.True);
                var list = (IList)fog.GetType().GetField("active", Flags).GetValue(fog);
                var block = list[0];
                var root = (GameObject)block.GetType().GetField("Root").GetValue(block);
                UnityEngine.Object.DestroyImmediate(root);
                Call(fog, "OnDisable"); // Reproduce the logged destruction-order failure.
                Assert.That(list.Count, Is.Zero);
                Assert.That((bool)Call(fog, "TrySpawn"), Is.True); // Leave live fog for real scene unload.
            }
            Call(Find("EnvironmentSwitcher"), "StartSwitch", target);
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((bool)transitionType.GetProperty("IsRunning").GetValue(null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            Assert.That((bool)transitionType.GetProperty("IsRunning").GetValue(null), Is.False);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo(target));
            Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType() == transitionType), Is.False);
            Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Get(Find("OutlawGameManager"), "State").ToString(), Is.EqualTo("Running"));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>().Backpack.Shots, Is.EqualTo(2));
        }
    }

    [UnityTest]
    public IEnumerator FailedTransitionsDoNotLeaveAnOverlay()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return PlayFailedTransitions();
        yield return new ExitPlayMode();
    }

    private IEnumerator PlayFailedTransitions()
    {
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var type = TransitionType();
        int cancelled = 0;
        Action cancel = () => cancelled++;
        bool failureLogged = false;
        Application.LogCallback captureLog = (message, stack, severity) =>
        {
            if (severity == LogType.Error && message.Contains("transition to 'Env_Snow' failed:")) failureLogged = true;
        };
        Application.logMessageReceived += captureLog;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            type.GetMethod("Run").Invoke(null, new object[] { "Env_Snow", .1f, Color.white, null,
                (Action)(() => throw new InvalidOperationException("Injected capture failure")), cancel });
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((bool)type.GetProperty("IsRunning").GetValue(null) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Application.logMessageReceived -= captureLog;
        }
        Assert.That(failureLogged, Is.True);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That((bool)type.GetProperty("IsRunning").GetValue(null), Is.False);
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo("Env_City"));
        Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x.GetType() == type), Is.False);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SceneTransition: destination.*not available"));
        type.GetMethod("Run").Invoke(null, new object[] { "MissingScene", .1f, Color.white, null, null, cancel });
        Assert.That(cancelled, Is.EqualTo(2));
        Assert.That((bool)type.GetProperty("IsRunning").GetValue(null), Is.False);
    }
    [UnityTest]
    public IEnumerator EndlessRunSoakKeepsStreamingBounded()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return PlayEndlessSoak();
        yield return new ExitPlayMode();
    }

    private IEnumerator PlayEndlessSoak()
    {
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        float deadline = Time.realtimeSinceStartup + 180f;
        float nextCheck = 0f;
        int switches = 0;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        while (Time.realtimeSinceStartup < deadline)
        {
            game = Find("OutlawGameManager");
            var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
            // Test-only survival assistance lets streaming run for ten simulated minutes.
            effects.RefillFuel(100f);
            effects.RepelPolice(100f);
            var fuel = Find("FuelMeter");
            Call(fuel, "SetFuel", (float)Get(fuel, "MaximumFuel"));
            Time.timeScale = 10f;
            string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (current != scene) { switches++; scene = current; }
            float elapsed = (float)Get(game, "ElapsedSeconds");
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            if (elapsed >= nextCheck)
            {
                nextCheck = elapsed + 10f;
                var objects = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Assert.That(objects.Count(x => x.GetType().Name == "RoadSegment"), Is.LessThan(40));
                Assert.That(objects.Count(x => x.GetType().Name == "TrafficLightObstacle"), Is.LessThan(128));
                Assert.That(objects.Count(x => x.GetType().Name == "PickupItem"), Is.LessThan(12));
                Assert.That(Camera.main != null && Camera.main.isActiveAndEnabled, Is.True);
            }
            if (elapsed >= 600f)
            {
                Assert.That(switches, Is.GreaterThanOrEqualTo(6));
                Debug.Log("Endless soak completed: " + elapsed + " simulated seconds, " + switches + " environment changes.");
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Endless soak did not reach ten simulated minutes within its time limit.");
    }
    [UnityTearDown] public IEnumerator Exit() { if(Application.isPlaying) yield return new ExitPlayMode(); }
}
