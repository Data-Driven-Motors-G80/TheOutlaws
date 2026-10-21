#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Added only by the editor validation command, never to a saved game scene or player build.
public sealed class PickupSmokeRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private InputSettings originalInputSettings;
    private HideFlags originalInputSettingsFlags;
    private InputSettings testInputSettings;
    private bool previousRunInBackground;
    private readonly List<Keyboard> suspendedKeyboards = new List<Keyboard>();
    private readonly List<string> passed = new List<string>();

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        try
        {
            // Headless validation has no focused Game View; route only the scripted test input.
            originalInputSettings = InputSystem.settings;
            originalInputSettingsFlags = originalInputSettings.hideFlags;
            // InputManager destroys replaced HideAndDontSave defaults; retain the original
            // during the test, including scene reload, then restore its exact flags.
            originalInputSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            previousRunInBackground = Application.runInBackground;
            testInputSettings = Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            Application.runInBackground = true;
            // Keep host keyboard input from competing with the scripted test events.
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Keyboard existing && existing.enabled)
                {
                    InputSystem.DisableDevice(existing);
                    suspendedKeyboards.Add(existing);
                }
            }
            keyboard = InputSystem.AddDevice<Keyboard>();
        }
        catch (Exception error) { Finish(false, error.ToString()); yield break; }
        IEnumerator checks = RunChecks();
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            if (!more) break;
            yield return checks.Current;
        }
        Finish(true, null);
    }

    private IEnumerator RunChecks()
    {
        yield return null;
        AutoDriveCar car = FindFirstObjectByType<AutoDriveCar>();
        CarPickupEffects effects = car.GetComponent<CarPickupEffects>();
        ObstacleSpawner obstacles = FindFirstObjectByType<ObstacleSpawner>();
        PickupSpawner spawner = FindFirstObjectByType<PickupSpawner>();
        Require(car != null && effects != null && car.enabled, "Scene car and effects are ready");
        Require(spawner != null && spawner.enabled, "Pickup spawner has valid scene and prefab references");
        Require(car.GetComponent<RiskRunController>() != null, "Risk run controller is attached");
        Require(SceneManager.GetActiveScene().buildIndex >= 0, "Test scene is registered for restart");
        obstacles.enabled = false;
        spawner.enabled = false;
        foreach (Transform child in obstacles.transform) child.gameObject.SetActive(false);
        foreach (Transform child in spawner.transform) child.gameObject.SetActive(false);

        int initialHandle = SceneManager.GetActiveScene().handle;
        SpawnPickup(car.transform, PickupEffectType.ReverseSteering, 4f);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (effects.ActiveEffect != PickupEffectType.ReverseSteering && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ActiveEffect == PickupEffectType.ReverseSteering, "Real trigger pickup activates reverse");
        Require(SceneManager.GetActiveScene().handle == initialHandle, "Pickup does not trigger crash restart");

        float x = car.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
        yield return new WaitForSeconds(0.16f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.25f);
        Require(car.transform.position.x > x + 0.1f, "Reverse: left arrow moves right");

        x = car.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
        yield return new WaitForSeconds(0.16f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.25f);
        Require(car.transform.position.x < x - 0.1f, "Reverse: right arrow moves left");

        yield return null;
        Time.timeScale = 0f;
        float pausedRemaining = effects.RemainingSeconds;
        float pausedGap = effects.RunState.PursuitGap;
        yield return new WaitForSecondsRealtime(0.2f);
        Require(Mathf.Abs(effects.RemainingSeconds - pausedRemaining) < 0.01f, "Pause freezes countdown");
        Require(Mathf.Abs(effects.RunState.PursuitGap - pausedGap) < 0.01f, "Pause freezes pursuit distance");
        Time.timeScale = 1f;
        deadline = Time.realtimeSinceStartup + 5f;
        while (effects.ActiveEffect != PickupEffectType.None && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ActiveEffect == PickupEffectType.None, "Timed effect expires in actual game loop");
        Require(effects.RunState.BankedPickupScore == 0 && effects.RunState.PendingBonus == 0,
            "Pickup effects do not change the score");

        x = car.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
        yield return new WaitForSeconds(0.16f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.25f);
        Require(car.transform.position.x < x - 0.1f, "Expiry restores normal left steering");
        Require(effects.RunState.BankedPickupScore == 0, "Later frames do not change the score");

        effects.Apply(PickupEffectType.ReverseSteering);
        int beforeCancel = effects.RunState.BankedPickupScore;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.1f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Require(effects.ActiveEffect == PickupEffectType.None && effects.RunState.PendingBonus == 0,
            "Space clears the active burden and forfeits pending bonus");
        Require(effects.RunState.BankedPickupScore == beforeCancel, "Bailing out preserves only already banked score");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
        yield return new WaitForSeconds(0.1f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(SceneManager.GetActiveScene().handle == initialHandle, "R does not restart an active run");

        float z = car.transform.position.z;
        float started = Time.time;
        yield return new WaitForSeconds(0.3f);
        float normalSpeed = (car.transform.position.z - z) / (Time.time - started);
        SpawnPickup(car.transform, PickupEffectType.Boost, 5f);
        deadline = Time.realtimeSinceStartup + 3f;
        while (effects.ActiveEffect != PickupEffectType.Boost && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ActiveEffect == PickupEffectType.Boost, "Real trigger pickup activates boost");
        z = car.transform.position.z;
        started = Time.time;
        yield return new WaitForSeconds(0.3f);
        float boostedSpeed = (car.transform.position.z - z) / (Time.time - started);
        Require(boostedSpeed > normalSpeed * 1.2f && boostedSpeed < normalSpeed * 1.5f, "Boost changes actual forward speed");

        effects.Clear();
        x = car.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.16f);
        float normalTurnDistance = car.transform.position.x - x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.4f);
        effects.Apply(PickupEffectType.SlipperySteering);
        x = car.transform.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.16f);
        float slipperyTurnDistance = car.transform.position.x - x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.8f);
        Require(slipperyTurnDistance > 0f && slipperyTurnDistance < normalTurnDistance * 0.7f, "Slippery steering changes actual lateral response");

        SpawnPickup(car.transform, PickupEffectType.ReverseSteering, 5f);
        deadline = Time.realtimeSinceStartup + 3f;
        while (effects.ActiveEffect != PickupEffectType.ReverseSteering && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ActiveEffect == PickupEffectType.ReverseSteering && effects.RemainingSeconds > 4f,
            "New pickup replaces existing effect with a fresh duration");

        int beforeReplacement = effects.RunState.BankedPickupScore;
        effects.Apply(PickupEffectType.Boost, 0.2f);
        yield return new WaitForSeconds(0.35f);
        Require(effects.RunState.BankedPickupScore == beforeReplacement,
            "Replacing an effect does not change the score");
        effects.Clear();

        SpawnPickup(car.transform, PickupEffectType.Shield, 5f);
        deadline = Time.realtimeSinceStartup + 3f;
        while (!effects.ShieldReady && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ShieldReady, "Real trigger pickup arms a one-hit shield");
        int shieldUsesBefore = effects.ShieldUseCount;
        float gapBeforeShieldedCrash = effects.RunState.PursuitGap;
        GameObject shieldWall = SpawnObstacle(car.transform, obstacles.transform);
        deadline = Time.realtimeSinceStartup + 4f;
        while (effects.ShieldUseCount == shieldUsesBefore && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.ShieldUseCount == shieldUsesBefore + 1,
            "Shield blocks the next real obstacle collision");
        Require(!effects.ShieldReady && effects.RunState.CrashSlowRemaining <= 0f
            && Mathf.Abs(effects.RunState.PursuitGap - gapBeforeShieldedCrash) < 0.5f,
            "Shield collision does not drain proximity or slow the car");
        shieldWall.SetActive(false);

        float gapBeforeCrash = effects.RunState.PursuitGap;
        GameObject wall = SpawnObstacle(car.transform, obstacles.transform);
        deadline = Time.realtimeSinceStartup + 4f;
        while (effects.RunState.CollisionProtectionRemaining <= 0f
            && !effects.RunState.IsGameOver && Time.realtimeSinceStartup < deadline)
            yield return null;
        Require(effects.RunState.CollisionProtectionRemaining > 0f,
            "Actual obstacle collision starts crash recovery");
        Require(!effects.RunState.IsGameOver && SceneManager.GetActiveScene().handle == initialHandle,
            "A first collision keeps the current run alive without scene reload");
        Require(effects.RunState.CrashSlowRemaining <= 0f
            && Mathf.Abs(effects.ForwardSpeedMultiplier - 1f) < 0.01f,
            "A crash does not slow the car");
        Require(effects.RunState.PursuitGap < gapBeforeCrash,
            "A crash drains the proximity gap");
        wall.SetActive(false);
        for (int impact = 0; impact < 4 && !effects.RunState.IsGameOver; impact++)
        {
            deadline = Time.realtimeSinceStartup + 4f;
            while (effects.RunState.CollisionProtectionRemaining > 0f
                && !effects.RunState.IsGameOver && Time.realtimeSinceStartup < deadline)
                yield return null;

            wall = SpawnObstacle(car.transform, obstacles.transform);
            deadline = Time.realtimeSinceStartup + 4f;
            while (!effects.RunState.IsGameOver
                && effects.RunState.CollisionProtectionRemaining <= 0f
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            wall.SetActive(false);
        }
        Require(effects.RunState.IsGameOver && effects.RunState.PursuitGap == 0f,
            "Repeated collisions drain proximity and end the run");
        Require(effects.RunState.PendingBonus == 0 && effects.ActiveEffect == PickupEffectType.None,
            "Capture removes unresolved pickup effects and rewards");
        Require(SceneManager.GetActiveScene().handle == initialHandle, "Game over waits for an explicit restart");
        Vector3 capturedPosition = car.transform.position;
        float capturedScore = FindFirstObjectByType<ScoreMeter>().Score;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.Space));
        yield return new WaitForSeconds(0.25f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(Vector3.Distance(car.transform.position, capturedPosition) < 0.001f,
            "Captured car cannot drive away or steer");
        Require(Mathf.Abs(FindFirstObjectByType<ScoreMeter>().Score - capturedScore) < 0.001f,
            "Final score stops changing after capture");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
        deadline = Time.realtimeSinceStartup + 5f;
        while (SceneManager.GetActiveScene().handle == initialHandle && Time.realtimeSinceStartup < deadline)
            yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Require(SceneManager.GetActiveScene().handle != initialHandle, "R restarts after capture");
        yield return null;
        CarPickupEffects restarted = FindFirstObjectByType<CarPickupEffects>();
        Require(restarted != null && restarted.ActiveEffect == PickupEffectType.None
            && !restarted.ShieldReady && restarted.ShieldUseCount == 0
            && restarted.RunState.PendingBonus == 0 && restarted.RunState.BankedPickupScore == 0
            && restarted.RunState.CrashSlowRemaining == 0f
            && restarted.RunState.CollisionProtectionRemaining == 0f
            && restarted.RunState.RecoveryWaitRemaining == 0f
            && !restarted.RunState.IsGameOver && restarted.RunState.PursuitGap >= 45f,
            "Restart clears rewards, collision timers and capture state");
    }

    private static GameObject SpawnObstacle(Transform car, Transform obstacles)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "SmokeTestObstacle";
        wall.transform.SetParent(obstacles);
        wall.transform.position = car.position + Vector3.forward * 5f;
        wall.transform.localScale = new Vector3(10f, 2f, 1f);
        return wall;
    }

    private static void SpawnPickup(Transform car, PickupEffectType effect, float duration)
    {
        var item = new GameObject("SmokeTestPickup");
        item.transform.position = car.position + Vector3.forward * 5f;
        SphereCollider collider = item.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.9f;
        item.AddComponent<PickupItem>().Configure(effect, duration);
    }

    private void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed.Add(description);
        Debug.Log("PICKUP_SMOKE_PASS: " + description);
    }

    private void Finish(bool success, string error)
    {
        Time.timeScale = 1f;
        RestoreKeyboards();
        var result = new Result { success = success, checks = passed.ToArray(), error = error, unityVersion = Application.unityVersion };
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-pickupSmokeReport") File.WriteAllText(args[i + 1], JsonUtility.ToJson(result, true));
        if (success) Debug.Log("PICKUP_SMOKE_COMPLETE");
        else Debug.LogError("PICKUP_SMOKE_FAILED: " + error);
        UnityEditor.EditorApplication.Exit(success ? 0 : 1);
    }

    private void OnDestroy()
    {
        RestoreKeyboards();
    }

    private void RestoreKeyboards()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        if (testInputSettings != null)
        {
            InputSystem.settings = originalInputSettings;
            originalInputSettings.hideFlags = originalInputSettingsFlags;
            Destroy(testInputSettings);
            testInputSettings = null;
            Application.runInBackground = previousRunInBackground;
        }
        foreach (Keyboard suspended in suspendedKeyboards)
        {
            if (suspended.added && !suspended.enabled) InputSystem.EnableDevice(suspended);
        }
        suspendedKeyboards.Clear();
    }

    [Serializable]
    private sealed class Result
    {
        public bool success;
        public string unityVersion;
        public string[] checks;
        public string error;
    }
}
#endif
