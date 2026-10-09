using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// LedStrip's idle battery display, as the drive board firmware draws it.
/// </summary>
public class LedStripTests
{
    [TestCase(1.0f, 42)]
    [TestCase(0.5f, 21)]
    [TestCase(0.25f, 10)]
    [TestCase(0.0f, 0)]
    public void BatteryFrame_LightsSocTimes42PerEnd(float charge, int perEnd)
    {
        Color32[] frame = LedStrip.BatteryFrame(charge);
        Color32 off = new Color32(0, 0, 0, 0);
        Assert.AreEqual(ActuatorCommands.LedCount, frame.Length);
        for (int i = 0; i < ActuatorCommands.LedCount; i++)
        {
            bool lit = i < perEnd || i >= ActuatorCommands.LedCount - perEnd;
            Assert.AreEqual(lit, !frame[i].Equals(off), $"LED {i}");
        }
        Assert.IsTrue(frame.Where(c => !c.Equals(off)).All(c => c.r == 255 && c.g == 0 && c.b == 0), "red");
    }

    [Test]
    public void BatteryFrame_ShrinksUnderLoad()
    {
        // The firmware reads charge from the loaded bus voltage, so sag shortens the bar
        int Lit(float volts) => LedStrip.BatteryFrame(BatteryModel.ChargeFromVoltage(volts)).Count(c => c.r > 0);
        Assert.Greater(Lit(7.80f), Lit(7.65f));
    }
}
