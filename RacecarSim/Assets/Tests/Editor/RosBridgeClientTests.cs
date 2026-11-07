using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// RosBridgeClient operations, store, services, and reconnection against a scripted transport.
/// </summary>
public class RosBridgeClientTests
{
    private static readonly Uri uri = new Uri("ws://car.test:9090");

    /// <summary>
    /// A transport whose connect result, received frames, and sent text the test controls.
    /// </summary>
    private sealed class ScriptedTransport : ICarLinkTransport
    {
        public readonly ConcurrentQueue<string> Sent = new ConcurrentQueue<string>();
        public readonly BlockingCollection<CarLinkFrame> Incoming = new BlockingCollection<CarLinkFrame>();
        public Exception ConnectError;
        public bool IsDisposed;

        public bool IsOpen { get; private set; }

        public Task ConnectAsync(Uri target, CancellationToken cancellation)
        {
            if (this.ConnectError != null)
            {
                return Task.FromException(this.ConnectError);
            }
            this.IsOpen = true;
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string text, CancellationToken cancellation)
        {
            this.Sent.Enqueue(text);
            return Task.CompletedTask;
        }

        public Task<CarLinkFrame> ReceiveAsync(CancellationToken cancellation)
        {
            return Task.Run(() =>
            {
                CarLinkFrame frame = this.Incoming.Take(cancellation);
                return frame.Text == "<close>" ? null : frame;
            });
        }

        public Task CloseAsync(CancellationToken cancellation)
        {
            this.IsOpen = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.IsDisposed = true;
        }

        public void Push(string json)
        {
            this.Incoming.Add(new CarLinkFrame { Text = json });
        }

        public List<CarLinkValue> SentOps()
        {
            return this.Sent.Select(CarLinkJson.Parse).ToList();
        }
    }

    private List<ScriptedTransport> transports;
    private List<TimeSpan> delays;
    private RosBridgeClient client;

    [SetUp]
    public void SetUp()
    {
        this.transports = new List<ScriptedTransport>();
        this.delays = new List<TimeSpan>();
    }

    [TearDown]
    public void TearDown()
    {
        this.client?.Dispose();
    }

    private RosBridgeClient Create(params ScriptedTransport[] attempts)
    {
        Queue<ScriptedTransport> queue = new Queue<ScriptedTransport>(attempts);
        this.transports.AddRange(attempts);
        this.client = new RosBridgeClient(
            () =>
            {
                lock (queue)
                {
                    return queue.Count > 1 ? queue.Dequeue() : queue.Peek();
                }
            },
            (span, token) =>
            {
                lock (this.delays)
                {
                    this.delays.Add(span);
                }
                return Task.Delay(1, token);
            });
        return this.client;
    }

