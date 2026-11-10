using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PickupValidation
{
    public static void RunSmoke()
    {
        EditorSceneManager.OpenScene(PickupDemoBuilder.DemoPath);
        new GameObject("TemporaryPickupSmokeRunner").AddComponent<PickupSmokeRunner>();
        EditorApplication.isPlaying = true;
    }

    public static void BuildWebGL()
    {
        string output = "Builds/PickupPlaygroundWebGL";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { PickupDemoBuilder.DemoPath },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
        Debug.Log("PICKUP_WEBGL_READY: " + output);
    }
}
