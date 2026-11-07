using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// State of the link to a physical car.
/// </summary>
public enum CarLinkStatus
{
    /// <summary>
    /// Asking the car's API for its info and version.
    /// </summary>
    Checking,

    /// <summary>
    /// Opening the car's bridge.
    /// </summary>
    Connecting,

    Connected,

    /// <summary>
    /// The bridge dropped; retrying.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The first connection failed; FailureMessage says why.
    /// </summary>
    Failed,

    /// <summary>
    /// Without a network connection (tests).
    /// </summary>
    Offline
}

/// <summary>
/// The link to one physical car, kept across scene changes: the car's API and bridge, the clock
/// offset, the newest car state, and the pose tracker the followed sim car reads.
/// </summary>
/// <remarks>
/// Begin starts the connection; the menu waits for Connected or Failed, then loads the car world.
/// Close ends it. Nothing here sends a drive command. MapRun sends map commands only when the
/// panel asks.
/// </remarks>
public sealed class CarLinkSession : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Time allowed for the bridge to open after the API answered (in seconds).
    /// </summary>
    public const float BridgeTimeout = 6;

    /// <summary>
    /// Time between clock samples once synchronized (in seconds).
    /// </summary>
    public const float ClockInterval = 2;

    private const string poseTopic = "/autonomy/pose";
    private const string odometryTopic = "/odom/integrated";
    private const string statusTopic = "/autonomy/status";
    private const string muxTopic = "/mux/state";
    private const string infoTopic = "/racecar/info";
    private const string batteryTopic = "/battery/voltage";
    private const string mapTopic = "/map";
    private const string scanTopic = "/scan";
    private const string driveTopic = "/mux_out";

    /// <summary>
    /// Folder under the data directory for downloads from cars, one subfolder per car.
    /// </summary>
    public const string CarMapsFolder = "Car maps";
    #endregion

    #region Public Interface
    /// <summary>
    /// The open session, or null.
    /// </summary>
    public static CarLinkSession Current { get; private set; }

    public CarLinkTarget Target { get; private set; }

    public CarLinkStatus Status { get; private set; }

    /// <summary>
    /// Why the connection failed, for the connect screen; null otherwise.
    /// </summary>
    public string FailureMessage { get; private set; }

    /// <summary>
    /// The car's info from its API, updated from /racecar/info.
    /// </summary>
    public RacecarInfo Info { get; private set; }

    public RosBridgeClient Bridge { get; private set; }

    public LinkClock Clock { get; } = new LinkClock();

    public CarPoseTracker Tracker { get; } = new CarPoseTracker();

    /// <summary>
    /// The newest drive arbitration state, or null.
    /// </summary>
    public MuxState Mux { get; private set; }

    /// <summary>
    /// The newest autonomy state, or null.
    /// </summary>
    public AutonomyStatus Autonomy { get; private set; }

    /// <summary>
    /// The newest battery voltage (in volts), or null.
    /// </summary>
    public float? BatteryVoltage { get; private set; }

    /// <summary>
    /// The newest occupancy grid from the car's mapper, or null; MapVersion counts updates.
    /// </summary>
    public OccupancyGridMsg Map { get; private set; }

    public int MapVersion { get; private set; }

    /// <summary>
    /// The newest lidar scan, or null; ScanVersion counts updates.
    /// </summary>
    public LaserScanMsg Scan { get; private set; }

    /// <summary>
    /// Link time (RosBridgeClient.Now) when the newest scan arrived.
    /// </summary>
    public double ScanTime { get; private set; }

    public int ScanVersion { get; private set; }

    /// <summary>
    /// The newest integrated odometry, or null.
    /// </summary>
    public OdometryMsg Odometry { get; private set; }

    /// <summary>
    /// The newest drive command the car applied (/mux_out), or null.
    /// </summary>
    public DriveCmd AppliedDrive { get; private set; }

    /// <summary>
    /// Mapping controls through the car's API; null until connected and in offline sessions.
    /// </summary>
    public CarMapRun MapRun { get; private set; }

    /// <summary>
    /// Records the trajectory while the car maps the run started from here.
    /// </summary>
    public SessionRecorder Recorder { get; } = new SessionRecorder();

    /// <summary>
    /// Sets the newest map, as a received /map would (offline sessions and tests).
    /// </summary>
    public void SetMap(OccupancyGridMsg map)
    {
        this.Map = map;
        this.MapVersion++;
    }

    /// <summary>
    /// Sets the newest scan, as a received /scan would (offline sessions and tests).
    /// </summary>
    /// <param name="time">Link time the scan was taken (RosBridgeClient.Now).</param>
    public void SetScan(LaserScanMsg scan, double time)
    {
        this.Scan = scan;
        this.ScanTime = time;
        this.ScanVersion++;
    }

    /// <summary>
    /// Starts connecting to a car, closing any open session.
    /// </summary>
    public static CarLinkSession Begin(CarLinkTarget target)
    {
        CarLinkSession session = CarLinkSession.Create(target);
        session.Status = CarLinkStatus.Checking;
        session.StartCoroutine(session.Connect());
        return session;
    }

    /// <summary>
    /// A session without a network connection, whose tracker the caller feeds (tests).
    /// </summary>
    public static CarLinkSession BeginOffline(CarLinkTarget target)
    {
        CarLinkSession session = CarLinkSession.Create(target);
        session.Status = CarLinkStatus.Offline;
        return session;
    }

    /// <summary>
    /// Ends the session: the bridge sends what is queued and closes.
    /// </summary>
    public void Close()
    {
        this.Shutdown();
        if (CarLinkSession.Current == this)
        {
            CarLinkSession.Current = null;
        }
        GameObject.Destroy(this.gameObject);
    }
    #endregion

    private readonly Dictionary<string, long> seen = new Dictionary<string, long>();
    private readonly HashSet<string> warned = new HashSet<string>();
    private CarApiClient api;
    private Task<CarLinkValue> clockRequest;
    private double clockSentAt;
    private float nextClock;

    private void Update()
    {
        if (this.Bridge == null)
        {
            return;
        }
        this.Bridge.Poll();
        // The API is separate from the bridge: map commands and downloads go on while it reconnects
        this.MapRun?.Tick(RosBridgeClient.Now);

        if (this.Status == CarLinkStatus.Connected || this.Status == CarLinkStatus.Reconnecting)
        {
            CarLinkStatus next = this.Bridge.State == CarLinkState.Connected ? CarLinkStatus.Connected : CarLinkStatus.Reconnecting;
            if (next == CarLinkStatus.Reconnecting && this.Status == CarLinkStatus.Connected)
            {
                this.Clock.Reset();
            }
            this.Status = next;
        }
        if (this.Status == CarLinkStatus.Connected)
        {
            this.ReadTopics();
            this.SyncClock();
        }
        this.Record();
    }

    /// <summary>
    /// Opens the recording when the car starts the run begun here, adds a row while the pose is
    /// fresh, and closes it when the run ends.
    /// </summary>
    private void Record()
    {
        bool isOwnRun = this.MapRun != null && this.MapRun.IsOwnRun;
        if (isOwnRun && !this.Recorder.IsRecording)
        {
            this.Recorder.Begin(this.MapRun.MapFolder(this.MapRun.OwnedMap));
        }
        else if (!isOwnRun && this.Recorder.IsRecording && this.MapRun?.State != null && !this.MapRun.State.JobRunning)
        {
            this.Recorder.End();
        }
        double now = RosBridgeClient.Now;
        if (!this.Recorder.IsRecording || this.Status != CarLinkStatus.Connected || !this.Tracker.TrySample(now, out CarPose pose))
        {
            return;
        }
        this.Recorder.Record(now, new TrajectorySample
        {
            CarTime = this.Clock.IsSynchronized ? this.Clock.ToCarTime(now) : double.NaN,
            Pose = pose,
            PoseSource = this.Tracker.Source,
            Speed = this.Odometry?.LinearX ?? double.NaN,
            CommandSpeed = this.AppliedDrive?.Speed ?? double.NaN,
            CommandSteer = this.AppliedDrive?.SteeringAngle ?? double.NaN,
            DriveSource = this.Mux?.Source ?? MuxSource.Unknown,
            Localized = this.Autonomy?.Localized,
        });
    }

    private void OnDestroy()
    {
        this.Shutdown();
        if (CarLinkSession.Current == this)
        {
            CarLinkSession.Current = null;
        }
    }

    private static CarLinkSession Create(CarLinkTarget target)
    {
        CarLinkSession.Current?.Close();
        GameObject holder = new GameObject("CarLinkSession");
        GameObject.DontDestroyOnLoad(holder);
        CarLinkSession session = holder.AddComponent<CarLinkSession>();
        session.Target = target;
        CarLinkSession.Current = session;
        return session;
    }

    private IEnumerator Connect()
    {
        this.api = new CarApiClient(this.Target.Host, this.Target.ApiPort);
        Task<RacecarInfo> info = this.api.GetInfoAsync();
        yield return new WaitUntil(() => info.IsCompleted);
        if (info.IsFaulted)
        {
            this.Fail(CarLinkSession.Describe(info.Exception?.InnerException, this.Target));
            yield break;
        }
        this.Info = info.Result;
        if (!this.Info.IsCompatible)
        {
            this.Fail(this.Info.Major > RacecarInfo.SupportedMajor
                ? "This car needs a newer simulator."
                : "This car's software is too old for this simulator.");
            yield break;
        }

        this.Status = CarLinkStatus.Connecting;
        this.Bridge = new RosBridgeClient();
        this.Bridge.Subscribe(CarLinkSession.infoTopic, new TopicOptions { Type = "std_msgs/msg/String" });
        this.Bridge.Subscribe(CarLinkSession.muxTopic, new TopicOptions { Type = "std_msgs/msg/String" });
        this.Bridge.Subscribe(CarLinkSession.statusTopic, new TopicOptions { Type = "diagnostic_msgs/msg/DiagnosticStatus" });
        this.Bridge.Subscribe(CarLinkSession.poseTopic, new TopicOptions { Type = "geometry_msgs/msg/PoseWithCovarianceStamped" });
        this.Bridge.Subscribe(CarLinkSession.odometryTopic, new TopicOptions { Type = "nav_msgs/msg/Odometry", ThrottleMs = 20 });
        this.Bridge.Subscribe(CarLinkSession.batteryTopic, new TopicOptions { Type = "std_msgs/msg/Float32", ThrottleMs = 1000 });
        this.Bridge.Subscribe(CarLinkSession.mapTopic, new TopicOptions { Type = "nav_msgs/msg/OccupancyGrid", Cbor = true });
        this.Bridge.Subscribe(CarLinkSession.scanTopic, new TopicOptions { Type = "sensor_msgs/msg/LaserScan", Cbor = true });
        this.Bridge.Subscribe(CarLinkSession.driveTopic, new TopicOptions { Type = "ackermann_msgs/msg/AckermannDriveStamped", ThrottleMs = 50 });
        this.Bridge.Connect(this.Target.BridgeUri);

        float deadline = Time.unscaledTime + CarLinkSession.BridgeTimeout;
        yield return new WaitUntil(() => this.Bridge.State == CarLinkState.Connected || Time.unscaledTime > deadline);
        if (this.Bridge.State != CarLinkState.Connected)
        {
            this.Fail($"The car's bridge did not answer on port {this.Target.BridgePort}.");
            yield break;
        }
        this.Target.Name = this.Info.Hostname ?? string.Empty;
        string carFolder = Path.Combine(SavedDataManager.DataDirectory, CarLinkSession.CarMapsFolder,
            CarMapRun.CarFolderName(string.IsNullOrEmpty(this.Info.Hostname) ? this.Target.Host : this.Info.Hostname));
        this.MapRun = new CarMapRun(this.api, carFolder);
        this.Status = CarLinkStatus.Connected;
    }

    private void Fail(string message)
    {
        this.FailureMessage = message;
        this.Status = CarLinkStatus.Failed;
        this.Shutdown();
    }

    private void Shutdown()
    {
        this.Recorder.End();
        this.MapRun?.Cancel();
        this.MapRun = null;
        this.Bridge?.Disconnect();
        this.Bridge = null;
        this.api?.Dispose();
        this.api = null;
    }

    /// <summary>
    /// A short reason for a failed API call, for the connect screen.
    /// </summary>
    public static string Describe(Exception exception, CarLinkTarget target)
    {
        if (!(exception is CarApiException failure))
        {
            return $"Could not connect to {target.Host}.";
        }
        switch (failure.Failure)
        {
            case CarApiFailure.NoRoute:
                return $"Cannot reach {target.Host}. Join the car's network.";
            case CarApiFailure.Refused:
                return $"The car link is off on {target.Host}. Turn it on from the car's home page.";
            case CarApiFailure.Timeout:
                return $"No answer from {target.Host}.";
            case CarApiFailure.Format:
                return "Simulator could not read car's reply";
            default:
                return $"The car returned an error ({failure.StatusCode}).";
        }
    }

    private void ReadTopics()
    {
        if (this.TryRead(CarLinkSession.infoTopic, out CarLinkValue info))
        {
            this.Parse(CarLinkSession.infoTopic, () => this.Info = RacecarInfo.Parse(CarLinkJson.Parse(info["data"].AsString())));
        }
        if (this.TryRead(CarLinkSession.muxTopic, out CarLinkValue mux))
        {
            this.Parse(CarLinkSession.muxTopic, () => this.Mux = MuxState.Parse(CarLinkJson.Parse(mux["data"].AsString())));
        }
        if (this.TryRead(CarLinkSession.statusTopic, out CarLinkValue status))
        {
            this.Parse(CarLinkSession.statusTopic, () =>
            {
                this.Autonomy = AutonomyStatus.Parse(status);
                bool hasMap = this.Autonomy.Mode == "mapping" || this.Autonomy.Mode == "localization";
                this.Tracker.IsMapAvailable = hasMap && this.Autonomy.Localized != false;
            });
        }
        if (this.TryRead(CarLinkSession.batteryTopic, out CarLinkValue battery))
        {
            this.Parse(CarLinkSession.batteryTopic, () => this.BatteryVoltage = Float32Msg.Parse(battery).Data);
        }
        if (this.TryRead(CarLinkSession.poseTopic, out CarLinkValue pose, out double poseTime))
        {
            this.Parse(CarLinkSession.poseTopic, () =>
            {
                PoseStamped2D parsed = PoseStamped2D.Parse(pose);
                this.Tracker.AddMapPose(CarLinkSession.SampleTime(parsed.Stamp, poseTime, this.Clock), parsed.X, parsed.Y, parsed.Yaw, poseTime);
            });
        }
        if (this.TryRead(CarLinkSession.mapTopic, out CarLinkValue map))
        {
            this.Parse(CarLinkSession.mapTopic, () => this.SetMap(OccupancyGridMsg.Parse(map)));
        }
        if (this.TryRead(CarLinkSession.scanTopic, out CarLinkValue scan, out double scanTime))
        {
            this.Parse(CarLinkSession.scanTopic, () =>
            {
                LaserScanMsg parsed = LaserScanMsg.Parse(scan);
                this.SetScan(parsed, CarLinkSession.SampleTime(parsed.Stamp, scanTime, this.Clock));
            });
        }
        if (this.TryRead(CarLinkSession.odometryTopic, out CarLinkValue odometry, out double odometryTime))
        {
            this.Parse(CarLinkSession.odometryTopic, () =>
            {
                OdometryMsg parsed = OdometryMsg.Parse(odometry);
                this.Odometry = parsed;
                this.Tracker.AddOdometry(CarLinkSession.SampleTime(parsed.Stamp, odometryTime, this.Clock), parsed.X, parsed.Y, parsed.Yaw, odometryTime);
            });
        }
        if (this.TryRead(CarLinkSession.driveTopic, out CarLinkValue drive))
        {
            this.Parse(CarLinkSession.driveTopic, () => this.AppliedDrive = DriveCmd.Parse(drive));
        }
    }

    /// <summary>
    /// When a message was taken, in link time: its car stamp through the synchronized clock, else
    /// when it arrived. Arrival times carry the network's jitter, which shows as the followed car
    /// speeding up and slowing down; stamps are as evenly spaced as the car sent them. A stamp
    /// mapped after its arrival is held at the arrival; one more than a second older than its
    /// arrival points at a clock problem, and the arrival is used.
    /// </summary>
    public static double SampleTime(double stamp, double receivedAt, LinkClock clock)
    {
        if (!clock.IsSynchronized || stamp <= 0)
        {
            return receivedAt;
        }
        double time = clock.ToSimTime(stamp);
        return receivedAt - time > 1 ? receivedAt : Math.Min(time, receivedAt);
    }

    private bool TryRead(string topic, out CarLinkValue message)
    {
        return this.TryRead(topic, out message, out double _);
    }

    /// <summary>
    /// The newest message on a topic if it is new since the last read.
    /// </summary>
    private bool TryRead(string topic, out CarLinkValue message, out double receivedAt)
    {
        message = CarLinkValue.Null;
        receivedAt = 0;
        if (!this.Bridge.TryGetLatest(topic, out TopicSample sample)
            || (this.seen.TryGetValue(topic, out long last) && last == sample.Sequence))
        {
            return false;
        }
        this.seen[topic] = sample.Sequence;
        message = sample.Message;
        receivedAt = sample.ReceivedAt;
        return true;
    }

    private void Parse(string topic, Action parse)
    {
        try
        {
            parse();
        }
        catch (CarLinkFormatException exception)
        {
            if (this.warned.Add(topic))
            {
                Debug.LogWarning($"Car link: cannot read {topic}: {exception.Message}");
            }
        }
    }

    private void SyncClock()
    {
        if (this.clockRequest != null)
        {
            if (!this.clockRequest.IsCompleted)
            {
                return;
            }
            if (this.clockRequest.Status == TaskStatus.RanToCompletion)
            {
                try
                {
                    this.Clock.AddSample(this.clockSentAt, CarLinkFields.Stamp(this.clockRequest.Result, "time"), RosBridgeClient.Now);
                }
                catch (CarLinkFormatException exception)
                {
                    Debug.LogWarning($"Car link: cannot read the car's time: {exception.Message}");
                }
            }
            else
            {
                // Observed, so a timeout during a reconnect is not reported as unobserved
                _ = this.clockRequest.Exception;
            }
            this.clockRequest = null;
        }
        if (Time.unscaledTime < this.nextClock)
        {
            return;
        }
        // Quick samples until synchronized, then a slow refresh
        this.nextClock = Time.unscaledTime + (this.Clock.IsSynchronized ? CarLinkSession.ClockInterval : 0.2f);
        this.clockSentAt = RosBridgeClient.Now;
        this.clockRequest = this.Bridge.CallService("/rosapi/get_time", CarLinkValue.Null, 2);
    }
}