    private void WaitFor(Func<bool> condition, string what)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!condition())
        {
            this.client.Poll();
            if (watch.Elapsed.TotalSeconds > 3)
            {
                Assert.Fail("timed out waiting for " + what);
            }
            Thread.Sleep(5);
        }
        this.client.Poll();
    }

    private static List<string> OpsOf(ScriptedTransport transport)
    {
        return transport.SentOps().Select(op => op["op"].AsString()).ToList();
    }

    [Test]
    public void Subscribe_SendsOptionsOnConnect()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Subscribe("/scan", new TopicOptions { Type = "sensor_msgs/msg/LaserScan", ThrottleMs = 250, Cbor = true });
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => transport.Sent.Count == 1, "subscribe");

        CarLinkValue op = transport.SentOps()[0];
        Assert.AreEqual("subscribe", op["op"].AsString());
        Assert.AreEqual("/scan", op["topic"].AsString());
        Assert.AreEqual("sensor_msgs/msg/LaserScan", op["type"].AsString());
        Assert.AreEqual(250, op["throttle_rate"].AsInt());
        Assert.AreEqual(1, op["queue_length"].AsInt());
        Assert.AreEqual("cbor", op["compression"].AsString());
        Assert.IsFalse(op["id"].IsNull);
        Assert.AreEqual(CarLinkState.Connected, bridge.State);
    }

    [Test]
    public void Publish_StoresNewestPerTopic()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Subscribe("/mux/state", new TopicOptions { Type = "std_msgs/msg/String" });
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        transport.Push("{\"op\": \"publish\", \"topic\": \"/mux/state\", \"msg\": {\"data\": \"one\"}}");
        transport.Push("{\"op\": \"publish\", \"topic\": \"/mux/state\", \"msg\": {\"data\": \"two\"}}");
        transport.Push("{\"op\": \"publish\", \"topic\": \"/not_subscribed\", \"msg\": {\"data\": \"x\"}}");
        this.WaitFor(() => bridge.MessageCount == 3, "messages");

        Assert.IsTrue(bridge.TryGetLatest("/mux/state", out TopicSample sample));
        Assert.AreEqual("two", sample.Message["data"].AsString());
        Assert.AreEqual(2, sample.Sequence);
        Assert.IsFalse(bridge.TryGetLatest("/not_subscribed", out TopicSample _));
    }

    [Test]
    public void BinaryFrame_DecodesCbor()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Subscribe("/map", new TopicOptions { Cbor = true });
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        // {"op": "publish", "topic": "/map", "msg": {"data": int8 [-1, 100]}}
        byte[] cbor =
        {
            0xa3, 0x62, 0x6f, 0x70, 0x67, 0x70, 0x75, 0x62, 0x6c, 0x69, 0x73, 0x68,
            0x65, 0x74, 0x6f, 0x70, 0x69, 0x63, 0x64, 0x2f, 0x6d, 0x61, 0x70,
            0x63, 0x6d, 0x73, 0x67, 0xa1, 0x64, 0x64, 0x61, 0x74, 0x61, 0xd8, 0x48, 0x42, 0xff, 0x64,
        };
        transport.Incoming.Add(new CarLinkFrame { Binary = cbor });
        this.WaitFor(() => bridge.TryGetLatest("/map", out TopicSample _), "map");

        bridge.TryGetLatest("/map", out TopicSample sample);
        Assert.AreEqual(new sbyte[] { -1, 100 }, sample.Message["data"].AsSBytes());
    }

    [Test]
    public void NothingIsPublishedUnlessAsked()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        Assert.IsFalse(bridge.Publish("/sim_drive", CarLinkValue.Null), "not connected");
        bridge.Subscribe("/scan", new TopicOptions());
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => transport.Sent.Count == 1, "subscribe");

        Assert.IsFalse(bridge.Publish("/sim_drive", CarLinkValue.Null), "not advertised");
        Thread.Sleep(50);
        CollectionAssert.AreEqual(new[] { "subscribe" }, RosBridgeClientTests.OpsOf(transport));
    }

    [Test]
    public void Advertise_PublishZero_Unadvertise_InOrder()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        bridge.Advertise("/sim_drive", "ackermann_msgs/msg/AckermannDriveStamped");
        for (int i = 0; i < 20; i++)
        {
            Assert.IsTrue(bridge.Publish("/sim_drive", DriveCmd.FromStudent(0.5f, 0, 1, i).ToValue()));
        }
        Assert.IsTrue(bridge.Publish("/sim_drive", DriveCmd.FromStudent(0, 0, 1, 20).ToValue()));
        bridge.Unadvertise("/sim_drive");
        Assert.IsFalse(bridge.Publish("/sim_drive", DriveCmd.FromStudent(1, 0, 1, 21).ToValue()), "after unadvertise");
        this.WaitFor(() => transport.Sent.Count == 23, "ops");

        List<CarLinkValue> ops = transport.SentOps();
        Assert.AreEqual("advertise", ops[0]["op"].AsString());
        Assert.AreEqual("ackermann_msgs/msg/AckermannDriveStamped", ops[0]["type"].AsString());
        Assert.AreEqual(0.0, ops[21].At("msg.drive.speed").AsDouble(), "zero before unadvertise");
        Assert.AreEqual("unadvertise", ops[22]["op"].AsString());
    }

    [Test]
    public void Reconnect_RestoresSubscriptionsAndAdvertisements()
    {
        ScriptedTransport first = new ScriptedTransport();
        ScriptedTransport second = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(first, second);
        bridge.Subscribe("/autonomy/pose", new TopicOptions { Type = "geometry_msgs/msg/PoseWithCovarianceStamped" });
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");
        bridge.Advertise("/sim_drive", "ackermann_msgs/msg/AckermannDriveStamped");
        this.WaitFor(() => first.Sent.Count == 2, "first ops");

        first.Push("<close>");
        this.WaitFor(() => second.Sent.Count == 2, "second ops");

        CollectionAssert.AreEquivalent(new[] { "subscribe", "advertise" }, RosBridgeClientTests.OpsOf(second));
        Assert.IsTrue(first.IsDisposed);
        Assert.AreEqual(TimeSpan.FromSeconds(1), this.delays[0]);
    }

    [Test]
    public void ConnectFailures_BackOff()
    {
        ScriptedTransport refused = new ScriptedTransport { ConnectError = new TimeoutException("no answer") };
        ScriptedTransport working = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(refused, refused, refused, refused, refused, working);
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        CollectionAssert.AreEqual(
            new[] { 1.0, 2.0, 5.0, 10.0, 10.0 },
            this.delays.Select(span => span.TotalSeconds).ToArray());
    }

    [Test]
    public void StateEvents_RaiseOnPoll()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        List<CarLinkState> states = new List<CarLinkState>();
        bridge.StateChanged += states.Add;
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => states.Contains(CarLinkState.Connected), "connected event");
        Assert.AreEqual(CarLinkState.Connecting, states[0]);
    }

    [Test]
    public void Service_MatchesResponseById()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        Task<CarLinkValue> call = bridge.CallService("/rosapi/get_time", CarLinkValue.Null, 2);
        this.WaitFor(() => transport.Sent.Count == 1, "call");
        CarLinkValue op = transport.SentOps()[0];
        Assert.AreEqual("call_service", op["op"].AsString());
        Assert.AreEqual("/rosapi/get_time", op["service"].AsString());
        string id = op["id"].AsString();

        transport.Push("{\"op\": \"service_response\", \"id\": \"other\", \"result\": true, \"values\": {}}");
        transport.Push("{\"op\": \"service_response\", \"id\": \"" + id + "\", \"service\": \"/rosapi/get_time\", \"result\": true,"
            + " \"values\": {\"time\": {\"sec\": 12, \"nanosec\": 500000000}}}");
        Assert.IsTrue(call.Wait(2000));
        Assert.AreEqual(12.5, CarLinkFields.Stamp(call.Result, "time"), 1e-9);
    }

    [Test]
    public void Service_FailureAndTimeoutFault()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        Task<CarLinkValue> early = bridge.CallService("/autonomy/save_map", CarLinkValue.Null, 1);
        Assert.IsInstanceOf<CarLinkException>(early.Exception.InnerException, "not connected");

        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");
        Task<CarLinkValue> failing = bridge.CallService("/autonomy/save_map", CarLinkValue.Null, 2);
        this.WaitFor(() => transport.Sent.Count == 1, "call");
        string id = transport.SentOps()[0]["id"].AsString();
        transport.Push("{\"op\": \"service_response\", \"id\": \"" + id + "\", \"service\": \"/autonomy/save_map\", \"result\": false, \"values\": \"not mapping\"}");
        AggregateException failure = Assert.Throws<AggregateException>(() => failing.Wait(2000));
        StringAssert.Contains("not mapping", failure.InnerException.Message);

        Task<CarLinkValue> slow = bridge.CallService("/rosapi/get_time", CarLinkValue.Null, 0.1);
        AggregateException timeout = Assert.Throws<AggregateException>(() => slow.Wait(2000));
        StringAssert.Contains("timed out", timeout.InnerException.Message);
    }

    [Test]
    public void Disconnect_FailsPendingCalls()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");
        Task<CarLinkValue> call = bridge.CallService("/rosapi/get_time", CarLinkValue.Null, 5);
        bridge.Disconnect();
        Assert.Throws<AggregateException>(() => call.Wait(2000));
        Assert.AreEqual(CarLinkState.Closed, bridge.State);
    }

    [Test]
    public void MalformedMessage_IsSkipped()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Subscribe("/odom", new TopicOptions());
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");

        transport.Push("{not json");
        transport.Incoming.Add(new CarLinkFrame { Binary = new byte[] { 0x82, 0x01 } });
        transport.Push("{\"op\": \"publish\", \"topic\": \"/odom\", \"msg\": {\"ok\": true}}");
        this.WaitFor(() => bridge.TryGetLatest("/odom", out TopicSample _), "message after bad ones");

        Assert.AreEqual(2, bridge.MalformedCount);
        Assert.AreEqual(CarLinkState.Connected, bridge.State);
    }

    [Test]
    public void StatusMessage_RaisesEvent()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        string received = null;
        bridge.StatusReceived += (level, text) => received = level + ": " + text;
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");
        transport.Push("{\"op\": \"status\", \"level\": \"error\", \"msg\": \"/drive is not allowed\"}");
        this.WaitFor(() => received != null, "status");
        Assert.AreEqual("error: /drive is not allowed", received);
    }

    [Test]
    public void Disconnect_SendsQueuedOperationsFirst()
    {
        ScriptedTransport transport = new ScriptedTransport();
        RosBridgeClient bridge = this.Create(transport);
        bridge.Connect(RosBridgeClientTests.uri);
        this.WaitFor(() => bridge.State == CarLinkState.Connected, "connect");
        bridge.Advertise("/sim_drive", "ackermann_msgs/msg/AckermannDriveStamped");
        bridge.Publish("/sim_drive", DriveCmd.FromStudent(0, 0, 1, 0).ToValue());
        bridge.Unadvertise("/sim_drive");
        bridge.Disconnect();

        CollectionAssert.AreEqual(new[] { "advertise", "publish", "unadvertise" }, RosBridgeClientTests.OpsOf(transport));
        Assert.IsFalse(transport.IsOpen);
        Assert.IsTrue(transport.IsDisposed);
    }
}
