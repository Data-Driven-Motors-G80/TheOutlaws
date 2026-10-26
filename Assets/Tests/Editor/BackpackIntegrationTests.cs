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
    public IEnumerator UnchosenMysteryBoxesStayVisibleThenFadeBehindPlayer()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return CheckPickupExit();
        yield return new ExitPlayMode();
    }

    private IEnumerator CheckPickupExit()
    {
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        var boxes = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None)
            .OrderBy(x => x.transform.position.x).ToArray();
        Assert.That(boxes.Length, Is.EqualTo(3));
        Vector3 row = boxes[1].transform.position;
        Vector3 forward = boxes[1].transform.forward;
        effects.transform.position = row;
        Call(boxes[1], "OnTriggerEnter", effects.GetComponentInChildren<Collider>());
        Assert.That(boxes[1].gameObject.activeSelf, Is.False);
        Assert.That(boxes[0].gameObject.activeInHierarchy && boxes[2].gameObject.activeInHierarchy, Is.True);
        Assert.That(boxes[0].GetComponent<BoxCollider>().enabled, Is.False, "Only one reward per row.");
        effects.transform.position = row + forward * 6f;
        var spawner = Find("PickupSpawner");
        Call(spawner, "Update");
        Assert.That(boxes[0].gameObject.activeInHierarchy, Is.True, "Passing must start a fade, not instantly delete the box.");
        Call(game, "Pause", "test");
        float pauseUntil = Time.realtimeSinceStartup + 1.1f;
        while (Time.realtimeSinceStartup < pauseUntil) yield return null;
        Assert.That(boxes[0] != null && boxes[0].gameObject.activeInHierarchy, Is.True, "Fade pauses with gameplay.");
        Call(game, "Resume");
        float fadeUntil = Time.time + 0.6f;
        while (Time.time < fadeUntil) yield return null;
        Assert.That(boxes[0] != null, Is.True);
        var visual = boxes[0].GetComponentInChildren<MeshRenderer>();
        var properties = new MaterialPropertyBlock();
        visual.GetPropertyBlock(properties);
        Assert.That(properties.GetColor("_BaseColor").a, Is.InRange(0.01f, 0.7f));
        fadeUntil = Time.time + 0.5f;
        while (Time.time < fadeUntil) yield return null;
        Assert.That(boxes[0] == null && boxes[2] == null, Is.True);
    }

    [UnityTest]
    public IEnumerator OceanReservationPreservesVisibleHazardsAndPause()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_OceanCoast.unity");
        yield return new EnterPlayMode(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var coast = Find("OceanCoastController");
        var obstacles = Find("OceanObstacleSpawner");
        var driver = Find("AutoDriveCar");
        float at = (float)Get(driver, "RoadPositionDistance");
        Assert.That((bool)Call(coast, "SpawnWave", at + 65f), Is.True);
        Assert.That((bool)Call(coast, "SpawnWake", at, 0f), Is.True);
        var waves = (IList)coast.GetType().GetField("waves", Flags).GetValue(coast);
        var wakes = (IList)coast.GetType().GetField("wakes", Flags).GetValue(coast);
        int waveCount = waves.Count, wakeCount = wakes.Count;
        var visual = (GameObject)wakes[0].GetType().GetField("Visual").GetValue(wakes[0]);
        obstacles.GetType().GetProperty("SuppressAutoSpawn").SetValue(obstacles, true);
        coast.GetType().GetField("wakeTimer", Flags).SetValue(coast, 0f);
        Call(coast, "Update");
        Assert.That(waves.Count, Is.EqualTo(waveCount));
        Assert.That(wakes.Count, Is.EqualTo(wakeCount), "Reservation must neither erase nor spawn visible wakes.");
        Assert.That(visual.activeInHierarchy, Is.True);
        float warning = (float)wakes[0].GetType().GetField("WarningRemaining").GetValue(wakes[0]);
        Call(game, "Pause", "test");
        yield return null; yield return null;
        Assert.That((float)wakes[0].GetType().GetField("WarningRemaining").GetValue(wakes[0]), Is.EqualTo(warning));
        Call(game, "Resume");
        obstacles.GetType().GetProperty("SuppressAutoSpawn").SetValue(obstacles, false);
        ((Behaviour)coast).enabled = false;
        Assert.That(waves.Count, Is.Zero);
        Assert.That(wakes.Count, Is.Zero);
        Assert.That((float)Get(driver, "EnvironmentSpeedMultiplier"), Is.EqualTo(1f));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PickupPacingAcrossEnvironmentsAndSpeeds()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return CheckPacingScenarios();
        yield return new ExitPlayMode();
    }

    private IEnumerator CheckPacingScenarios()
    {
        foreach (string scene in new[] { "Env_City", "Env_Snow", "Env_Desert", "Env_OceanCoast" })
        foreach (float speed in new[] { 20f, 55f })
        {
            var session = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RunSession")).First(t => t != null);
            session.GetMethod("ClearPending").Invoke(null, null);
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            yield return null; yield return null;
            var game = Find("OutlawGameManager");
            var driver = Find("AutoDriveCar");
            driver.GetType().GetField("forwardSpeed", Flags).SetValue(driver, speed);
            driver.GetType().GetField("maximumForwardSpeed", Flags).SetValue(driver, speed);
            ((Behaviour)Find("EnvironmentSwitcher")).enabled = false;
            Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
            var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
            var fuel = Find("FuelMeter");
            var spawner = Find("PickupSpawner");
            var obstacles = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(x => x.GetType().GetMethod("TryReservePickupGap", Flags) != null);
            Type obstacleType = obstacles.GetType();
            while (obstacleType.GetField("distanceUntilSpawn", Flags) == null) obstacleType = obstacleType.BaseType;
            var timer = obstacleType.GetField("distanceUntilSpawn", Flags);
            int rewards = 0, rows = 0;
            float lastReward = 0f, longestRewardGap = 0f, lastRow = 0f, longestRowGap = 0f, previous = 0f;
            Action<PickupKind> spawned = kind => {
                if (kind != PickupKind.Mystery) return;
                float now = (float)Get(game, "ElapsedSeconds");
                longestRewardGap = Mathf.Max(longestRewardGap, now - lastReward);
                lastReward = now; rewards++;
            };
            GameEvents.PickupSpawned += spawned;
            float deadline = Time.realtimeSinceStartup + 30f;
            bool usedNitro = false;
            try
            {
                while ((float)Get(game, "ElapsedSeconds") < 65f && Time.realtimeSinceStartup < deadline)
                {
                    effects.RefillFuel(100f); effects.RepelPolice(100f);
                    Call(fuel, "SetFuel", (float)Get(fuel, "MaximumFuel"));
                    float now = (float)Get(game, "ElapsedSeconds");
                    if (!usedNitro && now > 30f) { effects.TryApply(PickupEffectType.Boost); usedNitro = true; }
                    else if (usedNitro && effects.Backpack.Item == RandomPickupOutcome.Nitro) effects.UseBackpack();
                    Time.timeScale = 8f;
                    float remaining = (float)timer.GetValue(obstacles);
                    if (remaining > previous + 8f) { rows++; longestRowGap = Mathf.Max(longestRowGap, now - lastRow); lastRow = now; }
                    previous = remaining;
                    Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
                    yield return null;
                }
                Assert.That((float)Get(game, "ElapsedSeconds"), Is.GreaterThanOrEqualTo(65f));
                Assert.That(rewards, Is.GreaterThanOrEqualTo(1), "Mystery rows must not starve: " + scene);
                Assert.That(rows, Is.GreaterThan(5), "Obstacle spawning must continue.");
                Debug.Log($"Pickup pacing {scene} {speed} m/s: {rewards} later reward rows, {rows} obstacle rows; longest observed spawn gaps {longestRewardGap:F1}s reward / {longestRowGap:F1}s obstacle.");
            }
            finally { GameEvents.PickupSpawned -= spawned; }
            Call(spawner, "ClearAndSuppress");
            Time.timeScale = 1f;
        }
    }

    [UnityTest]
    public IEnumerator MysteryGapCanBeSkippedOrEnteredWhileOverlapping()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        yield return CheckMysteryCenterCollection();
        yield return new ExitPlayMode();
    }

    private IEnumerator CheckMysteryCenterCollection()
    {
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        var boxes = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None)
            .OrderBy(x => x.transform.position.x).ToArray();
        Assert.That(boxes.Length, Is.EqualTo(3));
        var original = effects.transform.position;
        var body = effects.GetComponentInChildren<Collider>();
        int count = effects.PickupCount;
        effects.transform.position = (boxes[0].transform.position + boxes[1].transform.position) * .5f;
        foreach (var box in boxes) Call(box, "OnTriggerEnter", body);
        foreach (var box in boxes) Call(box, "OnTriggerStay", body);
        Assert.That(effects.PickupCount, Is.EqualTo(count), "Threading the visible gap must avoid collection.");
        effects.transform.position = boxes[1].transform.position;
        foreach (var box in boxes) Call(box, "OnTriggerStay", body);
        Assert.That(effects.PickupCount, Is.EqualTo(count + 1), "Steering into a box must collect exactly one reward.");
        effects.transform.position = original;
        var obstacles = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .First(x => x.GetType().GetMethod("TryReservePickupGap", Flags) != null);
        effects.Backpack.Restore(RandomPickupOutcome.None, 0);
        foreach (TrafficObstacleState state in Enum.GetValues(typeof(TrafficObstacleState)))
        {
            var wall = (Component)Call(obstacles, "SpawnTutorialWall", 40f, state);
            var collider = wall.GetComponentInChildren<Collider>();
            for (int shot = 0; shot < 3 && wall.gameObject.activeInHierarchy; shot++)
                Assert.That((bool)Call(obstacles, "TryApplyShot", collider), Is.True);
            Assert.That(wall.gameObject.activeInHierarchy, Is.False);
        }
        Assert.That(effects.Backpack.Shots, Is.Zero);
        Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Count(x => x.GetType().Name == "OutlawAmmoPickup"), Is.Zero, "No obstacle strength may drop legacy ammunition.");
    }

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
        Assert.That(UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Length, Is.EqualTo(3), "Search ahead for a natural gap.");
        Assert.That((bool)Get(obstacles, "SuppressAutoSpawn"), Is.False);
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
        foreach (var target in new[] { "Env_Snow", "Env_City", "Env_Desert", "Env_OceanCoast", "Env_City", "Env_Desert", "Env_Snow", "Env_OceanCoast", "Env_City" })
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
    [UnityTest]
    public IEnumerator MysteryVisualsMatchCollectionWidthAndKeepTriggersSteady()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var boxes = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
        Assert.That(boxes.Length, Is.EqualTo(3));
        foreach (var box in boxes)
        {
            var trigger = box.GetComponent<BoxCollider>();
            var visual = box.GetComponentInChildren<MeshRenderer>();
            Assert.That(trigger.size.x * box.transform.lossyScale.x, Is.EqualTo(visual.bounds.size.x).Within(.01f));
            Assert.That(visual.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(visual.sharedMaterial.color.a, Is.InRange(.65f,.8f));
            Assert.That(visual.transform.localPosition.y, Is.GreaterThan(.5f));
        }
        float width = boxes[0].GetComponent<BoxCollider>().size.x;
        float spacing = Vector3.Distance(boxes[0].transform.position,boxes[1].transform.position);
        // FindObjects order is unspecified; measure nearest neighbour lane spacing.
        spacing = Mathf.Min(spacing,Vector3.Distance(boxes[0].transform.position,boxes[2].transform.position));
        Assert.That(width / spacing, Is.EqualTo(.4f).Within(.01f));
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        effects.Backpack.Restore(RandomPickupOutcome.Shield,0);
        var spawner = Find("PickupSpawner");
        Call(spawner,"SpawnTutorialMystery",22f,0f,PickupEffectType.Shield);
        yield return null;
        yield return null;
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator DashboardFeedbackShowsOverlappingEffectsAndEmptyInput()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_City.unity");
        yield return new EnterPlayMode(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        yield return null;
        var hud = Find("OutlawHUD");
        var effects = UnityEngine.Object.FindFirstObjectByType<CarPickupEffects>();
        effects.Backpack.Restore(RandomPickupOutcome.Shield,0);
        Assert.That(effects.UseBackpack(),Is.True);
        effects.Backpack.Restore(RandomPickupOutcome.Nitro,0);
        Assert.That(effects.UseBackpack(),Is.True);
        Call(hud,"RefreshActiveIcons");
        var icons=(IList)hud.GetType().GetField("activeIcons",Flags).GetValue(hud);
        Assert.That(icons.Contains(RandomPickupOutcome.Shield),Is.True);
        Assert.That(icons.Contains(RandomPickupOutcome.Nitro),Is.True);
        Assert.That((float)hud.GetType().GetField("activationFlash",Flags).GetValue(hud),Is.GreaterThan(0));
        Assert.That(effects.UseBackpack(),Is.False);
        Assert.That((float)hud.GetType().GetField("activationFlash",Flags).GetValue(hud),Is.Zero);
        Assert.That((float)hud.GetType().GetField("emptyFlash",Flags).GetValue(hud),Is.GreaterThan(0));
        effects.Backpack.Restore(RandomPickupOutcome.Gun,3);
        Assert.That(effects.UseBackpack(),Is.True);
        Assert.That(effects.Backpack.Shots,Is.EqualTo(2));
        float flash=(float)hud.GetType().GetField("activationFlash",Flags).GetValue(hud);
        Call(game,"Pause","hud-test");
        yield return null;
        Assert.That((float)hud.GetType().GetField("activationFlash",Flags).GetValue(hud),Is.EqualTo(flash));
        Call(game,"Resume");
        effects.Backpack.Restore(RandomPickupOutcome.SunnyDay,0);
        Assert.That(effects.UseBackpack(),Is.True);
        effects.TryApply(PickupEffectType.ReverseSteering);
        effects.Backpack.Restore(RandomPickupOutcome.ProximityRecovery,0);
        Assert.That(effects.UseBackpack(),Is.True);
        Call(hud,"RefreshActiveIcons");
        Assert.That(icons.Count,Is.EqualTo(5),"All simultaneous effects and the instant-use confirmation must remain visible.");
        Assert.That((float)hud.GetType().GetField("jammerFeedback",Flags).GetValue(hud),Is.GreaterThan(0));
        Assert.That(effects.TryConsumeShield(),Is.True);
        Call(hud,"Update");
        Assert.That((float)hud.GetType().GetField("shieldFeedback",Flags).GetValue(hud),Is.GreaterThan(0));
        var mirror=Find("RearViewCamera");
        var texture=(RenderTexture)Get(mirror,"Texture");
        Assert.That(texture.width,Is.EqualTo(512));
        Assert.That(texture.height,Is.EqualTo(128));
        Assert.That(mirror.GetComponent<Camera>().aspect,Is.EqualTo(4f).Within(.01f));
        foreach(var icon in new[]{"empty","gun","shield","nitro","jammer","sun","reverse"})
            Assert.That(Resources.Load<Texture2D>("BackpackIcons/"+icon),Is.Not.Null);
        yield return new ExitPlayMode();
    }
    [UnityTearDown] public IEnumerator Exit() { if(Application.isPlaying) yield return new ExitPlayMode(); }
}
