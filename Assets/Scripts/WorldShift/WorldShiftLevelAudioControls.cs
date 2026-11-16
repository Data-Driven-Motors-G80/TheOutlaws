using UnityEngine;
using UnityEngine.UI;

namespace Outlaws.WorldShift
{
    public sealed partial class WorldShiftLevelHud
    {
        private void AudioControls(Transform parent)
        {
            VolumeSlider(parent, "Music", .39f, WorldShiftAudioSettings.Music, value => WorldShiftAudioSettings.Music = value);
            VolumeSlider(parent, "Effects", .31f, WorldShiftAudioSettings.Effects, value => WorldShiftAudioSettings.Effects = value);
            if (!game.Course.Training) MusicCredits(parent);
        }
        private void MusicCredits(Transform parent)
        {
            var title = WorldShiftMenu.Label(parent, WorldShiftMusicLibrary.Title(game.Environment), .18f, .445f, .82f, .485f, 20f);
            title.alignment = TMPro.TextAlignmentOptions.Center;
            var credits = WorldShiftMenu.Panel(parent, "Music credits panel", new Vector2(.12f, .1f), new Vector2(.88f, .9f), new Color(.015f, .03f, .045f, 1f));
            string text = "MUSIC\n\nCity: Funkorama\nDesert: Whiskey on the Mississippi\nOcean: Surf Shimmy\nSnow: Cold Funk\nMountain: Mountain Emperor\n\nAll songs by Kevin MacLeod (incompetech.com)\nLicensed under Creative Commons: By Attribution 4.0\nFull recordings; looping, fades and Night filtering in-game.";
            var label = WorldShiftMenu.Label(credits, text, .05f, .22f, .95f, .95f, 25f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            WorldShiftMenu.Button(credits, "Song source", new Vector2(.04f, .06f), new Vector2(.33f, .16f), () => Application.OpenURL(WorldShiftMusicLibrary.SourceUrl(game.Environment)));
            WorldShiftMenu.Button(credits, "CC BY 4.0", new Vector2(.36f, .06f), new Vector2(.65f, .16f), () => Application.OpenURL(WorldShiftMusicLibrary.LicenseUrl));
            WorldShiftMenu.Button(credits, "Back", new Vector2(.68f, .06f), new Vector2(.96f, .16f), () => credits.gameObject.SetActive(false));
            credits.gameObject.SetActive(false);
            WorldShiftMenu.Button(parent, "Music credits", new Vector2(.40f, .035f), new Vector2(.60f, .085f), () => { credits.gameObject.SetActive(true); credits.SetAsLastSibling(); });
        }
        private void VolumeSlider(Transform parent, string label, float y, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            var row = WorldShiftMenu.Panel(parent, label + " volume", new Vector2(.5f, y), new Vector2(.5f, y), Color.clear);
            row.sizeDelta = new Vector2(520f, 46f);
            WorldShiftMenu.Label(row, label, 0f, 0f, .27f, 1f, 25f);
            var track = WorldShiftMenu.Panel(row, "Slider", new Vector2(.31f, .1f), new Vector2(1f, .9f), new Color(.18f, .25f, .3f));
            var slider = track.gameObject.AddComponent<Slider>();
            var fillArea = WorldShiftMenu.Panel(track, "Fill area", Vector2.zero, Vector2.one, Color.clear);
            fillArea.offsetMin = new Vector2(10f, 8f); fillArea.offsetMax = new Vector2(-10f, -8f);
            var fill = WorldShiftMenu.Panel(fillArea, "Fill", Vector2.zero, Vector2.one, Gold);
            var handleArea = WorldShiftMenu.Panel(track, "Handle area", Vector2.zero, Vector2.one, Color.clear);
            handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = WorldShiftMenu.Panel(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), Cream);
            handle.sizeDelta = new Vector2(20f, 0f);
            slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value); slider.onValueChanged.AddListener(changed);
            // Left/right adjusts the selected slider; Up/down travels between controls.
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }
    }
}
