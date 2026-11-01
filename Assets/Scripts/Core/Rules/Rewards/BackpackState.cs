// Stores one power-up and tracks remaining shots for the gun.
using System;

public sealed class BackpackState
{
    public const int GunShots = 3;
    public RandomPickupOutcome Item { get; private set; }
    public int Shots { get; private set; }
    public event Action<RandomPickupOutcome, RandomPickupOutcome, int> Replaced;
    public event Action<RandomPickupOutcome> Used;
    // Replacing an item discards any remaining shots from the previous gun.
    public bool Receive(RandomPickupOutcome item)
    {
        if (item == RandomPickupOutcome.None || item == RandomPickupOutcome.ReverseSteering) return false;
        var old = Item; int lost = Shots;
        Item = item; Shots = item == RandomPickupOutcome.Gun ? GunShots : 0;
        Replaced?.Invoke(old, item, lost);
        return true;
    }
    public RandomPickupOutcome Use()
    {
        var used = Item;
        if (used == RandomPickupOutcome.None) return used;
        if (used != RandomPickupOutcome.Gun || --Shots <= 0) { Item = RandomPickupOutcome.None; Shots = 0; }
        Used?.Invoke(used);
        return used;
    }
    // Sanitize saved ammo counts before restoring the stored item.
    public void Restore(RandomPickupOutcome item, int shots)
    {
        Item = item; Shots = item == RandomPickupOutcome.Gun ? Math.Max(0, Math.Min(GunShots, shots)) : 0;
        if (item == RandomPickupOutcome.ReverseSteering || (item == RandomPickupOutcome.Gun && Shots == 0)) Clear();
    }
    public void Clear() { Item = RandomPickupOutcome.None; Shots = 0; }
}
