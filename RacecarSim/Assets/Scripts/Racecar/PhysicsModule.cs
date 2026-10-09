using UnityEngine;

/// <summary>
/// Simulates the IMU: the accelerometer and gyro read by racecar_core, and the LSM9DS1
/// magnetometer.
/// </summary>
/// <remarks>
/// Readings use REP-103 body axes, as the physical car's driver publishes them: x forward, y left,
/// z up. The accelerometer reports specific force, so a car at rest reads (0, 0, +9.81); rotation
/// rates follow the right-hand rule, so a left turn is positive about z. Protocol version 1
/// clients get the protocol version 1 frame (Legacy properties).
/// </remarks>
public class PhysicsModule : RacecarModule
{
    #region Constants
    /// <summary>
    /// The number of past samples to average for linear acceleration.
    /// </summary>
    private const int accelerationSamples = 4;

    /// <summary>
    /// Gravitational acceleration (in m/s^2).
    /// </summary>
    private const float gravity = 9.81f;

    /// <summary>
    /// The average relative error of linear acceleration measurements.
    /// This value is made up (it is NOT specified in the Intel RealSense D435i datasheet).
    /// </summary>
    private const float linearErrorFactor = 0.001f;

    /// <summary>
    /// The average fixed error applied to all linear acceleration measurements.
    /// This value is made up (it is NOT specified in the Intel RealSense D435i datasheet).
    /// </summary>
    private const float linearErrorFixed = 0.05f;

    /// <summary>
    /// The average relative error of angular velocity measurements.
    /// This value is made up (it is NOT specified in the Intel RealSense D435i datasheet).
    /// </summary>
    private const float angularErrorFactor = 0.001f;

    /// <summary>
    /// The average fixed error applied to all angular velocity measurements.
    /// This value is made up (it is NOT specified in the Intel RealSense D435i datasheet).
    /// </summary>
    private const float angularErrorFixed = 0.005f;

    /// <summary>
    /// Standard deviation of the per-level accelerometer bias in realism mode (in m/s^2).
    /// </summary>
    private const float linearBiasSigma = 0.05f;

    /// <summary>
    /// Standard deviation of the per-level gyro bias in realism mode (in rad/s).
    /// </summary>
    private const float angularBiasSigma = 0.005f;

    /// <summary>
    /// Random walk of the accelerometer and gyro biases after one second, as a fraction of their
    /// standard deviation: about one standard deviation over ten minutes.
    /// </summary>
    private const float biasDriftPerRootSecond = 0.04f;

    /// <summary>
    /// Earth's magnetic field at MIT (WMM2025, 2026.77) in world space (in Tesla): 20.66 uT
    /// horizontal toward magnetic north, which is world +Z in every level, and 46.78 uT down.
    /// </summary>
    private static readonly Vector3 earthField = new Vector3(0, -46.78e-6f, 20.66e-6f);

    /// <summary>
    /// Standard deviation of the per-level residual hard-iron bias after calibration, per axis, in
    /// realism mode (in Tesla).
    /// </summary>
    private const float magneticBiasSigma = 1.0e-6f;

    /// <summary>
    /// RMS noise of the magnetometer in realism mode (in Tesla), from the LIS3MDL (the same die)
    /// at 3.2 to 4.1 mgauss.
    /// </summary>
    private const float magneticNoise = 0.35e-6f;

    /// <summary>
    /// LSM9DS1 magnetometer resolution at 8 gauss full scale, 0.29 mgauss (in Tesla).
    /// </summary>
    private const float magneticStep = 0.029e-6f;

    /// <summary>
    /// Position of the IMU in car root coordinates (in dm): the breakout on the RACECAR Neo V2,
    /// 190 mm ahead of the rear axle and 105 mm above the ground. Linear acceleration is measured
    /// there, so turns add the lever arm from the center of mass.
    /// </summary>
    private static readonly Vector3 imuPosition = new Vector3(0, 1.05f, 0.775f);
    #endregion

    #region Public Interface
    /// <summary>
    /// Specific force at the IMU in REP-103 body axes (in m/s^2): (0, 0, +9.81) at rest.
    /// </summary>
    public Vector3 LinearAcceleration
    {
        get { return PhysicsModule.ToBodyAxes(this.localSpecificForce); }
    }

