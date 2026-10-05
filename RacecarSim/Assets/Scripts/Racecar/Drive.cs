using System;
using UnityEngine;

/// <summary>
/// Controls car physics and movement.
/// </summary>
public class Drive : RacecarModule
{
    #region Set in Unity Editor
    /// <summary>
    /// The models which visualize the car's wheels, centered on their spin axis and oriented
    /// like the car at rest, in WheelColliders order.
    /// </summary>
    [SerializeField]
    private GameObject[] Wheels = new GameObject[4];

    /// <summary>
    /// The front steering knuckle models (left, right), pivoting about their local Y axis.
    /// </summary>
    [SerializeField]
    private Transform[] SteeringKnuckles = new Transform[2];

    /// <summary>
    /// The invisible colliders responsible for the physics of the car's wheels.
    /// </summary>
    public WheelCollider[] WheelColliders = new WheelCollider[4];
    #endregion

    #region Constants
    /// <summary>
    /// The default value of MaxSpeed.
    /// </summary>
    public const float DefaultMaxSpeed = 0.25f;

    /// <summary>
    /// The target speed (in units/s) when Speed times MaxSpeed is 1: 4.0 m/s, as on the physical
    /// car. MaxSpeed 0.25 caps the car at 1.0 m/s.
    /// </summary>
    public const float FullCommandSpeed = 40;

    /// <summary>
    /// Feedforward: total motor torque over all wheels per unit/s of target speed, which holds a
    /// steady speed against the wheel damping on flat ground.
    /// </summary>
    private const float feedforwardTorque = 0.8759f;

    /// <summary>
    /// Proportional gain: total motor torque per unit/s of speed error.
    /// </summary>
    private const float proportionalGain = 5.0f;

    /// <summary>
    /// Integral gain: total motor torque per unit of accumulated speed error (units/s times s),
    /// which holds the target on slopes.
    /// </summary>
    private const float integralGain = 2.0f;

    /// <summary>
    /// The largest total motor torque, about the 0.8 g the tires can transmit.
    /// </summary>
    private const float maxTorque = 66.7f;

    /// <summary>
    /// The ESC's drag brake at Speed 0: total brake torque over all wheels, which with the wheel
    /// damping slows the car at about 1.4 m/s^2, as measured on the physical car (0.37 m to stop
    /// from 1.07 m/s).
    /// </summary>
    private const float dragBrakeTorque = 8.0f;

    /// <summary>
    /// The steering angle at full lock (Angle = 1) of a single front wheel on the car's center
    /// line (bicycle model), in degrees, from the physical car's full-lock turning radius (443 mm
    /// at the rear axle). With full Ackermann the inner wheel turns 31.3 degrees, the outer 23.6.
    /// </summary>
    private const float maxDriveAngle = 26.9f;

    /// <summary>
    /// Fraction of full Ackermann geometry: 0 steers both front wheels by the same angle, 1 turns
    /// them about a common center on the rear axle line.
    /// </summary>
    private const float ackermannFraction = 1;

    /// <summary>
    /// The fastest the steering servo turns the wheels, in degrees per second of the bicycle
    /// angle: lock to lock in 0.3 s (physical car: 0.42 s upper bound, including tire lag).
    /// </summary>
    private const float maxSteerRate = 180;

    /// <summary>
    /// The fastest a wheel model's contact offset changes, in units/s (1 mm per physics step).
    /// </summary>
    private const float maxContactOffsetRate = 0.5f;

    /// <summary>
    /// The number of substeps used in wheel physics calculations.
    /// </summary>
    private const int vehicalSubsteps = 20;
    #endregion

    #region Public Interface
    /// <summary>
    /// The speed input, ranging from -1 (full reverse) to 1 (full forward). The car holds
    /// Speed times MaxSpeed times FullCommandSpeed.
    /// </summary>
    public float Speed { get; set; } = 0;

    /// <summary>
    /// The current angle of the car's front wheels, ranging from -1 (full left) to 1 (full right).
    /// </summary>
    public float Angle { get; set; } = 0;

    /// <summary>
    /// The max speed set by the user, ranging from 0 to 1: the fraction of FullCommandSpeed that
    /// Speed 1 targets.
    /// </summary>
    public float MaxSpeed { get; set; } = Drive.DefaultMaxSpeed;

