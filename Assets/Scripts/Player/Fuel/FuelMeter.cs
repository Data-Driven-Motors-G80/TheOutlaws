// Maintains the visible fuel supply and applies gameplay fuel changes.
using UnityEngine;
using UnityEngine.UI;

public enum FuelDrainMode
{
    Normal,

    Frozen,

    TutorialPlunge
}

public sealed class FuelMeter : MonoBehaviour
{
    public const float TutorialFloorFraction = 0.08f;
    private const float TutorialPlungeRate = 0.55f;

    [SerializeField, Min(0)] private float maxFuel = 100f;
    [SerializeField, Min(0)] private float consumptionPerSecond = 4f;

    [SerializeField] private Image bar;

    private float remainingFuel;
    private HandleCrash crash;

    public float CurrentFuel => remainingFuel;
    public float MaximumFuel => maxFuel;
    public float NormalizedFuel => maxFuel > 0f ? remainingFuel / maxFuel : 0f;
    public bool HarderDifficultyActive { get; set; }
    public bool IsEmpty => remainingFuel <= 0f;
    public FuelDrainMode DrainMode { get; set; } = FuelDrainMode.Normal;

    private void Awake()
    {
        remainingFuel = maxFuel;
        crash = GetComponent<HandleCrash>();
        if (bar != null)
        {
            bar.fillAmount = NormalizedFuel;
        }

    }

    private void Start()
    {
        if (RunSession.TryGetPending(out RunSnapshot carried)) SetFuel(carried.MeterFuel);
    }

    public void SetFuel(float amount)
    {
        remainingFuel = Mathf.Clamp(amount, 0f, maxFuel);
        if (bar != null) bar.fillAmount = NormalizedFuel;
    }

    private void Update()
    {
        if (DrainMode == FuelDrainMode.TutorialPlunge)
        {

            float floor = maxFuel * TutorialFloorFraction;
            if (remainingFuel > floor)
                remainingFuel = Mathf.Max(floor, remainingFuel - (remainingFuel - floor) * TutorialPlungeRate * Time.deltaTime);
            if (bar != null) bar.fillAmount = NormalizedFuel;
            return;
        }

        if (DrainMode == FuelDrainMode.Frozen)
        {
            if (bar != null) bar.fillAmount = NormalizedFuel;
            return;
        }

        remainingFuel = Mathf.Max(0, remainingFuel - (HarderDifficultyActive ? 3.8f : consumptionPerSecond) * Time.deltaTime);
        if (bar != null)
        {
            bar.fillAmount = NormalizedFuel;
        }

        if (remainingFuel <= 0)
        {
            enabled = false;
            crash.OutOfFuel();
        }
    }

    // Clamp refills and penalties through the same visible fuel path.
    public void ChangeFuel(float amount)
    {
        remainingFuel = Mathf.Clamp(remainingFuel + amount, 0f, maxFuel);
        if (bar != null)
        {
            bar.fillAmount = NormalizedFuel;
        }
    }
}
