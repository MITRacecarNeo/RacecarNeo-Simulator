using System;
using System.Collections.Generic;

/// <summary>
/// Where the followed car's pose comes from.
/// </summary>
public enum CarPoseSource
{
    None,

    /// <summary>
    /// The car's integrated odometry (odom frame), before a map exists.
    /// </summary>
    Odometry,

    /// <summary>
    /// The car's localization on its map (map frame).
    /// </summary>
    Map
}

/// <summary>
/// A planar car pose (base_link) in meters and radians, counterclockwise.
/// </summary>
public struct CarPose
{
    public double X;
    public double Y;
    public double Yaw;

    public CarPose(double x, double y, double yaw)
    {
        this.X = x;
        this.Y = y;
        this.Yaw = yaw;
    }
}

/// <summary>
/// Turns the car's pose messages into a smooth pose for the followed sim car: the map pose while
/// the car is localized and it is fresh, else odometry; interpolated between samples one sample
/// interval behind, so the shown pose passes through every received pose.
/// </summary>
public class CarPoseTracker
{
    #region Constants
    /// <summary>
    /// A source with no sample for this long (in seconds) is not used.
    /// </summary>
    public const double StaleAfter = 0.5;

    /// <summary>
    /// Longest render delay (in seconds), for slow or bursty sources.
    /// </summary>
    public const double MaxDelay = 0.25;

    /// <summary>
    /// Render delay beyond 1.5 sample intervals and the longest recent arrival lag (in seconds).
    /// </summary>
    public const double DelayMargin = 0.02;

    /// <summary>
    /// Fastest growth of the render delay (seconds per second): too short a delay holds the car
    /// still until the next sample, so it grows fast, showing the car up to 5% slow meanwhile.
    /// </summary>
    public const double DelayGrowth = 0.05;

    /// <summary>
    /// Fastest shrink of the render delay (seconds per second): the car shows up to 1% fast.
    /// </summary>
    public const double DelayShrink = 0.01;

    /// <summary>
    /// Samples kept per source.
    /// </summary>
    public const int Capacity = 16;
    #endregion

    #region Public Interface
    /// <summary>
    /// True while the car reports a map it is localized on (mapping or localization mode).
    /// </summary>
    public bool IsMapAvailable { get; set; }

    /// <summary>
    /// The source of the last sampled pose.
    /// </summary>
    public CarPoseSource Source { get; private set; }

    /// <summary>
    /// True when the last sample held an old pose because every source went quiet.
    /// </summary>
    public bool IsStale { get; private set; }

    /// <param name="time">Time of the pose (in seconds, the caller's clock).</param>
    /// <param name="arrivedAt">When it arrived, same clock; the gap sets the render delay. NaN when
    /// the time is the arrival.</param>
    public void AddMapPose(double time, double x, double y, double yaw, double arrivedAt = double.NaN)
    {
        CarPoseTracker.Add(this.map, this.mapLags, time, new CarPose(x, y, yaw), arrivedAt);
    }

    /// <param name="time">Time of the pose (in seconds, the caller's clock).</param>
    /// <param name="arrivedAt">When it arrived, same clock; NaN when the time is the arrival.</param>
    public void AddOdometry(double time, double x, double y, double yaw, double arrivedAt = double.NaN)
    {
        CarPoseTracker.Add(this.odometry, this.odometryLags, time, new CarPose(x, y, yaw), arrivedAt);
    }

