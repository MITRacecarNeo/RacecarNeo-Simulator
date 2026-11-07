using NUnit.Framework;
using UnityEngine;

/// <summary>
/// CarFrames conversions between the physical car's REP-103 frames and the sim.
/// </summary>
public class CarFramesTests
{
    private const float tolerance = 1e-4f;

    [Test]
    public void ToUnity_MapsEachAxis()
    {
        Assert.AreEqual(new Vector3(0, 0, 10), CarFrames.ToUnity(1, 0, 0), "x forward is +Z");
        Assert.AreEqual(new Vector3(-10, 0, 0), CarFrames.ToUnity(0, 1, 0), "y left is -X");
        Assert.AreEqual(new Vector3(0, 10, 0), CarFrames.ToUnity(0, 0, 1), "z up is +Y");
    }

    [Test]
    public void FromUnity_RoundTrips()
    {
        (double x, double y, double z) = CarFrames.FromUnity(CarFrames.ToUnity(1.25, -0.5, 0.195));
        Assert.AreEqual(1.25, x, 1e-6);
        Assert.AreEqual(-0.5, y, 1e-6);
        Assert.AreEqual(0.195, z, 1e-6);
    }

    [Test]
    public void Yaw_LeftTurnIsNegativeUnityYaw()
    {
        // Counterclockwise from above is a left turn; Unity's +Y rotation turns right
        Assert.AreEqual(-90f, CarFrames.YawToUnity(Mathf.PI / 2), CarFramesTests.tolerance);
        Vector3 forward = CarFrames.RotationToUnity(Mathf.PI / 2) * Vector3.forward;
        Assert.AreEqual(-1f, forward.x, CarFramesTests.tolerance, "facing car +y (left) is Unity -X");
    }

    [TestCase(0.0)]
    [TestCase(1.0)]
    [TestCase(-2.5)]
    [TestCase(3.1)]
    public void Yaw_RoundTrips(double yaw)
    {
        Assert.AreEqual(yaw, CarFrames.YawFromUnity(CarFrames.YawToUnity(yaw)), 1e-5);
    }

    [Test]
    public void Yaw_FullTurnWraps()
    {
        Assert.AreEqual(0.5, CarFrames.YawFromUnity(CarFrames.YawToUnity(0.5 + 2 * Mathf.PI)), 1e-5);
        Assert.AreEqual(System.Math.PI, System.Math.Abs(CarFrames.WrapAngle(3 * System.Math.PI)), 1e-9);
    }

    [Test]
    public void QuaternionToYaw_ReadsHeading()
    {
        double half = System.Math.PI / 8;
        Assert.AreEqual(System.Math.PI / 4, CarFrames.QuaternionToYaw(0, 0, System.Math.Sin(half), System.Math.Cos(half)), 1e-12);
        Assert.AreEqual(0, CarFrames.QuaternionToYaw(0, 0, 0, 1), 1e-12);
    }

    [Test]
    public void RootPose_IsHalfAWheelbaseAhead()
    {
        (Vector3 position, Quaternion rotation) = CarFrames.RootPose(0, 0, 0);
        Assert.AreEqual(1.125f, position.z, CarFramesTests.tolerance);
        Assert.AreEqual(0f, position.x, CarFramesTests.tolerance);

        // Facing left (+y), the root is 0.1125 m further along +y, Unity -X
        (Vector3 left, Quaternion _) = CarFrames.RootPose(1, 2, System.Math.PI / 2);
        Assert.AreEqual(-21.125f, left.x, CarFramesTests.tolerance);
        Assert.AreEqual(10f, left.z, CarFramesTests.tolerance);
    }

    [Test]
    public void BaseLinkPose_InvertsRootPose()
    {
        (Vector3 position, Quaternion rotation) = CarFrames.RootPose(3.2, -1.4, 2.0);
        (double x, double y, double yaw) = CarFrames.BaseLinkPose(position, rotation);
        Assert.AreEqual(3.2, x, 1e-4);
        Assert.AreEqual(-1.4, y, 1e-4);
        Assert.AreEqual(2.0, yaw, 1e-4);
    }

    [Test]
    public void OffsetToUnity_PlacesTheLidar()
    {
        // Lidar at x 238.3, z 195.0 mm in base_link: 125.8 mm ahead of the root, 195 mm up
        Vector3 offset = CarFrames.OffsetToUnity(238.3, 0, 195.0);
        Assert.AreEqual(1.258f, offset.z, CarFramesTests.tolerance);
        Assert.AreEqual(1.95f, offset.y, CarFramesTests.tolerance);
        Assert.AreEqual(0f, offset.x, CarFramesTests.tolerance);
    }
}
