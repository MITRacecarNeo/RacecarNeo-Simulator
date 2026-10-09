using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The car's LED strip in a level: the battery display at load, a program's frame, and the return
/// to the battery display when the program disconnects. Realism off, so the full battery reads
/// exactly 8.4 V.
/// </summary>
public class LedStripPlayTests
{
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
    }

    [UnityTest]
    public IEnumerator FullBattery_LightsTheWholeStripRed()
    {
        yield return new WaitForFixedUpdate();
        yield return null;
        Color32[] shown = LevelManager.GetCar().GetComponent<LedStrip>().Shown;
        Assert.AreEqual(ActuatorCommands.LedCount, shown.Length);
        Assert.IsTrue(shown.All(c => c.r == 255 && c.g == 0 && c.b == 0), "8.4 V: 42 red LEDs from each end");
    }

    [UnityTest]
    public IEnumerator ProgramFrame_ReplacesBatteryDisplayUntilCleared()
    {
        Racecar car = LevelManager.GetCar();
        Color32[] frame = Enumerable.Range(0, ActuatorCommands.LedCount).Select(i => new Color32((byte)(3 * i), 0, 255, 255)).ToArray();
        car.Actuators.SetLeds(frame);
        yield return null;
        yield return null;
        CollectionAssert.AreEqual(frame, car.GetComponent<LedStrip>().Shown);

        car.Actuators.Clear();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.IsTrue(car.GetComponent<LedStrip>().Shown.All(c => c.r == 255 && c.b == 0), "back to the battery display");
    }
}
