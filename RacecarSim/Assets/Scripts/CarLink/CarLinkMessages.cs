using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Field access for car link messages that names the dotted path of a missing or mistyped field.
/// </summary>
public static class CarLinkFields
{
    public static CarLinkValue Required(CarLinkValue root, string path)
    {
        CarLinkValue value = root.At(path);
        if (value.IsNull)
        {
            throw new CarLinkFormatException($"missing field '{path}'");
        }
        return value;
    }

    public static double Double(CarLinkValue root, string path)
    {
        return CarLinkFields.Convert(root, path, v => v.AsDouble());
    }

    public static float Float(CarLinkValue root, string path)
    {
        return CarLinkFields.Convert(root, path, v => v.AsFloat());
    }

    public static int Int(CarLinkValue root, string path)
    {
        return CarLinkFields.Convert(root, path, v => v.AsInt());
    }

    public static bool Bool(CarLinkValue root, string path)
    {
        return CarLinkFields.Convert(root, path, v => v.AsBool());
    }

    public static string String(CarLinkValue root, string path)
    {
        return CarLinkFields.Convert(root, path, v => v.AsString());
    }

    /// <summary>
    /// The number at the path, or null when the field is missing or null.
    /// </summary>
    public static double? OptionalDouble(CarLinkValue root, string path)
    {
        CarLinkValue value = root.At(path);
        return value.IsNull ? (double?)null : CarLinkFields.Double(root, path);
    }

    public static int? OptionalInt(CarLinkValue root, string path)
    {
        CarLinkValue value = root.At(path);
        return value.IsNull ? (int?)null : CarLinkFields.Int(root, path);
    }

    public static bool? OptionalBool(CarLinkValue root, string path)
    {
        CarLinkValue value = root.At(path);
        return value.IsNull ? (bool?)null : CarLinkFields.Bool(root, path);
    }

    public static string OptionalString(CarLinkValue root, string path)
    {
        CarLinkValue value = root.At(path);
        return value.IsNull ? null : CarLinkFields.String(root, path);
    }

    /// <summary>
    /// A ROS time (sec and nanosec fields) in seconds.
    /// </summary>
    public static double Stamp(CarLinkValue root, string path)
    {
        return CarLinkFields.Double(root, path + ".sec") + CarLinkFields.Double(root, path + ".nanosec") * 1e-9;
    }

    /// <summary>
    /// Yaw (in radians, counterclockwise about +z) of a geometry_msgs Quaternion.
    /// </summary>
    public static double Yaw(CarLinkValue root, string path)
    {
        double x = CarLinkFields.Double(root, path + ".x");
        double y = CarLinkFields.Double(root, path + ".y");
        double z = CarLinkFields.Double(root, path + ".z");
        double w = CarLinkFields.Double(root, path + ".w");
        return CarFrames.QuaternionToYaw(x, y, z, w);
    }

    /// <summary>
    /// A ROS time value (sec and nanosec) for a time in seconds.
    /// </summary>
    public static CarLinkValue StampValue(double seconds)
    {
        long sec = (long)Math.Floor(seconds);
        long nanosec = Math.Min(999999999, (long)Math.Round((seconds - sec) * 1e9));
        return CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>
        {
            { "sec", CarLinkValue.FromLong(sec) },
            { "nanosec", CarLinkValue.FromLong(nanosec) },
        });
    }

    private static T Convert<T>(CarLinkValue root, string path, Func<CarLinkValue, T> read)
    {
        CarLinkValue value = CarLinkFields.Required(root, path);
        try
        {
            return read(value);
        }
        catch (CarLinkFormatException exception)
        {
            throw new CarLinkFormatException($"field '{path}': {exception.Message}");
        }
    }
}

/// <summary>
/// The car's identity, configuration, and calibration (/racecar/info and GET /api/v1/info).
/// </summary>
public class RacecarInfo
{
    /// <summary>
    /// The link interface major version this build of the sim reads.
    /// </summary>
    public const int SupportedMajor = 1;

