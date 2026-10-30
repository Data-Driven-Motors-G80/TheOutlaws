using System;
using System.IO;
using Outlaws.TransformStunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Headless acceptance checks exercise the same simulation used by the playable scenes.</summary>
public static class TransformStuntValidation
{
    public static void ValidateAndBuild()
    {
        Run();
        TransformStuntSetup.BuildWebGL();
    }
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("STUNT CHECK FAILED: " + message); checks++; }

    public static void Run()
    {
        checks = 0;
        Check(!StuntRun.Overlap(Vector3.zero, Vector3.one, 0, new Vector3(3, 0, 0), Vector3.one, 45), "separated oriented boxes");
        Check(StuntRun.Overlap(Vector3.zero, Vector3.one, 45, Vector3.one, Vector3.one, 0), "overlapping oriented boxes");
        var slot = new StuntCourse(StuntEnvironment.City); slot.Obstacles.Clear(); slot.Gate(20, 1.35f);
        var fit = new StuntRun(slot);
        Check(!fit.Clear(new Vector3(0, 0, 20), 1, 0, 0), "normal cannot fit narrow slot");
        Check(fit.Clear(new Vector3(0, 0, 20), .55f, 0, 0), "small fits narrow slot");
        Check(!fit.Clear(new Vector3(0, 0, 20), .55f, 90, 0), "sideways footprint changes collision");
        slot.Obstacles.Clear(); slot.Gate(20, 1.5f, -45);
        Check(!fit.Clear(new Vector3(0, 0, 20), .55f, 0, 0), "small alone cannot bypass angled slot");
        Check(fit.Clear(new Vector3(0, 0, 20), .55f, -45, 0), "small aligned fits angled slot");
        slot.Obstacles.Clear(); slot.Gate(20, 3.4f, x: -1.2f);
        Check(!fit.Clear(new Vector3(-4, 0, 20), 1, 0, 0) &&
              !fit.Clear(new Vector3(4, 0, 20), 1, 0, 0), "full-road gate cannot be bypassed at either edge");
        Check(fit.Clear(new Vector3(-1.2f, 0, 20), 1, 0, 0), "normal car can use the intended opening");

        var empty = new StuntCourse(StuntEnvironment.City); empty.Obstacles.Clear(); empty.Fuel.Clear();
        var normal = new StuntRun(empty) { Started = true };
        var tiny = new StuntRun(empty) { Started = true }; tiny.Transform(true, false);
        for (int i = 0; i < 120; i++) { normal.Advance(1f / 60, 0); tiny.Advance(1f / 60, 0); }
        Check(normal.Position.z > tiny.Position.z * 1.6f, "small is slower");
        Check(Mathf.Abs(normal.Fuel.CurrentFuel - tiny.Fuel.CurrentFuel) < .01f, "fuel drains equally per second");
        float clock = normal.Clock, fuel = normal.Fuel.CurrentFuel; Vector3 position = normal.Position;
        normal.Paused = true; normal.Transform(true, true); normal.Advance(1, 1);
        Check(normal.Position == position && normal.Clock == clock && normal.Fuel.CurrentFuel == fuel && !normal.Small, "pause freezes inputs and simulation");
        normal.Paused = false; normal.Fuel.Consume(100); normal.Advance(.02f, 0);
        Check(normal.RetryRemaining > 0, "empty fuel retries");
        for (int i = 0; i < 90; i++) normal.Advance(1f / 60, 0);
        Check(normal.Fuel.CurrentFuel > 60 && normal.Crashes == 1, "fuel retry cannot softlock");

        var windCourse = new StuntCourse(StuntEnvironment.City); windCourse.Obstacles.Clear(); windCourse.Fuel.Clear();
        windCourse.Winds.Add(new StuntWind { Start = 0, End = 100, Acceleration = Vector3.right });
        var windNormal = new StuntRun(windCourse) { Started = true };
        var windSmall = new StuntRun(windCourse) { Started = true }; windSmall.Transform(true, false);
        for (int i = 0; i < 60; i++) { windNormal.Advance(1f / 60, 0); windSmall.Advance(1f / 60, 0); }
        Check(windSmall.Position.x > windNormal.Position.x + .5f, "small responds more to wind");

        var narrow = new StuntCourse(StuntEnvironment.City); narrow.Obstacles.Clear(); narrow.Gate(20, 1.35f);
        var inside = new StuntRun(narrow) { Started = true }; inside.Transform(true, false);
        while (inside.Position.z < 20) inside.Advance(1f / 240, 0);
        inside.Transform(true, false);
        Check(inside.Small && inside.Feedback.Contains("ROOM"), "growth inside slot rejected");
        inside.Transform(false, true);
        Check(inside.AngleIndex == 0, "intermediate rotation cannot clip slot walls");

        var jumpCourse = new StuntCourse(StuntEnvironment.City);
        jumpCourse.Obstacles.Clear(); jumpCourse.Fuel.Clear(); jumpCourse.Jumps.Clear();
        jumpCourse.Jumps.Add(new StuntJump { Takeoff = 30, Landing = 58, End = 90 });
        var jumping = new StuntRun(jumpCourse) { Started = true };
        while (!jumping.Airborne && jumping.Crashes == 0) jumping.Advance(1f / 120, 0);
        jumping.Transform(true, false);
        for (int i = 0; i < 60; i++) jumping.Advance(1f / 120, 0);
        Check(jumping.Airborne && Mathf.Abs(jumping.Speed - StuntRun.NormalSpeed) < .01f && jumping.Small, "airborne shrink preserves launch momentum");
        for (int i = 0; i < 240 && jumping.Airborne; i++) jumping.Advance(1f / 120, 0);
        Check(!jumping.Airborne && jumping.Crashes == 0 && jumping.Position.z > 58, "actual successful landing");
        var shortJump = new StuntRun(jumpCourse) { Started = true }; shortJump.Transform(true, false);
        for (int i = 0; i < 1500 && shortJump.Crashes == 0; i++) shortJump.Advance(1f / 120, 0);
        Check(shortJump.Crashes == 1, "small approach misses long jump");

        var restoreCourse = new StuntCourse(StuntEnvironment.City); restoreCourse.Obstacles.Clear(); restoreCourse.Fuel.Clear();
        restoreCourse.Fuel.Add(new Vector3(0, .65f, 110)); restoreCourse.Fuel.Add(new Vector3(0, .65f, 260));
        var restoring = new StuntRun(restoreCourse) { Started = true };
        while (restoring.Position.z < 270) restoring.Advance(1f / 120, 0);
        Check(restoring.FuelTaken[0] && restoring.FuelTaken[1] && restoring.Checkpoint == 1, "checkpoint records prior fuel state");
        restoring.Transform(true, true); restoring.Retry();
        while (restoring.RetryRemaining > 0) restoring.Advance(1f / 120, 0);
        Check(restoring.Position.z == 145 && !restoring.Small && restoring.AngleIndex == 0 && !restoring.Airborne, "retry restores safe pose");
        Check(restoring.FuelTaken[0] && !restoring.FuelTaken[1] && Mathf.Abs(restoring.Clock - 145f / StuntRun.NormalSpeed) < .02f, "retry restores pickups and machinery clock");

        var lesson = new StuntRun(new StuntCourse(StuntEnvironment.Tutorial)) { Started = true };
        for (int i = 0; i < 1800 && lesson.Crashes == 0; i++) lesson.Advance(1f / 120, 0);
        Check(lesson.Crashes == 1 && lesson.Lesson == 0, "tutorial cannot advance without required action");

        foreach (StuntEnvironment environment in Enum.GetValues(typeof(StuntEnvironment)))
        {
            var course = new StuntCourse(environment);
            foreach (Vector3 pickup in course.Fuel) Check(course.HasRoad(pickup.z), environment + " fuel on reachable road");
            foreach (float cp in course.Checkpoints) Check(new StuntRun(course).Clear(new Vector3(0, 0, cp), 1, 0, 0), environment + " safe checkpoint");
            DriveCourse(environment);
        }
        TransformStuntSetup.Create();
        foreach (string name in StuntGame.SceneNames)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var game = UnityEngine.Object.FindFirstObjectByType<StuntGame>();
            Check(game != null && game.fuelPrefab != null && game.roadTemplate != null && game.carPrefab != null, name + " references");
            Check(UnityEngine.Object.FindObjectsByType<PickupSpawner>(FindObjectsSortMode.None).Length == 0, name + " no mystery spawners");
        }
        EditorSceneManager.OpenScene(TransformStuntSetup.MenuPath);
        Debug.Log("TRANSFORM_STUNT_VALIDATION_PASSED: " + checks + " checks");
        File.WriteAllText("stunt-validation.txt", checks + " checks passed; tutorial and all three courses completed through real collision and fuel simulation.");
    }

    public static void DriveCourse(StuntEnvironment environment)
    {
        var run = new StuntRun(new StuntCourse(environment)) { Started = true };
        for (int frame = 0; frame < 18000 && !run.Finished; frame++)
        {
            Pilot(run, out bool small, out int angle, out float steer);
            if (run.Small != small) run.Transform(true, false);
            if (run.AngleIndex != angle) run.Transform(false, true);
            run.Advance(1f / 120, steer);
            if (run.Crashes > 0) throw new InvalidOperationException($"STUNT ROUTE {environment}: {run.Feedback}, z={run.Position.z:F2}, x={run.Position.x:F2}, y={run.Position.y:F2}, yaw={run.Yaw:F1}, lesson={run.Lesson}");
        }
        Check(run.Finished, environment + " complete route");
        Debug.Log($"STUNT_ROUTE_OK {environment}: {run.Duration:F1}s, {run.Collected} fuel");
    }

    // This is a continuous steering/switching route, not a teleport or collision bypass.
    public static void Pilot(StuntRun run, out bool small, out int angle, out float steer)
    {
        float z = run.Position.z, target = 0, feed = 0;
        small = false; angle = 0;
        var course = run.Course;
        for (int i = 0; i + 1 < course.Obstacles.Count; i++)
        {
            var a = course.Obstacles[i]; var b = course.Obstacles[i + 1];
            if (a.GateId < 0 || a.GateId != b.GateId) continue;
            float centreZ = (a.Center.z + b.Center.z) / 2;
            if (z < centreZ - 35 || z > centreZ + 10) continue;
            var centre = (a.Position(run.Clock) + b.Position(run.Clock)) / 2;
            if (a.Motion == StuntMotion.Slide) target = centre.x;
            else
            {
                small = true;
                if (a.Yaw != 0)
                {
                    angle = a.Yaw < 0 ? 1 : 3;
                    float direction = Mathf.Sign(a.Yaw);
                    target = Mathf.Clamp(direction * (z - centreZ + .6f), -3f, 3f);
                    if (Mathf.Abs(z - centreZ) < 3.5f) feed = direction * (course.Tutorial ? StuntRun.SmallSpeed * .7f : StuntRun.SmallSpeed);
                }
                else target = centre.x;
            }
            break;
        }
        foreach (var jump in course.Jumps)
            if (z > jump.Takeoff - 38 && z < jump.Landing + 20)
            { small = run.Airborne || z > jump.Landing; angle = 0; target = 0; feed = 0; }
        if (course.Environment == StuntEnvironment.Coast && z > 770 && z < 840) target = 2.5f;
        if (course.Tutorial)
        {
            if (run.Lesson == 0) target = z < 28 ? -1.5f : z < 55 ? 1.5f : 0;
            if (run.Lesson == 1 && z > 215) small = false;
            if (run.Lesson == 5) small = z > 755 && z < 790;
        }
        // Counteract steady wind while retaining steering inertia and bounded player input.
        float windCompensation = 0;
        foreach (var w in course.Winds) if (w.Contains(run.Position)) windCompensation += w.Acceleration.x * (small ? 4 : 1);
        steer = Mathf.Clamp((feed + (target - run.Position.x) * 9 - windCompensation * .5f) / Mathf.Max(1, Mathf.Min(14, run.Speed)), -1, 1);
    }
}