    /// <summary>
    /// Linear acceleration in the protocol version 1 frame (in m/s^2): x right, y up, z forward, with gravity added, so a car at rest reads (0, -9.81, 0).
    /// </summary>
    public Vector3 LegacyLinearAcceleration { get; private set; } = Vector3.zero;

    /// <summary>
    /// The linear velocity of the car relative to the car's transform (in meters/second)
    /// </summary>
    public Vector3 LinearVelocity
    {
        get
        {
            if (!this.linearVelocity.HasValue)
            {
                this.linearVelocity = this.transform.InverseTransformDirection(this.rBody.linearVelocity) / 10;
            }
            return this.linearVelocity.Value;
        }
    }

    /// <summary>
    /// The angular velocity of the car in REP-103 body axes (in rad/s), right-hand rule: a left
    /// turn is positive about z.
    /// </summary>
    public Vector3 AngularVelocity
    {
        get { return PhysicsModule.ToBodyAxes(this.LocalAngularVelocity); }
    }

    /// <summary>
    /// Angular velocity in the protocol version 1 frame (in rad/s): about x right, y up, z forward, right-hand rule, so a left turn is positive about y.
    /// </summary>
    public Vector3 LegacyAngularVelocity
    {
        get { return this.LocalAngularVelocity; }
    }

    /// <summary>
    /// The magnetic field at the magnetometer in REP-103 body axes (in Tesla). A level car facing
    /// magnetic north (world +Z) reads about (20.7, 0, -46.8) uT.
    /// </summary>
    public Vector3 MagneticField
    {
        get
        {
            if (!this.magneticField.HasValue)
            {
                Vector3 field = PhysicsModule.ToBodyAxes(this.transform.InverseTransformDirection(PhysicsModule.earthField));
                if (Settings.IsRealism)
                {
                    field += this.magneticBias.Value + new Vector3(
                        NormalDist.Random(0, PhysicsModule.magneticNoise),
                        NormalDist.Random(0, PhysicsModule.magneticNoise),
                        NormalDist.Random(0, PhysicsModule.magneticNoise));
                    field = new Vector3(
                        Mathf.Round(field.x / PhysicsModule.magneticStep) * PhysicsModule.magneticStep,
                        Mathf.Round(field.y / PhysicsModule.magneticStep) * PhysicsModule.magneticStep,
                        Mathf.Round(field.z / PhysicsModule.magneticStep) * PhysicsModule.magneticStep);
                }
                this.magneticField = field;
            }
            return this.magneticField.Value;
        }
    }

    /// <summary>
    /// Converts a vector from Unity car-local axes (x right, y up, z forward) to REP-103 body axes
    /// (x forward, y left, z up). Rotation rates convert the same way when both use the
    /// right-hand rule about their axes.
    /// </summary>
    public static Vector3 ToBodyAxes(Vector3 local)
    {
        return new Vector3(local.z, -local.x, local.y);
    }
    #endregion

    /// <summary>
    /// The rigidbody of the car.
    /// </summary>
    private Rigidbody rBody;

    /// <summary>
    /// The world-space velocity of the car (in real-world meters/second) at the previous physics step.
    /// </summary>
    private Vector3 prevWorldVelocity;

    /// <summary>
    /// Running average of the specific force in car-local axes (in m/s^2).
    /// </summary>
    private Vector3 localSpecificForce = Vector3.zero;

    /// <summary>
    /// Private member for the LinearVelocity accessor
    /// </summary>
    private Vector3? linearVelocity = null;

    /// <summary>
    /// Private member for the LocalAngularVelocity accessor
    /// </summary>
    private Vector3? angularVelocity = null;

    /// <summary>
    /// Private member for the MagneticField accessor
    /// </summary>
    private Vector3? magneticField = null;

    /// <summary>
    /// Realism biases, drawn when the car is created (on every level load), in car-local axes for
    /// the accelerometer and gyro and in body axes for the magnetometer.
    /// </summary>
    private readonly SensorBias linearBias = new SensorBias(PhysicsModule.linearBiasSigma, PhysicsModule.biasDriftPerRootSecond);
    private readonly SensorBias angularBias = new SensorBias(PhysicsModule.angularBiasSigma, PhysicsModule.biasDriftPerRootSecond);
    private readonly SensorBias magneticBias = new SensorBias(PhysicsModule.magneticBiasSigma, 0);

