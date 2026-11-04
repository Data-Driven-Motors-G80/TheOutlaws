using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    /// <summary>Level select, unlock progress and level-to-level flow.</summary>
    public static class WorldShiftLevels
    {
        // Slot 0 is training. Every level runs in the shared City scene; the course
        // sets the environment, so new levels need no new scene.
        public static readonly ShiftLevel[] Environments =
            { ShiftLevel.Tutorial, ShiftLevel.City, ShiftLevel.Desert, ShiftLevel.Ocean, ShiftLevel.Snow, ShiftLevel.Mountain };
        public static int Count => Environments.Length;
        public const string LevelScene = "City_WorldShift";
        private const string BeatenKey = "WorldShift.Levels.HighestBeaten";
        private const string UnlockAllKey = "WorldShift.Levels.UnlockAll";

        /// <summary>The level being played, or -1 outside level play.</summary>
        public static int Current { get; private set; } = -1;
        public static bool Active => Current >= 0;
        public static int Attempt { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { Current = -1; Attempt = 0; }

        public static string Name(int level) => level == 0 ? "Training" : Environments[level].ToString();
        public static int HighestBeaten => PlayerPrefs.GetInt(BeatenKey, -1);
        public static bool IsBeaten(int level) => level <= HighestBeaten;
        public static bool IsUnlocked(int level) => level >= 0 && level < Count && (level == 0 || UnlockAll || level <= HighestBeaten + 1);
        public static bool HasNext => Current >= 0 && Current + 1 < Count;

        /// <summary>Team testing switch: opens every level without changing saved progress.</summary>
        public static bool UnlockAll
        {
            get => PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;
            set { PlayerPrefs.SetInt(UnlockAllKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void ResetProgress() { PlayerPrefs.DeleteKey(BeatenKey); PlayerPrefs.Save(); }

        public static bool Play(int level, string from = "menu")
        {
            if (!IsUnlocked(level)) return false;
            WorldShiftLevelAnalytics.EndAttempt(from == "retry" ? "retry" : "left");
            Current = level;
            Attempt++;
            WorldShiftLevelAnalytics.PendingStart = from;
            Time.timeScale = 1f;
            SceneManager.LoadScene(LevelScene);
            return true;
        }

        public static void Complete(int level)
        {
            if (level > HighestBeaten) { PlayerPrefs.SetInt(BeatenKey, level); PlayerPrefs.Save(); }
        }

        /// <summary>Other entry points (older endless escape, validation scenes) are not level play.</summary>
        public static void Leave() => Current = -1;

        public static void Retry() => Play(Current, "retry");
        public static void Next() { if (HasNext) Play(Current + 1, "next"); else Menu(); }

        public static void Menu()
        {
            WorldShiftLevelAnalytics.EndAttempt("menu");
            Current = -1;
            Time.timeScale = 1f;
            SceneManager.LoadScene(WorldShiftFlow.MenuScene);
        }
    }
}
