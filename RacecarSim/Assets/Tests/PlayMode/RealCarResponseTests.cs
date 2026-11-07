using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// With Real car response on, the car's speed after a step from rest follows the physical car's
/// measured curves (speed over setpoint, mean of the reference car's runs, 2026-11-07).
/// </summary>
public class RealCarResponseTests
{
    /// <summary>
    /// Allowed difference in speed over setpoint at each checkpoint.
    /// </summary>
    private const float tolerance = 0.12f;

    /// <summary>
    /// Time between checkpoints (in seconds), the first one after this long.
    /// </summary>
    private const float checkpointSeconds = 0.25f;

    /// <summary>
    /// Time scale for the runs; the physics step is unchanged, so the results match time scale 1.
    /// </summary>
    private const float runTimeScale = 4;

    // Measured speed over setpoint at 0.25 s, 0.5 s, ... 2.5 s after the command
    private static readonly float[] forward = { 0.54f, 0.91f, 1.04f, 1.13f, 1.17f, 1.19f, 1.18f, 1.13f, 1.10f, 1.10f };
    private static readonly float[] reverse = { 0.62f, 0.77f, 0.87f, 0.96f, 1.05f, 1.09f, 1.18f, 1.21f, 1.26f, 1.29f };

    private Racecar car;

    private Rigidbody body;

    private bool wasRealCarResponse;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        this.car = LevelManager.GetCar();
        this.body = this.car.GetComponent<Rigidbody>();
        Time.timeScale = RealCarResponseTests.runTimeScale;
        this.wasRealCarResponse = Settings.IsRealCarResponse;
        Settings.IsRealCarResponse = true;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        Settings.IsRealCarResponse = this.wasRealCarResponse;
        yield return PlayModeLevels.Unload();
    }

    /// <summary>
    /// Forward 0.75 m/s (the physical car's /drive 0.5).
    /// </summary>
    [UnityTest]
    public IEnumerator Forward_FollowsMeasuredCurve()
    {
        yield return this.Check(0.75f, RealCarResponseTests.forward);
    }

    /// <summary>
    /// Reverse 0.75 m/s (the physical car's /drive -0.5).
    /// </summary>
    [UnityTest]
    public IEnumerator Reverse_FollowsMeasuredCurve()
    {
        yield return this.Check(-0.75f, RealCarResponseTests.reverse);
    }

    /// <summary>
    /// Forward 4.0 m/s, past the measured band: the same curve, scaled to the target, once tire
    /// grip stops limiting the acceleration (as it does with Real car response off).
    /// </summary>
    [UnityTest]
    public IEnumerator FullSpeed_FollowsMeasuredCurve()
    {
        yield return this.Check(4.0f, RealCarResponseTests.forward, 4);
    }

    /// <summary>
    /// Drives from rest at a setpoint (in m/s) and compares speed over setpoint at each checkpoint
    /// from firstChecked on.
    /// </summary>
    private IEnumerator Check(float setpoint, float[] expected, int firstChecked = 0)
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 0.5f, -1500));
        yield return new WaitForSeconds(1);

        this.car.Drive.MaxSpeed = 1;
        this.car.Drive.Speed = setpoint * 10 / Drive.FullCommandSpeed;
        this.car.Drive.Angle = 0;
        int stepsPerCheckpoint = Mathf.RoundToInt(RealCarResponseTests.checkpointSeconds / Time.fixedDeltaTime);
        float[] actual = new float[expected.Length];
        for (int i = 0; i < expected.Length; i++)
        {
            for (int step = 0; step < stepsPerCheckpoint; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            actual[i] = Vector3.Dot(this.body.linearVelocity, this.car.transform.forward) / (setpoint * 10);
        }
        this.car.Drive.Speed = 0;

        string log = string.Join(" ", System.Array.ConvertAll(actual, a => a.ToString("F2")));
        Debug.Log($"Real car response {TestContext.CurrentContext.Test.Name}: {log}");
        for (int i = firstChecked; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i], actual[i], RealCarResponseTests.tolerance, $"speed over setpoint at {(i + 1) * RealCarResponseTests.checkpointSeconds:F2} s ({log})");
        }
    }
}
