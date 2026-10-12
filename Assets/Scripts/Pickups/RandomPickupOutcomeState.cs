using System;

public enum RandomPickupOutcome
{
    None = 0,
    ReverseSteering = 1,
    ProximityRecovery = 2,
    Shield = 3,
    Nitro = 4
}

/// <summary>Reverse steering has a 10% chance; each helpful effect has 30%. Seeds support reproducible tests.</summary>
public sealed class RandomPickupOutcomeState
{
    private readonly Random random;

    public RandomPickupOutcomeState(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public RandomPickupOutcome Next()
    {
        int roll = random.Next(10);
        if (roll == 0) return RandomPickupOutcome.ReverseSteering;
        if (roll < 4) return RandomPickupOutcome.Nitro;
        return roll < 7 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}
