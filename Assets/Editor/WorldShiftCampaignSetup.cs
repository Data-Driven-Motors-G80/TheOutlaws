using System;
using System.Collections.Generic;
using System.Linq;
using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates missing campaign scenes and keeps development scenes out of the build.</summary>
public static class WorldShiftCampaignSetup
{
    public const string MenuPath = "Assets/Scenes/WorldShift_Menu.unity";
    public const string TutorialPath = "Assets/Scenes/WorldShift_Tutorial.unity";
    public const string DesertPath = "Assets/Scenes/Desert_WorldShift.unity";
    public static readonly string[] GameScenePaths =
    {
        MenuPath, WorldShiftCitySetup.ScenePath, DesertPath, "Assets/Scenes/Ocean_WorldShift.unity",
        "Assets/Scenes/Snow_WorldShift.unity", "Assets/Scenes/Mountain_WorldShift.unity", TutorialPath
    };
    private static readonly string[] DevelopmentScenePaths =
    {
        "Assets/Scenes/Env_City.unity", "Assets/Scenes/Env_Desert.unity", "Assets/Scenes/Env_OceanCoast.unity", "Assets/Scenes/Env_Snow.unity",
        WorldShiftDualViewSetup.ScenePath, DesertShiftTrialSetup.ScenePath
    };

    [MenuItem("The Outlaws/World Shift/Open Game Menu")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!Exists(MenuPath)) Create();
        EditorSceneManager.OpenScene(MenuPath);
    }

    [MenuItem("The Outlaws/World Shift/Setup/Create Missing Campaign Scenes")]
    public static void EnsureScenes()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create();
    }

    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before setting up scenes.");
        if (!Exists(WorldShiftCitySetup.ScenePath)) WorldShiftCitySetup.Create();
        CreateCityVariant(TutorialPath, ShiftLevel.City, true);
        CreateCityVariant(GameScenePaths[3], ShiftLevel.Ocean, false);
        CreateCityVariant(GameScenePaths[4], ShiftLevel.Snow, false);
        CreateCityVariant(GameScenePaths[5], ShiftLevel.Mountain, false);
        if (!Exists(DesertPath))
        {
            if (!Exists(DesertShiftTrialSetup.ScenePath)) DesertShiftTrialSetup.Create();
            var scene = EditorSceneManager.OpenScene(DesertShiftTrialSetup.ScenePath);
            var game = Object.FindFirstObjectByType<DesertShiftTrial>();
            var properties = new SerializedObject(game);
            properties.FindProperty("fullLevel").boolValue = true;
            properties.FindProperty("policePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car/CityPoliceVehicle.prefab");
            properties.ApplyModifiedPropertiesWithoutUndo();
            game.name = "WorldShift Desert";
            Save(scene, DesertPath);
        }
        if (!Exists(MenuPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("WorldShift menu").AddComponent<WorldShiftMenu>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .045f, .07f); camera.gameObject.AddComponent<AudioListener>();
            Save(scene, MenuPath);
        }
        var scenes = GameScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
        foreach (var scene in EditorBuildSettings.scenes)
            if (!GameScenePaths.Contains(scene.path))
                scenes.Add(new EditorBuildSettingsScene(scene.path, !DevelopmentScenePaths.Contains(scene.path) && scene.enabled));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("WorldShift setup ready: five environments and tutorial. Existing scenes preserved.");
    }

    private static void CreateCityVariant(string path, ShiftLevel environment, bool tutorial)
    {
        if (Exists(path)) return;
        var scene = EditorSceneManager.OpenScene(WorldShiftCitySetup.ScenePath);
        var game = Object.FindFirstObjectByType<WorldShiftCity>();
        if (game == null) throw new InvalidOperationException("City template has no WorldShift controller.");
        var properties = new SerializedObject(game);
        properties.FindProperty("environment").enumValueIndex = (int)environment;
        properties.FindProperty("tutorialLevel").boolValue = tutorial;
        properties.FindProperty("splitViewValidation").boolValue = false;
        properties.ApplyModifiedPropertiesWithoutUndo();
        game.name = tutorial ? "WorldShift tutorial" : "WorldShift " + environment;
        Save(scene, path);
    }
    private static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
    private static void Save(Scene scene, string path)
    {
        if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
    }
}
