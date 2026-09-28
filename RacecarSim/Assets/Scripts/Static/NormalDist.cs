using System;

/// <summary>
/// Samples from a normal distribution for sensor noise.
/// </summary>
public static class NormalDist
{
    /// <summary>
    /// Source of uniform samples. Separate from UnityEngine.Random so sensor noise neither reseeds
    /// nor consumes the global generator used elsewhere (for example random scene selection).
    /// </summary>
    private static readonly System.Random generator = new System.Random();

    /// <summary>
    /// Takes a random sample from a normal distribution.
    /// </summary>
    /// <param name="mean">The mean of the distribution.</param>
    /// <param name="sdev">The standard deviation of the distribution.</param>
    /// <returns>A random number sampled from the specified distribution.</returns>
    public static float Random(float mean = 0, float sdev = 1)
    {
        double u1;
        double u2;
        lock (NormalDist.generator)
        {
            // 1 - NextDouble() lies in (0, 1], so the logarithm below is finite
            u1 = 1.0 - NormalDist.generator.NextDouble();
            u2 = NormalDist.generator.NextDouble();
        }

        // Box-Muller transform
        double standard = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + sdev * (float)standard;
    }
}
