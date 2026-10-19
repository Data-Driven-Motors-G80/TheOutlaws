using UnityEngine;

[DisallowMultipleComponent]
public sealed class SunnyDayPickup : MonoBehaviour
{
    [Tooltip("Chance that a ? box turns out to be Sunny Day. The other outcomes share the rest.")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.25f;
    [Tooltip("Seconds all fog is removed after collecting Sunny Day.")]
    [SerializeField, Min(1f)] private float clearSeconds = 15f;

    private CarPickupEffects effects;

    private void OnEnable()
    {
        effects = FindFirstObjectByType<CarPickupEffects>();
        Apply();
    }

    private void OnDisable()
    {
        if (effects != null) effects.ConfigureSunnyDay(0f, 0f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    private void Apply()
    {
        if (effects != null) effects.ConfigureSunnyDay(chance, clearSeconds);
    }
}