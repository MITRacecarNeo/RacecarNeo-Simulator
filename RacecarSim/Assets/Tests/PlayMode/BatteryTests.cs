using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The car battery in a level: full at load, the battery mode cutoff, and the autograder
/// exemption. Realism off.
/// </summary>
public class BatteryTests
{
    private Racecar car;

    private Rigidbody body;

    private bool realism;

    private bool batteryMode;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
        this.realism = Settings.IsRealism;
        this.batteryMode = Settings.IsBatteryMode;
        Settings.IsRealism = false;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        Settings.IsRealism = this.realism;
        Settings.IsBatteryMode = this.batteryMode;
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
    public IEnumerator FullAtLevelLoad()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, this.car.Drive.Battery.Charge, 1e-4f);
        Assert.AreEqual(8.40f, this.car.Readings.BatteryVoltage, 0.001f);
        Assert.AreEqual(2.50f, this.car.Readings.BatteryCurrent, 0.001f);
    }

    [UnityTest]
    public IEnumerator EmptyBattery_StopsCarOnlyInBatteryMode()
    {
        this.Drain();
        Settings.IsBatteryMode = true;
        float held = 0;
        yield return this.DriveFor(2, value => held = value);
        Debug.Log($"Battery empty, battery mode on: speed {held:F3} m/s, voltage {this.car.Readings.BatteryVoltage:F3} V");
        Assert.Less(held, 0.05f, "battery mode holds the car");
        Assert.AreEqual(BatteryModel.EmptyVoltage, this.car.Readings.BatteryVoltage, 0.001f);

        Settings.IsBatteryMode = false;
        float free = 0;
        yield return this.DriveFor(2, value => free = value);
        Debug.Log($"Battery empty, battery mode off: speed {free:F3} m/s");
        Assert.Greater(free, 0.5f, "without battery mode the car drives");
    }

    [Test]
    public void AutograderLevels_NeverCutOff()
    {
        this.Drain();
        Settings.IsBatteryMode = true;
        LevelManagerMode mode = LevelManager.LevelManagerMode;
        try
        {
            Assert.IsTrue(this.car.Drive.IsBatteryCutoff);
            LevelManager.LevelManagerMode = LevelManagerMode.Autograder;
            Assert.IsFalse(this.car.Drive.IsBatteryCutoff);
        }
        finally
        {
            LevelManager.LevelManagerMode = mode;
        }
    }

    private void Drain()
    {
        this.car.Drive.Battery.Step(BatteryModel.CapacityAh * 3600 / BatteryModel.IdleCurrent + 1, false, 0);
        Assert.IsTrue(this.car.Drive.Battery.IsEmpty);
    }

    /// <summary>
    /// Drives at speed 1 (max speed 0.25, 1 m/s) from rest on the pad and reports the forward
    /// speed at the end, in m/s.
    /// </summary>
    private IEnumerator DriveFor(float seconds, System.Action<float> speed)
    {
        PlayModeLevels.PlaceOnPad(this.car, new Vector3(0, 0.5f, -1500));
        yield return new WaitForSeconds(1);
        this.car.Drive.MaxSpeed = Drive.DefaultMaxSpeed;
        this.car.Drive.Speed = 1;
        yield return new WaitForSeconds(seconds);
        speed(Vector3.Dot(this.body.linearVelocity, this.car.transform.forward) / 10);
        this.car.Drive.Speed = 0;
    }
}
