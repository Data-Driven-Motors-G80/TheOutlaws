using UnityEngine;
using UnityEngine.UI;

public sealed class FuelMeter : MonoBehaviour
{
    [SerializeField, Min(0)] private float maxFuel = 100f;
    [SerializeField, Min(0)] private float consumptionPerSecond = 4f;

    [SerializeField] private Image bar;

    private float remainingFuel;
    private HandleCrash crash;

    public float CurrentFuel => remainingFuel;
    public float MaximumFuel => maxFuel;
    public float NormalizedFuel => maxFuel > 0f ? remainingFuel / maxFuel : 0f;
    public bool IsEmpty => remainingFuel <= 0f;

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
