using Outlaws.WorldShift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Team testing helpers for level play. In builds, Shift+U on the home screen toggles the same switch.</summary>
public static class WorldShiftLevelTools
{
    private const string UnlockAllMenu = "The Outlaws/Levels/Unlock All Levels (testing)";

    [MenuItem(UnlockAllMenu)]
    private static void ToggleUnlockAll() => WorldShiftLevels.UnlockAll = !WorldShiftLevels.UnlockAll;

    [MenuItem(UnlockAllMenu, true)]
    private static bool ToggleUnlockAllCheck()
    {
        Menu.SetChecked(UnlockAllMenu, WorldShiftLevels.UnlockAll);
        return true;
    }

    [MenuItem("The Outlaws/Levels/Reset Level Progress")]
    private static void ResetProgress()
    {
        WorldShiftLevels.ResetProgress();
        Debug.Log("WorldShift level progress cleared. Only Training is open (unless Unlock All is on).");
    }

    [MenuItem("The Outlaws/Levels/Play From Home Screen")]
    private static void PlayFromHome()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        EditorApplication.isPlaying = true;
    }
}
