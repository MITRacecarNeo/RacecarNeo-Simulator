using UnityEngine;

/// <summary>
/// Simulates the IMU.
/// </summary>
public class PhysicsModule : RacecarModule
{
    #region Constants
    /// <summary>
    /// The number of past samples to average for linear acceleration.
    /// </summary>
    private const int accelerationSamples = 4;

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
    /// Position of the IMU in car root coordinates (in dm): the breakout on the RACECAR Neo V2,
    /// 190 mm ahead of the rear axle and 105 mm above the ground. Linear acceleration is measured
    /// there, so turns add the lever arm from the center of mass.
    /// </summary>
    private static readonly Vector3 imuPosition = new Vector3(0, 1.05f, 0.775f);
    #endregion

    #region Public Interface
    /// <summary>
    /// The linear acceleration of the car relative to the car's transform (in meters/second^2).
    /// </summary>
    public Vector3 LinearAcceleration { get; private set; } = Vector3.zero;

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
    /// The angular velocity of the car (in radians/second) about the car's own axes: x right,
    /// y up, z forward. Positive values follow the right-hand rule (counterclockwise when viewed
    /// from the positive end of the axis), so a left turn has positive y.
    /// </summary>
    public Vector3 AngularVelocity
    {
        get
        {
            if (!this.angularVelocity.HasValue)
            {
                // Unity reports world-space rates with left-hand-rule signs; the API uses car-frame
                // axes with right-hand-rule signs
                Vector3 angVel = -this.transform.InverseTransformDirection(this.rBody.angularVelocity);

                if (Settings.IsRealism)
                {
                    angVel *= NormalDist.Random(1, PhysicsModule.angularErrorFactor);
                    angVel.x += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                    angVel.y += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                    angVel.z += NormalDist.Random(0, PhysicsModule.angularErrorFixed);
                }

                this.angularVelocity = angVel;
            }
            return this.angularVelocity.Value;
        }
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
    /// Private member for the LinearVelocity accessor
    /// </summary>
    private Vector3? linearVelocity = null;

    /// <summary>
    /// Private member for the AngularVelocity accessor
    /// </summary>
    private Vector3? angularVelocity = null;

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

        // Calculate current linear acceleration, incorporating gravity and error rate
        Vector3 curAcceleration = this.transform.InverseTransformDirection(worldAcceleration + Vector3.down * 9.81f);
        if (Settings.IsRealism)
        {
            curAcceleration *= NormalDist.Random(1, PhysicsModule.linearErrorFactor);
            curAcceleration.x += NormalDist.Random(0, PhysicsModule.linearErrorFixed);
            curAcceleration.y += NormalDist.Random(0, PhysicsModule.linearErrorFixed);
            curAcceleration.z += NormalDist.Random(0, PhysicsModule.linearErrorFixed);
        }

        // Update linear acceleration running average
        this.LinearAcceleration += (curAcceleration - this.LinearAcceleration) / PhysicsModule.accelerationSamples;
    }

    private void LateUpdate()
    {
        this.linearVelocity = null;
        this.angularVelocity = null;
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
