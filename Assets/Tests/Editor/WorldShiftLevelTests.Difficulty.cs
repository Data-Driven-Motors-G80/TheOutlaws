using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

// Playtesters did not know Snow and Desert were hard: every level card shows its difficulty.
public sealed partial class WorldShiftLevelTests
{
    [Test]
    public void LevelDifficultiesFollowObstacleDensity()
    {
        string[] expected = { "Tutorial", "Easy", "Medium", "Hard", "Medium", "Hard" };
        for (int level = 0; level < 6; level++)
            Assert.That(Static(Levels, "Difficulty", level).ToString(), Is.EqualTo(expected[level]), $"Level {level}");
    }

    [UnityTest]
    public IEnumerator EveryLevelCardShowsItsDifficulty()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Levels.GetProperty("UnlockAll").SetValue(null, true);
        var menu = Find("WorldShiftMenu");
        Call(menu, "BuildLevels");
        yield return null;
        CheckDifficultyCards(menu);
        Capture("menu-difficulty");
        WorldShiftTestCapture.Capture("menu-difficulty-portrait", 720, 1080);
    }

    // Kept out of the coroutine: entering play mode reloads the domain and drops lambda captures.
    private static void CheckDifficultyCards(MonoBehaviour menu)
    {
        string[] expected = { "TUTORIAL", "EASY", "MEDIUM", "HARD", "MEDIUM", "HARD" };
        for (int level = 0; level < 6; level++)
        {
            var card = menu.GetComponentsInChildren<Transform>().Single(t => t.name == "Level " + level);
            var tag = card.GetComponentsInChildren<Transform>().Single(t => t.name.StartsWith("Difficulty ") && t.name != "Difficulty pip");
            Assert.That(tag.GetComponentsInChildren<TMPro.TMP_Text>().Single().text, Is.EqualTo(expected[level]), $"Level {level} card");
        }
    }
}
