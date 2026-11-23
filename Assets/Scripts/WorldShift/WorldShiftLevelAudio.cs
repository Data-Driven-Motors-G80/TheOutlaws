using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Presentation only: driving feedback and licensed environment songs.</summary>
    public sealed partial class WorldShiftLevelAudio : MonoBehaviour
    {
        private WorldShiftCity game;
        private AudioSource engine, tyres, cues, music, ocean, gull;
        private AudioSource cityCrowd;
        private readonly AudioSource[] cityVoices = new AudioSource[2];
        private readonly float[] cityVoiceIn = { .5f, 1.8f };
        private readonly int[] cityVoiceIndex = { 0, 5 };
        private AudioClip[] cityPhrases;
        private WorldShiftEnvironmentScenery cityScenery;
        private float cityCrowdGain;
        private AudioLowPassFilter musicFilter;
        private float musicGain;
        private AudioClip takeoff, landing, crash, victory;
        private ShiftRunState previousState;
        private bool previousAirborne, suspended, winCuePlayed;
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
            crash = Load("WS_Crash"); victory = Load(game.LevelIndex == WorldShiftLevels.Count-1 ? "WS_Finale" : "WS_Victory");
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
            if (game.Environment == ShiftLevel.City && !game.Course.Training)
            {
                cityScenery = game.GetComponentInChildren<WorldShiftEnvironmentScenery>();
                cityCrowd = Source("City crowd conversation bed", "WS_CityCrowd", true);
                cityCrowd.priority = 180;
                cityCrowd.Play();
                cityPhrases = Resources.LoadAll<AudioClip>("WorldShiftCityVoices");
                System.Array.Sort(cityPhrases, (a,b) => string.CompareOrdinal(a.name,b.name));
                for (int i=0;i<cityVoices.Length;i++)
                {
                    cityVoices[i] = Source(i==0 ? "Left footpath conversation" : "Right footpath conversation", null, false);
                    cityVoices[i].panStereo = i==0 ? -.65f : .65f;
                    cityVoices[i].priority = 155;
                }
            }
            if (game.Environment == ShiftLevel.Mountain && !game.Course.Training) BindMountainAmbience();
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
                foreach (var source in new[] { engine, tyres, cues, music, ocean, gull, cityCrowd, cityVoices[0], cityVoices[1], wind, wildlife })
                    if (source != null) { if (pause) source.Pause(); else source.UnPause(); }
                suspended = pause;
                previousLateral = game.Lateral;
            }
            if (pause) return;
            bool running = game.State == ShiftRunState.Running;
            bool airborne = game.IsRelayAirborne;
            float effects = WorldShiftAudioSettings.Effects;
            RefreshCityCrowd(delta, running, effects);
            cues.volume = .65f * effects;
            if (previousState == ShiftRunState.Running && game.State == ShiftRunState.Retrying && game.Capturing)
                cues.PlayOneShot(crash);
            if (game.State == ShiftRunState.Won && !winCuePlayed && (game.Course.Training || game.Escape == null || game.Escape.Active && game.Escape.Ready && (!game.Escape.IsFinale || game.Escape.FinaleRevealed)))
            { cues.PlayOneShot(victory, .8f); winCuePlayed = true; }
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
                ocean.volume = effects * (running ? .35f : game.Escape != null && game.Escape.Active ? .28f : .12f);
                gull.volume = effects * .30f;
                if (running && game.World == ShiftWorld.Day)
                {
                    gullIn -= delta;
                    if (gullIn <= 0f) { gull.Play(); gullIn = 17f; }
                }
            }
            if (wind != null) RefreshMountainAmbience(delta, running, effects);
            previousState = game.State;
            previousAirborne = airborne;
            previousLateral = game.Lateral;
        }

        private void RefreshCityCrowd(float delta, bool running, float effects)
        {
            if (cityCrowd == null) return;
            float left = cityScenery == null ? 0f : cityScenery.CityCrowdPresence(game.Distance,-1f);
            float right = cityScenery == null ? 0f : cityScenery.CityCrowdPresence(game.Distance,1f);
            float presence = Mathf.Max(left,right);
            cityCrowdGain = Mathf.MoveTowards(cityCrowdGain,running ? .44f*presence : 0f,delta*.7f);
            cityCrowd.volume = effects*cityCrowdGain;
            for (int i=0;i<cityVoices.Length;i++)
            {
                var source=cityVoices[i];
                if (!running)
                {
                    source.Stop(); cityVoiceIn[i]=.5f+i*1.3f;
                    continue;
                }
                float nearby=i==0 ? left : right;
                source.volume=effects*.30f*nearby;
                cityVoiceIn[i]-=delta;
                if(cityVoiceIn[i]>0f || source.isPlaying || cityPhrases.Length==0 || nearby<.15f) continue;
                int index=cityVoiceIndex[i]%cityPhrases.Length;
                source.clip=cityPhrases[index];
                source.pitch=.94f+(index%4)*.045f;
                source.Play();
                cityVoiceIndex[i]+=7;
                cityVoiceIn[i]=2.6f+(index%5)*.37f;
            }
        }
    }
}
