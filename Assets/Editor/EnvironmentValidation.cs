using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only checks of saved environment wiring, including moved scripts and prefabs.</summary>
public static class EnvironmentValidation
{
    [MenuItem("The Outlaws/Validate Environment Setup")]
    public static void Validate()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Validate outside Play mode.");
        foreach (string name in new[] { "Env_City", "Env_Snow" })
        {
            string path = $"Assets/Scenes/{name}.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                foreach (Transform child in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        $"{name}: missing script on {child.name}");
                EnvironmentRoot environment = roots.SelectMany(root => root.GetComponentsInChildren<EnvironmentRoot>(true)).Single();
                Require(environment.ValidateSetup(), name + ": incomplete EnvironmentRoot");
                Require(environment.GetComponent<OutlawGameManager>() != null, name + ": missing game manager");
                Require(environment.GetComponent<EnvironmentSwitcher>() != null, name + ": missing portal switcher");
                Require(environment.IsSnow == (name == "Env_Snow"), name + ": wrong environment flag");
                Require(environment.Rewards.SunnyDayChance == (environment.IsSnow ? 0.25f : 0f), name + ": changed reward odds");
                Require(environment.Obstacles.RoadReference == environment.Road, name + ": inconsistent road references");
                Require(environment.Player.GetComponent<CarPickupEffects>() != null, name + ": missing effects");
                Require(environment.Player.GetComponent<OutlawShooting>() != null, name + ": missing shooting");
                Require(environment.Player.GetComponent<FuelMeter>() != null, name + ": missing fuel");
                var obstacleSetup = new SerializedObject(environment.Obstacles);
                Require((obstacleSetup.FindProperty("motion").objectReferenceValue != null) == environment.IsSnow,
                    name + ": wrong obstacle movement");
                if (environment.IsSnow)
                {
                    Require(environment.Obstacles.GetComponent<SnowFogObstacle>() != null, name + ": missing fog");
                    Require(obstacleSetup.FindProperty("rowSpacingMultiplier").floatValue == 1.7f, name + ": changed row spacing");
                    Require(obstacleSetup.FindProperty("roadblockSpacingMultiplier").floatValue == 1.7f, name + ": changed roadblock spacing");
                }
                Require(new SerializedObject(environment.MysterySpawner).FindProperty("pickupPrefab").objectReferenceValue != null,
                    name + ": missing mystery pickup prefab");
                Require(new SerializedObject(environment.FuelSpawner).FindProperty("fuelTankPrefab").objectReferenceValue != null,
                    name + ": missing fuel prefab");
                Debug.Log("ENVIRONMENT_VALID: " + name);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
