using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit, one-time prototype builder. Never overwrites an existing desert scene.</summary>
public static class DesertSceneSetup
{
    private static readonly string[] Scenes = { "Env_City", "Env_Snow", "Env_OceanCoast", "Env_Desert" };

    [MenuItem("The Outlaws/Create Desert Prototype")]
    public static void Create()
    {
        const string path = "Assets/Scenes/Env_Desert.unity";
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            throw new InvalidOperationException("Desert already exists, or Play mode is active. Open the saved scene to tune it.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Env_City.unity", path)) throw new InvalidOperationException("Could not copy city scene.");
        FinishPrototype();
    }

    // Also permits resuming the initial setup if Unity interrupted before the desert flag was saved.
    public static void FinishPrototype()
    {
        const string path = "Assets/Scenes/Env_Desert.unity";
        var scene = EditorSceneManager.OpenScene(path);
        var environment = UnityEngine.Object.FindFirstObjectByType<EnvironmentRoot>();
        if (environment.IsDesert) throw new InvalidOperationException("Desert is already configured.");
        var oldSpawner = environment.Obstacles;
        var source = new SerializedObject(oldSpawner);
        source.FindProperty("m_Script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/Scripts/World/Environments/Desert/DesertObstacleSpawner.cs");
        source.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Reload the serialized subclass while preserving the component's file ID and all references.
        scene = EditorSceneManager.OpenScene(path);
        environment = UnityEngine.Object.FindFirstObjectByType<EnvironmentRoot>();
        if (!(environment.Obstacles is DesertObstacleSpawner)) throw new InvalidOperationException("Spawner replacement failed.");
        var rootSetup = new SerializedObject(environment);
        rootSetup.FindProperty("desert").boolValue = true;
        rootSetup.ApplyModifiedPropertiesWithoutUndo();
        environment.gameObject.AddComponent<DesertCrossingController>();
        environment.gameObject.AddComponent<DesertSandstorm>();
        var presentation = environment.gameObject.AddComponent<DesertPresentation>();
        var sand = CreateMaterial("DesertSand", new Color(0.64f, 0.39f, 0.2f));
        var road = CreateMaterial("DesertRoad", new Color(0.36f, 0.24f, 0.16f));
        var visuals = new SerializedObject(presentation);
        visuals.FindProperty("sandMaterial").objectReferenceValue = sand;
        visuals.ApplyModifiedPropertiesWithoutUndo();
        var roadSetup = new SerializedObject(environment.Road);
        roadSetup.FindProperty("roadSurfaceMaterial").objectReferenceValue = road;
        roadSetup.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.39f, 0.35f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.30f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.20f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 140f;
        RenderSettings.fogEndDistance = 310f;
        RenderSettings.fogColor = new Color(0.68f, 0.40f, 0.29f);
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.40f, 0.29f);
            if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.color = new Color(1f, 0.66f, 0.38f);
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(18f, -35f, 0f);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        foreach (string name in Scenes)
        {
            var current = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var switcher = UnityEngine.Object.FindFirstObjectByType<EnvironmentSwitcher>();
            var setup = new SerializedObject(switcher);
            var destinations = setup.FindProperty("environmentScenes");
            destinations.arraySize = Scenes.Length;
            for (int i = 0; i < Scenes.Length; i++) destinations.GetArrayElementAtIndex(i).stringValue = Scenes[i];
            setup.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(current);
            EditorSceneManager.SaveScene(current);
        }
        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Where(s => s.path != path).Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(path);
        EnvironmentValidation.Validate();
        Debug.Log("DESERT_SETUP_COMPLETE");
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = "Assets/Materials/Road/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Resources.Load<Material>("WeeWooRuntimeLit"));
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
