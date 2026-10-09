using UnityEngine;

/// <summary>
/// The drive encoder of the RACECAR Neo V2: the drive board times the motor's hall sensor edges
/// and reports wheel speed in m/s, signed, positive forward.
/// </summary>
/// <remarks>
/// The physical car's firmware decodes 6 hall states per electrical revolution on a 2 pole pair
/// motor (12 edges per motor revolution), gears 18/61 then 15/37 to the wheels, a 72 mm wheel,
/// an exponential filter (alpha 0.8) on the measured speed, and reads 0 after 30 ms without an
/// edge. With realism off Drive passes the car's true forward speed, which Step returns unchanged.
/// </remarks>
public class HallEncoder
{
    #region Constants
    /// <summary>
    /// Nominal wheel diameter used by the firmware (in meters).
    /// </summary>
    public const float WheelDiameter = 0.072f;

    /// <summary>
    /// Wheel travel between hall edges (in meters): one twelfth of a motor revolution through
    /// the 18/61 and 15/37 gear stages, about 2.26 mm.
    /// </summary>
    public const float EdgeDistance = Mathf.PI * HallEncoder.WheelDiameter * (18f / 61f) * (15f / 37f) / 12f;

    /// <summary>
    /// Time without a hall edge after which the firmware reads 0 (in seconds).
    /// </summary>
    public const float StopTimeout = 0.030f;

    /// <summary>
    /// Weight of each new speed measurement in the firmware's exponential filter.
    /// </summary>
    public const float FilterAlpha = 0.8f;
    #endregion

    #region Public Interface
    /// <summary>
    /// The latest encoder reading (in m/s), positive forward.
    /// </summary>
    public float Speed { get; private set; }

    /// <summary>
    /// Wheel surface speed (in m/s) from a wheel's rotation rate, for the firmware's wheel size.
    /// </summary>
    /// <param name="rpm">Wheel revolutions per minute, positive rolling forward.</param>
    public static float WheelSpeed(float rpm)
    {
        return rpm * Mathf.PI * HallEncoder.WheelDiameter / 60f;
    }

    /// <summary>
    /// Advances the encoder by one physics step.
    /// </summary>
    /// <param name="wheelSpeed">Speed this step (in m/s), positive forward: the mean wheel surface
    /// speed with realism on, the true forward speed with realism off.</param>
    /// <param name="deltaTime">Step length (in seconds).</param>
    /// <param name="isRealism">True to model the hall edge timing and filter; false to return wheelSpeed.</param>
    /// <returns>The new reading (in m/s).</returns>
    public float Step(float wheelSpeed, float deltaTime, bool isRealism)
    {
        if (!isRealism)
        {
            this.Speed = wheelSpeed;
            this.travelSinceEdge = 0;
            this.timeSinceEdge = 0;
            return this.Speed;
        }

        this.travelSinceEdge += Mathf.Abs(wheelSpeed) * deltaTime;
        this.timeSinceEdge += deltaTime;

        int edges = Mathf.FloorToInt(this.travelSinceEdge / HallEncoder.EdgeDistance);
        if (edges > 0)
        {
            // Travel past the last edge happened after it; split the elapsed time in proportion
            float leftover = this.travelSinceEdge - edges * HallEncoder.EdgeDistance;
            float sinceLastEdge = this.timeSinceEdge * leftover / this.travelSinceEdge;

            // Speed measured from the time the edges took, signed by the rotation direction
            float measured = Mathf.Sign(wheelSpeed) * edges * HallEncoder.EdgeDistance / (this.timeSinceEdge - sinceLastEdge);
            this.Speed = (1 - HallEncoder.FilterAlpha) * this.Speed + HallEncoder.FilterAlpha * measured;
            this.travelSinceEdge = leftover;
            this.timeSinceEdge = sinceLastEdge;
        }
        else if (this.timeSinceEdge > HallEncoder.StopTimeout)
        {
            this.Speed = 0;
        }
        return this.Speed;
    }
    #endregion

    /// <summary>
    /// Wheel travel since the last hall edge (in meters).
    /// </summary>
    private float travelSinceEdge;

    /// <summary>
    /// Time since the last hall edge (in seconds).
    /// </summary>
    private float timeSinceEdge;
}
