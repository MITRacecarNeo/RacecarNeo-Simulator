using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Connection state of a RosBridgeClient.
/// </summary>
public enum CarLinkState
{
    Idle,
    Connecting,
    Connected,
    Retrying,
    Closed
}

/// <summary>
/// The car link failed at run time: not connected, a service call failed, or it timed out.
/// </summary>
public class CarLinkException : Exception
{
    public CarLinkException(string message) : base(message) { }
}

/// <summary>
/// How the car's bridge sends one subscribed topic.
/// </summary>
public sealed class TopicOptions
{
    /// <summary>
    /// ROS message type, for example "sensor_msgs/msg/LaserScan".
    /// </summary>
    public string Type;

    /// <summary>
    /// Least time between messages (in milliseconds); 0 sends every message.
    /// </summary>
    public int ThrottleMs;

    /// <summary>
    /// Messages the bridge holds while throttling; 1 keeps only the newest.
    /// </summary>
    public int QueueLength = 1;

    /// <summary>
    /// True to receive the topic as CBOR (typed arrays for large numeric fields).
    /// </summary>
    public bool Cbor;
}

/// <summary>
/// The newest message on a topic.
/// </summary>
public sealed class TopicSample
{
    public CarLinkValue Message;

    /// <summary>
    /// Link time (in seconds, RosBridgeClient.Now) when the message arrived.
    /// </summary>
    public double ReceivedAt;

    /// <summary>
    /// Count of messages received on the topic; changes when a new message arrives.
    /// </summary>
    public long Sequence;
}

/// <summary>
/// Client for the car's rosbridge (rosbridge v2 protocol over a WebSocket): subscriptions,
/// advertised topics, service calls, and reconnection.
/// </summary>
/// <remarks>
/// A background task owns the connection: it receives and decodes messages into a newest-message
/// store per topic, sends queued operations in order, and reconnects with backoff, restoring
/// subscriptions and advertisements. The main thread reads the store and calls Poll each frame to
/// raise events. Publishing is dropped while not connected, never queued for later, so a stale
/// drive command cannot reach the car after a reconnect.
/// </remarks>
public sealed class RosBridgeClient : IDisposable
{
    #region Constants
    /// <summary>
    /// Waits between connection attempts (in seconds); the last repeats.
    /// </summary>
    public static readonly double[] RetryDelays = { 1, 2, 5, 10 };

    /// <summary>
    /// Time Disconnect waits for queued operations to go out (in seconds).
    /// </summary>
    public const double FlushTimeout = 0.5;
    #endregion

    #region Public Interface
    /// <summary>
    /// Link time (in seconds) from a monotonic clock shared by every link component.
    /// </summary>
    public static double Now { get { return RosBridgeClient.stopwatch.Elapsed.TotalSeconds; } }

    public CarLinkState State { get { return (CarLinkState)this.state; } }

    /// <summary>
    /// The reason for the last failed connection or message, or null.
    /// </summary>
    public string LastError { get; private set; }

    public Uri Uri { get; private set; }

    public long MessageCount { get { return Interlocked.Read(ref this.messageCount); } }

    /// <summary>
    /// Messages that could not be decoded; each is skipped and the connection kept.
    /// </summary>
    public long MalformedCount { get { return Interlocked.Read(ref this.malformedCount); } }

    /// <summary>
    /// Raised from Poll when the connection state changes.
    /// </summary>
    public event Action<CarLinkState> StateChanged;

    /// <summary>
    /// Raised from Poll for a rosbridge status message (level, text), such as a refused operation.
    /// </summary>
    public event Action<string, string> StatusReceived;

    /// <param name="transportFactory">Creates a transport per attempt; WebSocketTransport by default.</param>
    /// <param name="delay">Waits between attempts; Task.Delay by default.</param>
    public RosBridgeClient(Func<ICarLinkTransport> transportFactory = null, Func<TimeSpan, CancellationToken, Task> delay = null)
    {
        this.transportFactory = transportFactory ?? (() => new WebSocketTransport());
        this.delay = delay ?? ((span, token) => Task.Delay(span, token));
    }

    /// <summary>
    /// Starts connecting to a bridge, for example ws://10.42.0.1:9090; retries until Disconnect.
    /// </summary>
    public void Connect(Uri uri)
    {
        lock (this.gate)
        {
            if (this.loop != null)
            {
                throw new InvalidOperationException("already connecting");
            }
            this.Uri = uri;
            this.cancellation = new CancellationTokenSource();
            this.loop = Task.Run(() => this.RunAsync(this.cancellation.Token));
        }
    }