    public string LinkInterface;
    public int Major;
    public int Minor;
    public string DriverVersion;
    public string Hostname;
    public int BridgePort;
    public int ApiPort;

    /// <summary>
    /// The info as received, kept with captured worlds as the car's profile snapshot.
    /// </summary>
    public CarLinkValue Source = CarLinkValue.Null;

    /// <summary>
    /// Firmware speed floor, ceiling, and slow-mode speed (in m/s) as configured on the car.
    /// </summary>
    public double? FirmwareMinSpeed;
    public double? FirmwareMaxSpeed;
    public double? FirmwareSlowSpeed;
    public double? FirmwareDeadband;
    public double? FirmwareSetpointOffset;

    public int? LidarSamples;
    public double? LidarAngleMin;
    public double? LidarAngleMax;
    public double? LidarRangeMin;
    public double? LidarRangeMax;
    public double? LidarRateHz;

    /// <summary>
    /// Sensor offsets in base_link (in millimeters and degrees), keyed as on the car
    /// (for example "laser_x_mm", "laser_yaw_deg").
    /// </summary>
    public Dictionary<string, double?> Frames = new Dictionary<string, double?>();

    /// <summary>
    /// The calibration block as sent; read by the car profile.
    /// </summary>
    public CarLinkValue Calibration = CarLinkValue.Null;

    /// <summary>
    /// Speed caps as fractions of full scale; null when off.
    /// </summary>
    public double? MappingSpeedCap;
    public double? SimSpeedCap;
    public double? SpeedCap;

    public bool TwinAvailable;

    /// <summary>
    /// True when this sim can read the car's link interface (same major version).
    /// </summary>
    public bool IsCompatible { get { return this.Major == RacecarInfo.SupportedMajor; } }

    public static RacecarInfo Parse(CarLinkValue value)
    {
        RacecarInfo info = new RacecarInfo();
        info.Source = value;
        info.LinkInterface = CarLinkFields.String(value, "link_interface");
        string[] parts = info.LinkInterface.Split('.');
        if (parts.Length < 1 || !int.TryParse(parts[0], out info.Major))
        {
            throw new CarLinkFormatException($"field 'link_interface': bad version '{info.LinkInterface}'");
        }
        if (parts.Length > 1)
        {
            int.TryParse(parts[1], out info.Minor);
        }
        info.DriverVersion = CarLinkFields.OptionalString(value, "driver_version");
        info.Hostname = CarLinkFields.OptionalString(value, "hostname");
        info.BridgePort = CarLinkFields.OptionalInt(value, "ports.bridge") ?? 9090;
        info.ApiPort = CarLinkFields.OptionalInt(value, "ports.api") ?? 8084;

        info.FirmwareMinSpeed = CarLinkFields.OptionalDouble(value, "firmware.min_speed_mps");
        info.FirmwareMaxSpeed = CarLinkFields.OptionalDouble(value, "firmware.max_speed_mps");
        info.FirmwareSlowSpeed = CarLinkFields.OptionalDouble(value, "firmware.slow_speed_mps");
        info.FirmwareDeadband = CarLinkFields.OptionalDouble(value, "firmware.deadband");
        info.FirmwareSetpointOffset = CarLinkFields.OptionalDouble(value, "firmware.setpoint_offset_mps");

        info.LidarSamples = CarLinkFields.OptionalInt(value, "lidar.samples");
        info.LidarAngleMin = CarLinkFields.OptionalDouble(value, "lidar.angle_min");
        info.LidarAngleMax = CarLinkFields.OptionalDouble(value, "lidar.angle_max");
        info.LidarRangeMin = CarLinkFields.OptionalDouble(value, "lidar.range_min");
        info.LidarRangeMax = CarLinkFields.OptionalDouble(value, "lidar.range_max");
        info.LidarRateHz = CarLinkFields.OptionalDouble(value, "lidar.rate_hz");

        foreach (KeyValuePair<string, CarLinkValue> entry in value["frames"].Entries)
        {
            info.Frames[entry.Key] = entry.Value.IsNull ? (double?)null : CarLinkFields.Double(value, "frames." + entry.Key);
        }
        info.Calibration = value["calibration"];

        info.MappingSpeedCap = CarLinkFields.OptionalDouble(value, "caps.mapping_speed_cap");
        info.SimSpeedCap = CarLinkFields.OptionalDouble(value, "caps.sim_speed_cap");
        info.SpeedCap = CarLinkFields.OptionalDouble(value, "caps.speed_cap");
        info.TwinAvailable = CarLinkFields.OptionalBool(value, "twin.available") ?? false;
        return info;
    }

