using UnityEngine;
using UnityEngine.UI;

public enum FuelDrainMode
{
    Normal,
    /// <summary>No drain (tutorial steps that are not about fuel).</summary>
    Frozen,
    /// <summary>Drops fast but eases towards a floor, so it never actually empties.</summary>
    TutorialPlunge,
    /// <summary>Normal drain rate, but stops at a floor above the low-fuel warning.</summary>
    TutorialSteady
}

public sealed class FuelMeter : MonoBehaviour
{
    public const float TutorialFloorFraction = 0.08f;
    public const float TutorialSteadyFloorFraction = 0.35f;
    private const float TutorialPlungeRate = 1.3f;

    [SerializeField, Min(0)] private float maxFuel = 100f;
    [SerializeField, Min(0)] private float consumptionPerSecond = 4f;

    [SerializeField] private Image bar;

    private float remainingFuel;
    private HandleCrash crash;

    public float CurrentFuel => remainingFuel;
    public float MaximumFuel => maxFuel;
    public float NormalizedFuel => maxFuel > 0f ? remainingFuel / maxFuel : 0f;
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
            // Exponential approach to the floor: a dramatic drop that can never reach zero.
            float floor = maxFuel * TutorialFloorFraction;
            if (remainingFuel > floor)
                remainingFuel = Mathf.Max(floor, remainingFuel - (remainingFuel - floor) * TutorialPlungeRate * Time.deltaTime);
            if (bar != null) bar.fillAmount = NormalizedFuel;
            return;
        }

        if (DrainMode == FuelDrainMode.TutorialSteady)
        {
            float floor = maxFuel * TutorialSteadyFloorFraction;
            if (remainingFuel > floor)
                remainingFuel = Mathf.Max(floor, remainingFuel - consumptionPerSecond * Time.deltaTime);
            if (bar != null) bar.fillAmount = NormalizedFuel;
            return;
        }

        if (DrainMode == FuelDrainMode.Frozen)
        {
            if (bar != null) bar.fillAmount = NormalizedFuel;
            return;
        }

        remainingFuel = Mathf.Max(0, remainingFuel - consumptionPerSecond * Time.deltaTime);
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

    public void ChangeFuel(float amount)
    {
        remainingFuel = Mathf.Clamp(remainingFuel + amount, 0f, maxFuel);
        if (bar != null)
        {
            bar.fillAmount = NormalizedFuel;
        }
    }
}
