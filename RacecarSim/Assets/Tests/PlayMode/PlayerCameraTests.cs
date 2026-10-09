using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The player camera in a level: switching views places the camera at the new view at its
/// default distance, and the camera keeps following the car.
/// </summary>
public class PlayerCameraTests
{
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

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
    }

    [UnityTest]
    public IEnumerator SetCamera_EveryViewStartsAtItsDefault()
    {
        Racecar car = LevelManager.GetCar();
        Assert.AreEqual(0, car.CameraView, "level starts in the chase view");

        for (int view = 0; view < PlayerCameraViews.Count; view++)
        {
            car.SetCamera(view);
            Assert.AreEqual(1, car.CameraZoom, $"view {view} zoom");
            yield return null;

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "main camera enabled");
            Vector3 expected = PlayerCameraViews.Position(car.transform, car.Center, view, 1);
            Assert.Less(Vector3.Distance(expected, camera.transform.position), 0.5f, $"view {view} position");
            Assert.Greater(Vector3.Dot(camera.transform.forward, (car.Center - camera.transform.position).normalized), 0.999f, $"view {view} looks at the car's center");
        }
    }

    [UnityTest]
    public IEnumerator RightSideView_IsOnTheCarsRight()
    {
        Racecar car = LevelManager.GetCar();
        car.SetCamera(5);
        yield return null;
        Vector3 offset = Camera.main.transform.position - car.Center;
        Assert.Greater(Vector3.Dot(offset, car.transform.right), 7, "right side view");
    }
}
