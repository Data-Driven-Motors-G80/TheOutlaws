using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Presentation only: driving feedback and licensed environment songs.</summary>
    public sealed class WorldShiftLevelAudio : MonoBehaviour
    {
        private WorldShiftCity game;
        private AudioSource engine, tyres, cues, music, ocean, gull;
        private AudioLowPassFilter musicFilter;
        private float musicGain;
        private AudioClip takeoff, landing, crash, victory;
        private ShiftRunState previousState;
        private bool previousAirborne, suspended;
        private float previousLateral, dayBlend = 1f, gullIn = 9f;
        public float DayBlend => dayBlend;
        public bool Suspended => suspended;

        public void Bind(WorldShiftCity controller)
        {
            game = controller;
            engine = Source("Player engine", "WS_Engine", true);
            tyres = Source("Player tyres", "WS_Tyres", true);
            cues = Source("Driving events", null, false);
            takeoff = Load("WS_Takeoff"); landing = Load("WS_Landing");
            crash = Load("WS_Crash"); victory = Load("WS_Victory");
            if (!game.Course.Training)
            {
                music = Source("Environment song", "WorldShiftMusic/" + game.Environment, true);
                music.priority = 64;
                musicFilter = music.gameObject.AddComponent<AudioLowPassFilter>();
                musicFilter.cutoffFrequency = 18000f;
                music.Play();
            }
            if (game.Environment == ShiftLevel.Ocean)
            {
                ocean = Source("Coastal wind and waves", "WS_Ocean", true);
                gull = Source("Distant gull", "WS_Gull", false);
                ocean.Play();
            }
            engine.Play(); tyres.Play();
            previousState = game.State; previousLateral = game.Lateral;
            previousAirborne = game.IsRelayAirborne;
        }

        private static AudioClip Load(string name) => Resources.Load<AudioClip>(name);
        private AudioSource Source(string name, string clip, bool loop)
        {
            var root = new GameObject(name); root.transform.SetParent(transform, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
            source.volume = 0f; source.dopplerLevel = 0f;
            if (clip != null) source.clip = Load(clip);
            return source;
        }
        private void LateUpdate() => Refresh(Time.unscaledDeltaTime);

        public void Refresh(float delta)
        {
            if (game == null) return;
            delta = Mathf.Max(0f, delta);
            bool pause = WorldShiftAudioSettings.Suspended(game);
            if (pause != suspended)
            {
                foreach (var source in new[] { engine, tyres, cues, music, ocean, gull })
                    if (source != null) { if (pause) source.Pause(); else source.UnPause(); }
                suspended = pause;
                previousLateral = game.Lateral;
            }
            if (pause) return;
            bool running = game.State == ShiftRunState.Running;
            bool airborne = game.IsRelayAirborne;
            float effects = WorldShiftAudioSettings.Effects;
            cues.volume = .65f * effects;
            if (previousState == ShiftRunState.Running && game.State == ShiftRunState.Retrying && game.Capturing)
                cues.PlayOneShot(crash);
            if (previousState != ShiftRunState.Won && game.State == ShiftRunState.Won)
                cues.PlayOneShot(victory, .8f);
            if (running && previousState == ShiftRunState.Running)
            {
                if (airborne && !previousAirborne) cues.PlayOneShot(takeoff, .65f);
                if (!airborne && previousAirborne) cues.PlayOneShot(landing, .85f);
            }
            float speed = Mathf.Clamp01(game.Speed / 40f);
            float steer = delta > .0001f ? Mathf.Abs(game.Lateral - previousLateral) / delta : 0f;
            engine.pitch = Mathf.MoveTowards(engine.pitch, airborne ? 1.30f : .65f + speed * .75f, delta * 1.5f);
            engine.volume = Mathf.MoveTowards(engine.volume, running ? effects * (airborne ? .055f : .16f) : 0f, delta);
            tyres.volume = Mathf.MoveTowards(tyres.volume, running && !airborne ? effects * Mathf.InverseLerp(3.5f, 8f, steer) * .12f : 0f, delta);
            if (music != null)
            {
                dayBlend = Mathf.MoveTowards(dayBlend, game.World == ShiftWorld.Day ? 1f : 0f, delta / .75f);
                float target = (running ? 1f : game.State == ShiftRunState.Retrying ? .35f : 0f);
                musicGain = Mathf.MoveTowards(musicGain, target, delta / .5f);
                // Preserve the song's position and pitch; Night changes its tone, never restarts it.
                musicFilter.cutoffFrequency = Mathf.Lerp(4200f, 18000f, dayBlend);
                float edge = music.clip == null ? 0f : Mathf.Min(1f, music.time / 1.2f, (music.clip.length - music.time) / 1.2f);
                music.volume = musicGain * WorldShiftAudioSettings.Music * WorldShiftMusicLibrary.MixGain(game.Environment) * .50f * Mathf.Lerp(.88f, 1f, dayBlend) * Mathf.Max(0f, edge);
            }
            if (ocean != null)
            {
                ocean.volume = effects * (running ? .35f : .12f);
                gull.volume = effects * .30f;
                if (running && game.World == ShiftWorld.Day)
                {
                    gullIn -= delta;
                    if (gullIn <= 0f) { gull.Play(); gullIn = 17f; }
                }
            }
            previousState = game.State;
            previousAirborne = airborne;
            previousLateral = game.Lateral;
        }
    }
}
