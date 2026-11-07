using NUnit.Framework;

/// <summary>
/// CarResponse shapes a step from rest like its fitted curves and settles at each direction's gain.
/// </summary>
public class CarResponseTests
{
    private const float deltaTime = 0.02f;

    [Test]
    public void Step_MatchesShapeStepResponse([Values(1.0f, -1.0f)] float target)
    {
        CarResponse response = new CarResponse();
        CarResponse.Shape shape = target > 0 ? CarResponse.Forward : CarResponse.Reverse;
        for (int i = 1; i <= 150; i++)
        {
            float value = response.Step(target, CarResponseTests.deltaTime) / target;
            Assert.AreEqual(shape.StepResponse(i * CarResponseTests.deltaTime), value, 1e-4f, $"step {i}");
        }
    }

    [Test]
    public void Forward_OvershootsThenSettlesAtTarget()
    {
        CarResponse response = new CarResponse();
        float peak = 0;
        float value = 0;
        for (int i = 0; i < 1000; i++)
        {
            value = response.Step(10, CarResponseTests.deltaTime);
            peak = System.Math.Max(peak, value);
        }
        Assert.AreEqual(11.7f, peak, 0.2f, "peak");
        Assert.AreEqual(10, value, 0.05f, "after 20 s");
    }

    [Test]
    public void Reverse_SettlesAboveTarget()
    {
        CarResponse response = new CarResponse();
        float value = 0;
        for (int i = 0; i < 1000; i++)
        {
            value = response.Step(-10, CarResponseTests.deltaTime);
        }
        Assert.AreEqual(-13.1f, value, 0.05f, "after 20 s");
    }

    [Test]
    public void Reset_HoldsTheCurrentSpeed()
    {
        CarResponse response = new CarResponse();
        response.Reset(5);
        Assert.AreEqual(5, response.Step(5, CarResponseTests.deltaTime), 1e-4f);
    }
}
