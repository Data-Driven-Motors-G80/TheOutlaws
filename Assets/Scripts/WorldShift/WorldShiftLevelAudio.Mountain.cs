using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>
    /// Mountain ambience: a gusting wind loop, a distant eagle cry now and then, and a bleat when a goat crossing is
    /// just ahead. Original synthesized clips from Tools/Audio/generate_mountain_audio.py; volumes follow the Effects slider.
    /// </summary>
    public sealed partial class WorldShiftLevelAudio
    {
        private AudioSource wind, wildlife;
        private AudioClip eagleCry, goatBleat;
        private float eagleIn = 11f;
        private ShiftHazard lastBleat;

        private void BindMountainAmbience()
        {
            wind = Source("Mountain wind", "WS_MountainWind", true);
            wildlife = Source("Mountain wildlife", null, false);
            eagleCry = Load("WS_EagleCry");
            goatBleat = Load("WS_GoatBleat");
            wind.Play();
        }

        private void RefreshMountainAmbience(float delta, bool running, float effects)
        {
            // Wind is a little stronger at Night and while flying off a jump.
            float target = running ? (game.IsRelayAirborne ? .34f : game.World == ShiftWorld.Echo ? .24f : .18f) : .08f;
            wind.volume = Mathf.MoveTowards(wind.volume, effects * target, delta * .5f);
            wildlife.volume = effects;
            if (!running) return;
            // Taking off on a jump with an eagle over it: a loud, close cry says "look up".
            if (game.IsRelayAirborne && !previousAirborne)
                foreach (var hazard in game.Hazards)
                    if (hazard.Rock != null && hazard.Rock.Airborne && hazard.Start > game.Distance && hazard.Start < game.Distance + 90f)
                    { wildlife.PlayOneShot(eagleCry, .7f); eagleIn = 12f; break; }
            eagleIn -= delta;
            if (eagleIn <= 0f)
            {
                wildlife.PlayOneShot(eagleCry, .22f);
                eagleIn = 15f + Mathf.Repeat(game.Distance * .37f, 9f);
            }
            // One bleat per goat, when a crossing comes within about 1.5 s of the car in Day (goats only walk in Day).
            ShiftHazard goat = null;
            foreach (var hazard in game.Hazards)
                if (hazard.Rock != null && !hazard.Rock.Airborne && hazard.Start > game.Distance + 8f && hazard.Start < game.Distance + 40f)
                { goat = hazard; break; }
            if (goat != null && goat != lastBleat && game.World == ShiftWorld.Day)
            {
                wildlife.PlayOneShot(goatBleat, .5f);
                lastBleat = goat;
            }
            // A checkpoint retry moves the car back: the same goat may bleat again on the next pass.
            if (lastBleat != null && lastBleat.Start > game.Distance + 60f) lastBleat = null;
        }
    }
}
