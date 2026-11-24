using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class WorldShiftLevelTests
{
    [UnityTest]
    public IEnumerator TrainingFreezeDemonstrationWaitsSafelyAndAcceptsEarlyInput()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return WaitForNavigation();
        var game = Started();
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 768f, 4f);
        Step(game, .2f);
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true), $"distance={Get(game, "Distance")} stop={Get(game, "TrainingFreezeStop")} state={Get(game, "State")} sign={Get(game, "TrainingLessonPaused")}");
        float distance = (float)Get(game, "Distance"), duration = (float)Get(game, "Duration");
        var sliders = ((Array)Field(game, "levelHazards")).Cast<object>()
            .Where(h => (bool)Field(h, "LateralMotion") && (float)Field(h, "Start") >= 700f && (float)Field(h, "End") < 970f).ToArray();
        Assert.That(sliders.Length, Is.EqualTo(3));
        Capture("training-freeze-demonstration-1080");
        float motion = (float)Get(sliders[0], "MotionTravel");
        Step(game, 1f, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(duration).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.Not.EqualTo(motion));
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        Call(game, "TogglePause");
        motion = (float)Get(sliders[0], "MotionTravel");
        Step(game, 1f);
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        Call(game, "TogglePause");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(1));
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true), "The first press demonstrates freezing without releasing the car.");
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        Capture("training-freeze-night-stopped-1080");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(2));
        Step(game, 1f);
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true));
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(distance).Within(.001f));
        Assert.That((float)Get(game, "Duration"), Is.EqualTo(duration).Within(.001f));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.Not.EqualTo(motion), "Day visibly restores obstacle motion while the car waits.");
        Capture("training-freeze-day-moving-1080");
        motion = (float)Get(sliders[0], "MotionTravel");
        Call(game, "TrySwitch");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(3));
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(false));
        Assert.That(analytics.Count(a => a.name == "wl_freeze_demo"), Is.EqualTo(1), "The demonstration time is recorded once.");
        Step(game, .25f);
        Assert.That((float)Get(game, "Distance"), Is.GreaterThan(distance));
        Assert.That((float)Get(sliders[0], "MotionTravel"), Is.EqualTo(motion));
        // Once the action is demonstrated, the checkpoint never tests compliance again.
        Call(game, "TrySwitch");
        Pose(game, 969f, 4f);
        Step(game, .1f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(5));

        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(3), "Completed demo survives checkpoint retry.");
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.False);
        // A new run must teach the demonstration again.
        Call(game, "RestartRun");
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(0));
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 740f, 4f);
        Call(game, "TrySwitch");
        Step(game, 2f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(770f).Within(.01f));
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(1), "Early freezing counts, but both states must be demonstrated.");
        Assert.That(Get(game, "TrainingFreezeWaiting"), Is.EqualTo(true));
        Call(game, "TrySwitch");
        Step(game, .25f);
        Call(game, "TrySwitch");
        Step(game, 11f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(5), "All three frozen sliders leave a navigable opening.");
        Assert.That(Get(game, "Deaths"), Is.EqualTo(0));
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 4);
        Call(game, "RetryCheckpoint");
        game.GetType().GetField("trainingLessonRead", Flags).SetValue(game, true);
        Pose(game, 768f, 0f);
        Step(game, .2f);
        Call(game, "TrySwitch");
        Call(game, "TrySwitch");
        Call(game, "TrySwitch");
        Pose(game, 809f, 0f);
        Step(game, .1f);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"), "Frozen sliders remain solid.");
        Step(game, 3f);
        Assert.That(Get(game, "CheckpointIndex"), Is.EqualTo(4));
        Assert.That(Get(game, "TrainingFreezeStep"), Is.EqualTo(3), "A crash does not erase the completed freeze lesson.");
    }

    [UnityTest]
    public IEnumerator TrainingUsesSignsAndFinishingUnlocksLevelOne()
    {
        Static(Levels, "ResetProgress");
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return WaitForNavigation();
        PlayerPrefs.SetInt(UnlockAllKey, 0);
        var game = Started();
        Assert.That(Get(game, "TrainingLessonPaused"), Is.False);
        var yard=Find("WorldShiftEnvironmentScenery");
        Assert.That(yard.GetComponentsInChildren<Collider>(true).Any(v=>v.enabled),Is.False,"Practice scenery must stay decorative.");
        Assert.That(yard.GetComponentsInChildren<Transform>(true).Count(v=>v.name=="Training practice depot"),Is.GreaterThan(4));
        var lighting=Find("WorldShiftLocalLighting");
        Call(Find("WorldShiftPresentation"),"UpdateVisibility",(float)Get(game,"Distance"));Call(lighting,"Refresh");
        Capture("training-practice-yard-day");
        Call(game,"ApplyWorld",World("Echo"));Call(lighting,"Refresh");Capture("training-practice-yard-night");
        Call(game,"ApplyWorld",World("Day"));
        float yardStart=(float)Get(game,"Distance");
        Pose(game,45f,0f);Call(Find("WorldShiftPresentation"),"UpdateVisibility",45f);Call(lighting,"Refresh");Capture("training-practice-depot-detail");
        Pose(game,yardStart,0f);Call(Find("WorldShiftPresentation"),"UpdateVisibility",yardStart);
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "Training stops with the road sign in view.");
        var trainingHud=Find("WorldShiftLevelHud");trainingHud.GetType().GetField("lessonFade",Flags).SetValue(trainingHud,1f);
        Capture("training-steering-sign-bindings");
        float firstSignStop = (float)Get(game, "Distance");
        Assert.That(firstSignStop, Is.GreaterThan(0f).And.LessThan(25f));
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(firstSignStop).Within(.001f));
        Call(game, "ContinueTrainingLesson");
        Step(game, .1f);
        Assert.That((float)Get(game, "Distance"), Is.GreaterThan(0f));
        Assert.That(game.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Highway sign")), Is.EqualTo(5));
        foreach (string lesson in new[] { "Steer", "Switch", "Oncoming", "Freeze", "Ramp" })
        {
            var sign = game.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Highway sign " + lesson);
            var artwork = sign.Find("Sign face/Sign artwork");
            Assert.That(artwork, Is.Not.Null, lesson + " uses the supplied road-sign artwork.");
            Assert.That(artwork.GetComponent<Renderer>().sharedMaterial.mainTexture, Is.Not.Null);
            if(lesson=="Steer")
            {
                // Rendered sign and enlarged lesson use the same generated texture.
                var hint=(string)Static(TypeFor("WorldShiftTrainingSignArt"),"Description",Enum.Parse(TypeFor("LevelSignKind"),"Steer"));
                Assert.That(hint,Does.Contain("A / D").And.Contain("ARROW KEYS"));
            }
        }
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Obstacle lesson"), Is.False,
            "The road signs teach with pictograms instead of sentences.");
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Oncoming road direction warnings"), Is.True,
            "Training traffic uses the same visible road-direction cue as the current van art.");
        var gates = game.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Checkpoint gate").ToArray();
        Assert.That(gates.Length, Is.GreaterThan(0));
        foreach (var gate in gates)
        {
            // Training uses the same checkpoint gantry as every level.
            Assert.That(gate.GetComponentsInChildren<TMPro.TMP_Text>(true).Single().text, Is.EqualTo("CHECKPOINT"));
            Assert.That(gate.Find("Actual checkpoint road stripe"), Is.Not.Null);
            Assert.That(gate.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        Pose(game, 177f, 0f);
        Step(game, .2f);
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "The next instruction waits by its own road sign.");
        float stoppedAt = (float)Get(game, "Distance");
        Step(game, 1f);
        Assert.That((float)Get(game, "Distance"), Is.EqualTo(stoppedAt).Within(.001f));
        Call(game, "ContinueTrainingLesson");
        Pose(game, 20f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-steer-sign");
        object course = Get(game, "Course");
        var ramp = ((Array)Field(course, "Ramps")).GetValue(0);
        // This test isolates the ramp; freeze gating is covered separately.
        game.GetType().GetField("checkpointIndex", Flags).SetValue(game, 5);
        Pose(game, (float)Field(ramp, "Ramp") - 50f, 0f);
        Call(game, "PresentWorldForCamera", World("Day"));
        Capture("training-ramp");
        Assert.That(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Ramp air cue"), Is.False);
        bool attemptedLanding = false;
        for (int i = 0; i < 400 && Get(game, "State").ToString() == "Running"; i++)
        {
            if ((bool)Get(game, "TrainingLessonPaused")) Call(game, "ContinueTrainingLesson");
            Call(game, "StepSimulation", .05f, 0f, false);
            if ((bool)Get(game, "IsRelayAirborne") && Get(game, "World").ToString() == "Day")
            {
                attemptedLanding = true;
                Call(game, "TrySwitch");
            }
        }
        Assert.That(attemptedLanding, Is.True);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Won"), "Launch in Day, switch midair, land in Night, reach the finish.");
        Assert.That(StaticGet(Levels, "HighestBeaten"), Is.EqualTo(0));
        Assert.That(Get(Get(game,"Escape"),"Active"),Is.False,"Training celebrates readiness without pretending it was a real police escape.");
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True);
        Assert.That(Static(Levels, "IsBeaten", 1), Is.False, "Training marks only itself as done.");
        yield return null;
        Capture("training-complete");
        WorldShiftTestCapture.Capture("training-complete-4x3",960,720);
        var row = analytics.Single(a => a.sheet == "ws_level_attempts").data;
        Assert.That(row["outcome"], Is.EqualTo("complete"));
        Assert.That(row["level"], Is.EqualTo(0));
        // The test skips ahead by reflection, so check the section the player actually finished in.
        Assert.That(row["first_try_" + row["section_at_end"]], Is.Not.EqualTo(-1), "Finishing the level resolves the final section's first try.");
    }

    [UnityTest]
    public IEnumerator TrainingRecordsSignReadingTriesAndWhereLessonsFail()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity"); yield return new EnterPlayMode();
        StartLevel(0); yield return WaitForNavigation();
        var game = Started();
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True);
        Assert.That(analytics.Count(a => a.name == "wl_sign_shown"), Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.5f);
        Call(game, "ContinueTrainingLesson");
        var read = analytics.Single(a => a.name == "wl_sign_read").data;
        Assert.That(read["lesson"], Is.EqualTo(0));
        Assert.That((float)read["read_s"], Is.GreaterThanOrEqualTo(.45f), "Reading time is the wall-clock time the card stayed up.");
        Assert.That(read["how"], Is.EqualTo("continue"));

        // Drive straight into the steering lesson's middle block.
        Pose(game, 84f, 0f);
        Step(game, 1f);
        Assert.That(Get(game, "State").ToString(), Is.EqualTo("Retrying"));
        Assert.That(analytics.Last(a => a.name == "wl_death").data["lane"], Is.EqualTo("M"));
        Step(game, 3f); // Finish the capture and restore the checkpoint.
        Step(game, 1.1f); // Pass the already-read sign again.
        Assert.That(Get(game, "TrainingLessonPaused"), Is.False, "Acknowledged instructions stay dismissed after a crash.");
        Assert.That(analytics.Count(a => a.name == "wl_sign_shown"), Is.EqualTo(1));
        Call(game, "RetryCheckpoint");
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.False, "Manual checkpoint retry also remembers read signs.");

        Static(TypeFor("WorldShiftLevelAnalytics"), "EndAttempt", "menu");
        var row = analytics.Single(a => a.sheet == "ws_level_attempts").data;
        Assert.That(row["reads_0"], Is.EqualTo(1));
        Assert.That((float)row["read_0"], Is.GreaterThanOrEqualTo(.45f), "First reading of the sign.");
        Assert.That((float)row["read_total_0"], Is.GreaterThanOrEqualTo((float)row["read_0"]));
        Assert.That((float)row["read_s"], Is.GreaterThanOrEqualTo(.45f));
        Assert.That(row["retries_0"], Is.EqualTo(1));
        Assert.That(row["tries_0"], Is.EqualTo(3), "First go + one crash + one manual restart.");
        Assert.That(row["first_try_0"], Is.EqualTo(0));
        Assert.That(row["first_try_1"], Is.EqualTo(-1));
        StringAssert.IsMatch(@"^\d+:M:crash$", (string)row["fails_0"]);
        Assert.That(row["quit_on_sign"], Is.EqualTo(-1));
        Assert.That((float)row["freeze_demo_s"], Is.EqualTo(-1f));
        Call(game, "RestartRun");
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "A fresh run shows instructions again.");
        Call(game, "RetryCheckpoint");
        Step(game, 1.1f);
        Assert.That(Get(game, "TrainingLessonPaused"), Is.True, "An instruction not acknowledged with Enter must still show.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator SwitchingHasOneSignThenUninterruptedPracticeBeforeTraffic()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");yield return new EnterPlayMode();
        StartLevel(0);yield return WaitForNavigation();
        var game=Started();var course=Get(game,"Course");
        Assert.That(Call(course,"SignIndexForCheckpoint",2),Is.EqualTo(-1),"Switch practice has no second instruction stop.");
        Assert.That(((Array)Get(game,"Checkpoints")).Length,Is.EqualTo(6),"Keep the existing saves and encounters.");
        game.GetType().GetField("checkpointIndex",Flags).SetValue(game,1);Call(game,"RetryCheckpoint");
        Step(game,1.1f);Assert.That(Get(game,"TrainingLessonPaused"),Is.True);
        var hud=Find("WorldShiftLevelHud");hud.GetType().GetField("lessonFade",Flags).SetValue(hud,1f);
        Capture("training-switch-worlds-single-lesson");
        Call(game,"ContinueTrainingLesson");Call(game,"TrySwitch");
        Assert.That(Get(game,"World").ToString(),Is.EqualTo("Echo"));
        Pose(game,348f,0f);Step(game,.2f);
        Assert.That(Get(game,"CheckpointIndex"),Is.EqualTo(2));
        Step(game,1.4f);
        Assert.That((float)Get(game,"Distance"),Is.GreaterThan(375));
        Assert.That(Get(game,"TrainingLessonPaused"),Is.False,"Drive through the former dashes-sign position without stopping.");
        Pose(game,418f,0f);Step(game,.5f);
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"Night lowers the Day barrier.");
        Call(game,"TrySwitch");
        Assert.That(Get(game,"World").ToString(),Is.EqualTo("Day"));
        Pose(game,468f,0f);Step(game,.5f);
        Assert.That(Get(game,"State").ToString(),Is.EqualTo("Running"),"Day lowers the Night barrier.");
        Assert.That(Get(game,"TrainingLessonPaused"),Is.False);
        Pose(game,528f,0f);Step(game,.2f);Step(game,1.1f);
        Assert.That(Get(game,"CheckpointIndex"),Is.EqualTo(3));Assert.That(Get(game,"TrainingLessonPaused"),Is.True);
        Call(hud,"Refresh");
        Assert.That(((RawImage)Field(hud,"signImage")).texture,Is.EqualTo(Resources.Load<Texture2D>("WorldShiftTrainingSigns/Oncoming")),
            "The next stop must show the traffic lesson, not the freeze or ramp sign.");
        Assert.That(analytics.Any(a=>a.name=="wl_sign_shown" && Convert.ToInt32(a.data["lesson"])==2),Is.False);
        hud.GetType().GetField("lessonFade",Flags).SetValue(hud,1f);Capture("training-traffic-after-switch-practice");
    }

}
