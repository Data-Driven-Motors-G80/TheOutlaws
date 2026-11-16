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
