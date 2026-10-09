using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SensorBias starting spread and bounded drift; PhysicsModule axis conversion.
/// </summary>
public class SensorBiasTests
{
    [Test]
    public void Drift_StaysWithinThreeSigmaOverTenMinutes()
    {
        SensorBias bias = new SensorBias(0.05f, 0.04f);
        for (int i = 0; i < 30000; i++)
        {
            bias.Step(0.02f);
            Vector3 value = bias.Value;
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)), bias.Bound + 1e-6f);
        }
    }

    [Test]
    public void StartingValue_HasRequestedSpread()
    {
        double sumSquares = 0;
        const int count = 4000;
        for (int i = 0; i < count; i++)
        {
            Vector3 value = new SensorBias(0.005f, 0).Value;
            sumSquares += value.x * value.x + value.y * value.y + value.z * value.z;
        }
        Assert.AreEqual(0.005, System.Math.Sqrt(sumSquares / (3 * count)), 0.0003);
    }

    [Test]
    public void ZeroDrift_IsConstant()
    {
        SensorBias bias = new SensorBias(1e-6f, 0);
        Vector3 start = bias.Value;
        bias.Step(1);
        Assert.AreEqual(start, bias.Value);
    }

    [Test]
    public void ToBodyAxes_MapsUnityLocalToRep103()
    {
        // Unity local: x right, y up, z forward; REP-103: x forward, y left, z up
        Assert.AreEqual(new Vector3(1, 0, 0), PhysicsModule.ToBodyAxes(new Vector3(0, 0, 1)));
        Assert.AreEqual(new Vector3(0, -1, 0), PhysicsModule.ToBodyAxes(new Vector3(1, 0, 0)));
        Assert.AreEqual(new Vector3(0, 0, 1), PhysicsModule.ToBodyAxes(new Vector3(0, 1, 0)));
    }
}
