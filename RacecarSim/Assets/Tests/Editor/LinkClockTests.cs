using NUnit.Framework;

/// <summary>
/// LinkClock offset estimation from time requests.
/// </summary>
public class LinkClockTests
{
    [Test]
    public void SymmetricRoundTrips_GiveExactOffset()
    {
        LinkClock clock = new LinkClock();
        for (int i = 0; i < 5; i++)
        {
            double sent = 10 + i;
            clock.AddSample(sent, sent + 0.01 + 1000, sent + 0.02);
        }
        Assert.IsTrue(clock.IsSynchronized);
        Assert.AreEqual(1000, clock.Offset, 1e-9);
        Assert.AreEqual(0.02, clock.RoundTrip, 1e-9);
        Assert.AreEqual(1010.5, clock.ToCarTime(10.5), 1e-9);
        Assert.AreEqual(10.5, clock.ToSimTime(1010.5), 1e-9);
    }

    [Test]
    public void SlowSamples_DoNotSkewTheOffset()
    {
        // Fast samples read the true offset 500; slow ones arrive with the reply delayed 0.4 s
        LinkClock clock = new LinkClock();
        for (int i = 0; i < 6; i++)
        {
            double sent = i;
            clock.AddSample(sent, sent + 0.005 + 500, sent + 0.01);
        }
        for (int i = 0; i < 4; i++)
        {
            double sent = 10 + i;
            clock.AddSample(sent, sent + 0.005 + 500, sent + 0.41);
        }
        Assert.AreEqual(500, clock.Offset, 1e-6);
    }

    [Test]
    public void NeedsMinimumSamples()
    {
        LinkClock clock = new LinkClock();
        clock.AddSample(0, 5, 0.01);
        clock.AddSample(1, 6, 1.01);
        Assert.IsFalse(clock.IsSynchronized);
        clock.AddSample(2, 7, 2.01);
        Assert.IsTrue(clock.IsSynchronized);
    }

    [Test]
    public void KeepsCapacityAndResets()
    {
        LinkClock clock = new LinkClock();
        for (int i = 0; i < LinkClock.Capacity + 10; i++)
        {
            clock.AddSample(i, i + 3, i + 0.01);
        }
        Assert.AreEqual(LinkClock.Capacity, clock.SampleCount);
        clock.Reset();
        Assert.AreEqual(0, clock.SampleCount);
        Assert.AreEqual(0, clock.Offset);
    }

    [Test]
    public void IgnoresNegativeRoundTrips()
    {
        LinkClock clock = new LinkClock();
        clock.AddSample(5, 100, 4);
        Assert.AreEqual(0, clock.SampleCount);
    }
}
