using System.Net;
using NUnit.Framework;

/// <summary>
/// Source address policy for Python connections.
/// </summary>
public class PythonSourceFilterTests
{
    [TestCase("127.0.0.1")]
    [TestCase("127.5.6.7")]
    public void IsAllowed_AcceptsLoopback(string address)
    {
        Assert.IsTrue(new PythonSourceFilter(false).IsAllowed(IPAddress.Parse(address)));
    }

    [TestCase("8.8.8.8")]
    [TestCase("203.0.113.9")]
    public void IsAllowed_RejectsOtherHostsByDefault(string address)
    {
        Assert.IsFalse(new PythonSourceFilter(false).IsAllowed(IPAddress.Parse(address)));
    }

    [Test]
    public void IsAllowed_AcceptsAnyHostWhenRemoteAllowed()
    {
        Assert.IsTrue(new PythonSourceFilter(true).IsAllowed(IPAddress.Parse("203.0.113.9")));
    }

    [TestCase("172.28.16.5", "172.28.16.1", "255.255.240.0", true)]
    [TestCase("172.28.31.254", "172.28.16.1", "255.255.240.0", true)]
    [TestCase("172.28.32.1", "172.28.16.1", "255.255.240.0", false)]
    [TestCase("192.168.1.20", "192.168.1.1", "255.255.255.0", true)]
    [TestCase("192.168.2.20", "192.168.1.1", "255.255.255.0", false)]
    public void IsInSubnet_MatchesMaskedPrefix(string address, string network, string mask, bool expected)
    {
        Assert.AreEqual(expected, PythonSourceFilter.IsInSubnet(IPAddress.Parse(address), IPAddress.Parse(network), IPAddress.Parse(mask)));
    }

    [Test]
    public void IsInSubnet_RejectsIPv6()
    {
        Assert.IsFalse(PythonSourceFilter.IsInSubnet(IPAddress.IPv6Loopback, IPAddress.Parse("172.28.16.1"), IPAddress.Parse("255.255.240.0")));
    }
}
