using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DesertShiftTrialSetup
{
    public const string ScenePath = "Assets/Scenes/Desert_WorldShiftTrial.unity";
    [MenuItem("The Outlaws/World Shift/Open Desert Rock Freeze Trial")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create(); EditorSceneManager.OpenScene(ScenePath);
    }
    public static void Create()
    {
        if (Application.isPlaying) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            var existing = EditorSceneManager.OpenScene(ScenePath);
            var controller = Object.FindFirstObjectByType<DesertShiftTrial>();
            if (controller == null) throw new System.InvalidOperationException("Desert trial controller missing.");
            var references = new SerializedObject(controller);
            SetDesertReferences(references); references.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(existing);
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("Desert - 40 second roll and freeze trial").AddComponent<DesertShiftTrial>();
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.gameObject.AddComponent<AudioListener>(); camera.fieldOfView = 68f; camera.nearClipPlane = .1f; camera.farClipPlane = 300f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camera.transform.SetPositionAndRotation(new Vector3(0f, 6.6f, -12f), Quaternion.Euler(10f, 0f, 0f));
        var sun = new GameObject("Desert prototype sun").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        var settings = new SerializedObject(game);
        settings.FindProperty("roadTemplate").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoadSegment>("Assets/Prefabs/Road/RoadSegment_Straight.prefab");
        settings.FindProperty("carPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car/Car.prefab");
        settings.FindProperty("view").objectReferenceValue = camera;
        settings.FindProperty("sun").objectReferenceValue = sun;
        SetDesertReferences(settings);
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("DESERT_SHIFT_TRIAL_CREATED: " + ScenePath);
    }

    private static void SetDesertReferences(SerializedObject settings)
    {
        settings.FindProperty("roadMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road/DesertRoad.mat");
        settings.FindProperty("sandMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Road/DesertSand.mat");
        settings.FindProperty("obstaclePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacles/Obstacle.prefab");
    }
}
