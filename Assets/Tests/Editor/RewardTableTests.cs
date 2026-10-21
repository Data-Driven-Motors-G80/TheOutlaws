using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RewardTableTests
{
    [TestCase("City", 0f)]
    [TestCase("Snow", 0.25f)]
    public void EnvironmentTable_PreservesLegacySequence(string environment, float weatherChance)
    {
        RewardTable table = AssetDatabase.LoadAssetAtPath<RewardTable>(
            $"Assets/Settings/Rewards/{environment}Rewards.asset");
        Assert.That(table, Is.Not.Null);
        Assert.That(table.SunnyDayChance, Is.EqualTo(weatherChance));
        Assert.That(table.SunnyDaySeconds, Is.EqualTo(15f));
        var legacy = new RandomPickupOutcomeState(1937) { SunnyDayChance = weatherChance };
        var configured = new RandomPickupOutcomeState(1937) { Table = table };
        for (int i = 0; i < 10000; i++)
            Assert.That(configured.Next(), Is.EqualTo(legacy.Next()), $"Selection {i}");
    }

    [Test]
    public void Table_CanRestrictRewardsWithoutChangingTheSpawner()
    {
        RewardTable table = ScriptableObject.CreateInstance<RewardTable>();
        try
        {
            var serialized = new SerializedObject(table);
            SerializedProperty entries = serialized.FindProperty("rewards");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("outcome").intValue = (int)RandomPickupOutcome.Shield;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("weight").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var sampler = new RandomPickupOutcomeState(42) { Table = table };
            for (int i = 0; i < 100; i++) Assert.That(sampler.Next(), Is.EqualTo(RandomPickupOutcome.Shield));
        }
        finally { Object.DestroyImmediate(table); }
    }

    [Test]
    public void EmptyTable_DoesNotAwardAnUnconfiguredEffect()
    {
        RewardTable table = ScriptableObject.CreateInstance<RewardTable>();
        try
        {
            var serialized = new SerializedObject(table);
            serialized.FindProperty("rewards").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(table.Roll(new System.Random(42)), Is.EqualTo(RandomPickupOutcome.None));
        }
        finally { Object.DestroyImmediate(table); }
    }
}
