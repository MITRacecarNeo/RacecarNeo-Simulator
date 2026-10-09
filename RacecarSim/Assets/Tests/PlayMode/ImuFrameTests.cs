using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// IMU readings in REP-103 body axes (x forward, y left, z up), the legacy frame for protocol
/// version 1, and the magnetometer, realism off unless a test turns it on.
/// </summary>
public class ImuFrameTests
{
    private const float gravity = 9.81f;

    private Racecar car;

    private Rigidbody body;

    private bool realism;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
        this.realism = Settings.IsRealism;
        Settings.IsRealism = false;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        Settings.IsRealism = this.realism;
        yield return PlayModeLevels.Unload();
    }

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        this.car = LevelManager.GetCar();
        this.body = this.car.GetComponent<Rigidbody>();
    }

    [UnityTest]
    public IEnumerator Level_ReadsGravityUpOnZ()
    {
        yield return this.Hold(Quaternion.identity);
        Vector3 accel = this.car.Physics.LinearAcceleration;
        Debug.Log($"IMU level: accel {accel:F4}, legacy {this.car.Physics.LegacyLinearAcceleration:F4}");
        Assert.AreEqual(0, accel.x, 0.05f);
        Assert.AreEqual(0, accel.y, 0.05f);
        Assert.AreEqual(ImuFrameTests.gravity, accel.z, 0.05f);

        // Protocol version 1 keeps the old frame: y up with gravity added
        Vector3 legacy = this.car.Physics.LegacyLinearAcceleration;
        Assert.AreEqual(-ImuFrameTests.gravity, legacy.y, 0.05f);
    }

    [UnityTest]
    public IEnumerator NoseUp_ReadsPositiveX()
    {
        // Unity rotates the nose up for a negative angle about the car's right axis
        yield return this.Hold(Quaternion.Euler(-30, 0, 0));
        Vector3 accel = this.car.Physics.LinearAcceleration;
        Debug.Log($"IMU nose up 30 deg: accel {accel:F4}");
        Assert.AreEqual(ImuFrameTests.gravity * 0.5f, accel.x, 0.05f);
        Assert.AreEqual(ImuFrameTests.gravity * Mathf.Cos(30 * Mathf.Deg2Rad), accel.z, 0.05f);
    }

    [UnityTest]
    public IEnumerator LeftSideDown_ReadsNegativeY()
    {
        // A positive angle about the forward axis raises the right side
        yield return this.Hold(Quaternion.Euler(0, 0, 30));
        Vector3 accel = this.car.Physics.LinearAcceleration;
        Debug.Log($"IMU left side down 30 deg: accel {accel:F4}");
        Assert.AreEqual(-ImuFrameTests.gravity * 0.5f, accel.y, 0.05f);
    }

    [UnityTest]
    public IEnumerator LeftTurn_IsPositiveAboutZ()
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 0.5f, -1500));
        yield return new WaitForSeconds(1);
        this.car.Drive.Speed = 1;
        this.car.Drive.Angle = -1;
        yield return new WaitForSeconds(4);
        Vector3 gyro = this.car.Physics.AngularVelocity;
        Vector3 legacy = this.car.Physics.LegacyAngularVelocity;
        Debug.Log($"IMU left turn: gyro {gyro:F4}, legacy {legacy:F4}");
        Assert.Greater(gyro.z, 0.5f);
        Assert.AreEqual(gyro.z, legacy.y, 1e-5f);
    }

    [UnityTest]
    public IEnumerator Magnetometer_FacingNorthAndWest()
    {
        yield return this.Hold(Quaternion.identity);
        Vector3 north = this.car.Physics.MagneticField * 1e6f;
        yield return this.Hold(Quaternion.Euler(0, -90, 0));
        Vector3 west = this.car.Physics.MagneticField * 1e6f;
        Debug.Log($"Magnetometer uT: facing north {north:F3}, facing west {west:F3}");

        Assert.AreEqual(51.14f, north.magnitude, 0.05f);
        Assert.AreEqual(20.66f, north.x, 0.01f);
        Assert.AreEqual(0, north.y, 0.01f);
        Assert.AreEqual(-46.78f, north.z, 0.01f);
        Assert.AreEqual(0, west.x, 0.01f);
        Assert.AreEqual(-20.66f, west.y, 0.01f);
    }

    [UnityTest]
    public IEnumerator Realism_MagnetometerNoiseAndBias()
    {
        Settings.IsRealism = true;
        yield return this.Hold(Quaternion.identity);
        const int frames = 300;
        Vector3 sum = Vector3.zero;
        Vector3 sumSquares = Vector3.zero;
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            Vector3 field = this.car.Physics.MagneticField * 1e6f;
            sum += field;
            sumSquares += Vector3.Scale(field, field);
        }
        Vector3 mean = sum / frames;
        Vector3 spread = new Vector3(
            Mathf.Sqrt(sumSquares.x / frames - mean.x * mean.x),
            Mathf.Sqrt(sumSquares.y / frames - mean.y * mean.y),
            Mathf.Sqrt(sumSquares.z / frames - mean.z * mean.z));
        Vector3 bias = mean - new Vector3(20.66f, 0, -46.78f);
        Debug.Log($"Magnetometer realism uT: mean {mean:F3}, bias {bias:F3}, sd {spread:F3}");

        for (int axis = 0; axis < 3; axis++)
        {
            Assert.AreEqual(0.35f, spread[axis], 0.06f, $"noise axis {axis}");
            Assert.Less(Mathf.Abs(bias[axis]), 3.2f, $"bias axis {axis}");
        }
    }

    /// <summary>
    /// Holds the car still (kinematic) at a fixed attitude on the pad and lets the readings settle.
    /// </summary>
    private IEnumerator Hold(Quaternion attitude)
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 5, 0));
        this.body.isKinematic = true;
        this.car.transform.rotation = attitude;
        this.body.rotation = attitude;
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        yield return null;
    }
}
