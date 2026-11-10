using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    public enum ShiftLevel { City, Desert, Tutorial, Ocean, Snow, Mountain }

    public static class WorldShiftFlow
    {
        private static readonly ShiftLevel[] environmentOrder = { ShiftLevel.City, ShiftLevel.Desert, ShiftLevel.Ocean, ShiftLevel.Snow, ShiftLevel.Mountain };
        public static ShiftLevel LevelForLeg(int leg) => environmentOrder[Mathf.Max(0, leg) % environmentOrder.Length];
        public static ShiftLevel NextEnvironment => LevelForLeg(Escape.Leg + 1);
        public static string EnvironmentName(ShiftLevel level) => level.ToString();
        public static bool CashOutAvailable => Escape.Leg % 2 == 1;
        public static int DifficultyTier => Mathf.Min(Escape.Leg / environmentOrder.Length, 3);
        public static float ObstacleMotionMultiplier => 1f + .15f * DifficultyTier;
        public static VehicleUpgrades Garage { get; } = new VehicleUpgrades();
        private static bool openGarage;
        public static bool ConsumeGarageRequest() { bool value = openGarage; openGarage = false; return value; }
        public static void OpenGarage()
        {
            WorldShiftAnalytics.Abandon("garage");
            Escape.Leave(); getawayAfterTutorial = false; autoStart = null;
            openGarage = true; Time.timeScale = 1f; SceneManager.LoadScene(MenuScene);
        }
        public static bool BuyUpgrade(VehicleUpgrades.Part part)
        {
            int price = Garage.Price(part);
            bool bought = Escape.Status != GetawayRunState.RunStatus.Running && Garage.Buy(part);
            if (bought) WorldShiftAnalytics.UpgradeBought(part, price);
            return bought;
        }

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
        { openGarage = false; autoStart = null; afterTutorial = ShiftLevel.City; TutorialFirst = true; getawayAfterTutorial = false; Escape = new GetawayRunState(); }

        public static void StartGetaway(bool tutorialFirst)
        {
            // Compatibility entry point: the old escape scenes have been retired.
            Select(tutorialFirst ? ShiftLevel.Tutorial : ShiftLevel.City, false);
        }

        public static void FinishGetawayLeg(bool continueRun)
        {
            if (Escape.Status != GetawayRunState.RunStatus.Running) return;
            // Ordinary environment endings cannot bank a run.
            if (!continueRun && !CashOutAvailable) return;
            WorldShiftAnalytics.Gate(continueRun);
            if (continueRun)
            {
                Escape.Continue();
                Load(LevelForLeg(Escape.Leg));
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

        // All current courses use the same scene; legacy callers must not load deleted variants.
        public static string SceneFor(ShiftLevel level) => WorldShiftLevels.LevelScene;
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
            Escape.Leave();
            getawayAfterTutorial = false;
            autoStart = null;
            int index = System.Array.IndexOf(WorldShiftLevels.Environments, level);
            WorldShiftLevels.Play(index);
        }
        public static void Next(ShiftLevel completed)
        {
            if (WorldShiftLevels.Active) WorldShiftLevels.Next(); else Menu();
        }

        public static string TutorialDestination => afterTutorial == ShiftLevel.Desert ? "DESERT" : "CITY";
        public static void Menu() { WorldShiftLevels.Leave(); WorldShiftAnalytics.Abandon("menu"); Escape.Leave(); getawayAfterTutorial = false; autoStart = null; Time.timeScale = 1f; SceneManager.LoadScene(MenuScene); }
    }
}
