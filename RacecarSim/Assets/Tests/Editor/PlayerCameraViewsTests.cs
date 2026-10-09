using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The player camera views and the scroll wheel zoom (PlayerCameraViews).
/// </summary>
public class PlayerCameraViewsTests
{
    private static readonly Vector3 Target = new Vector3(10, 2.22f, 20);

    private GameObject car;

    [SetUp]
    public void CreateCar()
    {
        // Facing world +x, so the car's right is world -z
        this.car = new GameObject("Car");
        this.car.transform.SetPositionAndRotation(new Vector3(10, 1, 20), Quaternion.Euler(0, 90, 0));
    }

    [TearDown]
    public void DestroyCar()
    {
        Object.DestroyImmediate(this.car);
    }

    private Vector3 Offset(int view, float zoom = 1)
    {
        return PlayerCameraViews.Position(this.car.transform, PlayerCameraViewsTests.Target, view, zoom) - PlayerCameraViewsTests.Target;
    }

    [Test]
    public void FirstThreeViews_KeepTheirIndices()
    {
        // AutograderLevelInfo.DefaultCameraIndex selects views by index
        AssertNear(new Vector3(-8, 4, 0), this.Offset(0), "chase");
        AssertNear(new Vector3(-2, 20, 0), this.Offset(1), "overhead");
        AssertNear(new Vector3(8, 4, 0), this.Offset(2), "front");
    }

    [Test]
    public void NewViews_LookFromTheNamedDirections()
    {
        Assert.AreEqual(9, PlayerCameraViews.Count);
        AssertNear(new Vector3(-6.4f, 6.4f, 0), this.Offset(3), "45 degree overhead, from behind");
        AssertNear(new Vector3(6.4f, 6.4f, 0), this.Offset(4), "45 degree front");
        AssertNear(new Vector3(0, 3, -8), this.Offset(5), "right side");
        AssertNear(new Vector3(0, 3, 8), this.Offset(6), "left side");
        AssertNear(new Vector3(-5.7f, 4, -5.7f), this.Offset(7), "45 degree right, from behind");
        AssertNear(new Vector3(-5.7f, 4, 5.7f), this.Offset(8), "45 degree left, from behind");
    }

    [Test]
    public void Zoom_ScalesTheOffset()
    {
        AssertNear(this.Offset(5) * 0.5f, this.Offset(5, 0.5f), "half distance");
        AssertNear(this.Offset(1) * 2, this.Offset(1, 2), "double distance");
    }

    [Test]
    public void ScrollUp_MovesCloserAndScrollDown_MovesAway()
    {
        Assert.Less(PlayerCameraViews.Zoom(1, 1), 1);
        Assert.Greater(PlayerCameraViews.Zoom(1, -1), 1);
        Assert.AreEqual(1, PlayerCameraViews.Zoom(PlayerCameraViews.Zoom(1, 3), -3), 1e-5);
    }

    [Test]
    public void Zoom_StaysWithinLimits()
    {
        Assert.AreEqual(PlayerCameraViews.MinZoom, PlayerCameraViews.Zoom(1, 1000));
        Assert.AreEqual(PlayerCameraViews.MaxZoom, PlayerCameraViews.Zoom(1, -1000));
    }

    private static void AssertNear(Vector3 expected, Vector3 actual, string view)
    {
        Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"{view}: expected {expected}, got {actual}");
    }
}
