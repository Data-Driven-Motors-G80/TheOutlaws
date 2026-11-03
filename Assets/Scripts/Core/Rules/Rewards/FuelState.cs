/// <summary>Small scene-independent fuel model used by the local pickup prototype.</summary>
public sealed class FuelState
{
    public const float MaximumFuel = 100f;
    public const float DefaultConsumptionPerSecond = 4f;

    public float Capacity { get; private set; } = MaximumFuel;
    public void SetCapacity(float capacity)
    {
        Capacity = IsFinite(capacity) && capacity > 0f ? capacity : MaximumFuel;
        CurrentFuel = ClampFinite(CurrentFuel, 0f, Capacity);
    }

    public float CurrentFuel { get; private set; }
    public bool IsEmpty => CurrentFuel <= 0f;

    public FuelState(float initialFuel = MaximumFuel)
    {
        Reset(initialFuel);
    }

    public void Reset(float initialFuel = MaximumFuel)
    {
        CurrentFuel = ClampFinite(initialFuel, 0f, Capacity);
    }

    public void Tick(float elapsedSeconds, float consumptionPerSecond = DefaultConsumptionPerSecond)
    {
        if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0f
            || !IsFinite(consumptionPerSecond) || consumptionPerSecond < 0f)
            return;

        CurrentFuel = ClampFinite(CurrentFuel - elapsedSeconds * consumptionPerSecond, 0f, Capacity);
    }

    public void Refill(float amount)
    {
        if (!IsFinite(amount) || amount <= 0f) return;
        CurrentFuel = ClampFinite(CurrentFuel + amount, 0f, Capacity);
    }

    public void Consume(float amount)
    {
        if (!IsFinite(amount) || amount <= 0f) return;
        CurrentFuel = ClampFinite(CurrentFuel - amount, 0f, Capacity);
    }

    public void RefillToFull()
    {
        CurrentFuel = Capacity;
    }

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
