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
        public static GetawayRunState Escape { get; private set; } = new GetawayRunState();
        private const string BankKey = "WorldShift.Getaway.Bank";
        private const string BestKey = "WorldShift.Getaway.Best";
        public static int Banked => PlayerPrefs.GetInt(BankKey, 0);
        public static int BestHaul => PlayerPrefs.GetInt(BestKey, 0);
        private static bool getawayAfterTutorial;
        public static bool TutorialStartsEscape => getawayAfterTutorial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        { autoStart = null; afterTutorial = ShiftLevel.City; TutorialFirst = true; getawayAfterTutorial = false; Escape = new GetawayRunState(); }

        public static void StartGetaway(bool tutorialFirst)
        {
            WorldShiftAnalytics.Abandon("restart");
            Escape.Begin();
            WorldShiftAnalytics.BeginRun(tutorialFirst);
            afterTutorial = ShiftLevel.City;
            getawayAfterTutorial = tutorialFirst;
            Load(tutorialFirst ? ShiftLevel.Tutorial : ShiftLevel.City);
        }

        public static void FinishGetawayLeg(bool continueRun)
        {
            if (Escape.Status != GetawayRunState.RunStatus.Running) return;
            WorldShiftAnalytics.Gate(continueRun);
            if (continueRun)
            {
                Escape.Continue();
                Load(Escape.Leg % 2 == 0 ? ShiftLevel.City : ShiftLevel.Desert);
            }
            else
            {
                int deposit = Escape.Bank();
                PlayerPrefs.SetInt(BankKey, (int)System.Math.Min(int.MaxValue, (long)Banked + deposit));
                PlayerPrefs.SetInt(BestKey, Mathf.Max(BestHaul, deposit));
                PlayerPrefs.Save();
                WorldShiftAnalytics.Banked(deposit);
            }
        }

        public static string SceneFor(ShiftLevel level) => level == ShiftLevel.City ? "City_WorldShift"
            : level == ShiftLevel.Desert ? "Desert_WorldShift" : "WorldShift_Tutorial";
        public static bool ConsumeAutoStart(ShiftLevel level)
        {
            if (autoStart != level) return false;
            autoStart = null; return true;
        }
        public static void Select(ShiftLevel level, bool tutorialFirst)
        {
            WorldShiftAnalytics.Abandon("menu");
            Escape.Leave();
            getawayAfterTutorial = false;
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
            if (completed == ShiftLevel.Tutorial)
            {
                WorldShiftAnalytics.EndTutorial("skipped");
                TutorialFirst = false;
                if (!getawayAfterTutorial) Escape.Leave();
                getawayAfterTutorial = false;
                Load(afterTutorial);
            }
            else if (completed == ShiftLevel.City) Load(ShiftLevel.Desert);
            else Menu();
        }
        public static string TutorialDestination => afterTutorial == ShiftLevel.Desert ? "DESERT" : "CITY";
        public static void Menu() { WorldShiftAnalytics.Abandon("menu"); Escape.Leave(); getawayAfterTutorial = false; autoStart = null; Time.timeScale = 1f; SceneManager.LoadScene(MenuScene); }
    }
}
