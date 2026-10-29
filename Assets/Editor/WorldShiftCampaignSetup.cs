using System.Collections.Generic;
using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldShiftCampaignSetup
{
    public const string MenuPath = "Assets/Scenes/WorldShift_Menu.unity";
    public const string TutorialPath = "Assets/Scenes/WorldShift_Tutorial.unity";
    public const string DesertPath = "Assets/Scenes/Desert_WorldShift.unity";
    [MenuItem("The Outlaws/World Shift/Open Game Menu")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null) Create();
        EditorSceneManager.OpenScene(MenuPath);
    }
    public static void Create()
    {
        var tutorial = EditorSceneManager.OpenScene(WorldShiftCitySetup.ScenePath);
        var city = Object.FindFirstObjectByType<WorldShiftCity>();
        var properties = new SerializedObject(city);
        properties.FindProperty("tutorialLevel").boolValue = true;
        properties.FindProperty("splitViewValidation").boolValue = false;
        properties.ApplyModifiedPropertiesWithoutUndo();
        city.name = "World Shift - independent 40 second tutorial";
        EditorSceneManager.SaveScene(tutorial, TutorialPath, true);

        var desert = EditorSceneManager.OpenScene(DesertShiftTrialSetup.ScenePath);
        var game = Object.FindFirstObjectByType<DesertShiftTrial>();
        properties = new SerializedObject(game);
        properties.FindProperty("fullLevel").boolValue = true;
        properties.FindProperty("policePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car/CityPoliceVehicle.prefab");
        properties.ApplyModifiedPropertiesWithoutUndo();
        game.name = "World Shift Desert - complete 2 minute level";
        EditorSceneManager.SaveScene(desert, DesertPath, true);

        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("The Outlaws - Level Select").AddComponent<WorldShiftMenu>();
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .045f, .07f); camera.gameObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(menu, MenuPath);
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in new[] { MenuPath, WorldShiftCitySetup.ScenePath, DesertPath, TutorialPath })
            scenes.Add(new EditorBuildSettingsScene(path, true));
        foreach (var scene in EditorBuildSettings.scenes)
            if (!scenes.Exists(s => s.path == scene.path)) scenes.Add(scene);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("WORLD_SHIFT_CAMPAIGN_READY");
    }
}
