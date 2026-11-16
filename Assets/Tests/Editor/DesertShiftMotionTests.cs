using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class DesertShiftMotionTests
{
    [Test]
    public void FinaleRocksHaveOneOwnerAndNeverCrossTheSeam()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Outlaws.WorldShift.DesertShiftLevel")).First(t => t != null);
        var world = type.Assembly.GetType("Outlaws.WorldShift.ShiftWorld");
        var day = Enum.Parse(world, "Day"); var night = Enum.Parse(world, "Echo");
        var rocks = (Array)type.GetMethod("CreateRocks").Invoke(null, null);
        var rows = new System.Collections.Generic.HashSet<float>();
        int count = 0;
        foreach (object rock in rocks)
        {
            var r = rock.GetType();
            float distance = (float)r.GetField("Distance").GetValue(rock);
            if (distance < 2400f) continue;
            count++;
            Assert.That(rows.Add(distance), Is.True, "No pair of world copies shares a transverse row.");
            bool dayVisible = (bool)r.GetMethod("VisibleIn").Invoke(rock, new[] { day });
            bool nightVisible = (bool)r.GetMethod("VisibleIn").Invoke(rock, new[] { night });
            Assert.That(dayVisible ^ nightVisible, Is.True);
            float radius = (float)r.GetField("Radius").GetValue(rock);
            for (int i = 0; i < 80; i++)
            {
                r.GetMethod("Restore").Invoke(rock, new object[] { i * .2f });
                r.GetMethod("CaptureNight").Invoke(rock, null);
                float x = (float)r.GetMethod("LateralIn").Invoke(rock, new[] { dayVisible ? day : night, (object)true });
                Assert.That(Math.Abs(x) - radius, Is.GreaterThan(.3f), "The entire visible body must clear the seam.");
                Assert.That(Math.Abs(x) + radius, Is.LessThan(4.7f));
                Assert.That(Math.Sign(x), Is.EqualTo(dayVisible ? -1 : 1));
            }
        }
        Assert.That(count, Is.EqualTo(10));
    }
}
