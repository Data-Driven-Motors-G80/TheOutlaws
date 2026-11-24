using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outlaws.WorldShift
{
    public enum LevelDifficulty { Tutorial, Easy, Medium, Hard }

    /// <summary>Level select, unlock progress and level-to-level flow.</summary>
    public static class WorldShiftLevels
    {
        // Slot 0 is training. Ordered by difficulty so both Hard levels (Snow, Desert) come last.
        // Every level runs in the shared City scene; the course
        // sets the environment, so new levels need no new scene.
        public static readonly ShiftLevel[] Environments =
            { ShiftLevel.Tutorial, ShiftLevel.City, ShiftLevel.Ocean, ShiftLevel.Mountain, ShiftLevel.Snow, ShiftLevel.Desert };
        public static int Count => Environments.Length;
        public const string LevelScene = "City_WorldShift";
        private const string BeatenKey = "WorldShift.Levels.HighestBeaten";
        private const string UnlockAllKey = "WorldShift.Levels.UnlockAll";

        /// <summary>The level being played, or -1 outside level play.</summary>
        public static int Current { get; private set; } = -1;
        public static bool Active => Current >= 0;
        public static int Attempt { get; private set; }
        private static bool transitionPending;
        public static bool StartWithStory { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Current = -1; Attempt = 0; transitionPending = false; StartWithStory = false;
            SceneManager.sceneLoaded -= TransitionLoaded;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Start each development session with every level available.
            UnlockAll = true;
#endif
        }

        // Ignore input still reaching the outgoing UI while the next scene loads.
        private static bool BeginTransition()
        {
            if (transitionPending || WorldShiftSceneLoading.Active) return false;
            transitionPending = true;
            SceneManager.sceneLoaded += TransitionLoaded;
            return true;
        }

        private static void TransitionLoaded(Scene scene, LoadSceneMode mode)
        {
            transitionPending = false;
            SceneManager.sceneLoaded -= TransitionLoaded;
        }

        public static string Name(int level) => level == 0 ? "Training" : Environments[level].ToString();

        /// <summary>
        /// Shown on each level card so players know what they are picking (playtest feedback: Snow and Desert
        /// were a surprise). Set from obstacle density: City ~10/km, Ocean ~12/km, Mountain ~19/km (medium-hard),
        /// Snow (with ice) and Desert ~26/km. Update when a level is retuned.
        /// </summary>
        public static LevelDifficulty Difficulty(int level)
        {
            switch (level >= 0 && level < Count ? Environments[level] : ShiftLevel.Tutorial)
            {
                case ShiftLevel.City: return LevelDifficulty.Easy;
                case ShiftLevel.Ocean: case ShiftLevel.Mountain: return LevelDifficulty.Medium;
                case ShiftLevel.Snow: case ShiftLevel.Desert: return LevelDifficulty.Hard;
                default: return LevelDifficulty.Tutorial;
            }
        }
        public static int HighestBeaten => PlayerPrefs.GetInt(BeatenKey, -1);
        public static bool IsBeaten(int level) => level <= HighestBeaten;
        public static bool IsUnlocked(int level) => level >= 0 && level < Count && (level == 0 || UnlockAll || level <= HighestBeaten + 1);
        public static bool HasNext => Current >= 0 && Current + 1 < Count;

        /// <summary>Team testing switch: opens every level without changing saved progress.</summary>
        public static bool UnlockAll
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                PlayerPrefs.SetInt(UnlockAllKey, value ? 1 : 0);
                PlayerPrefs.Save();
#endif
            }
        }

        public static void ResetProgress() { PlayerPrefs.DeleteKey(BeatenKey); PlayerPrefs.Save(); }

        public static bool Play(int level, string from = "menu")
        {
            if (!IsUnlocked(level) || !BeginTransition()) return false;
            WorldShiftLevelAnalytics.EndAttempt(from == "retry" ? "retry" : "left");
            Current = level;
            StartWithStory = (level == 1 || level == 2) && from != "retry";
            Attempt++;
            WorldShiftLevelAnalytics.PendingStart = from;
            Time.timeScale = 1f;
            WorldShiftSceneLoading.Load(LevelScene, Name(level), level > 0 ? "WorldShiftMusic/" + Environments[level] : null);
            return true;
        }

        public static void Complete(int level)
        {
            if (level > HighestBeaten) { PlayerPrefs.SetInt(BeatenKey, level); PlayerPrefs.Save(); }
        }

        /// <summary>Other entry points (older endless escape, validation scenes) are not level play.</summary>
        public static void Leave() => Current = -1;

        public static void Retry() => Play(Current, "retry");
        public static void Next() { if (transitionPending) return; if (HasNext) Play(Current + 1, "next"); else Menu(); }

        public static void Menu()
        {
            if (!BeginTransition()) return;
            WorldShiftLevelAnalytics.EndAttempt("menu");
            Current = -1;
            StartWithStory = false;
            Time.timeScale = 1f;
            WorldShiftSceneLoading.Load(WorldShiftFlow.MenuScene, "Wee Woo Run");
        }
    }
}
