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
    private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);

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

    [UnityTest]
    public IEnumerator CrossingPhasesReachAllThreeLanesAtNormalSpeed() => CheckCrossingPhases(25f);

    [UnityTest]
    public IEnumerator CrossingPhasesReachAllThreeLanesAtMaximumSpeed() => CheckCrossingPhases(55f);

    private IEnumerator CheckCrossingPhases(float speed)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        ((Behaviour)game).enabled = false; // Keep the chosen speed steady while exercising actual driving.
        var driver = Find("AutoDriveCar");
        Call(driver, "SetDifficultySpeed", speed);
        // Observe rocks at the crossing plane without resolving them at the front bumper.
        foreach (var collider in driver.GetComponentsInChildren<Collider>()) collider.enabled = false;
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        var spawner = Find("DesertObstacleSpawner"); ((Behaviour)spawner).enabled = false;
        var controller = Find("DesertCrossingController");
        Set(controller, "minimumOrdinaryRocks", 3);
        Set(controller, "maximumOrdinaryRocks", 3);
        Set(controller, "timer", 0f);
        var observations = new System.Collections.Generic.Dictionary<Component, CrossingObservation>();
        var lanes = new System.Collections.Generic.HashSet<int>();
        float until = Time.time + 25f;
        bool started = false;
        while (Time.time < until)
        {
            yield return null;
            if ((bool)Get(controller, "CrossingActive")) started = true;
            if (started && !(bool)Get(controller, "CrossingActive")) break;
            float distance = (float)Get(driver, "RoadPositionDistance");
            foreach (object item in (IEnumerable)Field(controller, "crossings"))
            {
                var rock = (Component)Field(item, "Rock");
                float at = (float)Field(item, "At");
                float lateral = (float)Get(rock, "Lateral");
                if (!observations.TryGetValue(rock, out CrossingObservation sample))
                {
                    sample = new CrossingObservation { Distance = distance, Lateral = lateral, At = at };
                    observations.Add(rock, sample);
                    Assert.That((at - distance) / speed, Is.GreaterThan(2.4f), "A rock must not appear inside the reaction window.");
                }
                if ((bool)Field(item, "Armed") && sample.ArmedAt < 0f) sample.ArmedAt = Time.time;
                if ((bool)Field(item, "Moving") && !sample.StartedMoving)
                {
                    sample.StartedMoving = true;
                    Assert.That(Time.time - sample.ArmedAt, Is.GreaterThanOrEqualTo(1.5f - 2f * Time.deltaTime), "Keep the full warning.");
                }
                if (!sample.Passed && distance >= at && sample.Distance < at)
                {
                    sample.Passed = true;
                    float crossingLateral = Mathf.Lerp(sample.Lateral, lateral, (at - sample.Distance) / (distance - sample.Distance));
                    object point = SampleRoad(Find("InfiniteRoad"), at);
                    float laneWidth = (float)Get(point, "Width") / 3f;
                    int lane = Mathf.RoundToInt(crossingLateral / laneWidth);
                    Assert.That(lane, Is.InRange(-1, 1));
                    Assert.That(Mathf.Abs(crossingLateral - lane * laneWidth), Is.LessThan(0.5f), "Rocks should meet the lane centres, not all the road centre.");
                    lanes.Add(lane);
                }
                sample.Distance = distance; sample.Lateral = lateral;
            }
        }
        Assert.That(started, Is.True, "Crossings must still start at the chosen speed.");
        Assert.That((bool)Get(controller, "CrossingActive"), Is.False);
        Assert.That(observations.Count, Is.EqualTo(3));
        Assert.That(lanes.SetEquals(new[] { -1, 0, 1 }), Is.True, "Camping in either edge lane must no longer avoid every rock.");
        var ordered = observations.Values.OrderBy(s => s.At).ToArray();
        for (int i = 1; i < ordered.Length; i++)
            Assert.That((ordered[i].At - ordered[i - 1].At) / (speed * 1.2f), Is.GreaterThanOrEqualTo(1.24f), "Keep the existing steering interval even with nitro.");
        Assert.That(Call(spawner, "TryReserveSafeGap", game), Is.True, "Release the gap for pickups and portals.");
        Call(spawner, "ReleaseSafeGap", game);
        yield return new ExitPlayMode();
    }

    private sealed class CrossingObservation
    {
        public float Distance, Lateral, At;
        public float ArmedAt = -1f;
        public bool StartedMoving, Passed;
    }

    [Test]
    public void LaterOrdinaryGroupsFavourTwoOrThreeWhileHonouringInspectorLimits()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DesertCrossingPattern")).First(t => t != null);
        var choose = type.GetMethod("OrdinaryCount");
        var counts = new int[4];
        for (int i = 0; i < 1000; i++)
            counts[(int)choose.Invoke(null, new object[] { 1, 3, true, (i + 0.5d) / 1000d })]++;
        Assert.That(counts, Is.EqualTo(new[] { 0, 200, 400, 400 }));
        for (int count = 1; count <= 3; count++)
            foreach (double roll in new[] { 0d, 0.5d, 0.999d })
                Assert.That(choose.Invoke(null, new object[] { count, count, true, roll }), Is.EqualTo(count));
    }

    [UnityTest]
    public IEnumerator SandClearsAllLanesAndPoliceAcrossHillsHairpinsAndStreaming()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Env_Desert.unity");
        yield return new EnterPlayMode(); DisableUploads(); yield return null;
        var game = Find("OutlawGameManager");
        Call(game, "SetTutorialSelected", false); Call(game, "BeginGame");
        var driver = Find("AutoDriveCar");
        var road = Find("InfiniteRoad");
        ((Behaviour)driver).enabled = false;
        ((Behaviour)Find("PickupSpawner")).enabled = false;
        ((Behaviour)Find("DesertObstacleSpawner")).enabled = false;
        ((Behaviour)Find("DesertCrossingController")).enabled = false;
        Time.timeScale = 0f; // Sample the real streamed route without input, fuel or pursuit simulation.
        bool climbed = false, descended = false, turnedBack = false, legacyPlaneWouldClip = false;
        for (float distance = 30f; distance <= 3030f; distance += 15f)
        {
            object point = SampleRoad(road, distance);
            Vector3 position = (Vector3)Get(point, "Position");
            Vector3 forward = (Vector3)Get(point, "Forward");
            Vector3 up = (Vector3)Get(point, "Up");
            driver.transform.SetPositionAndRotation(position + up * 0.5f, Quaternion.LookRotation(forward, up));
            climbed |= forward.y > 0.025f;
            descended |= forward.y < -0.025f;
            turnedBack |= forward.z < 0f;
            yield return null; yield return null;

            var backdrop = GameObject.Find("Prototype sand surround");
            Assert.That(backdrop, Is.Not.Null);
            foreach (var segment in road.GetComponentsInChildren<MonoBehaviour>().Where(c => c.GetType().Name == "RoadSegment"))
            {
                int count = (int)Get(segment, "PathPointCount");
                for (int i = 0; i < count; i++)
                {
                    float pathY = ((Vector3)Call(segment, "GetPathPoint", i)).y;
                    Assert.That(backdrop.transform.position.y, Is.LessThanOrEqualTo(pathY - 1.9f),
                        "Sand backdrop must clear the whole loaded road, including behind the player.");
                    legacyPlaneWouldClip |= pathY < position.y - 0.12f;
                }
                var bank = segment.transform.Find("Desert roadside sand");
                Assert.That(bank, Is.Not.Null, "Every pooled road instance needs its own banks.");
                Assert.That(bank.GetComponent<Collider>(), Is.Null, "Sand must not change driving collisions.");
            }
            var sandMeshes = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.name == "Desert roadside sand" || f.name == "Prototype sand surround" || f.name.StartsWith("Prototype dune "))
                .Where(f => f.GetComponent<Renderer>().enabled).ToArray();
            foreach (float offset in new[] { -10f, 0f, 30f })
            {
                object probe = SampleRoad(road, distance + offset);
                Vector3 roadPoint = (Vector3)Get(probe, "Position");
                Vector3 right = (Vector3)Get(probe, "Right");
                float halfWidth = (float)Get(probe, "Width") * 0.5f;
                foreach (float lateral in new[] { -halfWidth + 0.5f, 0f, halfWidth - 0.5f })
                    foreach (MeshFilter filter in sandMeshes)
                        Assert.That(HighestSurfaceAt(filter, roadPoint + right * lateral), Is.LessThan(roadPoint.y - 0.02f),
                            filter.name + " covers a lane at " + (distance + offset) + "m.");
            }
            foreach (var police in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c => c.GetType().Name == "ChaseCar"))
                foreach (var renderer in police.GetComponentsInChildren<Renderer>())
                    Assert.That(renderer.bounds.min.y, Is.GreaterThan(backdrop.transform.position.y), "The backdrop must not bury the pursuing car.");
            if ((distance == 705f || distance == 780f) && System.Environment.GetEnvironmentVariable("OUTLAWS_DESERT_PREVIEW") == "1")
            {
                Camera.main.transform.SetPositionAndRotation(position - forward * 8f + up * 4f,
                    Quaternion.LookRotation(forward * 20f - up * 1.5f, up));
                CapturePreview("outlaws-desert-slope-" + distance + ".png");
                CapturePreview("outlaws-desert-slope-rear-" + distance + ".png", GameObject.Find("Rear View Camera").GetComponent<Camera>());
            }
        }
        Assert.That(climbed && descended && turnedBack, Is.True, "The regression route must include both slopes and a hairpin.");
        Assert.That(legacyPlaneWouldClip, Is.True, "This route must reproduce the old player-height plane intersection.");
        Assert.That((float)Get(road, "FirstDistance"), Is.GreaterThan(0f), "Exercise pooled road reuse, not just initial geometry.");
        yield return new ExitPlayMode();
    }

    private static object SampleRoad(Component road, float distance)
    {
        object[] arguments = { distance, null };
        Assert.That(road.GetType().GetMethod("TrySample").Invoke(road, arguments), Is.True, "Missing streamed road at " + distance);
        return arguments[1];
    }

    // Test the rendered triangles in XZ without introducing physics colliders for decoration.
    private static float HighestSurfaceAt(MeshFilter filter, Vector3 point)
    {
        Bounds bounds = filter.GetComponent<Renderer>().bounds;
        if (point.x < bounds.min.x || point.x > bounds.max.x || point.z < bounds.min.z || point.z > bounds.max.z)
            return float.NegativeInfinity;
        Vector3[] vertices = filter.sharedMesh.vertices;
        int[] indices = filter.sharedMesh.triangles;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
        float highest = float.NegativeInfinity;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
            float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(determinant) < 0.00001f) continue;
            float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / determinant;
            float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / determinant;
            float w = 1f - u - v;
            if (u >= -0.00001f && v >= -0.00001f && w >= -0.00001f)
                highest = Mathf.Max(highest, u * a.y + v * b.y + w * c.y);
        }
        return highest;
    }

    private static void CaptureStormPreview() => CapturePreview("outlaws-desert-storm.png");

    private static void CapturePreview(string filename, Camera camera = null)
    {
        if (camera == null) camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), filename), image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active;
        target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
    }
}
