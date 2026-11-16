using UnityEngine;

namespace Outlaws.WorldShift
{
    public static class WorldShiftAudioSettings
    {
        public const string MusicKey = "ws_audio_music", EffectsKey = "ws_audio_effects";
        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, .65f);
            set => Store(MusicKey, value);
        }
        public static float Effects
        {
            get => PlayerPrefs.GetFloat(EffectsKey, .8f);
            set => Store(EffectsKey, value);
        }
        private static void Store(string key, float value)
        {
            PlayerPrefs.SetFloat(key, float.IsNaN(value) ? 0f : Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
        public static bool Suspended(WorldShiftCity game) => WorldShiftSceneLoading.Active || game.StoryActive || game.State == ShiftRunState.Paused
            || game.TrainingLessonPaused || game.TrainingFreezeWaiting;
    }
}
