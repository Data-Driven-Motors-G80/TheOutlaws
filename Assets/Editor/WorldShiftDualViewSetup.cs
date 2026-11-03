using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Outlaws.WorldShift;

/// <summary>Separate opt-in scene; the longer city prototype stays available for comparison.</summary>
public static class WorldShiftDualViewSetup
{
    public const string ScenePath = "Assets/Scenes/City_DualWorldValidation.unity";

    [MenuItem("The Outlaws/World Shift/Open Short Dual-World Validation")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create();
        EditorSceneManager.OpenScene(ScenePath);
    }

    public static void Create()
    {
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
        var scene = EditorSceneManager.OpenScene(WorldShiftCitySetup.ScenePath);
        var game = Object.FindFirstObjectByType<WorldShiftCity>();
        if (game == null) throw new System.InvalidOperationException("Original City World Shift controller is missing.");
        var settings = new SerializedObject(game);
        settings.FindProperty("splitViewValidation").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        game.name = "City - 20 second dual-world crop validation";
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        AssetDatabase.Refresh();
        Debug.Log("DUAL_WORLD_VALIDATION_CREATED: " + ScenePath);
    }
}
