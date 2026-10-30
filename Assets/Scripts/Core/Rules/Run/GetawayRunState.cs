using System;

/// <summary>One escape across any number of environment legs. No checkpoint can undo settlement.</summary>
public sealed class GetawayRunState
{
    public enum RunStatus { Inactive, Running, Banked, Lost }
    public RunStatus Status { get; private set; }
    public int Leg { get; private set; }
    public int Carried { get; private set; }
    public int SettledHaul { get; private set; }
    public string Failure { get; private set; } = "";
    public bool HasShield { get; private set; }
    public float LootMultiplier => 1f + Math.Min(Leg, 40) * .5f;
    public float ExtraPursuitSpeed => Math.Min(Leg, 6) * .025f;

    public void Begin()
    { Status = RunStatus.Running; Leg = Carried = SettledHaul = 0; Failure = ""; HasShield = false; }

    public bool CollectShield()
    {
        if (Status != RunStatus.Running || HasShield) return false;
        HasShield = true;
        return true;
    }

    public bool AbsorbHit()
    {
        if (Status != RunStatus.Running || !HasShield) return false;
        HasShield = false;
        return true;
    }

    public int Collect(int baseValue)
    {
        if (Status != RunStatus.Running || baseValue <= 0) return 0;
        int amount = (int)Math.Min(int.MaxValue - (long)Carried, Math.Round(baseValue * LootMultiplier));
        Carried += amount;
        return amount;
    }

    public bool Continue()
    {
        if (Status != RunStatus.Running) return false;
        if (Leg < int.MaxValue) Leg++;
        return true;
    }

    // Returning the deposit once prevents duplicate UI/input events from minting money.
    public int Bank()
    {
        if (Status != RunStatus.Running) return 0;
        SettledHaul = Carried; Carried = 0; HasShield = false; Status = RunStatus.Banked;
        return SettledHaul;
    }

    public void Lose(string reason)
    {
        if (Status != RunStatus.Running) return;
        Failure = reason; SettledHaul = Carried; Carried = 0; HasShield = false; Status = RunStatus.Lost;
    }

    public void Leave() { Lose("RUN ABANDONED"); }
}
