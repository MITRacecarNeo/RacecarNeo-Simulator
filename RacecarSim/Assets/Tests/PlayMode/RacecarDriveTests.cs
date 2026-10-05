using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The V2 car rests on its wheels and drives with the wheel and knuckle models following the wheel
/// colliders; Ackermann steering sets its low-speed turning radius.
/// </summary>
public class RacecarDriveTests
{
    private const float settleSeconds = 1;

    private const float driveSeconds = 2;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Car_DrivesWithWheelModelsOnTheirColliders()
    {
        yield return PlayModeLevels.Load(PlayModeLevels.Find("Demo World"), LevelManagerMode.Exploration, PlayModeLevels.Find("Demo World").BuildIndex);

        // Stop default drive so the test sets the inputs
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        yield return new WaitForSeconds(RacecarDriveTests.settleSeconds);

        foreach (WheelCollider wheel in car.Drive.WheelColliders)
        {
            Assert.IsTrue(wheel.isGrounded, $"{wheel.name} grounded at rest");
        }

        Vector3 start = car.transform.position;
        car.Drive.MaxSpeed = 1;
        car.Drive.Speed = 0.5f;
        car.Drive.Angle = 1;
        yield return new WaitForSeconds(RacecarDriveTests.driveSeconds);
        yield return new WaitForFixedUpdate();

        Assert.Greater(Vector3.Distance(start, car.transform.position), 1, "car moved");
        Transform model = car.transform.Find("TransformShift/Model");
        string[] wheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        for (int i = 0; i < wheelNames.Length; i++)
        {
            WheelCollider wheelCollider = car.Drive.WheelColliders[i];
            Transform wheel = model.Find(wheelNames[i]);
            // The models were placed during the last physics step; the pose read after it moves by
            // a fraction of a millimeter
            // The model sits on the pose's axis, at most a few millimeters above it
            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion _);
            Vector3 offset = wheel.position - position;
            float along = Vector3.Dot(offset, wheelCollider.transform.up);
            Assert.Less((offset - wheelCollider.transform.up * along).magnitude, 0.01f, $"{wheelNames[i]} on its collider axis");
            Assert.Less(Mathf.Abs(along), 0.05f, $"{wheelNames[i]} near its collider pose");

            // The axle stays on the car's lateral axis turned by the steer angle, so each side's
            // model keeps facing outward
            Vector3 axle = Quaternion.AngleAxis(wheelCollider.steerAngle, car.transform.up) * car.transform.right;
            Assert.Less(Vector3.Angle(axle, wheel.right), 1, $"{wheelNames[i]} axle");
            Assert.AreEqual(i % 2 == 0, car.transform.InverseTransformPoint(wheel.position).x < 0, $"{wheelNames[i]} side");
        }

