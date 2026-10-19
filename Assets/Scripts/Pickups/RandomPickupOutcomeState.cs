using System;

public enum RandomPickupOutcome
{
    None = 0,
    ReverseSteering = 1,
    ProximityRecovery = 2,
    Shield = 3,
    Nitro = 4,
    SunnyDay = 5
}

/// <summary>Reverse steering has a 10% chance; each helpful effect has 30%. Seeds support reproducible tests.</summary>
public sealed class RandomPickupOutcomeState
{
    private readonly Random random;

    public float SunnyDayChance { get; set; }

    public RandomPickupOutcomeState(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public RandomPickupOutcome Next()
    {
        if (SunnyDayChance > 0f && random.NextDouble() < SunnyDayChance) return RandomPickupOutcome.SunnyDay;
        int roll = random.Next(10);
        if (roll == 0) return RandomPickupOutcome.ReverseSteering;
        if (roll < 4) return RandomPickupOutcome.Nitro;
        return roll < 7 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}