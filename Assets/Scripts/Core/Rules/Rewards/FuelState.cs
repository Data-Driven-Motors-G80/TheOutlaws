// Fuel arithmetic shared by gameplay systems; values stay within tank limits.

public sealed class FuelState
{
    public const float MaximumFuel = 100f;
    public const float DefaultConsumptionPerSecond = 4f;

    public float CurrentFuel { get; private set; }
    public bool IsEmpty => CurrentFuel <= 0f;

    public FuelState(float initialFuel = MaximumFuel)
    {
        Reset(initialFuel);
    }

    public void Reset(float initialFuel = MaximumFuel)
    {
        CurrentFuel = ClampFinite(initialFuel, 0f, MaximumFuel);
    }

    // Drain only for valid elapsed time and a valid consumption rate.
    public void Tick(float elapsedSeconds, float consumptionPerSecond = DefaultConsumptionPerSecond)
    {
        if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0f
            || !IsFinite(consumptionPerSecond) || consumptionPerSecond < 0f)
            return;

        CurrentFuel = ClampFinite(CurrentFuel - elapsedSeconds * consumptionPerSecond, 0f, MaximumFuel);
    }

    public void Refill(float amount)
    {
        if (!IsFinite(amount) || amount <= 0f) return;
        CurrentFuel = ClampFinite(CurrentFuel + amount, 0f, MaximumFuel);
    }

    public void Consume(float amount)
    {
        if (!IsFinite(amount) || amount <= 0f) return;
        CurrentFuel = ClampFinite(CurrentFuel - amount, 0f, MaximumFuel);
    }

    public void RefillToFull()
    {
        CurrentFuel = MaximumFuel;
    }

    // Reject invalid numbers before they can reach the fuel display.
    private static float ClampFinite(float value, float min, float max)
    {
        if (!IsFinite(value)) return min;
        return value < min ? min : value > max ? max : value;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
