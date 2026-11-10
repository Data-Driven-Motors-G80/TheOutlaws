using System;
using System.Linq;
using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates only the menu and shared level scene, without overwriting existing scenes.</summary>
public static class WorldShiftCampaignSetup
{
    public const string MenuPath = "Assets/Scenes/WorldShift_Menu.unity";
    public static readonly string[] GameScenePaths = { MenuPath, WorldShiftCitySetup.ScenePath };

    [MenuItem("The Outlaws/Levels/Open Home Screen")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!Exists(MenuPath)) Create();
        EditorSceneManager.OpenScene(MenuPath);
    }

    [MenuItem("The Outlaws/Levels/Create Missing Game Scenes")]
    public static void EnsureScenes()
    {
        if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create();
    }

    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before setting up scenes.");
        if (!Exists(WorldShiftCitySetup.ScenePath)) WorldShiftCitySetup.Create();
        if (!Exists(MenuPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("WorldShift menu").AddComponent<WorldShiftMenu>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .045f, .07f); camera.gameObject.AddComponent<AudioListener>();
            if (!EditorSceneManager.SaveScene(scene, MenuPath)) throw new InvalidOperationException("Could not save " + MenuPath);
        }
        EditorBuildSettings.scenes = GameScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Level setup ready: menu and shared scene for Training and all five levels. Existing scenes preserved.");
    }

    private static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
}
