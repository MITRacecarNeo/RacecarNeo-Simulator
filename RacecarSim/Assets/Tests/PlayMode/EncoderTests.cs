using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The drive encoder reading (SensorReadings.EncoderSpeed) against the car's motion: the true
/// forward speed with realism off, the hall encoder on the wheels with realism on.
/// </summary>
public class EncoderTests
{
    /// <summary>
    /// Allowed relative difference between the encoder and the Rigidbody speed on a straight.
    /// </summary>
    private const float tolerance = 0.03f;

    /// <summary>
    /// Simulated seconds of driving before the averaging window.
    /// </summary>
    private const float settleSeconds = 6;

    /// <summary>
    /// Simulated seconds averaged at the end of a drive.
    /// </summary>
    private const float windowSeconds = 2;

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
        Time.timeScale = 4;
    }

    [UnityTest]
    public IEnumerator RealismOff_ReportsTrueSpeed()
    {
        Sample sample = new Sample();
        yield return this.DriveAndSample(Drive.DefaultMaxSpeed, 1, sample);
        Debug.Log($"Encoder realism off: encoder {sample.Encoder:F4} m/s, ground {sample.Ground:F4} m/s");
        Assert.AreEqual(1.0f, sample.Ground, 0.05f, "ground speed near 1 m/s");
        Assert.AreEqual(sample.Ground, sample.Encoder, 0.0001f);
    }

    [UnityTest]
    public IEnumerator RealismOn_TracksWheelSpeed()
    {
        Settings.IsRealism = true;
        Sample sample = new Sample();
        yield return this.DriveAndSample(Drive.DefaultMaxSpeed, 1, sample);
        Debug.Log($"Encoder realism on: encoder {sample.Encoder:F4} m/s, ground {sample.Ground:F4} m/s");
        Assert.AreEqual(sample.Ground, sample.Encoder, EncoderTests.tolerance * sample.Ground);
    }

    [UnityTest]
    public IEnumerator RealismOn_ReverseIsNegative()
    {
        Settings.IsRealism = true;
        Sample sample = new Sample();
        yield return this.DriveAndSample(0.5f, -0.5f, sample);
        Debug.Log($"Encoder reverse: encoder {sample.Encoder:F4} m/s, ground {sample.Ground:F4} m/s");
        Assert.Less(sample.Encoder, -0.5f);
        Assert.AreEqual(sample.Ground, sample.Encoder, EncoderTests.tolerance * Mathf.Abs(sample.Ground));
    }

    [UnityTest]
    public IEnumerator RealismOn_HeldCarShowsWheelSpin()
    {
        // A held car (like one pushing a wall) spins its wheels; the motor encoder sees the spin
        Settings.IsRealism = true;
        Sample sample = new Sample();
        yield return this.DriveAndSample(Drive.DefaultMaxSpeed, 1, sample, true);
        Debug.Log($"Encoder held: encoder {sample.Encoder:F4} m/s, ground {sample.Ground:F4} m/s");
        Assert.Less(Mathf.Abs(sample.Ground), 0.01f, "car held in place");
        Assert.Greater(sample.Encoder, 0.2f);
    }

    /// <summary>
    /// Places the car on the pad, drives with the given inputs, and averages the encoder and the
    /// forward ground speed (in m/s) over the last window.
    /// </summary>
    private IEnumerator DriveAndSample(float maxSpeed, float speed, Sample sample, bool hold = false)
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 0.5f, -1500));
        yield return new WaitForSeconds(1);
        if (hold)
        {
            this.body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationY;
        }

        this.car.Drive.MaxSpeed = maxSpeed;
        this.car.Drive.Speed = speed;
        int settle = Mathf.RoundToInt(EncoderTests.settleSeconds / Time.fixedDeltaTime);
        int window = Mathf.RoundToInt(EncoderTests.windowSeconds / Time.fixedDeltaTime);
        for (int i = 0; i < settle + window; i++)
        {
            yield return new WaitForFixedUpdate();
            if (i >= settle)
            {
                sample.Encoder += this.car.Readings.EncoderSpeed / window;
                sample.Ground += Vector3.Dot(this.body.linearVelocity, this.car.transform.forward) / 10 / window;
            }
        }
    }

    /// <summary>
    /// Mean speeds (in m/s) over the averaging window.
    /// </summary>
    private class Sample
    {
        public float Encoder;

        public float Ground;
    }
}
