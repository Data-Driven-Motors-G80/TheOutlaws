using System;
using System.Linq;
using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit new-scene builder; does not modify any existing environment scene.</summary>
public static class WorldShiftCitySetup
{
    public const string ScenePath = "Assets/Scenes/City_WorldShift.unity";

    [MenuItem("The Outlaws/World Shift/Open City Prototype")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
        else EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("The Outlaws/World Shift/Create City Prototype")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before creating the prototype.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        { Debug.Log("World Shift scene already exists; leaving it unchanged."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("City World Shift - 120 second prototype");
        var game = root.AddComponent<WorldShiftCity>();
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.fieldOfView = 62f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 400f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camera.transform.SetPositionAndRotation(new Vector3(0f, 8f, -14f), Quaternion.Euler(12f, 0f, 0f));
        var lightObject = new GameObject("Prototype sun");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        var setup = new SerializedObject(game);
        setup.FindProperty("roadTemplate").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoadSegment>("Assets/Prefabs/Road/RoadSegment_Straight.prefab");
        setup.FindProperty("carPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car/Car.prefab");
        setup.FindProperty("view").objectReferenceValue = camera;
        setup.FindProperty("sun").objectReferenceValue = light;
        SetPrototypePrefabs(setup);
        setup.ApplyModifiedPropertiesWithoutUndo();
        if (setup.FindProperty("roadTemplate").objectReferenceValue == null || setup.FindProperty("carPrefab").objectReferenceValue == null)
            throw new InvalidOperationException("Existing road/car prototype assets could not be found.");
        RenderSettings.skybox = null;
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("WORLD_SHIFT_CITY_SETUP_COMPLETE: " + ScenePath);
    }

    // Also upgrades the already-created scene without recreating its objects.
    public static void UpdateReferences()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Create(); return; }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<WorldShiftCity>();
        if (game == null) throw new InvalidOperationException("City World Shift controller is missing.");
        var setup = new SerializedObject(game);
        SetPrototypePrefabs(setup);
        setup.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("WORLD_SHIFT_CITY_REFERENCES_UPDATED");
    }

    private static void SetPrototypePrefabs(SerializedObject setup)
    {
        string[] fields = { "obstaclePrefab", "policePrefab", "streetlightPrefab", "fuelPrefab" };
        string[] paths = { "Assets/Prefabs/Obstacles/Obstacle.prefab", "Assets/Prefabs/Car/CityPoliceVehicle.prefab",
            "Assets/Prefabs/Streetlights/CityStreetlight.prefab", "Assets/Prefabs/Rewards/FuelTank.prefab" };
        for (int i = 0; i < fields.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null) throw new InvalidOperationException("Missing prototype prefab: " + paths[i]);
            setup.FindProperty(fields[i]).objectReferenceValue = prefab;
        }
    }
}
