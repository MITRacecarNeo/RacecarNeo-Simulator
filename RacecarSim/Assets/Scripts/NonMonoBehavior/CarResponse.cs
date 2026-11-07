using UnityEngine;

/// <summary>
/// The physical car's speed response to a drive command, used with Real car response on: shapes
/// Drive's target speed so the car follows the curves measured on the reference car (firmware
/// v0.9.2, 2026-11-07) instead of reaching the command directly.
/// </summary>
/// <remarks>
/// Measured as speed over setpoint after a step from rest (setpoints 0.5 to 1.0 m/s). Forward
/// overshoots to about 1.17 times the setpoint at 1.25 s, then settles back over a few seconds.
/// Reverse starts quickly, then climbs to about 1.31 times the setpoint by 2.5 s and stays there.
/// The shape scales with the target, so faster commands follow the same curves; the runs ended
/// after 2.5 to 3 s, so the later settling is extrapolated.
/// <para>
/// Each shape is a sum of first-order lags on the target:
/// rise + Bump * (fast - slow) + (Gain - 1) * gain, whose step response is
/// (1 - e^(-t/Rise)) + Bump * (e^(-t/Slow) - e^(-t/Fast)) + (Gain - 1) * (1 - e^(-t/GainLag)).
/// </para>
/// </remarks>
public class CarResponse
{
    #region Constants
    /// <summary>
    /// The time constants (in seconds) and gains of one direction's speed shape.
    /// </summary>
    public readonly struct Shape
    {
        public readonly float Rise;
        public readonly float Bump;
        public readonly float Fast;
        public readonly float Slow;
        public readonly float Gain;
        public readonly float GainLag;

        public Shape(float rise, float bump, float fast, float slow, float gain, float gainLag)
        {
            this.Rise = rise;
            this.Bump = bump;
            this.Fast = fast;
            this.Slow = slow;
            this.Gain = gain;
            this.GainLag = gainLag;
        }

        /// <summary>
        /// The shaped speed over the target at a time after a step from rest.
        /// </summary>
        public float StepResponse(float time)
        {
            return (1 - Mathf.Exp(-time / this.Rise))
                + this.Bump * (Mathf.Exp(-time / this.Slow) - Mathf.Exp(-time / this.Fast))
                + (this.Gain - 1) * (1 - Mathf.Exp(-time / this.GainLag));
        }
    }

    /// <summary>
    /// Forward: rise, overshoot to about 1.17, settle at the target. The rise is shorter than the
    /// fitted 0.32 s by Drive's own speed loop lag.
    /// </summary>
    public static readonly Shape Forward = new Shape(0.24f, 3.0f, 0.7914f, 0.9505f, 1.0f, 0.8f);

    /// <summary>
    /// Reverse: fast start, then a climb to 1.31 times the target.
    /// </summary>
    public static readonly Shape Reverse = new Shape(0.0987f, -2.9946f, 0.4026f, 0.5678f, 1.3098f, 0.5704f);
    #endregion

    #region Public Interface
    /// <summary>
    /// Advances the shape by one step and returns the speed the car should hold now.
    /// </summary>
    /// <param name="target">The commanded speed (any unit; the result is in the same unit).</param>
    /// <param name="deltaTime">The step length (in seconds).</param>
    public float Step(float target, float deltaTime)
    {
        Shape shape = target >= 0 ? CarResponse.Forward : CarResponse.Reverse;
        this.rise = CarResponse.Lag(this.rise, target, shape.Rise, deltaTime);
        this.fast = CarResponse.Lag(this.fast, target, shape.Fast, deltaTime);
        this.slow = CarResponse.Lag(this.slow, target, shape.Slow, deltaTime);
        this.gain = CarResponse.Lag(this.gain, target, shape.GainLag, deltaTime);
        return this.rise + shape.Bump * (this.fast - this.slow) + (shape.Gain - 1) * this.gain;
    }

    /// <summary>
    /// Starts the next command from a speed: the shape continues from it as if the car had been
    /// commanded there without its overshoot or reverse climb. Drive calls this while neutral.
    /// </summary>
    public void Reset(float speed)
    {
        this.rise = speed;
        this.fast = speed;
        this.slow = speed;
        this.gain = 0;
    }
    #endregion

    private float rise;
    private float fast;
    private float slow;
    private float gain;

    /// <summary>
    /// One exact step of a first-order lag toward a target.
    /// </summary>
    private static float Lag(float value, float target, float timeConstant, float deltaTime)
    {
        return target + (value - target) * Mathf.Exp(-deltaTime / timeConstant);
    }
}
