using System;

public enum RandomPickupOutcome
{
    None = 0,
    ReverseSteering = 1,
    ProximityRecovery = 2,
    Shield = 3,
    Nitro = 4,
    SunnyDay = 5,
    Gun = 6
}

/// <summary>City: reverse 10%, gun 30%, other helpful items 20% each. Seeds support reproducible tests.</summary>
public sealed class RandomPickupOutcomeState
{
    private readonly Random random;

    public float SunnyDayChance { get; set; }
    public RewardTable Table { get; set; }

    public RandomPickupOutcomeState(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public RandomPickupOutcome Next()
    {
        if (Table != null) return Table.Roll(random);
        if (SunnyDayChance > 0f && random.NextDouble() < SunnyDayChance) return RandomPickupOutcome.SunnyDay;
        int roll = random.Next(10);
        if (roll == 0) return RandomPickupOutcome.ReverseSteering;
        if (roll < 4) return RandomPickupOutcome.Gun;
        if (roll < 6) return RandomPickupOutcome.Nitro;
        return roll < 8 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}