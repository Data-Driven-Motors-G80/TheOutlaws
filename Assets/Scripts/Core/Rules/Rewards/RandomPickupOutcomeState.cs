// Selects mystery rewards using the configured table or fallback weights.
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

public sealed class RandomPickupOutcomeState
{
    private readonly Random random;

    public float SunnyDayChance { get; set; }
    public RewardTable Table { get; set; }
    public bool HarderDifficulty { get; set; }

    public RandomPickupOutcomeState(int? seed = null)
    {
        random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // Prefer the configured reward table; use fallback weights when absent.
    public RandomPickupOutcome Next()
    {
        if (Table != null) return Table.Roll(random, HarderDifficulty);
        if (SunnyDayChance > 0f && random.NextDouble() < SunnyDayChance) return RandomPickupOutcome.SunnyDay;
        if (HarderDifficulty)
        {
            int hardRoll = random.Next(9);
            if (hardRoll < 2) return RandomPickupOutcome.ReverseSteering;
            if (hardRoll < 5) return RandomPickupOutcome.Gun;
            return hardRoll < 7 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
        }
        int roll = random.Next(8);
        if (roll == 0) return RandomPickupOutcome.ReverseSteering;
        if (roll < 4) return RandomPickupOutcome.Gun;
        return roll < 6 ? RandomPickupOutcome.ProximityRecovery : RandomPickupOutcome.Shield;
    }
}