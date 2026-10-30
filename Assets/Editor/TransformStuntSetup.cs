using System;
using System.Collections.Generic;
using Outlaws.TransformStunt;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class TransformStuntSetup
{
    public const string MenuPath = "Assets/Scenes/Stunt_Menu.unity";
    [MenuItem("The Outlaws/Transform Stunt/Open Prototype")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null) Create();
        EditorSceneManager.OpenScene(MenuPath);
    }

    [MenuItem("The Outlaws/Transform Stunt/Create Scenes")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scenes = new List<EditorBuildSettingsScene>();
        for (int i = -1; i < 4; i++)
        {
            string path = i < 0 ? MenuPath : "Assets/Scenes/" + StuntGame.SceneNames[i] + ".unity";
            scenes.Add(new EditorBuildSettingsScene(path, true));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) continue;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Transform Stunt / Fuel Only").AddComponent<StuntGame>();
            game.environment = i;
            game.roadTemplate = AssetDatabase.LoadAssetAtPath<RoadSegment>("Assets/Prefabs/Road/RoadSegment_Straight.prefab");
            game.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car/Car.prefab");
            game.fuelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rewards/FuelTank.prefab");
            game.streetlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Streetlights/CityStreetlight.prefab");
            game.desertMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road/DesertSand.mat");
            game.coastMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road/RoadOcean.mat");
            if (game.roadTemplate == null || game.carPrefab == null || game.fuelPrefab == null) throw new InvalidOperationException("Required reusable assets missing.");
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>(); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.backgroundColor = new Color(.025f, .045f, .065f); camera.clearFlags = CameraClearFlags.SolidColor;
            game.view = camera;
            var sun = new GameObject("Stunt sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            EditorSceneManager.SaveScene(scene, path);
        }
        foreach (var scene in EditorBuildSettings.scenes) if (!scenes.Exists(s => s.path == scene.path)) scenes.Add(scene);
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.OpenScene(MenuPath);
        AssetDatabase.SaveAssets();
        Debug.Log("TRANSFORM_STUNT_SCENES_READY");
    }

    [MenuItem("The Outlaws/Transform Stunt/Build WebGL")]
    public static void BuildWebGL()
    {
        Create();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        var paths = new List<string> { MenuPath };
        foreach (string scene in StuntGame.SceneNames) paths.Add("Assets/Scenes/" + scene + ".unity");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = paths.ToArray(),
            locationPathName = "Builds/TransformStunt-WebGL", target = BuildTarget.WebGL, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
        Debug.Log("TRANSFORM_STUNT_WEBGL_READY");
    }
}
