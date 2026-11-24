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
        string[] expected = { "Tutorial", "Easy", "Medium", "Medium", "Hard", "Hard" };
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
        string[] expected = { "WARM-UP", "EASY", "MILD", "MILD", "SPICY", "SPICY" };
        for (int level = 0; level < 6; level++)
        {
            var card = menu.GetComponentsInChildren<Transform>().Single(t => t.name == "Level " + level);
            var tag = card.GetComponentsInChildren<Transform>().Single(t => t.name.StartsWith("Difficulty ") && t.name != "Difficulty pip");
            Assert.That(tag.GetComponentsInChildren<TMPro.TMP_Text>().Single().text, Is.EqualTo(expected[level]), $"Level {level} card");
            int[] peppers = { 0, 1, 2, 2, 3, 3 };
            Assert.That(tag.GetComponentsInChildren<Transform>().Count(t => t.name == "Chili pepper"), Is.EqualTo(peppers[level]), $"Level {level} chili peppers");
        }
    }

    [UnityTest]
    public IEnumerator BeatenLevelsShowCompletedButStayPlayableWithDifficulty()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/WorldShift_Menu.unity");
        yield return new EnterPlayMode();
        Static(Levels, "ResetProgress"); for (int i = 0; i <= 2; i++) Static(Levels, "Complete", i); PlayerPrefs.SetInt(UnlockAllKey, 0);
        var menu = Find("WorldShiftMenu");
        Call(menu, "BuildLevels");
        yield return null;
        CheckCompletedCards(menu);
        Capture("menu-completed");
        Static(Levels, "ResetProgress");
    }

    private static void CheckCompletedCards(MonoBehaviour menu)
    {
        for (int level = 0; level < 6; level++)
        {
            var card = menu.GetComponentsInChildren<Transform>().Single(t => t.name == "Level " + level);
            bool stamped = card.GetComponentsInChildren<Transform>().Any(t => t.name == "Completed stamp");
            Assert.That(stamped, Is.EqualTo(level <= 2), $"Level {level} COMPLETED! stamp");
            Assert.That(card.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("Difficulty ")), Is.True, $"Level {level} keeps its difficulty");
        }
        Assert.That(Static(Levels, "IsUnlocked", 1), Is.True, "A completed level can be replayed.");
    }
}