    /// <summary>
    /// A frame offset by key, or a fallback when the car does not report it.
    /// </summary>
    public double Frame(string key, double fallback)
    {
        return this.Frames.TryGetValue(key, out double? value) && value.HasValue ? value.Value : fallback;
    }

    /// <summary>
    /// The firmware's setpoint (in m/s) for a normalized speed command, from the configured floor,
    /// deadband, and ceiling; 0 inside the deadband. Null when the car does not report them.
    /// </summary>
    public double? SetpointFor(double command)
    {
        if (!this.FirmwareMinSpeed.HasValue || !this.FirmwareMaxSpeed.HasValue)
        {
            return null;
        }
        double deadband = this.FirmwareDeadband ?? 0.01;
        double magnitude = Math.Abs(command);
        if (magnitude <= deadband)
        {
            return 0;
        }
        magnitude = Math.Min(1, magnitude);
        double min = this.FirmwareMinSpeed.Value;
        double max = this.FirmwareMaxSpeed.Value;
        return Math.Sign(command) * (min + (magnitude - deadband) / (1 - deadband) * (max - min));
    }
}

/// <summary>
/// What the car's drive mux forwards.
/// </summary>
public enum MuxSource
{
    Unknown,
    Idle,
    Gamepad,
    Autonomy,
    Sim
}

/// <summary>
/// The car's drive arbitration state (/mux/state).
/// </summary>
public class MuxState
{
    public MuxSource Source;

    /// <summary>
    /// True when a client advertises /sim_drive.
    /// </summary>
    public bool Twin;

    public int SimPublishers;
    public bool Rb;
    public bool Lb;

    /// <summary>
    /// True while the car holds zero after a source change under RB, until RB is released.
    /// </summary>
    public bool RbWaitRelease;

    public bool GuardActive;
    public double? GuardRange;

    /// <summary>
    /// Caps as fractions of full scale; null when off. Applied is the cap on the current source.
    /// </summary>
    public double? CapSim;
    public double? CapSpeed;
    public double? CapMapping;
    public double? CapApplied;

    /// <summary>
    /// True when the car reports an RC transmitter block (absent while it is not part of the system).
    /// </summary>
    public bool HasRc;

    /// <summary>
    /// The car's system time (in seconds).
    /// </summary>
    public double Stamp;

    public static MuxState Parse(CarLinkValue value)
    {
        MuxState state = new MuxState();
        state.Source = MuxState.ParseSource(CarLinkFields.String(value, "source"));
        state.Twin = CarLinkFields.OptionalBool(value, "twin") ?? false;
        state.SimPublishers = CarLinkFields.OptionalInt(value, "sim_publishers") ?? 0;
        state.Rb = CarLinkFields.OptionalBool(value, "rb") ?? false;
        state.Lb = CarLinkFields.OptionalBool(value, "lb") ?? false;
        state.RbWaitRelease = CarLinkFields.OptionalBool(value, "rb_wait_release") ?? false;
        state.GuardActive = CarLinkFields.OptionalBool(value, "guard.active") ?? false;
        state.GuardRange = CarLinkFields.OptionalDouble(value, "guard.range_m");
        state.CapSim = CarLinkFields.OptionalDouble(value, "cap.sim");
        state.CapSpeed = CarLinkFields.OptionalDouble(value, "cap.speed");
        state.CapMapping = CarLinkFields.OptionalDouble(value, "cap.mapping");
        state.CapApplied = CarLinkFields.OptionalDouble(value, "cap.applied");
        state.HasRc = !value["rc"].IsNull;
        state.Stamp = CarLinkFields.OptionalDouble(value, "stamp") ?? 0;
        return state;
    }

