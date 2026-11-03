using UnityEngine;

/// <summary>Permanent local upgrades; shares the existing bank without touching best-haul records.</summary>
public sealed class VehicleUpgrades
{
    public enum Part { FuelTank, Armor, Headlights }
    public const int MaxLevel = 3;
    private readonly string prefix;
    public VehicleUpgrades(string savePrefix = "WorldShift.Getaway.") { prefix = savePrefix; }
    private string Key(Part part) => prefix + "Upgrade." + part;
    public int Banked => Mathf.Max(0, PlayerPrefs.GetInt(prefix + "Bank", 0));
    public int Level(Part part) => Mathf.Clamp(PlayerPrefs.GetInt(Key(part), 0), 0, MaxLevel);
    public int Price(Part part) => Level(part) >= MaxLevel ? 0 : 500 << Level(part);
    public float FuelCapacity => 100f + 10f * Level(Part.FuelTank);
    public float CollisionMultiplier => 1f - .05f * Level(Part.Armor);
    public float HeadlightRange => 74f + 15f * Level(Part.Headlights);

    public bool Buy(Part part)
    {
        if (part < Part.FuelTank || part > Part.Headlights || Level(part) >= MaxLevel) return false;
        int price = Price(part);
        if (Banked < price) return false;
        PlayerPrefs.SetInt(prefix + "Bank", Banked - price);
        PlayerPrefs.SetInt(Key(part), Level(part) + 1);
        PlayerPrefs.Save();
        return true;
    }
}
