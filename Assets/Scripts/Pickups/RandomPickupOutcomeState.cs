using System;

public enum RandomPickupOutcome
{
    None = 0,
    ReverseSteering = 1,
    ProximityRecovery = 2,
    Shield = 3
}

/// <summary>Deterministic 50/25/25 outcome source; a seed makes editor tests reproducible.</summary>
public sealed class RandomPickupOutcomeState
{
    private readonly Random random;

    public RandomPickupOutcomeState(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public RandomPickupOutcome Next()
    {
        int roll = random.Next(4);
        if (roll < 2) return RandomPickupOutcome.ReverseSteering;
        return roll == 2 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}
