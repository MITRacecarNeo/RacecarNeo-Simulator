using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The speed controller holds Speed times MaxSpeed times Drive.FullCommandSpeed on a straight.
/// Pins the time to reach it, the stopping distance after Speed 0, and the steady speed on a
/// full-lock circle to recorded baselines.
/// </summary>
public class RacecarSpeedTests
{
    /// <summary>
    /// Allowed relative change from a baseline.
    /// </summary>
    private const float tolerance = 0.05f;

    /// <summary>
    /// Allowed relative difference between the steady speed and the target.
    /// </summary>
    private const float setpointTolerance = 0.02f;

    /// <summary>
    /// Simulated seconds of driving before the steady window ends.
    /// </summary>
    private const float driveSeconds = 15;

    /// <summary>
    /// Length of the window, at the end of the drive, averaged for the steady speed.
    /// </summary>
    private const float steadyWindowSeconds = 2;

    /// <summary>
    /// Fraction of the steady speed that ends the rise time.
    /// </summary>
    private const float riseFraction = 0.9f;

    /// <summary>
    /// The longest a stop may take, in simulated seconds.
    /// </summary>
    private const float stopTimeoutSeconds = 20;

    /// <summary>
    /// Time scale for the runs; the physics step is unchanged, so the results match time scale 1.
    /// </summary>
    private const float runTimeScale = 4;

    // Baselines in s, units/s, and units, recorded with the speed controller and the drag brake
    private const float straightDefaultMaxRise = 0.38f;
    private const float straightFullRise = 1.06f;
    private const float straightHalfRise = 0.62f;
    private const float straightHalfStopDistance = 9.12f;
    private const float straightHalfStopTime = 1.06f;
    private const float straightDefaultMaxStopDistance = 2.92f;
    private const float straightDefaultMaxStopTime = 0.64f;
    private const float circleHalfSpeed = 19.98f;

    private Racecar car;

    private Rigidbody body;

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
        Time.timeScale = RacecarSpeedTests.runTimeScale;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Straight_FullCommandAtDefaultMaxSpeed()
    {
        Run run = new Run();
        yield return this.DriveRun(Drive.DefaultMaxSpeed, 1, 0, run);
        yield return this.Stop(run);
        Assert.IsTrue(run.Stopped, "car stopped");
        RacecarSpeedTests.AssertSetpoint(Drive.DefaultMaxSpeed, run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightDefaultMaxStopDistance, run.StopDistance, "stop distance", run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightDefaultMaxStopTime, run.StopTime, "stop time", run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightDefaultMaxRise, run.RiseTime, "rise time", run);
    }

    [UnityTest]
    public IEnumerator Straight_FullCommandAtFullMaxSpeed()
    {
        Run run = new Run();
        yield return this.DriveRun(1, 1, 0, run);
        RacecarSpeedTests.AssertSetpoint(1, run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightFullRise, run.RiseTime, "rise time", run);
    }

    [UnityTest]
    public IEnumerator Straight_HalfCommandAndStop()
    {
        Run run = new Run();
        yield return this.DriveRun(1, 0.5f, 0, run);
        yield return this.Stop(run);
        Assert.IsTrue(run.Stopped, "car stopped");
        RacecarSpeedTests.AssertSetpoint(0.5f, run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightHalfRise, run.RiseTime, "rise time", run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightHalfStopDistance, run.StopDistance, "stop distance", run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.straightHalfStopTime, run.StopTime, "stop time", run);
    }

    [UnityTest]
    public IEnumerator Straight_HalfCommandInReverse()
    {
        Run run = new Run();
        yield return this.DriveRun(1, -0.5f, 0, run);
        Assert.Less(Vector3.Dot(this.body.linearVelocity, this.car.transform.forward), 0, "moving backward");
        RacecarSpeedTests.AssertSetpoint(0.5f, run);
    }

    [UnityTest]
    public IEnumerator FullLockCircle_HalfCommand()
    {
        Run run = new Run();
        yield return this.DriveRun(1, 0.5f, -1, run);
        RacecarSpeedTests.AssertNear(RacecarSpeedTests.circleHalfSpeed, run.SteadySpeed, "steady speed", run);
    }

