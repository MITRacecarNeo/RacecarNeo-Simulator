using UnityEngine;

/// <summary>
/// A per-axis sensor bias for realism mode: drawn once per level from a normal distribution, then
/// drifting as a random walk kept within three standard deviations.
/// </summary>
public class SensorBias
{
    #region Public Interface
    /// <summary>
    /// Creates a bias and draws its starting value.
    /// </summary>
    /// <param name="sigma">Standard deviation of the starting bias per axis.</param>
    /// <param name="driftPerRootSecond">Random walk per axis after one second, as a fraction of
    /// sigma; 0 for a bias that does not drift.</param>
    public SensorBias(float sigma, float driftPerRootSecond)
    {
        this.sigma = sigma;
        this.drift = driftPerRootSecond * sigma;
        this.Value = new Vector3(NormalDist.Random(0, sigma), NormalDist.Random(0, sigma), NormalDist.Random(0, sigma));
    }

    /// <summary>
    /// The current bias per axis.
    /// </summary>
    public Vector3 Value { get; private set; }

    /// <summary>
    /// The largest bias on any axis: three standard deviations.
    /// </summary>
    public float Bound
    {
        get { return 3 * this.sigma; }
    }

    /// <summary>
    /// Advances the drift by one step.
    /// </summary>
    /// <param name="deltaTime">Step length (in seconds).</param>
    public void Step(float deltaTime)
    {
        if (this.drift <= 0)
        {
            return;
        }
        float step = this.drift * Mathf.Sqrt(deltaTime);
        Vector3 next = this.Value + new Vector3(NormalDist.Random(0, step), NormalDist.Random(0, step), NormalDist.Random(0, step));
        this.Value = new Vector3(
            Mathf.Clamp(next.x, -this.Bound, this.Bound),
            Mathf.Clamp(next.y, -this.Bound, this.Bound),
            Mathf.Clamp(next.z, -this.Bound, this.Bound));
    }
    #endregion

    private readonly float sigma;

    private readonly float drift;
}
