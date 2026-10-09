using NUnit.Framework;

/// <summary>
/// BatteryModel drain times, firmware charge table, sag, and sensor resolution.
/// </summary>
public class BatteryModelTests
{
    private const float step = 0.02f;

    [Test]
    public void StartsFullAtFullVoltage()
    {
        BatteryModel battery = new BatteryModel();
        battery.Step(BatteryModelTests.step, false, 0);
        Assert.AreEqual(BatteryModel.FullVoltage, battery.Voltage, 0.0005f);
        Assert.AreEqual(BatteryModel.IdleCurrent, battery.Current);
    }

    [TestCase(false, 0f, 7200f)]
    [TestCase(true, 0f, 3600f)]
    [TestCase(true, 4f, 1800f)]
    [TestCase(true, 8f, 1800f)]
    public void DrainTime(bool isDriving, float speed, float seconds)
    {
        BatteryModel battery = new BatteryModel();
        int steps = 0;
        while (!battery.IsEmpty && steps < 1000000)
        {
            battery.Step(BatteryModelTests.step, isDriving, speed);
            steps++;
        }
        Assert.AreEqual(seconds, steps * BatteryModelTests.step, 1.0f);
    }

    [Test]
    public void DriveCurrentRisesWithSpeed()
    {
        Assert.AreEqual(2.5f, BatteryModel.DrawCurrent(false, 3), 1e-5f);
        Assert.AreEqual(5.0f, BatteryModel.DrawCurrent(true, 0), 1e-5f);
        Assert.AreEqual(6.25f, BatteryModel.DrawCurrent(true, 1), 1e-5f);
        Assert.AreEqual(6.25f, BatteryModel.DrawCurrent(true, -1), 1e-5f);
        Assert.AreEqual(10.0f, BatteryModel.DrawCurrent(true, 4), 1e-5f);
    }

    [TestCase(1.00f, 8.400f)]
    [TestCase(0.88f, 8.200f)]
    [TestCase(0.64f, 7.800f)]
    [TestCase(0.40f, 7.600f)]
    [TestCase(0.13f, 7.400f)]
    [TestCase(0.04f, 7.200f)]
    [TestCase(0.00f, 7.000f)]
    [TestCase(0.50f, 7.6667f)]
    public void OpenCircuitVoltage_FollowsFirmwareTable(float charge, float volts)
    {
        Assert.AreEqual(volts, BatteryModel.OpenCircuitVoltage(charge), 0.0005f);
        Assert.AreEqual(charge, BatteryModel.ChargeFromVoltage(volts), 0.0005f);
    }

    [Test]
    public void SagsUnderLoad()
    {
        BatteryModel battery = new BatteryModel();
        battery.Step(BatteryModelTests.step, true, 4);
        float open = BatteryModel.OpenCircuitVoltage(battery.Charge);
        Assert.AreEqual(0.15f, open - battery.Voltage, 0.0001f);
    }

    [Test]
    public void ReadingsUseSensorResolution()
    {
        BatteryModel battery = new BatteryModel();
        battery.Step(BatteryModelTests.step, true, 1.234f);
        float volts = battery.ReadVoltage(false);
        float amps = battery.ReadCurrent(false);
        Assert.AreEqual(System.Math.Round(volts * 1000), volts * 1000, 1e-3);
        Assert.AreEqual(System.Math.Round(amps * 1000), amps * 1000, 1e-3);
        Assert.AreEqual(battery.Current, amps, 0.0006f);
    }

    [Test]
    public void EmptyBatteryStaysEmpty()
    {
        BatteryModel battery = new BatteryModel();
        battery.Step(BatteryModel.CapacityAh * 3600 / BatteryModel.IdleCurrent + 10, false, 0);
        Assert.IsTrue(battery.IsEmpty);
        Assert.AreEqual(0, battery.Charge);
        Assert.AreEqual(BatteryModel.EmptyVoltage, battery.Voltage, 0.0005f);
    }
}
