using System;
using UnityEngine;

/// <summary>
/// Conversions between the physical car's frames (REP-103: x forward, y left, z up, meters,
/// yaw counterclockwise) and the sim (Unity: +Z forward, +X right, +Y up, 1 unit = 1 dm).
/// </summary>
/// <remarks>
/// The car's base_link sits on the floor below the rear axle midpoint; the sim car's root sits on
/// the floor below the model origin, midway between the axles, half a wheelbase ahead.
/// </remarks>
public static class CarFrames
{
    #region Constants
    /// <summary>
    /// Sim units per meter.
    /// </summary>
    public const float UnitsPerMeter = 10f;

    /// <summary>
    /// Distance (in meters) from base_link forward to the sim car's root: half the 225 mm wheelbase.
    /// </summary>
    public const double BaseLinkToRoot = 0.1125;
    #endregion

    #region Public Interface
    /// <summary>
    /// A position in a car frame (in meters) as a sim position.
    /// </summary>
    public static Vector3 ToUnity(double x, double y, double z)
    {
        return new Vector3(
            (float)(-y * CarFrames.UnitsPerMeter),
            (float)(z * CarFrames.UnitsPerMeter),
            (float)(x * CarFrames.UnitsPerMeter));
    }

    /// <summary>
    /// A sim position as a car frame position (x, y, z in meters).
    /// </summary>
    public static (double x, double y, double z) FromUnity(Vector3 position)
    {
        return (
            position.z / (double)CarFrames.UnitsPerMeter,
            -position.x / (double)CarFrames.UnitsPerMeter,
            position.y / (double)CarFrames.UnitsPerMeter);
    }

    /// <summary>
    /// A car heading (in radians, counterclockwise from +x) as a sim yaw (in degrees about +Y).
    /// </summary>
    public static float YawToUnity(double yaw)
    {
        return (float)(-yaw * 180.0 / Math.PI);
    }

    /// <summary>
    /// A sim yaw (in degrees about +Y) as a car heading (in radians, in [-pi, pi]).
    /// </summary>
    public static double YawFromUnity(float degrees)
    {
        return CarFrames.WrapAngle(-degrees * Math.PI / 180.0);
    }

    /// <summary>
    /// The sim rotation for a car heading.
    /// </summary>
    public static Quaternion RotationToUnity(double yaw)
    {
        return Quaternion.Euler(0, CarFrames.YawToUnity(yaw), 0);
    }

    /// <summary>
    /// The sim car's root position and rotation for a base_link pose on the floor.
    /// </summary>
    public static (Vector3 position, Quaternion rotation) RootPose(double x, double y, double yaw)
    {
        double rootX = x + CarFrames.BaseLinkToRoot * Math.Cos(yaw);
        double rootY = y + CarFrames.BaseLinkToRoot * Math.Sin(yaw);
        return (CarFrames.ToUnity(rootX, rootY, 0), CarFrames.RotationToUnity(yaw));
    }

    /// <summary>
    /// The base_link pose (x, y in meters, yaw in radians) for a sim car root.
    /// </summary>
    public static (double x, double y, double yaw) BaseLinkPose(Vector3 rootPosition, Quaternion rootRotation)
    {
        double yaw = CarFrames.YawFromUnity(rootRotation.eulerAngles.y);
        (double rootX, double rootY, double _) = CarFrames.FromUnity(rootPosition);
        return (rootX - CarFrames.BaseLinkToRoot * Math.Cos(yaw), rootY - CarFrames.BaseLinkToRoot * Math.Sin(yaw), yaw);
    }

    /// <summary>
    /// A sensor offset in base_link (in millimeters) as a position relative to the sim car's root.
    /// </summary>
    public static Vector3 OffsetToUnity(double xMm, double yMm, double zMm)
    {
        return CarFrames.ToUnity(xMm / 1000.0 - CarFrames.BaseLinkToRoot, yMm / 1000.0, zMm / 1000.0);
    }

    /// <summary>
    /// Yaw (in radians, counterclockwise about +z) of a quaternion.
    /// </summary>
    public static double QuaternionToYaw(double x, double y, double z, double w)
    {
        return Math.Atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z));
    }

    /// <summary>
    /// An angle (in radians) wrapped to [-pi, pi].
    /// </summary>
    public static double WrapAngle(double angle)
    {
        angle = Math.IEEERemainder(angle, 2 * Math.PI);
        return angle <= -Math.PI ? angle + 2 * Math.PI : angle;
    }
    #endregion
}
