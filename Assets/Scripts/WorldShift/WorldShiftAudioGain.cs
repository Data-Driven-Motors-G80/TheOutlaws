using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Effects volume and pause handling for existing collection chimes and warning horns.</summary>
    public sealed class WorldShiftAudioGain : MonoBehaviour
    {
        private AudioSource source;
        private WorldShiftCity game;
        private float gain;
        private bool paused;
        public static void Bind(AudioSource source, WorldShiftCity game, float gain)
        {
            var bus = source.gameObject.AddComponent<WorldShiftAudioGain>();
            bus.source = source; bus.game = game; bus.gain = gain;
            source.volume = gain * WorldShiftAudioSettings.Effects;
        }
        private void LateUpdate()
        {
            if (source == null || game == null) return;
            source.volume = gain * WorldShiftAudioSettings.Effects;
            bool suspend = WorldShiftAudioSettings.Suspended(game);
            if (suspend && !paused) source.Pause();
            else if (!suspend && paused) source.UnPause();
            paused = suspend;
            if (game.State == ShiftRunState.Won || game.State == ShiftRunState.Lost) source.Stop();
        }
    }
}
