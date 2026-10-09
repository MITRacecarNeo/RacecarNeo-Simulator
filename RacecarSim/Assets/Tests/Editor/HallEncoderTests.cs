using NUnit.Framework;

/// <summary>
/// HallEncoder conversion, edge timing, filter, and stop timeout.
/// </summary>
public class HallEncoderTests
{
    private const float step = 0.02f;

    [Test]
    public void EdgeDistance_MatchesFirmwareGearing()
    {
        Assert.AreEqual(0.002255f, HallEncoder.EdgeDistance, 0.000005f);
    }

    [Test]
    public void WheelSpeed_ConvertsRpm()
    {
        // One revolution per second rolls one 72 mm circumference
        Assert.AreEqual(0.22619f, HallEncoder.WheelSpeed(60), 0.00001f);
        Assert.AreEqual(-0.22619f, HallEncoder.WheelSpeed(-60), 0.00001f);
    }

    [Test]
    public void RealismOff_ReportsExactSpeed()
    {
        HallEncoder encoder = new HallEncoder();
        Assert.AreEqual(0.01f, encoder.Step(0.01f, HallEncoderTests.step, false));
        Assert.AreEqual(-1.5f, encoder.Step(-1.5f, HallEncoderTests.step, false));
    }

    [TestCase(1.0f)]
    [TestCase(4.0f)]
    [TestCase(-0.5f)]
    public void RealismOn_SettlesOnConstantSpeed(float speed)
    {
        HallEncoder encoder = new HallEncoder();
        for (int i = 0; i < 50; i++)
        {
            encoder.Step(speed, HallEncoderTests.step, true);
        }
        Assert.AreEqual(speed, encoder.Speed, 0.001f * System.Math.Abs(speed));
    }

    [Test]
    public void RealismOn_FiltersStep()
    {
        HallEncoder encoder = new HallEncoder();
        encoder.Step(1.0f, HallEncoderTests.step, true);
        Assert.AreEqual(0.8f, encoder.Speed, 0.001f);
    }

    [Test]
    public void RealismOn_ReadsZeroBetweenSlowEdges()
    {
        // At 0.02 m/s edges are 113 ms apart, longer than the 30 ms stop timeout
        HallEncoder encoder = new HallEncoder();
        int zeros = 0;
        for (int i = 0; i < 100; i++)
        {
            zeros += encoder.Step(0.02f, HallEncoderTests.step, true) == 0 ? 1 : 0;
        }
        Assert.Greater(zeros, 50);
    }

    [Test]
    public void RealismOn_NeverZeroAboveTimeoutSpeed()
    {
        HallEncoder encoder = new HallEncoder();
        encoder.Step(0.5f, HallEncoderTests.step, true);
        for (int i = 0; i < 100; i++)
        {
            Assert.AreNotEqual(0, encoder.Step(0.5f, HallEncoderTests.step, true));
        }
    }

    [Test]
    public void RealismOn_StopsAfterTimeout()
    {
        HallEncoder encoder = new HallEncoder();
        for (int i = 0; i < 10; i++)
        {
            encoder.Step(1.0f, HallEncoderTests.step, true);
        }
        encoder.Step(0, HallEncoderTests.step, true);
        encoder.Step(0, HallEncoderTests.step, true);
        Assert.AreEqual(0, encoder.Speed);
    }
}
