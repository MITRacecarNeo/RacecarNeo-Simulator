using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PythonInterface packet validation and async (port 5064) connection handling over loopback.
/// Requires UDP ports 5064 and 5065 to be free.
/// </summary>
public class PythonInterfaceTests
{
    private const int asyncPort = 5064;
    private const int syncPort = 5065;
    private const byte headerError = 0;
    private const byte headerConnect = 1;
    private const byte headerPythonExit = 7;
    private const byte protocolVersion = 1;

    private PythonInterface pythonInterface;

    [SetUp]
    public void SetUp()
    {
        LevelManager.NumPlayers = 1;
    }

    [TearDown]
    public void TearDown()
    {
        this.pythonInterface?.HandleExit();
        this.pythonInterface = null;
    }

    [TestCase(new byte[] { }, false)]
    [TestCase(new byte[] { 200 }, false)]
    [TestCase(new byte[] { 5 }, true)]
    [TestCase(new byte[] { 16 }, false)]
    [TestCase(new byte[] { 16, 0 }, true)]
    [TestCase(new byte[] { 22, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [TestCase(new byte[] { 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [TestCase(new byte[] { 24, 0, 0, 0 }, false)]
    public void IsWellFormed_ChecksHeaderAndLength(byte[] data, bool expected)
    {
        Assert.AreEqual(expected, PythonInterface.IsWellFormed(data));
    }

    [Test]
    public void IsWellFormed_RejectsNull()
    {
        Assert.IsFalse(PythonInterface.IsWellFormed(null));
    }

    [Test]
    public void Connect_AssignsFirstCar()
    {
        this.pythonInterface = new PythonInterface();
        Assert.IsNull(this.pythonInterface.StartupError);

        using (UdpClient python = PythonInterfaceTests.CreatePython())
        {
            byte[] reply = this.Request(python, new byte[] { headerConnect, protocolVersion });
            CollectionAssert.AreEqual(new byte[] { headerConnect, 0 }, reply);
            CollectionAssert.AreEqual(new[] { true }, this.pythonInterface.ConnectedPrograms);
        }
    }

    [Test]
    public void Connect_SameEndpointKeepsCar()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient python = PythonInterfaceTests.CreatePython())
        {
            this.Request(python, new byte[] { headerConnect, protocolVersion });
            byte[] reply = this.Request(python, new byte[] { headerConnect, protocolVersion });
            CollectionAssert.AreEqual(new byte[] { headerConnect, 0 }, reply);
        }
    }

    [Test]
    public void Connect_ReportsNoFreeCarWithTwoByteError()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient first = PythonInterfaceTests.CreatePython())
        using (UdpClient second = PythonInterfaceTests.CreatePython())
        {
            this.Request(first, new byte[] { headerConnect, protocolVersion });
            LogAssert.Expect(LogType.Error, new Regex("every car already has a connected program"));
            byte[] reply = this.Request(second, new byte[] { headerConnect, protocolVersion });
            CollectionAssert.AreEqual(new byte[] { headerError, 3 }, reply);
        }
    }

    [Test]
    public void Connect_ReportsOutdatedPython()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient python = PythonInterfaceTests.CreatePython())
        {
            LogAssert.Expect(LogType.Error, new Regex("outdated, incompatible version of racecar_core"));
            byte[] reply = this.Request(python, new byte[] { headerConnect, 0 });
            CollectionAssert.AreEqual(new byte[] { headerError, 4 }, reply);
        }
    }

    [Test]
    public void AsyncThread_SurvivesMalformedPackets()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient python = PythonInterfaceTests.CreatePython())
        {
            LogAssert.Expect(LogType.Error, new Regex("invalid async request"));
            LogAssert.Expect(LogType.Error, new Regex("invalid async request"));
            CollectionAssert.AreEqual(new byte[] { headerError, 0 }, this.Request(python, new byte[] { }));
            CollectionAssert.AreEqual(new byte[] { headerError, 0 }, this.Request(python, new byte[] { 250 }));

            byte[] reply = this.Request(python, new byte[] { headerConnect, protocolVersion });
            CollectionAssert.AreEqual(new byte[] { headerConnect, 0 }, reply);
        }
    }

    [Test]
    public void PythonExit_FreesCar()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient python = PythonInterfaceTests.CreatePython())
        {
            this.Request(python, new byte[] { headerConnect, protocolVersion });
            python.Send(new byte[] { headerPythonExit }, 1, new IPEndPoint(IPAddress.Loopback, asyncPort));

            Stopwatch timer = Stopwatch.StartNew();
            while (this.pythonInterface.ConnectedPrograms.Length > 0 && timer.ElapsedMilliseconds < 2000)
            {
                this.pythonInterface.ProcessPendingRequests();
            }
            Assert.IsEmpty(this.pythonInterface.ConnectedPrograms);
        }
    }

    [Test]
    public void PythonExit_FromOtherPortKeepsCar()
    {
        this.pythonInterface = new PythonInterface();
        using (UdpClient python = PythonInterfaceTests.CreatePython())
        using (UdpClient other = PythonInterfaceTests.CreatePython())
        {
            this.Request(python, new byte[] { headerConnect, protocolVersion });
            other.Send(new byte[] { headerPythonExit }, 1, new IPEndPoint(IPAddress.Loopback, asyncPort));

            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 1000)
            {
                this.pythonInterface.ProcessPendingRequests();
            }
            CollectionAssert.AreEqual(new[] { true }, this.pythonInterface.ConnectedPrograms);
        }
    }

    [Test]
    public void HandleExit_ReleasesPorts()
    {
        this.pythonInterface = new PythonInterface();
        this.pythonInterface.HandleExit();
        this.pythonInterface = null;

        Assert.DoesNotThrow(() =>
        {
            using (new UdpClient(new IPEndPoint(IPAddress.Any, syncPort)))
            using (new UdpClient(new IPEndPoint(IPAddress.Any, asyncPort)))
            {
            }
        });
    }

    [Test]
    public void Constructor_ReportsPortInUse()
    {
        using (new UdpClient(new IPEndPoint(IPAddress.Any, syncPort)))
        {
            LogAssert.Expect(LogType.Error, new Regex("already in use"));
            this.pythonInterface = new PythonInterface();
            StringAssert.Contains("already in use", this.pythonInterface.StartupError);
            Assert.IsEmpty(this.pythonInterface.ConnectedPrograms);
            Assert.DoesNotThrow(() => this.pythonInterface.HandleUpdate());
        }
    }

    private static UdpClient CreatePython()
    {
        UdpClient client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        client.Client.ReceiveTimeout = 2000;
        return client;
    }

    /// <summary>
    /// Sends a packet to the async port and pumps ProcessPendingRequests until a reply arrives.
    /// </summary>
    private byte[] Request(UdpClient python, byte[] packet)
    {
        python.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, asyncPort));

        Stopwatch timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 2000)
        {
            this.pythonInterface.ProcessPendingRequests();
            if (python.Available > 0)
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                return python.Receive(ref from);
            }
        }
        throw new TimeoutException("No reply from PythonInterface within 2 s.");
    }
}
