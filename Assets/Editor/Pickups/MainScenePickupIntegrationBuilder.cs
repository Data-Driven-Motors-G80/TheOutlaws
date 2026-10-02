using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Wires the reviewed pickup slice into the team's latest main scene.</summary>
public static class MainScenePickupIntegrationBuilder
{
    private const string ScenePath = "Assets/Scenes/GetawayChase.unity";
    private const string PrefabPath = "Assets/Prefabs/Pickups/MysteryPickup.prefab";

    [MenuItem("Money Heist/Pickups/Integrate Into Main Scene")]
    public static void Integrate()
    {
        if (!Application.isBatchMode
            && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        AssetDatabase.Refresh();
        PickupItem pickupPrefab = AssetDatabase.LoadAssetAtPath<PickupItem>(PrefabPath);
        if (pickupPrefab == null || !EditorUtility.IsPersistent(pickupPrefab))
        {
            throw new InvalidOperationException(
                $"Pickup prefab is missing at {PrefabPath}. Run the pickup builder first.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AutoDriveCar car = UnityEngine.Object.FindFirstObjectByType<AutoDriveCar>();
        InfiniteRoad road = UnityEngine.Object.FindFirstObjectByType<InfiniteRoad>();
        ObstacleSpawner obstacles = UnityEngine.Object.FindFirstObjectByType<ObstacleSpawner>();
        if (car == null || road == null || obstacles == null)
        {
            throw new InvalidOperationException(
                "GetawayChase is missing the car, road, or obstacle spawner.");
        }

        CarPickupEffects effects = EnsureComponent<CarPickupEffects>(car.gameObject);
        EnsureComponent<RiskRunController>(car.gameObject);

        HandleCrash crash = car.GetComponent<HandleCrash>();
        if (crash != null)
        {
            SetReference(crash, "obstacles", obstacles);
            SetReference(crash, "pickupEffects", effects);
        }

        GameObject system = GameObject.Find("PickupSystem");
        if (system == null)
        {
            system = new GameObject("PickupSystem");
        }

        PickupSpawner spawner = EnsureComponent<PickupSpawner>(system);
        SetReference(spawner, "road", road);
        SetReference(spawner, "car", car.transform);
        SetReference(spawner, "pickupPrefab", pickupPrefab);
        SerializedObject verifySpawner = new SerializedObject(spawner);
        if (verifySpawner.FindProperty("pickupPrefab").objectReferenceValue == null)
        {
            throw new InvalidOperationException("PickupSpawner prefab reference could not be serialized.");
        }
        SetReference(spawner, "obstacles", obstacles);
        SetValue(spawner, "spawnDistance", 55f);
        SetValue(spawner, "spacing", 95f);
        SetValue(spawner, "effectDuration", 5f);
        SetValue(spawner, "cycleEffectsForTesting", false);

        PickupHUD hud = EnsureComponent<PickupHUD>(system);
        SetReference(hud, "effects", effects);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException($"Could not save {ScenePath}.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("PICKUP_MAIN_SCENE_READY: " + ScenePath);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{target.GetType().Name} has no serialized field '{propertyName}'.");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetValue<T>(UnityEngine.Object target, string propertyName, T value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{target.GetType().Name} has no serialized field '{propertyName}'.");
        }

        if (value is float floatValue)
            property.floatValue = floatValue;
        else if (value is bool boolValue)
            property.boolValue = boolValue;
        else
            throw new InvalidOperationException($"Unsupported serialized value type for '{propertyName}'.");

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
