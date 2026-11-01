// Inspector-configurable reward weights with an optional harder profile.
using System;
using UnityEngine;

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
        new Entry(RandomPickupOutcome.Gun, 3),
        new Entry(RandomPickupOutcome.ProximityRecovery, 2),
        new Entry(RandomPickupOutcome.Shield, 2)
    };

    [SerializeField, Range(0f, 1f)] private float sunnyDayChance;
    [SerializeField, Min(1f)] private float sunnyDaySeconds = 15f;

    public float SunnyDayChance => sunnyDayChance;
    public float SunnyDaySeconds => sunnyDaySeconds;

    // Treat the weights as ranges in one cumulative random selection.
    public RandomPickupOutcome Roll(System.Random random, bool harder = false)
    {
        if (sunnyDayChance > 0f && random.NextDouble() < sunnyDayChance)
            return RandomPickupOutcome.SunnyDay;

        int total = 0;
        foreach (Entry entry in rewards)
            if (entry.outcome != RandomPickupOutcome.None && entry.outcome != RandomPickupOutcome.Nitro && entry.weight > 0)
                total = checked(total + entry.weight * (harder && entry.outcome == RandomPickupOutcome.ReverseSteering ? 2 : 1));
        if (total == 0) return RandomPickupOutcome.None;

        int roll = random.Next(total);
        foreach (Entry entry in rewards)
        {
            if (entry.outcome == RandomPickupOutcome.None || entry.outcome == RandomPickupOutcome.Nitro || entry.weight <= 0) continue;
            int weight = entry.weight * (harder && entry.outcome == RandomPickupOutcome.ReverseSteering ? 2 : 1);
            if (roll < weight) return entry.outcome;
            roll -= weight;
        }
        return RandomPickupOutcome.None;
    }
}
