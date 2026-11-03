using NUnit.Framework;
using UnityEngine;

public sealed class VehicleUpgradesTests
{
    private string prefix;
    private VehicleUpgrades garage;
    [SetUp] public void Setup()
    {
        prefix = "GarageTests." + System.Guid.NewGuid().ToString("N") + ".";
        garage = new VehicleUpgrades(prefix);
    }
    [TearDown] public void Cleanup()
    {
        PlayerPrefs.DeleteKey(prefix + "Bank");
        foreach (VehicleUpgrades.Part part in System.Enum.GetValues(typeof(VehicleUpgrades.Part)))
            PlayerPrefs.DeleteKey(prefix + "Upgrade." + part);
        PlayerPrefs.Save();
    }
    [Test] public void PurchasesRequireSavingsAndStopAtMaximum()
    {
        Assert.That(garage.Buy(VehicleUpgrades.Part.FuelTank), Is.False);
        Assert.That(garage.FuelCapacity, Is.EqualTo(100f));
        PlayerPrefs.SetInt(prefix + "Bank", 4000);
        foreach (int price in new[] { 500, 1000, 2000 })
        {
            int before = garage.Banked;
            Assert.That(garage.Price(VehicleUpgrades.Part.FuelTank), Is.EqualTo(price));
            Assert.That(garage.Buy(VehicleUpgrades.Part.FuelTank), Is.True);
            Assert.That(garage.Banked, Is.EqualTo(before - price));
        }
        Assert.That(garage.Buy(VehicleUpgrades.Part.FuelTank), Is.False);
        Assert.That(garage.Banked, Is.EqualTo(500));
        Assert.That(new VehicleUpgrades(prefix).FuelCapacity, Is.EqualTo(130f));
        Assert.That(garage.Buy((VehicleUpgrades.Part)99), Is.False);
    }
    [Test] public void BenefitsAreBoundedAndPurchasesSurviveLostRuns()
    {
        PlayerPrefs.SetInt(prefix + "Bank", 20000);
        foreach (VehicleUpgrades.Part part in System.Enum.GetValues(typeof(VehicleUpgrades.Part)))
            for (int i = 0; i < 3; i++) Assert.That(garage.Buy(part), Is.True);
        var run = new GetawayRunState(); run.Begin(); run.Collect(500); run.Lose("CAUGHT");
        var loaded = new VehicleUpgrades(prefix);
        Assert.That(loaded.CollisionMultiplier, Is.EqualTo(.85f).Within(.0001f));
        Assert.That(loaded.HeadlightRange, Is.EqualTo(119f));
        Assert.That(loaded.Banked, Is.EqualTo(9500));
    }
    [Test] public void LargerTankDoesNotIncreaseRefillOrReduceDrain()
    {
        var fuel = new FuelState();
        fuel.SetCapacity(130f); fuel.RefillToFull();
        fuel.Tick(20f, 2f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(90f));
        fuel.Refill(26f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(116f));
        fuel.Refill(26f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(130f));
        fuel.SetCapacity(100f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f));
    }
}
