using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Ackermann steering angles (Drive.AckermannAngles).
/// </summary>
public class DriveTests
{
    private const float Wheelbase = 2.25f;
    private const float KingpinTrack = 1.46f;

    [Test]
    public void Straight_BothWheelsZero()
    {
        (float left, float right) = Drive.AckermannAngles(0, Wheelbase, KingpinTrack, 1);
        Assert.AreEqual(0, left);
        Assert.AreEqual(0, right);
    }

    [TestCase(20f)]
    [TestCase(-20f)]
    [TestCase(5f)]
    public void FullAckermann_WheelsShareOneTurnCenterOnTheRearAxle(float angle)
    {
        (float left, float right) = Drive.AckermannAngles(angle, Wheelbase, KingpinTrack, 1);
        float radius = Wheelbase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
        float inner = angle > 0 ? right : left;
        float outer = angle > 0 ? left : right;

        Assert.Greater(Mathf.Abs(inner), Mathf.Abs(angle), "inner wheel turns more");
        Assert.Less(Mathf.Abs(outer), Mathf.Abs(angle), "outer wheel turns less");
        Assert.AreEqual(radius - KingpinTrack / 2, Wheelbase / Mathf.Tan(Mathf.Abs(inner) * Mathf.Deg2Rad), 1e-3f);
        Assert.AreEqual(radius + KingpinTrack / 2, Wheelbase / Mathf.Tan(Mathf.Abs(outer) * Mathf.Deg2Rad), 1e-3f);
        Assert.AreEqual(Mathf.Sign(angle), Mathf.Sign(left));
        Assert.AreEqual(Mathf.Sign(angle), Mathf.Sign(right));
    }

    [Test]
    public void RightTurn_MirrorsLeftTurn()
    {
        (float leftR, float rightR) = Drive.AckermannAngles(15, Wheelbase, KingpinTrack, 1);
        (float leftL, float rightL) = Drive.AckermannAngles(-15, Wheelbase, KingpinTrack, 1);
        Assert.AreEqual(rightR, -leftL, 1e-5f);
        Assert.AreEqual(leftR, -rightL, 1e-5f);
    }

    [Test]
    public void ZeroFraction_IsParallelSteer()
    {
        (float left, float right) = Drive.AckermannAngles(20, Wheelbase, KingpinTrack, 0);
        Assert.AreEqual(20, left, 1e-5f);
        Assert.AreEqual(20, right, 1e-5f);
    }

    [Test]
    public void HalfFraction_LiesBetweenParallelAndFull()
    {
        (float fullLeft, float fullRight) = Drive.AckermannAngles(20, Wheelbase, KingpinTrack, 1);
        (float left, float right) = Drive.AckermannAngles(20, Wheelbase, KingpinTrack, 0.5f);
        Assert.AreEqual((20 + fullLeft) / 2, left, 1e-4f);
        Assert.AreEqual((20 + fullRight) / 2, right, 1e-4f);
    }
}
