using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NUnit.Framework;

/// <summary>
/// The car link's real WebSocket and HTTP paths end to end against a loopback server.
/// </summary>
public class CarLinkLoopbackTests
{
    private CarLinkLoopbackServer server;
    private RosBridgeClient bridge;
    private string tempFolder;

    [SetUp]
    public void SetUp()
    {
        this.server = new CarLinkLoopbackServer();
        this.tempFolder = Path.Combine(Path.GetTempPath(), "racecarsim-carlink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.tempFolder);
    }

    [TearDown]
    public void TearDown()
    {
        this.bridge?.Dispose();
        this.server.Dispose();
        foreach (string file in Directory.GetFiles(this.tempFolder))
        {
            File.Delete(file);
        }
        Directory.Delete(this.tempFolder);
    }

    private void WaitFor(Func<bool> condition, string what, double seconds = 5)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!condition())
        {
            this.bridge?.Poll();
            if (watch.Elapsed.TotalSeconds > seconds)
            {
                Assert.Fail("timed out waiting for " + what);
            }
            Thread.Sleep(5);
        }
    }

    private string NextReceived()
    {
        Assert.IsTrue(this.server.Received.TryTake(out string text, 5000), "nothing received");
        return text;
    }

    private RosBridgeClient ConnectBridge()
    {
        this.bridge = new RosBridgeClient();
        this.bridge.Connect(new Uri($"ws://127.0.0.1:{this.server.Port}"));
        this.WaitFor(() => this.bridge.State == CarLinkState.Connected && this.server.HasClient, "connect");
        return this.bridge;
    }

    [Test]
    public void WebSocket_SubscribeReceiveAndPublish()
    {
        RosBridgeClient client = this.ConnectBridge();
        client.Subscribe("/autonomy/pose", new TopicOptions { Type = "geometry_msgs/msg/PoseWithCovarianceStamped" });
        CarLinkValue subscribe = CarLinkJson.Parse(this.NextReceived());
        Assert.AreEqual("subscribe", subscribe["op"].AsString());

        this.server.SendText("{\"op\": \"publish\", \"topic\": \"/autonomy/pose\", \"msg\": {\"header\": {\"stamp\": {\"sec\": 4, \"nanosec\": 0}, \"frame_id\": \"map\"},"
            + " \"pose\": {\"pose\": {\"position\": {\"x\": 1.0, \"y\": 2.0, \"z\": 0.0}, \"orientation\": {\"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1}}}}}");
        this.WaitFor(() => client.TryGetLatest("/autonomy/pose", out TopicSample _), "pose");
        client.TryGetLatest("/autonomy/pose", out TopicSample sample);
        Assert.AreEqual(2.0, PoseStamped2D.Parse(sample.Message).Y);

        client.Advertise("/sim_drive", "ackermann_msgs/msg/AckermannDriveStamped");
        client.Publish("/sim_drive", DriveCmd.FromStudent(1, 0, 0.25f, 4).ToValue());
        client.Unadvertise("/sim_drive");
        Assert.AreEqual("advertise", CarLinkJson.Parse(this.NextReceived())["op"].AsString());
        CarLinkValue publish = CarLinkJson.Parse(this.NextReceived());
        Assert.AreEqual(0.25, publish.At("msg.drive.speed").AsDouble(), 1e-6);
        Assert.AreEqual("unadvertise", CarLinkJson.Parse(this.NextReceived())["op"].AsString());
    }

    [Test]
    public void WebSocket_LargeBinaryMap()
    {
        RosBridgeClient client = this.ConnectBridge();
        client.Subscribe("/map", new TopicOptions { Cbor = true });
        this.NextReceived();

        // 4 MB int8 grid inside {"op": "publish", "topic": "/map", "msg": {"data": <tag 72>}}
        int cells = 4 * 1024 * 1024;
        List<byte> cbor = new List<byte>(cells + 64)
        {
            0xa3, 0x62, 0x6f, 0x70, 0x67, 0x70, 0x75, 0x62, 0x6c, 0x69, 0x73, 0x68,
            0x65, 0x74, 0x6f, 0x70, 0x69, 0x63, 0x64, 0x2f, 0x6d, 0x61, 0x70,
            0x63, 0x6d, 0x73, 0x67, 0xa1, 0x64, 0x64, 0x61, 0x74, 0x61, 0xd8, 0x48, 0x5a,
            (byte)(cells >> 24), (byte)(cells >> 16), (byte)(cells >> 8), (byte)cells,
        };
        for (int i = 0; i < cells; i++)
        {
            cbor.Add((byte)(i % 101));
        }
        this.server.SendBinary(cbor.ToArray());
        this.WaitFor(() => client.TryGetLatest("/map", out TopicSample _), "map", 10);

        client.TryGetLatest("/map", out TopicSample sample);
        sbyte[] data = sample.Message["data"].AsSBytes();
        Assert.AreEqual(cells, data.Length);
        Assert.AreEqual(100, data[100]);
        Assert.AreEqual((sbyte)((cells - 1) % 101), data[cells - 1]);
    }

    [Test]
    public void WebSocket_ReconnectsAfterDrop()
    {
        RosBridgeClient client = this.ConnectBridge();
        client.Subscribe("/mux/state", new TopicOptions());
        this.NextReceived();
        this.server.DropClient();
        this.WaitFor(() => client.State != CarLinkState.Connected, "drop seen");
        this.WaitFor(() => client.State == CarLinkState.Connected && this.server.HasClient, "reconnect", 8);
        Assert.AreEqual("subscribe", CarLinkJson.Parse(this.NextReceived())["op"].AsString(), "subscription restored");
    }

    [Test]
    public void Http_InfoStateAndAction()
    {
        CarLinkLoopbackServer.Request lastAction = null;
        this.server.Http = request =>
        {
            switch (request.Path)
            {
                case "/api/v1/info":
                    return CarLinkLoopbackServer.Response.Json("{\"link_interface\": \"1.0\", \"driver_version\": \"0.9.1\", \"twin\": {\"available\": true}}");
                case "/api/v1/state":
                    return CarLinkLoopbackServer.Response.Json("{\"mode\": \"mapping\", \"map\": \"room\", \"localized\": true, \"pose\": [1, 2, 0.5], \"job\": {\"running\": false}, \"twin\": false, \"link_clients\": 1}");
                case "/api/v1/action":
                    lastAction = request;
                    return CarLinkLoopbackServer.Response.Json("{\"ok\": true, \"job\": \"j7\"}");
                default:
                    return null;
            }
        };
        using (CarApiClient api = new CarApiClient("127.0.0.1", this.server.Port))
        {
            RacecarInfo info = api.GetInfoAsync().Result;
            Assert.IsTrue(info.IsCompatible);
            Assert.AreEqual("0.9.1", info.DriverVersion);

            ApiState state = api.GetStateAsync().Result;
            Assert.AreEqual("mapping", state.Mode);
            Assert.AreEqual(2, state.PoseY);

            string job = api.SendActionAsync(new CarAction { Action = "start", Name = "room_1", Record = "session" }).Result;
            Assert.AreEqual("j7", job);
            Assert.AreEqual("POST", lastAction.Method);
            CarLinkValue body = CarLinkJson.Parse(lastAction.Body);
            Assert.AreEqual("start", body["action"].AsString());
            Assert.AreEqual("racecarsim", body["client"].AsString());
            Assert.IsTrue(body["confirm"].AsBool());
            Assert.AreEqual("session", body["record"].AsString());
        }
    }

    [Test]
    public void Http_ErrorsAreClassified()
    {
        this.server.Http = request => request.Path == "/api/v1/action"
            ? CarLinkLoopbackServer.Response.Json("{\"ok\": false, \"message\": \"busy\"}", 409, "Conflict")
            : null;
        using (CarApiClient api = new CarApiClient("127.0.0.1", this.server.Port))
        {
            CarApiException busy = CarLinkLoopbackTests.Failure(() => api.SendActionAsync(new CarAction { Action = "save" }).Wait());
            Assert.AreEqual(CarApiFailure.Busy, busy.Failure);
            Assert.AreEqual(409, busy.StatusCode);

            CarApiException missing = CarLinkLoopbackTests.Failure(() => api.GetMapsAsync().Wait());
            Assert.AreEqual(CarApiFailure.Http, missing.Failure);
            StringAssert.Contains("not found", missing.Message);

            CarApiException badName = CarLinkLoopbackTests.Failure(() => api.GetMapAsync("../etc").Wait());
            Assert.AreEqual(CarApiFailure.BadName, badName.Failure);
        }

        // A port with nothing listening
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using (CarApiClient api = new CarApiClient("127.0.0.1", closedPort))
        {
            CarApiException refused = CarLinkLoopbackTests.Failure(() => api.GetInfoAsync().Wait());
            Assert.AreEqual(CarApiFailure.Refused, refused.Failure);
        }
    }

    [Test]
    public void Http_DownloadResumesAndChecksums()
    {
        byte[] file = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("P5 0123456789abcdef", 5000)));
        string hash;
        using (SHA256 sha = SHA256.Create())
        {
            hash = string.Concat(sha.ComputeHash(file).Select(b => b.ToString("x2")));
        }
        string rangeSeen = null;
        bool corrupt = false;
        this.server.Http = request =>
        {
            if (request.Path != "/api/v1/maps/room_1/files/room_1.pgm")
            {
                return null;
            }
            byte[] served = corrupt ? Encoding.ASCII.GetBytes("wrong") : file;
            CarLinkLoopbackServer.Response response = new CarLinkLoopbackServer.Response { Body = served };
            response.Headers["X-Content-SHA256"] = hash;
            response.Headers["Accept-Ranges"] = "bytes";
            if (request.Headers.TryGetValue("Range", out string range))
            {
                rangeSeen = range;
                int start = int.Parse(range.Substring("bytes=".Length).TrimEnd('-'));
                response.Status = 206;
                response.Reason = "Partial Content";
                response.Body = served.Skip(start).ToArray();
                response.Headers["Content-Range"] = $"bytes {start}-{served.Length - 1}/{served.Length}";
            }
            return response;
        };

        string target = Path.Combine(this.tempFolder, "room_1.pgm");
        File.WriteAllBytes(target + ".part", file.Take(30000).ToArray());
        List<double> progress = new List<double>();
        using (CarApiClient api = new CarApiClient("127.0.0.1", this.server.Port))
        {
            api.DownloadFileAsync("room_1", "room_1.pgm", target, new SyncProgress(progress)).Wait();
            Assert.AreEqual("bytes=30000-", rangeSeen);
            CollectionAssert.AreEqual(file, File.ReadAllBytes(target));
            Assert.IsFalse(File.Exists(target + ".part"));
            Assert.AreEqual(1.0, progress.Last(), 1e-9);

            corrupt = true;
            string second = Path.Combine(this.tempFolder, "second.pgm");
            CarApiException mismatch = CarLinkLoopbackTests.Failure(() => api.DownloadFileAsync("room_1", "room_1.pgm", second).Wait());
            Assert.AreEqual(CarApiFailure.Checksum, mismatch.Failure);
            Assert.IsFalse(File.Exists(second + ".part"));
            Assert.IsFalse(File.Exists(second));

            CarApiException notAllowed = CarLinkLoopbackTests.Failure(() => api.DownloadFileAsync("room_1", "../../secret", second).Wait());
            Assert.AreEqual(CarApiFailure.BadName, notAllowed.Failure);
        }
    }

    private static CarApiException Failure(Action call)
    {
        try
        {
            call();
        }
        catch (AggregateException exception) when (exception.InnerException is CarApiException failure)
        {
            return failure;
        }
        catch (CarApiException failure)
        {
            return failure;
        }
        Assert.Fail("expected a CarApiException");
        return null;
    }

    /// <summary>
    /// Records progress on the reporting thread (Progress posts to a context the test blocks).
    /// </summary>
    private sealed class SyncProgress : IProgress<double>
    {
        private readonly List<double> values;

        public SyncProgress(List<double> values)
        {
            this.values = values;
        }

        public void Report(double value)
        {
            lock (this.values)
            {
                this.values.Add(value);
            }
        }
    }
}
