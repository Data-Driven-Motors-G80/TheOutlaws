using System;

public enum RandomPickupOutcome
{
    None = 0,
    ReverseSteering = 1,
    ProximityRecovery = 2,
    Shield = 3,
    Nitro = 4
}

/// <summary>Four equally likely outcomes; a seed makes editor tests reproducible.</summary>
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
        if (roll == 0) return RandomPickupOutcome.ReverseSteering;
        if (roll == 1) return RandomPickupOutcome.Nitro;
        return roll == 2 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}
