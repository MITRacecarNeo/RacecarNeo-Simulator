using NUnit.Framework;
using UnityEngine;

/// <summary>
/// HUD value formatting: lap time text, the battery bar (which steps with the LED strip), and the
/// body frame figure's axes and projection.
/// </summary>
public class HudFormatTests
{
    [TestCase(0f, "00:00.00")]
    [TestCase(61.5f, "01:01.50")]
    [TestCase(83.456f, "01:23.46")]
    public void FormatLapTime_MinutesSecondsHundredths(float seconds, string expected)
    {
        Assert.AreEqual(expected, Hud.FormatLapTime(seconds));
    }

    [Test]
    public void BatteryBar_FullAndEmptyAtStripLimits()
    {
        Assert.AreEqual(1, Hud.BatteryBarFraction(8.4f));
        Assert.AreEqual(1, Hud.BatteryBarFraction(9f));
        Assert.AreEqual(0, Hud.BatteryBarFraction(7.0f));
        Assert.AreEqual(0, Hud.BatteryBarFraction(6.5f));
    }

    [Test]
    public void BatteryBar_MatchesLitLeds()
    {
        foreach (float volts in new[] { 7.3f, 7.55f, 7.7f, 8.1f })
        {
            int lit = 0;
            foreach (Color32 color in LedStrip.BatteryFrame(BatteryModel.ChargeFromVoltage(volts)))
            {
                lit += color.r > 0 ? 1 : 0;
            }
            Assert.AreEqual(lit / (float)ActuatorCommands.LedCount, Hud.BatteryBarFraction(volts), 1e-6f, $"{volts} V");
        }
    }

    [Test]
    public void BodyFrame_AxesFollowTheCar()
    {
        Vector3[] level = BodyFrameView.Axes(Quaternion.identity);
        Assert.AreEqual(Vector3.forward, level[0], "x forward");
        Assert.AreEqual(Vector3.left, level[1], "y left");
        Assert.AreEqual(Vector3.up, level[2], "z up");

        Vector3[] turned = BodyFrameView.Axes(Quaternion.Euler(0, 90, 0));
        Assert.Less(Vector3.Distance(Vector3.right, turned[0]), 1e-5f, "x after a right turn");
    }

    [Test]
    public void BodyFrame_UpDrawsUpward()
    {
        Assert.Less(BodyFrameView.Project(Vector3.up).y, 0, "screen y grows downward");
        Assert.Greater(BodyFrameView.Project(Vector3.forward).z, 0, "world forward points away from the viewer");
    }
}