    /// <summary>
    /// Stops the car (equivalent to setting Speed and Angle to 0).
    /// </summary>
    public void Stop()
    {
        this.Speed = 0;
        this.Angle = 0;
    }
    #endregion

    /// <summary>
    /// The rigidbody of the car.
    /// </summary>
    private Rigidbody rBody;

    /// <summary>
    /// The four wheel positions in the order they appear in the car prefab.
    /// </summary>
    private enum WheelPosition
    {
        FrontLeft,
        FrontRight,
        BackLeft,
        BackRight
    }

    /// <summary>
    /// All wheel positions, cached because Enum.GetValues allocates on every call.
    /// </summary>
    private static readonly WheelPosition[] wheelPositions = (WheelPosition[])Enum.GetValues(typeof(WheelPosition));

    /// <summary>
    /// Local positions of the steering knuckles at rest.
    /// </summary>
    private Vector3[] knuckleRestPositions;

    /// <summary>
    /// Heights of the front wheel models above the car root at rest, in SteeringKnuckles order.
    /// </summary>
    private float[] frontWheelRestHeights;

    /// <summary>
    /// Current offset of each wheel model above its WheelCollider pose, in WheelColliders order.
    /// </summary>
    private readonly float[] wheelContactOffsets = new float[4];

    /// <summary>
    /// Speed error accumulated by the speed controller (in units/s times seconds).
    /// </summary>
    private float speedErrorIntegral;

    /// <summary>
    /// Distance between the front and rear wheel colliders.
    /// </summary>
    private float wheelbase;

    /// <summary>
    /// Distance between the left and right steering pivots (kingpins).
    /// </summary>
    private float kingpinTrack;

    /// <summary>
    /// The bicycle-model steering angle after the servo rate limit, in degrees.
    /// </summary>
    private float steerAngle;

    /// <summary>
    /// Left and right front wheel angles for a bicycle-model steering angle. Positive angles turn
    /// right.
    /// </summary>
    /// <param name="angle">Steering angle of a center-line front wheel, in degrees.</param>
    /// <param name="wheelbase">Distance between the front and rear axles.</param>
    /// <param name="kingpinTrack">Distance between the steering pivots.</param>
    /// <param name="fraction">Fraction of full Ackermann geometry, 0 to 1.</param>
    public static (float Left, float Right) AckermannAngles(float angle, float wheelbase, float kingpinTrack, float fraction)
    {
        if (Mathf.Abs(angle) < 0.001f)
        {
            return (angle, angle);
        }

        // Turn center on the rear axle line, at radius from the car's center line
        float radius = wheelbase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
        float inner = Mathf.Atan(wheelbase / (radius - kingpinTrack / 2)) * Mathf.Rad2Deg;
        float outer = Mathf.Atan(wheelbase / (radius + kingpinTrack / 2)) * Mathf.Rad2Deg;
        float magnitude = Mathf.Abs(angle);
        float innerAngle = Mathf.Sign(angle) * (magnitude + fraction * (inner - magnitude));
        float outerAngle = Mathf.Sign(angle) * (magnitude + fraction * (outer - magnitude));
        return angle > 0 ? (outerAngle, innerAngle) : (innerAngle, outerAngle);
    }

    /// <summary>
    /// How far a wheel model sits above its WheelCollider pose, along the wheel's up axis. The
    /// pose sits a few millimeters into the ground under load (more with more damping), so a
    /// grounded wheel is drawn on its contact point instead.
    /// </summary>
    /// <param name="wheelCollider">The wheel's collider.</param>
    /// <param name="posePosition">The wheel center from WheelCollider.GetWorldPose.</param>
    /// <returns>The offset that puts the wheel on its contact point, or 0 when not grounded.</returns>
    public static float ContactOffset(WheelCollider wheelCollider, Vector3 posePosition)
    {
        if (!wheelCollider.GetGroundHit(out WheelHit hit))
        {
            return 0;
        }
        Vector3 up = wheelCollider.transform.up;
        return Vector3.Dot(hit.point + up * wheelCollider.radius - posePosition, up);
    }

    protected override void Awake()
    {
        this.rBody = this.GetComponent<Rigidbody>();

        base.Awake();
    }

