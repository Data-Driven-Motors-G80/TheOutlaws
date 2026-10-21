using System;
using UnityEngine;

/// <summary>Environment reward choices. Runtime state belongs to the player, never this asset.</summary>
[CreateAssetMenu(menuName = "The Outlaws/Reward Table")]
public sealed class RewardTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public RandomPickupOutcome outcome;
        [Min(0)] public int weight;

        public Entry(RandomPickupOutcome outcome, int weight)
        {
            this.outcome = outcome;
            this.weight = weight;
        }
    }

    [SerializeField] private Entry[] rewards =
    {
        new Entry(RandomPickupOutcome.ReverseSteering, 1),
        new Entry(RandomPickupOutcome.Nitro, 3),
        new Entry(RandomPickupOutcome.ProximityRecovery, 3),
        new Entry(RandomPickupOutcome.Shield, 3)
    };

    // Preserve snow's existing two-stage roll: weather first, then the normal rewards.
    [SerializeField, Range(0f, 1f)] private float sunnyDayChance;
    [SerializeField, Min(1f)] private float sunnyDaySeconds = 15f;

    public float SunnyDayChance => sunnyDayChance;
    public float SunnyDaySeconds => sunnyDaySeconds;

    public RandomPickupOutcome Roll(System.Random random)
    {
        if (sunnyDayChance > 0f && random.NextDouble() < sunnyDayChance)
            return RandomPickupOutcome.SunnyDay;

        int total = 0;
        foreach (Entry entry in rewards)
            if (entry.outcome != RandomPickupOutcome.None && entry.weight > 0)
                total = checked(total + entry.weight);
        if (total == 0) return RandomPickupOutcome.None;

        int roll = random.Next(total);
        foreach (Entry entry in rewards)
        {
            if (entry.outcome == RandomPickupOutcome.None || entry.weight <= 0) continue;
            if (roll < entry.weight) return entry.outcome;
            roll -= entry.weight;
        }
        return RandomPickupOutcome.None;
    }
}