    /// <summary>
    /// Sends what is queued (up to FlushTimeout), closes the connection, and stops retrying.
    /// </summary>
    public void Disconnect()
    {
        Task running;
        lock (this.gate)
        {
            running = this.loop;
            if (running == null)
            {
                return;
            }
            this.loop = null;
        }
        this.drained.Wait(TimeSpan.FromSeconds(RosBridgeClient.FlushTimeout));
        this.cancellation.Cancel();
        try
        {
            running.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop reports its own errors through LastError
        }
        this.cancellation.Dispose();
        this.cancellation = null;
    }

    public void Subscribe(string topic, TopicOptions options)
    {
        lock (this.gate)
        {
            this.subscriptions[topic] = options;
            if (this.State == CarLinkState.Connected)
            {
                this.EnqueueLocked(this.SubscribeOp(topic, options));
            }
        }
    }

    public void Unsubscribe(string topic)
    {
        lock (this.gate)
        {
            if (this.subscriptions.Remove(topic) && this.State == CarLinkState.Connected)
            {
                this.EnqueueLocked(RosBridgeClient.Op("unsubscribe", this.NextId(), ("topic", CarLinkValue.FromString(topic))));
            }
            this.latest.TryRemove(topic, out TopicSlot _);
        }
    }

    /// <summary>
    /// The newest message on a subscribed topic, if any arrived.
    /// </summary>
    public bool TryGetLatest(string topic, out TopicSample sample)
    {
        if (this.latest.TryGetValue(topic, out TopicSlot slot))
        {
            sample = slot.Sample;
            return sample != null;
        }
        sample = null;
        return false;
    }

    /// <summary>
    /// Smoothed message rate (in Hz) on a subscribed topic; 0 before two messages.
    /// </summary>
    public double RateHz(string topic)
    {
        return this.latest.TryGetValue(topic, out TopicSlot slot) && slot.MeanInterval > 0 ? 1 / slot.MeanInterval : 0;
    }

    /// <summary>
    /// Advertises a topic this client publishes; restored after a reconnect.
    /// </summary>
    public void Advertise(string topic, string type)
    {
        lock (this.gate)
        {
            this.advertised[topic] = type;
            if (this.State == CarLinkState.Connected)
            {
                this.EnqueueLocked(this.AdvertiseOp(topic, type));
            }
        }
    }

    /// <summary>
    /// Publishes on an advertised topic. Returns false, sending nothing, when not connected or
    /// not advertised.
    /// </summary>
    public bool Publish(string topic, CarLinkValue message)
    {
        lock (this.gate)
        {
            if (this.State != CarLinkState.Connected || !this.advertised.ContainsKey(topic))
            {
                return false;
            }
            this.EnqueueLocked(RosBridgeClient.Op("publish", null, ("topic", CarLinkValue.FromString(topic)), ("msg", message)));
            return true;
        }
    }

    public void Unadvertise(string topic)
    {
        lock (this.gate)
        {
            if (this.advertised.Remove(topic) && this.State == CarLinkState.Connected)
            {
                this.EnqueueLocked(RosBridgeClient.Op("unadvertise", this.NextId(), ("topic", CarLinkValue.FromString(topic))));
            }
        }
    }

