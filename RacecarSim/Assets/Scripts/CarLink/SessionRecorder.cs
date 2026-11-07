using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// One trajectory row: the car's pose, speed, applied command, and drive source at one time.
/// </summary>
public struct TrajectorySample
{
    /// <summary>
    /// Car clock time (in seconds), or NaN before the clocks are synchronized.
    /// </summary>
    public double CarTime;

    public CarPose Pose;
    public CarPoseSource PoseSource;

    /// <summary>
    /// Forward speed from the car's odometry (in m/s), or NaN.
    /// </summary>
    public double Speed;

    /// <summary>
    /// The drive command the car applied (/mux_out; speed and steering in [-1, 1], positive
    /// steering left), or NaN.
    /// </summary>
    public double CommandSpeed;
    public double CommandSteer;

    public MuxSource DriveSource;
    public bool? Localized;
}

/// <summary>
/// Writes the car's trajectory during a mapping run to trajectory.csv beside the run's downloads.
/// </summary>
/// <remarks>
/// Rows are flushed as written, so a run cut short keeps what was recorded. Opening a file
/// replaces an earlier recording of the same map.
/// </remarks>
public sealed class SessionRecorder : IDisposable
{
    #region Constants
    /// <summary>
    /// Time between rows (in seconds).
    /// </summary>
    public const double Interval = 0.1;

    public const string FileName = "trajectory.csv";

    public const string Header = "car_time,x,y,yaw,pose_source,speed,command_speed,command_steer,drive_source,localized";
    #endregion

    #region Public Interface
    /// <summary>
    /// The file being written, or null.
    /// </summary>
    public string Path { get; private set; }

    public int RowCount { get; private set; }

    public bool IsRecording { get { return this.writer != null; } }

    /// <summary>
    /// Starts a recording in a folder, closing any open one.
    /// </summary>
    public void Begin(string folder)
    {
        this.End();
        Directory.CreateDirectory(folder);
        this.Path = System.IO.Path.Combine(folder, SessionRecorder.FileName);
        this.writer = new StreamWriter(this.Path, false, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        this.writer.WriteLine(SessionRecorder.Header);
        this.RowCount = 0;
        this.nextRow = double.NegativeInfinity;
    }

    /// <summary>
    /// Writes a row if Interval has passed since the last one.
    /// </summary>
    /// <param name="now">Link time (RosBridgeClient.Now).</param>
    public void Record(double now, TrajectorySample sample)
    {
        if (this.writer == null || now < this.nextRow)
        {
            return;
        }
        this.nextRow = now + SessionRecorder.Interval;
        this.writer.WriteLine(SessionRecorder.Row(sample));
        this.RowCount++;
    }

    public void End()
    {
        this.writer?.Dispose();
        this.writer = null;
    }

    public void Dispose()
    {
        this.End();
    }

    /// <summary>
    /// One CSV row; unknown values are empty.
    /// </summary>
    public static string Row(TrajectorySample sample)
    {
        return string.Join(",",
            SessionRecorder.Number(sample.CarTime, "0.000"),
            SessionRecorder.Number(sample.Pose.X, "0.0000"),
            SessionRecorder.Number(sample.Pose.Y, "0.0000"),
            SessionRecorder.Number(sample.Pose.Yaw, "0.0000"),
            sample.PoseSource.ToString().ToLowerInvariant(),
            SessionRecorder.Number(sample.Speed, "0.000"),
            SessionRecorder.Number(sample.CommandSpeed, "0.000"),
            SessionRecorder.Number(sample.CommandSteer, "0.0000"),
            sample.DriveSource.ToString().ToLowerInvariant(),
            sample.Localized.HasValue ? (sample.Localized.Value ? "1" : "0") : string.Empty);
    }
    #endregion

    private StreamWriter writer;
    private double nextRow;

    private static string Number(double value, string format)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? string.Empty : value.ToString(format, CultureInfo.InvariantCulture);
    }
}
