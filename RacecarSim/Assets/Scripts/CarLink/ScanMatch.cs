using System;
using UnityEngine;

/// <summary>
/// Compares the car's real lidar scan with the sim lidar cast from the same pose into the built
/// world: the share of real returns with a sim return within a tolerance. It checks the frames,
/// the map, the world builder, and the lidar model together.
/// </summary>
public static class ScanMatch
{
    #region Constants
    /// <summary>
    /// A real and a sim return within this distance (in meters) match.
    /// </summary>
    public const float Tolerance = 0.10f;

    /// <summary>
    /// Every Stride-th beam is compared (1080 beams: 270).
    /// </summary>
    public const int Stride = 4;
    #endregion

    #region Public Interface
    /// <summary>
    /// The share of compared beams (a valid real range) whose sim range is within the tolerance;
    /// NaN when no beam is valid. Sim ranges are NaN or infinity where the cast hit nothing.
    /// </summary>
    public static float Score(LaserScanMsg scan, float[] simRanges, out int compared)
    {
        compared = 0;
        int matched = 0;
        for (int i = 0; i < scan.Ranges.Length && i < simRanges.Length; i += ScanMatch.Stride)
        {
            if (!scan.IsValid(i))
            {
                continue;
            }
            compared++;
            float sim = simRanges[i];
            if (!float.IsNaN(sim) && !float.IsInfinity(sim) && Math.Abs(sim - scan.Ranges[i]) <= ScanMatch.Tolerance)
            {
                matched++;
            }
        }
        return compared == 0 ? float.NaN : (float)matched / compared;
    }

    /// <summary>
    /// Casts every Stride-th beam of a scan from the lidar's pose into the sim world; other beams
    /// stay NaN.
    /// </summary>
    /// <param name="scan">The real scan (beam angles).</param>
    /// <param name="laser">Lidar position and heading in the map frame (meters, radians).</param>
    /// <param name="height">Sim height of the lidar plane (units).</param>
    /// <param name="mask">Layers the beams hit.</param>
    public static float[] Cast(LaserScanMsg scan, (double x, double y, double yaw) laser, float height, int mask)
    {
        float[] ranges = new float[scan.Ranges.Length];
        Vector3 origin = CarFrames.ToUnity(laser.x, laser.y, 0);
        origin.y = height;
        float reach = scan.RangeMax * CarFrames.UnitsPerMeter;
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = float.NaN;
            if (i % ScanMatch.Stride != 0)
            {
                continue;
            }
            double angle = laser.yaw + scan.AngleMin + i * scan.AngleIncrement;
            Vector3 direction = CarFrames.ToUnity(Math.Cos(angle), Math.Sin(angle), 0).normalized;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
            {
                ranges[i] = hit.distance / CarFrames.UnitsPerMeter;
            }
        }
        return ranges;
    }

    /// <summary>
    /// The lidar's pose in the map frame from the car's base_link pose and the lidar mount
    /// (x and y offsets in meters, yaw in radians).
    /// </summary>
    public static (double x, double y, double yaw) LaserPose(CarPose car, double offsetX, double offsetY, double offsetYaw)
    {
        double cos = Math.Cos(car.Yaw);
        double sin = Math.Sin(car.Yaw);
        return (car.X + offsetX * cos - offsetY * sin, car.Y + offsetX * sin + offsetY * cos, car.Yaw + offsetYaw);
    }
    #endregion
}