    private void Start()
    {
        foreach (WheelCollider wheel in this.WheelColliders)
        {
            wheel.ConfigureVehicleSubsteps(1, Drive.vehicalSubsteps, Drive.vehicalSubsteps);
        }

        this.wheelbase = this.transform.InverseTransformPoint(this.WheelColliders[(int)WheelPosition.FrontLeft].transform.position).z
            - this.transform.InverseTransformPoint(this.WheelColliders[(int)WheelPosition.BackLeft].transform.position).z;
        this.kingpinTrack = Mathf.Abs(this.SteeringKnuckles[1].localPosition.x - this.SteeringKnuckles[0].localPosition.x);

        this.knuckleRestPositions = new Vector3[this.SteeringKnuckles.Length];
        this.frontWheelRestHeights = new float[this.SteeringKnuckles.Length];
        for (int i = 0; i < this.SteeringKnuckles.Length; i++)
        {
            this.knuckleRestPositions[i] = this.SteeringKnuckles[i].localPosition;
            this.frontWheelRestHeights[i] = this.transform.InverseTransformPoint(this.Wheels[i].transform.position).y;
        }
    }

    private void FixedUpdate()
    {
        using SimProfiler.Scope profile = SimProfiler.Measure(SimProfiler.Section.Drive);
        // Speed 0 is neutral, as on the physical car: no drive torque, the ESC's drag brake, and a
        // cleared integrator. Otherwise feedforward plus PI control holds the target speed.
        float target = this.Speed * this.MaxSpeed * Drive.FullCommandSpeed;
        float torque = 0;
        float brakeTorque = 0;
        if (this.Speed == 0)
        {
            brakeTorque = Drive.dragBrakeTorque / this.WheelColliders.Length;
            this.speedErrorIntegral = 0;
        }
        else
        {
            float error = target - Vector3.Dot(this.rBody.linearVelocity, this.transform.forward);
            float unclamped = Drive.feedforwardTorque * target + Drive.proportionalGain * error + Drive.integralGain * this.speedErrorIntegral;
            torque = Mathf.Clamp(unclamped, -Drive.maxTorque, Drive.maxTorque);

            // Integrate only while the output is off its limit or the error leads back inside it
            if (torque == unclamped || Mathf.Sign(error) != Mathf.Sign(unclamped))
            {
                this.speedErrorIntegral += error * Time.fixedDeltaTime;
            }
        }

        // Four-wheel drive: equal torque on every wheel
        foreach (WheelCollider wheel in this.WheelColliders)
        {
            wheel.brakeTorque = brakeTorque;
            wheel.motorTorque = torque / this.WheelColliders.Length;
        }

        // Servo rate limit, then Ackermann angles for the two front wheels
        this.steerAngle = Mathf.MoveTowards(this.steerAngle, this.Angle * Drive.maxDriveAngle, Drive.maxSteerRate * Time.fixedDeltaTime);
        (float left, float right) = Drive.AckermannAngles(this.steerAngle, this.wheelbase, this.kingpinTrack, Drive.ackermannFraction);
        this.WheelColliders[(int)WheelPosition.FrontLeft].steerAngle = left;
        this.WheelColliders[(int)WheelPosition.FrontRight].steerAngle = right;

        // Update wheel models to match wheel colliders. The pose excludes a collider's own rest
        // yaw (the left colliders face backward), so each side's model keeps facing outward.
        foreach (WheelPosition wheelPosition in Drive.wheelPositions)
        {
            // The contact offset changes at a limited rate: a wheel that touches the ground only
            // on some physics steps in a hard turn would otherwise jump between the two heights
            int index = (int)wheelPosition;
            WheelCollider wheelCollider = this.WheelColliders[index];
            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            this.wheelContactOffsets[index] = Mathf.MoveTowards(
                this.wheelContactOffsets[index],
                Drive.ContactOffset(wheelCollider, position),
                Drive.maxContactOffsetRate * Time.fixedDeltaTime);
            this.Wheels[index].transform.SetPositionAndRotation(position + wheelCollider.transform.up * this.wheelContactOffsets[index], rotation);
        }

        // Steer the knuckles and move them with the suspension travel of their wheel
        for (int i = 0; i < this.SteeringKnuckles.Length; i++)
        {
            float travel = this.transform.InverseTransformPoint(this.Wheels[i].transform.position).y - this.frontWheelRestHeights[i];
            this.SteeringKnuckles[i].localPosition = this.knuckleRestPositions[i] + Vector3.up * travel;
            this.SteeringKnuckles[i].localRotation = Quaternion.Euler(0, this.WheelColliders[i].steerAngle, 0);
        }
    }
}
