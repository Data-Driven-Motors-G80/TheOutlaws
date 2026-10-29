using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    public enum ShiftLevel { City, Desert, Tutorial }

    public static class WorldShiftFlow
    {
        public const string MenuScene = "WorldShift_Menu";
        private static ShiftLevel? autoStart;
        private static ShiftLevel afterTutorial = ShiftLevel.City;
        public static bool TutorialFirst = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        { autoStart = null; afterTutorial = ShiftLevel.City; TutorialFirst = true; }

        public static string SceneFor(ShiftLevel level) => level == ShiftLevel.City ? "City_WorldShift"
            : level == ShiftLevel.Desert ? "Desert_WorldShift" : "WorldShift_Tutorial";
        public static bool ConsumeAutoStart(ShiftLevel level)
        {
            if (autoStart != level) return false;
            autoStart = null; return true;
        }
        public static void Select(ShiftLevel level, bool tutorialFirst)
        {
            TutorialFirst = tutorialFirst;
            afterTutorial = level == ShiftLevel.Tutorial ? ShiftLevel.City : level;
            Load(tutorialFirst && level != ShiftLevel.Tutorial ? ShiftLevel.Tutorial : level);
        }
        private static void Load(ShiftLevel level)
        {
            Time.timeScale = 1f;
            autoStart = level;
            SceneManager.LoadScene(SceneFor(level));
        }
        public static void Next(ShiftLevel completed)
        {
            if (completed == ShiftLevel.Tutorial) { TutorialFirst = false; Load(afterTutorial); }
            else if (completed == ShiftLevel.City) Load(ShiftLevel.Desert);
            else Menu();
        }
        public static string TutorialDestination => afterTutorial == ShiftLevel.Desert ? "DESERT" : "CITY";
        public static void Menu() { autoStart = null; Time.timeScale = 1f; SceneManager.LoadScene(MenuScene); }
    }
}