    /// <summary>
    /// The angular velocity in car-local axes (in rad/s), right-hand rule about each axis.
    /// </summary>
    private Vector3 LocalAngularVelocity
    {
        get
        {
            if (!this.angularVelocity.HasValue)
            {
                // Unity reports world-space rates with left-hand-rule signs; negate for
                // right-hand-rule rates about the car-local axes
                Vector3 angVel = -this.transform.InverseTransformDirection(this.rBody.angularVelocity);

                if (Settings.IsRealism)
                {
                    angVel *= NormalDist.Random(1, PhysicsModule.angularErrorFactor);
                    angVel.x += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                    angVel.y += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                    angVel.z += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                    angVel += this.angularBias.Value;
                }

                this.angularVelocity = angVel;
            }
            return this.angularVelocity.Value;
        }
    }

    protected override void Awake()
    {
        this.rBody = this.GetComponent<Rigidbody>();

        base.Awake();
    }

    private void Start()
    {
        this.prevWorldVelocity = this.ImuVelocity();
    }

    private void Update()
    {
        this.racecar.Readings.LinearAcceleration = this.LinearAcceleration;
        this.racecar.Readings.AngularVelocity = this.AngularVelocity;
        this.racecar.Readings.LegacyLinearAcceleration = this.LegacyLinearAcceleration;
        this.racecar.Readings.LegacyAngularVelocity = this.LegacyAngularVelocity;
        this.racecar.Readings.MagneticField = this.MagneticField;

        if (this.racecar.Hud != null)
        {
            this.racecar.Hud.UpdatePhysics(this.LinearVelocity.magnitude, this.LinearAcceleration, this.AngularVelocity);
        }
    }

    private void FixedUpdate()
    {
        // Differentiate the world-space velocity read fresh each physics step, then rotate into the
        // car frame. Differentiating in the world frame keeps the centripetal term that a car-frame
        // derivative drops.
        Vector3 worldVelocity = this.ImuVelocity();
        Vector3 worldAcceleration = (worldVelocity - this.prevWorldVelocity) / Time.fixedDeltaTime;
        this.prevWorldVelocity = worldVelocity;

        Vector3 acceleration = this.transform.InverseTransformDirection(worldAcceleration);
        Vector3 down = this.transform.InverseTransformDirection(Vector3.down * PhysicsModule.gravity);

        // Specific force (what an accelerometer measures) and the legacy acceleration plus gravity
        Vector3 specificForce = acceleration - down;
        Vector3 legacy = acceleration + down;
        if (Settings.IsRealism)
        {
            this.linearBias.Step(Time.fixedDeltaTime);
            this.angularBias.Step(Time.fixedDeltaTime);

            float scale = NormalDist.Random(1, PhysicsModule.linearErrorFactor);
            Vector3 error = this.linearBias.Value + new Vector3(
                NormalDist.Random(0, PhysicsModule.linearErrorFixed),
                NormalDist.Random(0, PhysicsModule.linearErrorFixed),
                NormalDist.Random(0, PhysicsModule.linearErrorFixed));
            specificForce = specificForce * scale + error;
            legacy = legacy * scale + error;
        }

        // Update the running averages
        this.localSpecificForce += (specificForce - this.localSpecificForce) / PhysicsModule.accelerationSamples;
        this.LegacyLinearAcceleration += (legacy - this.LegacyLinearAcceleration) / PhysicsModule.accelerationSamples;
    }

    private void LateUpdate()
    {
        this.linearVelocity = null;
        this.angularVelocity = null;
        this.magneticField = null;
    }

    /// <summary>
    /// The world-space velocity of the IMU (in real-world meters/second). The sim world is 10x
    /// scale, so divide by 10. Reads the Rigidbody pose, which interpolation does not move.
    /// </summary>
    private Vector3 ImuVelocity()
    {
        Vector3 imu = this.rBody.position + this.rBody.rotation * PhysicsModule.imuPosition;
        return this.rBody.GetPointVelocity(imu) / 10;
    }
}
