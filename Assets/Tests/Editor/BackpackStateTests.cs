using NUnit.Framework;
public sealed class BackpackStateTests
{
    [Test] public void ThreeShotsThenEmpty()
    {
        var p = new BackpackState(); Assert.That(p.Item, Is.EqualTo(RandomPickupOutcome.None));
        p.Receive(RandomPickupOutcome.Gun);
        for (int i = 3; i > 0; i--) { Assert.That(p.Shots, Is.EqualTo(i)); Assert.That(p.Use(), Is.EqualTo(RandomPickupOutcome.Gun)); }
        Assert.That(p.Item, Is.EqualTo(RandomPickupOutcome.None)); Assert.That(p.Use(), Is.EqualTo(RandomPickupOutcome.None));
    }
    [Test] public void ReplacementDiscardsBulletsAndDoesNotActivate()
    {
        var p = new BackpackState(); int used = 0, lost = 0;
        p.Used += _ => used++; p.Replaced += (a,b,c) => lost = c;
        p.Receive(RandomPickupOutcome.Gun); p.Use(); p.Receive(RandomPickupOutcome.Shield);
        Assert.That(lost, Is.EqualTo(2)); Assert.That(used, Is.EqualTo(1)); Assert.That(p.Shots, Is.Zero);
        Assert.That(p.Use(), Is.EqualTo(RandomPickupOutcome.Shield)); Assert.That(p.Item, Is.EqualTo(RandomPickupOutcome.None));
    }
    [Test] public void ReverseDoesNotReplaceAndGunDoesNotStack()
    {
        var p = new BackpackState(); p.Receive(RandomPickupOutcome.Gun); p.Use();
        Assert.That(p.Receive(RandomPickupOutcome.ReverseSteering), Is.False); Assert.That(p.Shots, Is.EqualTo(2));
        p.Receive(RandomPickupOutcome.Gun); Assert.That(p.Shots, Is.EqualTo(3));
    }
    [Test] public void RestorePreservesPartialGunAndClearResetsIt()
    {
        var p = new BackpackState(); p.Restore(RandomPickupOutcome.Gun, 1);
        Assert.That(p.Shots, Is.EqualTo(1)); p.Clear(); Assert.That(p.Item, Is.EqualTo(RandomPickupOutcome.None));
    }
}