    /// <summary>
    /// Stops from the physical car's speeds at command off (2026-11-07 runs, m/s) within 10 % of
    /// its measured stopping distances (m).
    /// </summary>
    [UnityTest]
    public IEnumerator Stop_MatchesMeasuredDistances(
        [ValueSource(nameof(RacecarSpeedTests.measuredStops))] float[] stop)
    {
        Run run = new Run();
        yield return this.DriveRun(1, stop[0] * 10 / Drive.FullCommandSpeed, 0, run);
        yield return this.Stop(run);
        float distance = run.StopDistance / 10;
        Debug.Log($"Measured stop from {stop[0]:F3} m/s: sim {distance:F3} m in {run.StopTime:F2} s, car {stop[1]:F3} m ({distance / stop[1] - 1:P1})");
        Assert.IsTrue(run.Stopped, "car stopped");
        Assert.AreEqual(stop[1], distance, 0.10f * stop[1], "stop distance");
    }

    private static readonly float[][] measuredStops =
    {
        new[] { 0.625f, 0.122f },
        new[] { 0.756f, 0.172f },
        new[] { 0.906f, 0.266f },
        new[] { 1.053f, 0.319f },
        new[] { -1.194f, 0.431f },
    };

    /// <summary>
    /// Places the car at rest on the pad, then drives with the given inputs and records the steady
    /// speed and the rise time.
    /// </summary>
    private IEnumerator DriveRun(float maxSpeed, float speed, float angle, Run run)
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 0.5f, -1500));
        yield return new WaitForSeconds(1);

        this.car.Drive.MaxSpeed = maxSpeed;
        this.car.Drive.Speed = speed;
        this.car.Drive.Angle = angle;
        int steps = Mathf.RoundToInt(RacecarSpeedTests.driveSeconds / Time.fixedDeltaTime);
        int windowSteps = Mathf.RoundToInt(RacecarSpeedTests.steadyWindowSeconds / Time.fixedDeltaTime);
        float[] speeds = new float[steps];
        for (int i = 0; i < steps; i++)
        {
            yield return new WaitForFixedUpdate();
            speeds[i] = this.body.linearVelocity.magnitude;
        }

        float sum = 0;
        for (int i = steps - windowSteps; i < steps; i++)
        {
            sum += speeds[i];
        }
        run.SteadySpeed = sum / windowSteps;
        run.WindowChange = Mathf.Abs(speeds[steps - 1] - speeds[steps - windowSteps]) / run.SteadySpeed;
        int rise = System.Array.FindIndex(speeds, s => s >= RacecarSpeedTests.riseFraction * run.SteadySpeed);
        run.RiseTime = (rise + 1) * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Sets Speed 0 and records the distance travelled until the car stops.
    /// </summary>
    private IEnumerator Stop(Run run)
    {
        this.car.Drive.Speed = 0;
        float threshold = 0.01f * run.SteadySpeed;
        for (float t = 0; t < RacecarSpeedTests.stopTimeoutSeconds; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            float speed = this.body.linearVelocity.magnitude;
            run.StopDistance += speed * Time.fixedDeltaTime;
            run.StopTime = t + Time.fixedDeltaTime;
            if (speed < threshold)
            {
                run.Stopped = true;
                yield break;
            }
        }
    }

    private static void AssertSetpoint(float command, Run run)
    {
        float target = command * Drive.FullCommandSpeed;
        RacecarSpeedTests.AssertNear(target, run.SteadySpeed, "steady speed", run, RacecarSpeedTests.setpointTolerance);
    }

    private static void AssertNear(float baseline, float actual, string quantity, Run run, float tolerance = RacecarSpeedTests.tolerance)
    {
        Debug.Log($"Speed baseline {TestContext.CurrentContext.Test.Name} {quantity}: {actual:F4} (steady {run.SteadySpeed:F4} units/s, window change {run.WindowChange:P2}, rise {run.RiseTime:F2} s, stop {run.StopDistance:F3} units in {run.StopTime:F2} s, stopped {run.Stopped})");
        Assert.Less(run.WindowChange, 0.01f, "speed steady at the end of the drive");
        Assert.AreEqual(baseline, actual, tolerance * baseline, quantity);
    }

    /// <summary>
    /// Values measured in one run.
    /// </summary>
    private class Run
    {
        public float SteadySpeed;

        public float WindowChange;

        public float RiseTime;

        public float StopDistance;

        public float StopTime;

        public bool Stopped;
    }
}