    public static MuxSource ParseSource(string source)
    {
        switch (source)
        {
            case "idle":
                return MuxSource.Idle;
            case "gamepad":
                return MuxSource.Gamepad;
            case "autonomy":
                return MuxSource.Autonomy;
            case "sim":
                return MuxSource.Sim;
            default:
                return MuxSource.Unknown;
        }
    }
}

/// <summary>
/// The car's autonomy state (/autonomy/status, a diagnostic_msgs DiagnosticStatus).
/// </summary>
public class AutonomyStatus
{
    public int Level;
    public string Message;
    public Dictionary<string, string> Values = new Dictionary<string, string>();

    /// <summary>
    /// "mapping", "localization", or "off".
    /// </summary>
    public string Mode;

    public string Map;
    public bool? Localized;

    public static AutonomyStatus Parse(CarLinkValue value)
    {
        AutonomyStatus status = new AutonomyStatus();
        status.Level = CarLinkFields.OptionalInt(value, "level") ?? 0;
        status.Message = CarLinkFields.OptionalString(value, "message");
        foreach (CarLinkValue pair in value["values"].Items)
        {
            string key = CarLinkFields.String(pair, "key");
            status.Values[key] = CarLinkFields.OptionalString(pair, "value") ?? string.Empty;
        }
        status.Mode = status.Values.TryGetValue("mode", out string mode) ? mode : null;
        status.Map = status.Values.TryGetValue("map", out string map) ? map : null;
        if (status.Values.TryGetValue("localized", out string localized))
        {
            status.Localized = localized.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        return status;
    }
}

/// <summary>
/// A planar pose with time and covariance (geometry_msgs PoseWithCovarianceStamped).
/// </summary>
public class PoseStamped2D
{
    public double Stamp;
    public string FrameId;

    /// <summary>
    /// Position (in meters) and heading (in radians, counterclockwise) in FrameId.
    /// </summary>
    public double X;
    public double Y;
    public double Yaw;

    /// <summary>
    /// Row-major 6 x 6 covariance (x, y, z, roll, pitch, yaw), or null when not sent.
    /// </summary>
    public double[] Covariance;

    public static PoseStamped2D Parse(CarLinkValue value)
    {
        PoseStamped2D pose = new PoseStamped2D();
        pose.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        pose.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        pose.X = CarLinkFields.Double(value, "pose.pose.position.x");
        pose.Y = CarLinkFields.Double(value, "pose.pose.position.y");
        pose.Yaw = CarLinkFields.Yaw(value, "pose.pose.orientation");
        CarLinkValue covariance = value.At("pose.covariance");
        pose.Covariance = covariance.IsNull ? null : covariance.AsDoubles();
        return pose;
    }
}

/// <summary>
/// A 2D occupancy grid (nav_msgs OccupancyGrid): -1 unknown, 0 to 100 occupancy, row 0 at the
/// origin, rows along +y.
/// </summary>
public class OccupancyGridMsg
{
    public double Stamp;
    public string FrameId;

    /// <summary>
    /// Cell size (in meters).
    /// </summary>
    public float Resolution;

    public int Width;
    public int Height;

    /// <summary>
    /// Pose of cell (0, 0)'s corner in FrameId (in meters and radians).
    /// </summary>
    public double OriginX;
    public double OriginY;
    public double OriginYaw;

    public sbyte[] Data;

