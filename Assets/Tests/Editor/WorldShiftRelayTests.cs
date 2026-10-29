using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WorldShiftRelayTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    private static void Capture(Component game, string name) => typeof(WorldShiftCityTests)
        .GetMethod("CapturePreview", Flags).Invoke(null, new object[] { game, name });

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ThreeBoostRelaysLandOnlyInTheOtherWorldAndRetrySafely()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/City_WorldShift.unity");
        yield return new EnterPlayMode();
        var game = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(c => c.GetType().Name == "WorldShiftCity");
        game.enabled = false;
        Call(game, "BeginRun");
        float[] ramps = { 600f, 1740f, 2300f }, takeoffs = { 660f, 1800f, 2360f }, landings = { 744f, 1896f, 2468f };
        int[] checkpoints = { 1, 3, 4 };
        var player = (Transform)Get(game, "Player");
        var police = (Component)Get(game, "Police");
        for (int i = 0; i < 3; i++)
        {
            game.GetType().GetField("checkpointIndex", Flags).SetValue(game, checkpoints[i]);
            Call(game, "RetryCheckpoint");
            string target = i == 1 ? "Day" : "Echo";
            float initialGap = (float)Get(game, "PursuitGap");
            Advance(game, ramps[i] + 36f);
            Assert.That((float)Get(game, "Speed"), Is.EqualTo(42f).Within(.05f));
            var foot = Call(Get(game, "Route"), "Sample", ramps[i]);
            var lip = Call(Get(game, "Route"), "Sample", takeoffs[i]);
            Assert.That(((Vector3)Get(lip, "Position")).y - ((Vector3)Get(foot, "Position")).y,
                Is.GreaterThan(10f), "Each launch must have a real uphill deck.");
            if (i == 0) Capture(game, "relay-01-uphill-boost");
            Vector3 policeBefore = police.transform.position;
            float beforeDistance = (float)Get(game, "Distance");
            Call(game, "StepSimulation", .2f, 0f, false);
            Assert.That((float)Get(game, "Distance") - beforeDistance, Is.EqualTo(8.4f).Within(.05f));
            Assert.That(Vector3.Distance(police.transform.position, policeBefore), Is.GreaterThan(7.8f), "Police must accelerate too.");
            Advance(game, takeoffs[i] + 14f);
            Assert.That(Get(game, "IsRelayAirborne"), Is.True);
            float groundY = ((Vector3)Get(Call(Get(game, "Route"), "Sample", Get(game, "Distance")), "Position")).y;
            Assert.That(player.position.y - groundY, Is.GreaterThan(2f));
            Assert.That((float)Get(game, "PursuitGap"), Is.LessThan(initialGap).And.GreaterThan(initialGap - 1f),
                "Boost changes both speeds and must not grant free pursuit distance.");
            Call(game, "TogglePause");
            Vector3 playerPaused = player.position, policePaused = police.transform.position;
            Call(game, "StepSimulation", .5f, 1f, true);
            Assert.That(player.position, Is.EqualTo(playerPaused));
            Assert.That(police.transform.position, Is.EqualTo(policePaused));
            Call(game, "TogglePause");
            float beforeSwitch = (float)Get(game, "Distance");
            Assert.That(Call(game, "TrySwitch"), Is.True);
            Assert.That(Get(game, "World").ToString(), Is.EqualTo(target));
            Assert.That(Get(game, "Distance"), Is.EqualTo(beforeSwitch));
            if (i == 0) Capture(game, "relay-02-airborne-night-landing");
            Advance(game, landings[i] + 4f);
            Assert.That(Get(game, "IsRelayAirborne"), Is.False);
            Assert.That(Get(game, "RelaysCompleted"), Is.EqualTo(i + 1));
            Assert.That(Get(game, "Hits"), Is.EqualTo(0));
            if (i == 0) Capture(game, "relay-03-landed");
            Advance(game, landings[i] + 57f);
            Assert.That((float)Get(game, "Speed"), Is.EqualTo(28f).Within(.05f));
        }
        // Deliberately omit Space on the first jump: no invisible landing or auto-switch.
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 1);
        Call(game, "RetryCheckpoint");
        for (int n = 0; n < 400 && Get(game, "State").ToString() == "Running"; n++)
            Call(game, "StepSimulation", .05f, 0f, false);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That((string)Get(game, "Message"), Does.Contain("MISSED RELAY LANDING"));
        float y = player.position.y;
        Call(game, "StepSimulation", .4f, 0f, false);
        Assert.That(player.position.y, Is.LessThan(y - 2f));
        Call(game, "StepSimulation", 1f, 0f, false);
        Assert.That(Get(game, "Distance"), Is.EqualTo(560f));
        Assert.That(Get(game, "IsRelayAirborne"), Is.False);
        Assert.That(Get(game, "World").ToString(), Is.EqualTo("Day"));
        Assert.That(Get(game, "Speed"), Is.EqualTo(28f));
        yield return new ExitPlayMode();
    }

    private static void Advance(object game, float target)
    {
        for (int frame = 0; (float)Get(game, "Distance") < target && frame < 1000; frame++)
        {
            Assert.That(Get(game, "State").ToString(), Is.EqualTo("Running"));
            Call(game, "StepSimulation", .05f, 0f, false);
        }
        Assert.That((float)Get(game, "Distance"), Is.GreaterThanOrEqualTo(target));
    }
}