        Transform knuckle = model.Find("Knuckle_FL");
        Assert.AreEqual(car.Drive.WheelColliders[0].steerAngle, Mathf.DeltaAngle(0, knuckle.localEulerAngles.y), 0.1f, "knuckle steer");
        Assert.Greater(car.Drive.WheelColliders[1].steerAngle, car.Drive.WheelColliders[0].steerAngle, "right turn: inner (right) wheel turns more");
    }

    [UnityTest]
    public IEnumerator Car_RestsWithWheelsAtTheirModelPositions()
    {
        yield return PlayModeLevels.Load(PlayModeLevels.Find("Demo World"), LevelManagerMode.Exploration, PlayModeLevels.Find("Demo World").BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));
        yield return new WaitForSeconds(1);
        int scans = car.Lidar.CompletedScans;
        float start = Time.time;
        yield return new WaitForSeconds(2);
        yield return new WaitForFixedUpdate();
        float scanRate = (car.Lidar.CompletedScans - scans) / (Time.time - start);

        // The colliders' anchors sit above the model wheel centers by the rest extension of the
        // suspension, so a car at rest has its wheels where the model places them
        float[] drops = new float[car.Drive.WheelColliders.Length];
        for (int i = 0; i < drops.Length; i++)
        {
            WheelCollider wheel = car.Drive.WheelColliders[i];
            wheel.GetWorldPose(out Vector3 position, out Quaternion _);
            drops[i] = Vector3.Dot(wheel.transform.position - position, car.transform.up);
            Debug.Log($"Rest {wheel.name}: drop {drops[i]:F4} units below the anchor, sprung mass {wheel.sprungMass:F3}");
        }
        for (int i = 0; i < drops.Length; i++)
        {
            WheelCollider wheel = car.Drive.WheelColliders[i];
            Assert.AreEqual(wheel.suspensionDistance * wheel.suspensionSpring.targetPosition, drops[i], 0.02f, $"{wheel.name} rest position");
        }
        Rigidbody body = car.GetComponent<Rigidbody>();
        Assert.Less(body.linearVelocity.magnitude, 0.01f, "car at rest");
        Assert.Less(Vector3.Angle(car.transform.up, Vector3.up), 0.2f, "car level");

        // The IMU reads 1 g up at rest; the LIDAR turns at 7.4 Hz
        Vector3 acceleration = car.Physics.LinearAcceleration;
        Assert.AreEqual(-9.81f, acceleration.y, 0.15f, "IMU vertical");
        Assert.Less(new Vector2(acceleration.x, acceleration.z).magnitude, 0.15f, "IMU horizontal");
        Assert.AreEqual(7.4f, scanRate, 0.6f, "LIDAR scans per second");
    }

    [UnityTest]
    public IEnumerator Tires_RestOnTheFloorWithoutSinking()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.6f, 0));
        yield return new WaitForSeconds(3);
        yield return new WaitForFixedUpdate();

        // Each tire model's bottom (center minus radius) touches the pad
        Transform model = car.transform.Find("TransformShift/Model");
        string[] wheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        for (int i = 0; i < wheelNames.Length; i++)
        {
            float bottom = model.Find(wheelNames[i]).position.y - car.Drive.WheelColliders[i].radius;
            Assert.AreEqual(PlayModeLevels.PadCenter.y, bottom, 0.002f, $"{wheelNames[i]} bottom on the floor");
        }
    }

    [UnityTest]
    public IEnumerator Wheels_MoveSmoothlyInAFastFullLockTurn()
    {
        // At 2 m/s on full lock the inside wheels touch the ground on some physics steps only; the
        // wheel models must not jump between heights when they do
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.6f, 0));
        yield return new WaitForSeconds(1);
        Time.timeScale = 4;
        car.Drive.MaxSpeed = 1;
        car.Drive.Speed = 0.5f;
        car.Drive.Angle = -1;
        yield return new WaitForSeconds(4);

        Transform model = car.transform.Find("TransformShift/Model");
        string[] wheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        float[] last = new float[wheelNames.Length];
        float[] maxStep = new float[wheelNames.Length];
        for (int i = 0; i < wheelNames.Length; i++)
        {
            last[i] = car.transform.InverseTransformPoint(model.Find(wheelNames[i]).position).y;
        }
        for (float t = 0; t < 4; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < wheelNames.Length; i++)
            {
                float height = car.transform.InverseTransformPoint(model.Find(wheelNames[i]).position).y;
                maxStep[i] = Mathf.Max(maxStep[i], Mathf.Abs(height - last[i]));
                last[i] = height;
            }
        }
        for (int i = 0; i < wheelNames.Length; i++)
        {
            Assert.Less(maxStep[i], 0.02f, $"{wheelNames[i]} height change per physics step");
        }
    }

    [UnityTest]
    public IEnumerator RollPrevention_CarRollsAtFullDefaultSpeedOnly()
    {
        // Lab 6a raises the center of mass so a full-lock turn at full default speed rolls the car
        LevelInfo level = PlayModeLevels.Find("Lab 6a: Roll Prevention");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        Time.timeScale = 4;

        float[] tilts = new float[2];
        float[] speeds = { 1, 0.5f };
        for (int i = 0; i < speeds.Length; i++)
        {
            PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));
            yield return new WaitForSeconds(1);
            car.Drive.MaxSpeed = Drive.DefaultMaxSpeed;
            car.Drive.Speed = speeds[i];
            car.Drive.Angle = -1;
            for (float t = 0; t < 8; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                tilts[i] = Mathf.Max(tilts[i], Vector3.Angle(car.transform.up, Vector3.up));
            }
        }
        Debug.Log($"Roll prevention: max tilt {tilts[0]:F1} deg at full default speed, {tilts[1]:F1} deg at half");
        Assert.Greater(tilts[0], 90, "rolls at full default speed");
        Assert.Less(tilts[1], 15, "stays upright at half speed");
    }

    [UnityTest]
    public IEnumerator FullLock_LowSpeedCircleMatchesSteeringGeometry()
    {
        yield return PlayModeLevels.Load(PlayModeLevels.Find("Demo World"), LevelManagerMode.Exploration, PlayModeLevels.Find("Demo World").BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));
        yield return new WaitForSeconds(RacecarDriveTests.settleSeconds);

        Time.timeScale = 4;
        car.Drive.MaxSpeed = 0.25f;
        car.Drive.Speed = 0.4f;
        car.Drive.Angle = -1;
        yield return new WaitForSeconds(3);

        // Full left lock: inner (left) 31.3 deg, outer (right) 23.6 deg
        Assert.AreEqual(-31.3f, car.Drive.WheelColliders[0].steerAngle, 0.1f, "inner wheel");
        Assert.AreEqual(-23.6f, car.Drive.WheelColliders[1].steerAngle, 0.1f, "outer wheel");

        // Outer front tire path over more than one turn: its extent across the circle
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        for (float t = 0; t < 16; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            car.Drive.WheelColliders[1].GetWorldPose(out Vector3 wheel, out Quaternion _);
            Vector2 point = new Vector2(wheel.x, wheel.z);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        float diameter = ((max.x - min.x) + (max.y - min.y)) / 2;

        // Kinematic: turn center on the rear axle line at wheelbase / tan(26.9 deg) from the
        // center line; the outer front wheel at half the track outside it and one wheelbase ahead
        float rearAxleRadius = 2.25f / Mathf.Tan(26.9f * Mathf.Deg2Rad);
        float expected = 2 * Mathf.Sqrt(Mathf.Pow(rearAxleRadius + 0.89f, 2) + 2.25f * 2.25f);
        Debug.Log($"Full-lock circle: outer front tire diameter {diameter:F3} units, kinematic {expected:F3}");
        Assert.AreEqual(expected, diameter, 0.05f * expected, "outer front tire circle diameter");
    }
}