    public static OccupancyGridMsg Parse(CarLinkValue value)
    {
        OccupancyGridMsg grid = new OccupancyGridMsg();
        grid.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        grid.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        grid.Resolution = CarLinkFields.Float(value, "info.resolution");
        grid.Width = CarLinkFields.Int(value, "info.width");
        grid.Height = CarLinkFields.Int(value, "info.height");
        grid.OriginX = CarLinkFields.Double(value, "info.origin.position.x");
        grid.OriginY = CarLinkFields.Double(value, "info.origin.position.y");
        grid.OriginYaw = CarLinkFields.Yaw(value, "info.origin.orientation");
        try
        {
            grid.Data = CarLinkFields.Required(value, "data").AsSBytes();
        }
        catch (CarLinkFormatException exception)
        {
            throw new CarLinkFormatException($"field 'data': {exception.Message}");
        }
        if (grid.Width < 0 || grid.Height < 0 || (long)grid.Width * grid.Height != grid.Data.Length)
        {
            throw new CarLinkFormatException($"grid {grid.Width} x {grid.Height} does not match {grid.Data.Length} cells");
        }
        if (!(grid.Resolution > 0))
        {
            throw new CarLinkFormatException($"grid resolution {grid.Resolution} is not positive");
        }
        return grid;
    }

    /// <summary>
    /// The cell value at a column and row; -1 off the grid.
    /// </summary>
    public sbyte At(int column, int row)
    {
        if (column < 0 || row < 0 || column >= this.Width || row >= this.Height)
        {
            return -1;
        }
        return this.Data[row * this.Width + column];
    }
}

/// <summary>
/// One lidar sweep (sensor_msgs LaserScan), ranges in meters, angles in radians counterclockwise.
/// </summary>
public class LaserScanMsg
{
    public double Stamp;
    public string FrameId;
    public float AngleMin;
    public float AngleMax;
    public float AngleIncrement;
    public float TimeIncrement;
    public float ScanTime;
    public float RangeMin;
    public float RangeMax;
    public float[] Ranges;
    public float[] Intensities;

    public static LaserScanMsg Parse(CarLinkValue value)
    {
        LaserScanMsg scan = new LaserScanMsg();
        scan.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        scan.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        scan.AngleMin = CarLinkFields.Float(value, "angle_min");
        scan.AngleMax = CarLinkFields.Float(value, "angle_max");
        scan.AngleIncrement = CarLinkFields.Float(value, "angle_increment");
        scan.TimeIncrement = (float)(CarLinkFields.OptionalDouble(value, "time_increment") ?? 0);
        scan.ScanTime = (float)(CarLinkFields.OptionalDouble(value, "scan_time") ?? 0);
        scan.RangeMin = CarLinkFields.Float(value, "range_min");
        scan.RangeMax = CarLinkFields.Float(value, "range_max");
        try
        {
            scan.Ranges = CarLinkFields.Required(value, "ranges").AsFloats();
            CarLinkValue intensities = value["intensities"];
            scan.Intensities = intensities.IsNull ? new float[0] : intensities.AsFloats();
        }
        catch (CarLinkFormatException exception)
        {
            throw new CarLinkFormatException($"scan arrays: {exception.Message}");
        }
        return scan;
    }

    /// <summary>
    /// True when a range is a return the sensor stands behind (finite and within its limits).
    /// </summary>
    public bool IsValid(int index)
    {
        float range = this.Ranges[index];
        return !float.IsNaN(range) && !float.IsInfinity(range) && range >= this.RangeMin && range <= this.RangeMax;
    }
}

/// <summary>
/// Odometry (nav_msgs Odometry): planar pose in FrameId, velocity in ChildFrameId.
/// </summary>
public class OdometryMsg
{
    public double Stamp;
    public string FrameId;
    public string ChildFrameId;
    public double X;
    public double Y;
    public double Yaw;
    public double LinearX;
    public double LinearY;
    public double AngularZ;

