using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit editor command: never changes an open scene automatically.
public static class OutlawsSceneSetup
{
    [MenuItem("The Outlaws/Apply Gameplay Layout to Current Scene")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.name != "GetawayChase")
            throw new InvalidOperationException("Open GetawayChase outside Play mode first.");
        var player = GameObject.Find("PlayerCar").transform;
        player.localScale = Vector3.one * 0.85f;
        // Resize the model and hitbox together without moving the child camera.
        player.Find("Car").localScale = new Vector3(1.76f, 1f, 3.08f);
        player.GetComponent<BoxCollider>().size = new Vector3(1.76f, 1f, 3.08f);
        var shooting = player.GetComponent<OutlawShooting>() ?? player.gameObject.AddComponent<OutlawShooting>();
        var camera = Camera.main;
        camera.transform.localPosition = new Vector3(0, 24, -32) / 0.85f;
        camera.transform.localRotation = Quaternion.Euler(35, 0, 0);
        camera.fieldOfView = 60;
        camera.transform.localScale = Vector3.one;

        var police = UnityEngine.Object.FindObjectsByType<ChaseCar>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < police.Length; i++)
        {
            police[i].gameObject.SetActive(i == 0);
            if (i != 0) continue;
            police[i].transform.localScale = new Vector3(2, 1, 3.5f) * 0.85f;
            police[i].transform.position = player.position - player.forward * 11.1875f;
            police[i].transform.rotation = player.rotation;
            foreach (var renderer in police[i].GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                renderer.sharedMaterial = SaveMaterial(renderer.sharedMaterial, "OutlawsPolice", new Color(0.08f, 0.2f, 0.55f));
            }
        }
        var root = GameObject.Find("The Outlaws Game Systems") ?? new GameObject("The Outlaws Game Systems");
        if (!root.GetComponent<OutlawGameManager>()) root.AddComponent<OutlawGameManager>();
        if (!root.GetComponent<OutlawPickupSpawner>()) root.AddComponent<OutlawPickupSpawner>();
        var hud = root.GetComponent<OutlawHUD>() ?? root.AddComponent<OutlawHUD>();
        hud.BuildLabeledBars();
        // Ammo is supplied by the distance-based spawner, never a free opening pickup.
        foreach (var pickup in root.GetComponentsInChildren<OutlawAmmoPickup>(true))
            pickup.gameObject.SetActive(false);
        // Record prefab overrides so the saved cars match the editor view.
        foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Material SaveMaterial(Material source, string name, Color color)
    {
        const string folder = "Assets/Materials/Outlaws";
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "Outlaws");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void BakeBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GetawayChase.unity");
        Apply();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("OUTLAWS SCENE BAKED");
    }
}
