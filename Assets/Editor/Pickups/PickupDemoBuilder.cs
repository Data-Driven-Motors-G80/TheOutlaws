using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class PickupDemoBuilder
{
    public const string DemoPath = "Assets/Scenes/PickupPlayground.unity";
    private const string PrefabPath = "Assets/Prefabs/Pickups/MysteryPickup.prefab";

    [MenuItem("Money Heist/Pickups/Create or Refresh Test Scene")]
    public static void CreateDemo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Prefabs/Pickups");
        AssetDatabase.Refresh();
        CreatePickupPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);

        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GetawayChase.unity", OpenSceneMode.Single);
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        PickupItem prefab = prefabAsset != null ? prefabAsset.GetComponent<PickupItem>() : null;
        if (prefab == null || !EditorUtility.IsPersistent(prefab))
            throw new InvalidOperationException("Pickup prefab asset could not be loaded after opening the scene.");
        // Remove inherited presentation effects from this separate mechanics test scene.
        RenderSettings.skybox = null;
        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = false;
        }
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            volume.enabled = false;
        AutoDriveCar car = UnityEngine.Object.FindFirstObjectByType<AutoDriveCar>();
        InfiniteRoad road = UnityEngine.Object.FindFirstObjectByType<InfiniteRoad>();
        ObstacleSpawner obstacles = UnityEngine.Object.FindFirstObjectByType<ObstacleSpawner>();
        if (car == null || road == null) throw new InvalidOperationException("Upstream scene is missing car or road.");
        CarPickupEffects effects = car.gameObject.AddComponent<CarPickupEffects>();
        car.gameObject.AddComponent<RiskRunController>();
        GameObject system = new GameObject("PickupSystem");
        PickupSpawner spawner = system.AddComponent<PickupSpawner>();
        SerializedObject setup = new SerializedObject(spawner);
        setup.FindProperty("road").objectReferenceValue = road;
        setup.FindProperty("car").objectReferenceValue = car.transform;
        setup.FindProperty("pickupPrefab").objectReferenceValue = prefab;
        setup.FindProperty("obstacles").objectReferenceValue = obstacles;
        setup.FindProperty("cycleEffectsForTesting").boolValue = false;
        setup.ApplyModifiedPropertiesWithoutUndo();
        PickupHUD hud = system.AddComponent<PickupHUD>();
        SerializedObject hudSetup = new SerializedObject(hud);
        hudSetup.FindProperty("effects").objectReferenceValue = effects;
        hudSetup.ApplyModifiedPropertiesWithoutUndo();
        // Leave enough reaction space for the temporary driving effects and recovery loop.
        if (obstacles != null)
        {
            SerializedObject obstacleSetup = new SerializedObject(obstacles);
            obstacleSetup.FindProperty("minSpacing").floatValue = 24f;
            obstacleSetup.FindProperty("maxSpacing").floatValue = 36f;
            obstacleSetup.FindProperty("doubleRowChance").floatValue = 0.15f;
            obstacleSetup.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!EditorSceneManager.SaveScene(scene, DemoPath)) throw new IOException("Could not save pickup test scene.");
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(entry => entry.path == DemoPath))
            scenes.Add(new EditorBuildSettingsScene(DemoPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PICKUP_DEMO_READY: " + DemoPath);
    }

    private static void CreatePickupPrefab()
    {
        GameObject root = new GameObject("MysteryPickup");
        try
        {
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.9f;
            collider.isTrigger = true;
            root.AddComponent<PickupItem>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "PickupPlaceholder";
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.85f;
            // Mechanics-only placeholder: default cube, no custom material or animation.
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