    public static OdometryMsg Parse(CarLinkValue value)
    {
        OdometryMsg odometry = new OdometryMsg();
        odometry.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        odometry.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        odometry.ChildFrameId = CarLinkFields.OptionalString(value, "child_frame_id");
        odometry.X = CarLinkFields.Double(value, "pose.pose.position.x");
        odometry.Y = CarLinkFields.Double(value, "pose.pose.position.y");
        odometry.Yaw = CarLinkFields.Yaw(value, "pose.pose.orientation");
        odometry.LinearX = CarLinkFields.Double(value, "twist.twist.linear.x");
        odometry.LinearY = CarLinkFields.Double(value, "twist.twist.linear.y");
        odometry.AngularZ = CarLinkFields.Double(value, "twist.twist.angular.z");
        return odometry;
    }
}

/// <summary>
/// An IMU sample (sensor_msgs Imu) in REP-103 body axes: angular velocity in rad/s, linear
/// acceleration in m/s^2.
/// </summary>
public class ImuMsg
{
    public double Stamp;
    public string FrameId;
    public Vector3 AngularVelocity;
    public Vector3 LinearAcceleration;

    public static ImuMsg Parse(CarLinkValue value)
    {
        ImuMsg imu = new ImuMsg();
        imu.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        imu.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        imu.AngularVelocity = ImuMsg.Vector(value, "angular_velocity");
        imu.LinearAcceleration = ImuMsg.Vector(value, "linear_acceleration");
        return imu;
    }

    private static Vector3 Vector(CarLinkValue value, string path)
    {
        return new Vector3(
            CarLinkFields.Float(value, path + ".x"),
            CarLinkFields.Float(value, path + ".y"),
            CarLinkFields.Float(value, path + ".z"));
    }
}

/// <summary>
/// A normalized drive command (ackermann_msgs AckermannDriveStamped as /drive and /sim_drive
/// carry it): speed and steering in [-1, 1], positive steering left.
/// </summary>
public class DriveCmd
{
    public double Stamp;
    public string FrameId = "base_link";
    public float Speed;
    public float SteeringAngle;

    public static DriveCmd Parse(CarLinkValue value)
    {
        DriveCmd command = new DriveCmd();
        command.Stamp = CarLinkFields.Stamp(value, "header.stamp");
        command.FrameId = CarLinkFields.OptionalString(value, "header.frame_id");
        command.Speed = CarLinkFields.Float(value, "drive.speed");
        command.SteeringAngle = CarLinkFields.Float(value, "drive.steering_angle");
        return command;
    }

    /// <summary>
    /// The command for a student call, as the physical car's library sends /drive: speed times the
    /// student's max speed, and the student angle negated (the library's positive is right).
    /// </summary>
    /// <param name="speed">Student speed in [-1, 1].</param>
    /// <param name="angle">Student angle in [-1, 1], positive right.</param>
    /// <param name="maxSpeed">Student max speed scale in [0, 1].</param>
    /// <param name="stamp">Car time estimate (in seconds).</param>
    public static DriveCmd FromStudent(float speed, float angle, float maxSpeed, double stamp)
    {
        return new DriveCmd
        {
            Stamp = stamp,
            Speed = Mathf.Clamp(speed, -1, 1) * Mathf.Clamp01(maxSpeed),
            SteeringAngle = -Mathf.Clamp(angle, -1, 1),
        };
    }

    public CarLinkValue ToValue()
    {
        return CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>
        {
            {
                "header", CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>
                {
                    { "stamp", CarLinkFields.StampValue(this.Stamp) },
                    { "frame_id", CarLinkValue.FromString(this.FrameId ?? "base_link") },
                })
            },
            {
                "drive", CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>
                {
                    { "steering_angle", CarLinkValue.FromDouble(this.SteeringAngle) },
                    { "steering_angle_velocity", CarLinkValue.FromDouble(0) },
                    { "speed", CarLinkValue.FromDouble(this.Speed) },
                    { "acceleration", CarLinkValue.FromDouble(0) },
                    { "jerk", CarLinkValue.FromDouble(0) },
                })
            },
        });
    }
}

/// <summary>
/// A std_msgs Float32 (encoder speed, battery voltage and current).
/// </summary>
public class Float32Msg
{
    public float Data;