    /// <summary>
    /// Calls a service through the bridge. Faults with CarLinkException when not connected, when
    /// the service reports failure, on disconnect, or after the timeout.
    /// </summary>
    public Task<CarLinkValue> CallService(string service, CarLinkValue args, double timeoutSeconds)
    {
        TaskCompletionSource<CarLinkValue> completion = new TaskCompletionSource<CarLinkValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        string id;
        lock (this.gate)
        {
            if (this.State != CarLinkState.Connected)
            {
                completion.SetException(new CarLinkException("not connected"));
                return completion.Task;
            }
            id = this.NextId();
            this.pending[id] = completion;
            this.EnqueueLocked(RosBridgeClient.Op("call_service", id,
                ("service", CarLinkValue.FromString(service)),
                ("args", args.IsNull ? CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>()) : args)));
        }
        CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        timeout.Token.Register(() =>
        {
            if (this.pending.TryRemove(id, out TaskCompletionSource<CarLinkValue> waiting))
            {
                waiting.TrySetException(new CarLinkException($"{service} timed out"));
            }
            timeout.Dispose();
        });
        return completion.Task;
    }

    /// <summary>
    /// Raises queued events on the calling (main) thread.
    /// </summary>
    public void Poll()
    {
        while (this.events.TryDequeue(out Action raise))
        {
            raise();
        }
    }

    public void Dispose()
    {
        this.Disconnect();
    }
    #endregion

    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    private readonly object gate = new object();
    private readonly Func<ICarLinkTransport> transportFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Dictionary<string, TopicOptions> subscriptions = new Dictionary<string, TopicOptions>();
    private readonly Dictionary<string, string> advertised = new Dictionary<string, string>();
    private readonly ConcurrentDictionary<string, TopicSlot> latest = new ConcurrentDictionary<string, TopicSlot>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CarLinkValue>> pending = new ConcurrentDictionary<string, TaskCompletionSource<CarLinkValue>>();
    private readonly ConcurrentQueue<Action> events = new ConcurrentQueue<Action>();
    private readonly Queue<string> outgoing = new Queue<string>();
    private readonly SemaphoreSlim outgoingSignal = new SemaphoreSlim(0);
    private readonly ManualResetEventSlim drained = new ManualResetEventSlim(true);

    private volatile int state = (int)CarLinkState.Idle;
    private long messageCount;
    private long malformedCount;
    private long nextId;
    private Task loop;
    private CancellationTokenSource cancellation;

    private sealed class TopicSlot
    {
        public volatile TopicSample Sample;
        public double LastReceivedAt = double.NaN;
        public double MeanInterval;
    }

    private async Task RunAsync(CancellationToken cancellation)
    {
        int attempt = 0;
        while (!cancellation.IsCancellationRequested)
        {
            this.SetState(CarLinkState.Connecting);
            ICarLinkTransport transport = this.transportFactory();
            Task sender = null;
            CancellationTokenSource connection = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            try
            {
                await transport.ConnectAsync(this.Uri, cancellation).ConfigureAwait(false);
                lock (this.gate)
                {
                    this.outgoing.Clear();
                    this.state = (int)CarLinkState.Connected;
                    foreach (KeyValuePair<string, TopicOptions> subscription in this.subscriptions)
                    {
                        this.EnqueueLocked(this.SubscribeOp(subscription.Key, subscription.Value));
                    }
                    foreach (KeyValuePair<string, string> advertisement in this.advertised)
                    {
                        this.EnqueueLocked(this.AdvertiseOp(advertisement.Key, advertisement.Value));
                    }
                }
                this.QueueStateEvent(CarLinkState.Connected);
                attempt = 0;
                this.LastError = null;
                sender = this.SendLoopAsync(transport, connection.Token);
                while (true)
                {
                    CarLinkFrame frame = await transport.ReceiveAsync(connection.Token).ConfigureAwait(false);
                    if (frame == null)
                    {
                        this.LastError = "the car closed the connection";
                        break;
                    }
                    this.Handle(frame);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                this.LastError = RosBridgeClient.Describe(exception);
            }
            finally
            {
                lock (this.gate)
                {
                    if (this.state == (int)CarLinkState.Connected)
                    {
                        this.state = (int)CarLinkState.Connecting;
                    }
                    this.outgoing.Clear();
                    this.drained.Set();
                }
                connection.Cancel();
                if (sender != null)
                {
                    try
                    {
                        await sender.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The receive side already recorded why the connection ended
                    }
                }
                if (cancellation.IsCancellationRequested)
                {
                    await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                transport.Dispose();
                connection.Dispose();
                this.FailPending("disconnected");
            }

            if (cancellation.IsCancellationRequested)
            {
                break;
            }
            this.SetState(CarLinkState.Retrying);
            double wait = RosBridgeClient.RetryDelays[Math.Min(attempt, RosBridgeClient.RetryDelays.Length - 1)];
            attempt++;
            try
            {
                await this.delay(TimeSpan.FromSeconds(wait), cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        this.SetState(CarLinkState.Closed);
    }

    private async Task SendLoopAsync(ICarLinkTransport transport, CancellationToken cancellation)
    {
        while (true)
        {
            await this.outgoingSignal.WaitAsync(cancellation).ConfigureAwait(false);
            string text;
            lock (this.gate)
            {
                if (this.outgoing.Count == 0)
                {
                    continue;
                }
                text = this.outgoing.Dequeue();
            }
            await transport.SendTextAsync(text, cancellation).ConfigureAwait(false);
            lock (this.gate)
            {
                if (this.outgoing.Count == 0)
                {
                    this.drained.Set();
                }
            }
        }
    }

    private void Handle(CarLinkFrame frame)
    {
        CarLinkValue value;
        try
        {
            value = frame.IsBinary ? CarLinkCbor.Read(frame.Binary) : CarLinkJson.Parse(frame.Text);
        }
        catch (CarLinkFormatException exception)
        {
            Interlocked.Increment(ref this.malformedCount);
            this.LastError = exception.Message;
            return;
        }
        Interlocked.Increment(ref this.messageCount);

        string op = value["op"].Kind == CarLinkValueKind.String ? value["op"].AsString() : null;
        switch (op)
        {
            case "publish":
                this.Store(value);
                break;
            case "service_response":
                this.Complete(value);
                break;
            case "status":
                string level = value["level"].Kind == CarLinkValueKind.String ? value["level"].AsString() : "info";
                string text = value["msg"].Kind == CarLinkValueKind.String ? value["msg"].AsString() : string.Empty;
                this.events.Enqueue(() => this.StatusReceived?.Invoke(level, text));
                break;
            default:
                Interlocked.Increment(ref this.malformedCount);
                this.LastError = $"unknown op '{op}'";
                break;
        }
    }

    private void Store(CarLinkValue value)
    {
        if (value["topic"].Kind != CarLinkValueKind.String)
        {
            Interlocked.Increment(ref this.malformedCount);
            return;
        }
        string topic = value["topic"].AsString();
        lock (this.gate)
        {
            if (!this.subscriptions.ContainsKey(topic))
            {
                return;
            }
        }
        double now = RosBridgeClient.Now;
        TopicSlot slot = this.latest.GetOrAdd(topic, _ => new TopicSlot());
        if (!double.IsNaN(slot.LastReceivedAt))
        {
            double interval = now - slot.LastReceivedAt;
            slot.MeanInterval = slot.MeanInterval > 0 ? slot.MeanInterval * 0.8 + interval * 0.2 : interval;
        }
        slot.LastReceivedAt = now;
        long sequence = (slot.Sample?.Sequence ?? 0) + 1;
        slot.Sample = new TopicSample { Message = value["msg"], ReceivedAt = now, Sequence = sequence };
    }

    private void Complete(CarLinkValue value)
    {
        string id = value["id"].Kind == CarLinkValueKind.String ? value["id"].AsString() : null;
        if (id == null || !this.pending.TryRemove(id, out TaskCompletionSource<CarLinkValue> completion))
        {
            return;
        }
        bool succeeded = value["result"].Kind != CarLinkValueKind.Bool || value["result"].AsBool();
        if (succeeded)
        {
            completion.TrySetResult(value["values"]);
        }
        else
        {
            CarLinkValue values = value["values"];
            string reason = values.Kind == CarLinkValueKind.String ? values.AsString() : CarLinkJson.Write(values);
            completion.TrySetException(new CarLinkException($"{value["service"].Describe()} failed: {reason}"));
        }
    }

    private void FailPending(string reason)
    {
        foreach (string id in this.pending.Keys)
        {
            if (this.pending.TryRemove(id, out TaskCompletionSource<CarLinkValue> completion))
            {
                completion.TrySetException(new CarLinkException(reason));
            }
        }
    }

    private void SetState(CarLinkState next)
    {
        lock (this.gate)
        {
            this.state = (int)next;
        }
        this.QueueStateEvent(next);
    }

    private void QueueStateEvent(CarLinkState next)
    {
        this.events.Enqueue(() => this.StateChanged?.Invoke(next));
    }

    private void EnqueueLocked(CarLinkValue op)
    {
        this.outgoing.Enqueue(CarLinkJson.Write(op));
        this.drained.Reset();
        this.outgoingSignal.Release();
    }

    private string NextId()
    {
        return "racecarsim:" + Interlocked.Increment(ref this.nextId);
    }

    private CarLinkValue SubscribeOp(string topic, TopicOptions options)
    {
        List<(string, CarLinkValue)> fields = new List<(string, CarLinkValue)>
        {
            ("topic", CarLinkValue.FromString(topic)),
            ("queue_length", CarLinkValue.FromLong(Math.Max(1, options.QueueLength))),
        };
        if (!string.IsNullOrEmpty(options.Type))
        {
            fields.Add(("type", CarLinkValue.FromString(options.Type)));
        }
        if (options.ThrottleMs > 0)
        {
            fields.Add(("throttle_rate", CarLinkValue.FromLong(options.ThrottleMs)));
        }
        if (options.Cbor)
        {
            fields.Add(("compression", CarLinkValue.FromString("cbor")));
        }
        return RosBridgeClient.Op("subscribe", this.NextId(), fields.ToArray());
    }

    private CarLinkValue AdvertiseOp(string topic, string type)
    {
        return RosBridgeClient.Op("advertise", this.NextId(), ("topic", CarLinkValue.FromString(topic)), ("type", CarLinkValue.FromString(type)));
    }

    private static CarLinkValue Op(string op, string id, params (string key, CarLinkValue value)[] fields)
    {
        Dictionary<string, CarLinkValue> map = new Dictionary<string, CarLinkValue> { { "op", CarLinkValue.FromString(op) } };
        if (id != null)
        {
            map["id"] = CarLinkValue.FromString(id);
        }
        foreach ((string key, CarLinkValue value) in fields)
        {
            map[key] = value;
        }
        return CarLinkValue.FromMap(map);
    }

    private static string Describe(Exception exception)
    {
        Exception inner = exception;
        while (inner.InnerException != null && !(inner is TimeoutException))
        {
            inner = inner.InnerException;
        }
        return inner.Message;
    }
}