    /// <summary>
    /// The pose to show at a time. False before any pose arrived.
    /// </summary>
    /// <param name="now">The current time (in seconds, the same clock as the samples).</param>
    public bool TrySample(double now, out CarPose pose)
    {
        List<(double time, CarPose pose)> samples = null;
        List<double> lags = null;
        CarPoseSource source = CarPoseSource.None;
        if (this.IsMapAvailable && CarPoseTracker.IsFresh(this.map, now))
        {
            samples = this.map;
            lags = this.mapLags;
            source = CarPoseSource.Map;
        }
        else if (CarPoseTracker.IsFresh(this.odometry, now))
        {
            samples = this.odometry;
            lags = this.odometryLags;
            source = CarPoseSource.Odometry;
        }

        if (samples == null)
        {
            this.IsStale = this.hasLast;
            pose = this.last;
            return this.hasLast;
        }

        // Enough behind the newest pose to cover one interval and the slowest recent arrival
        double lag = 0;
        foreach (double each in lags)
        {
            lag = Math.Max(lag, each);
        }
        double target = Math.Min(CarPoseTracker.MaxDelay, 1.5 * CarPoseTracker.MeanInterval(samples) + lag + CarPoseTracker.DelayMargin);
        if (double.IsNaN(this.delay))
        {
            this.delay = target;
        }
        else
        {
            double elapsed = Math.Max(0, now - this.lastSampleAt);
            this.delay += Math.Max(-CarPoseTracker.DelayShrink * elapsed, Math.Min(CarPoseTracker.DelayGrowth * elapsed, target - this.delay));
        }
        this.lastSampleAt = now;
        pose = CarPoseTracker.Interpolate(samples, now - this.delay);
        this.Source = source;
        this.IsStale = false;
        this.last = pose;
        this.hasLast = true;
        return true;
    }

    /// <summary>
    /// The pose at a past time from the same source choice as TrySample, without changing the
    /// tracker's state; for placing a sensor reading where the car was when it was taken.
    /// </summary>
    public bool TryPoseAt(double time, out CarPose pose)
    {
        List<(double time, CarPose pose)> samples = this.IsMapAvailable && this.map.Count > 0 ? this.map
            : (this.odometry.Count > 0 ? this.odometry : null);
        if (samples == null)
        {
            pose = default;
            return false;
        }
        pose = CarPoseTracker.Interpolate(samples, time);
        return true;
    }

    /// <summary>
    /// The pose at a time between samples (sorted by time); held at the ends.
    /// </summary>
    public static CarPose Interpolate(IReadOnlyList<(double time, CarPose pose)> samples, double time)
    {
        if (time <= samples[0].time)
        {
            return samples[0].pose;
        }
        for (int i = 1; i < samples.Count; i++)
        {
            if (time <= samples[i].time)
            {
                (double t0, CarPose a) = samples[i - 1];
                (double t1, CarPose b) = samples[i];
                double f = t1 > t0 ? (time - t0) / (t1 - t0) : 1;
                double turn = CarFrames.WrapAngle(b.Yaw - a.Yaw);
                return new CarPose(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, CarFrames.WrapAngle(a.Yaw + turn * f));
            }
        }
        return samples[samples.Count - 1].pose;
    }

    /// <summary>
    /// Forgets every sample (after a reconnect or a frame change on the car).
    /// </summary>
    public void Clear()
    {
        this.map.Clear();
        this.odometry.Clear();
        this.mapLags.Clear();
        this.odometryLags.Clear();
        this.Source = CarPoseSource.None;
        this.IsStale = false;
        this.hasLast = false;
        this.delay = double.NaN;
    }
    #endregion

    private readonly List<(double time, CarPose pose)> map = new List<(double time, CarPose pose)>();
    private readonly List<(double time, CarPose pose)> odometry = new List<(double time, CarPose pose)>();
    private readonly List<double> mapLags = new List<double>();
    private readonly List<double> odometryLags = new List<double>();
    private CarPose last;
    private bool hasLast;
    private double delay = double.NaN;
    private double lastSampleAt;

    private static void Add(List<(double time, CarPose pose)> samples, List<double> lags, double time, CarPose pose, double arrivedAt)
    {
        // A sample older than the newest (reordered or repeated) is dropped
        if (samples.Count > 0 && time <= samples[samples.Count - 1].time)
        {
            return;
        }
        samples.Add((time, pose));
        lags.Add(double.IsNaN(arrivedAt) ? 0 : Math.Max(0, arrivedAt - time));
        if (samples.Count > CarPoseTracker.Capacity)
        {
            samples.RemoveAt(0);
            lags.RemoveAt(0);
        }
    }

    private static bool IsFresh(List<(double time, CarPose pose)> samples, double now)
    {
        return samples.Count > 0 && now - samples[samples.Count - 1].time <= CarPoseTracker.StaleAfter;
    }

    private static double MeanInterval(List<(double time, CarPose pose)> samples)
    {
        if (samples.Count < 2)
        {
            return 0;
        }
        return (samples[samples.Count - 1].time - samples[0].time) / (samples.Count - 1);
    }
}