    public static Float32Msg Parse(CarLinkValue value)
    {
        return new Float32Msg { Data = CarLinkFields.Float(value, "data") };
    }
}

/// <summary>
/// The car's map service state (GET /api/v1/state).
/// </summary>
public class ApiState
{
    public string Mode;
    public string Map;
    public bool? Localized;
    public bool HasPose;
    public double PoseX;
    public double PoseY;
    public double PoseYaw;
    public bool JobRunning;
    public string JobAction;
    public string JobClient;
    public string JobResult;
    public bool Twin;
    public int LinkClients;

    /// <summary>
    /// True while the car's map viewer has not caught up after a quiet period (mode not yet known).
    /// </summary>
    public bool IsWaiting { get { return this.Mode == null; } }

    public static ApiState Parse(CarLinkValue value)
    {
        ApiState state = new ApiState();
        state.Mode = CarLinkFields.OptionalString(value, "mode");
        state.Map = CarLinkFields.OptionalString(value, "map");
        state.Localized = CarLinkFields.OptionalBool(value, "localized");
        CarLinkValue pose = value["pose"];
        if (!pose.IsNull)
        {
            double[] values = pose.AsDoubles();
            if (values.Length != 3)
            {
                throw new CarLinkFormatException($"field 'pose': expected 3 numbers, got {values.Length}");
            }
            state.HasPose = true;
            state.PoseX = values[0];
            state.PoseY = values[1];
            state.PoseYaw = values[2];
        }
        state.JobRunning = CarLinkFields.OptionalBool(value, "job.running") ?? false;
        state.JobAction = CarLinkFields.OptionalString(value, "job.action");
        state.JobClient = CarLinkFields.OptionalString(value, "job.client");
        state.JobResult = CarLinkFields.OptionalString(value, "job.result");
        state.Twin = CarLinkFields.OptionalBool(value, "twin") ?? false;
        state.LinkClients = CarLinkFields.OptionalInt(value, "link_clients") ?? 0;
        return state;
    }
}

/// <summary>
/// One saved map on the car (GET /api/v1/maps entries).
/// </summary>
public class MapEntry
{
    public string Name;
    public bool Saved;
    public bool Autosave;
    public string Created;
    public double? Width;
    public double? Height;
    public double? Resolution;
    public string DriverVersion;
    public bool HasSession;
    public bool HasRgbd;

    /// <summary>
    /// The map's meta.yaml as sent by GET /api/v1/maps/name; Null in list entries.
    /// </summary>
    public CarLinkValue Meta = CarLinkValue.Null;

    public static MapEntry Parse(CarLinkValue value)
    {
        MapEntry entry = new MapEntry();
        entry.Name = CarLinkFields.String(value, "name");
        entry.Saved = CarLinkFields.OptionalBool(value, "saved") ?? false;
        entry.Autosave = CarLinkFields.OptionalBool(value, "autosave") ?? false;
        entry.Created = CarLinkFields.OptionalString(value, "created");
        CarLinkValue size = value["size_m"];
        if (!size.IsNull)
        {
            double[] values = size.AsDoubles();
            if (values.Length == 2)
            {
                entry.Width = values[0];
                entry.Height = values[1];
            }
        }
        entry.Resolution = CarLinkFields.OptionalDouble(value, "resolution");
        entry.DriverVersion = CarLinkFields.OptionalString(value, "driver_version");
        entry.HasSession = CarLinkFields.OptionalBool(value, "session") ?? false;
        entry.HasRgbd = CarLinkFields.OptionalBool(value, "rgbd") ?? false;
        entry.Meta = value["meta"];
        return entry;
    }

    public static List<MapEntry> ParseList(CarLinkValue value)
    {
        List<MapEntry> entries = new List<MapEntry>();
        foreach (CarLinkValue item in value["maps"].Items)
        {
            entries.Add(MapEntry.Parse(item));
        }
        return entries;
    }
}
