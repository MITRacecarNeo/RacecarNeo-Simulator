using System;
using System.Collections.Generic;

/// <summary>
/// Estimates the offset between the car's clock and the sim's from time requests: the car's
/// reply is taken as read halfway through the round trip.
/// </summary>
/// <remarks>
/// Keeps the latest samples; the estimate is the median offset of the faster half, since a slow
/// round trip says least about when the car read its clock.
/// </remarks>
public class LinkClock
{
    #region Constants
    /// <summary>
    /// Samples kept for the estimate.
    /// </summary>
    public const int Capacity = 20;

    /// <summary>
    /// Samples needed before the estimate is used.
    /// </summary>
    public const int MinSamples = 3;
    #endregion

    #region Public Interface
    /// <summary>
    /// Car time minus sim time (in seconds).
    /// </summary>
    public double Offset { get; private set; }

    /// <summary>
    /// Round trip (in seconds) of the fastest kept sample.
    /// </summary>
    public double RoundTrip { get; private set; }

    public int SampleCount { get { return this.samples.Count; } }

    /// <summary>
    /// True once enough samples are kept for the offset to be used.
    /// </summary>
    public bool IsSynchronized { get { return this.samples.Count >= LinkClock.MinSamples; } }

    /// <summary>
    /// Adds one time request.
    /// </summary>
    /// <param name="sentAt">Sim time (in seconds) when the request was sent.</param>
    /// <param name="carTime">Car time (in seconds) in the reply.</param>
    /// <param name="receivedAt">Sim time (in seconds) when the reply arrived.</param>
    public void AddSample(double sentAt, double carTime, double receivedAt)
    {
        double roundTrip = receivedAt - sentAt;
        if (roundTrip < 0 || double.IsNaN(carTime))
        {
            return;
        }
        this.samples.Add((carTime - (sentAt + receivedAt) / 2, roundTrip));
        if (this.samples.Count > LinkClock.Capacity)
        {
            this.samples.RemoveAt(0);
        }

        List<(double offset, double roundTrip)> sorted = new List<(double offset, double roundTrip)>(this.samples);
        sorted.Sort((a, b) => a.roundTrip.CompareTo(b.roundTrip));
        int keep = Math.Max(1, (sorted.Count + 1) / 2);
        List<double> offsets = new List<double>(keep);
        for (int i = 0; i < keep; i++)
        {
            offsets.Add(sorted[i].offset);
        }
        offsets.Sort();
        this.Offset = keep % 2 == 1 ? offsets[keep / 2] : (offsets[keep / 2 - 1] + offsets[keep / 2]) / 2;
        this.RoundTrip = sorted[0].roundTrip;
    }

    /// <summary>
    /// A car time as sim time (in seconds).
    /// </summary>
    public double ToSimTime(double carTime)
    {
        return carTime - this.Offset;
    }

    /// <summary>
    /// A sim time as car time (in seconds).
    /// </summary>
    public double ToCarTime(double simTime)
    {
        return simTime + this.Offset;
    }

    /// <summary>
    /// Forgets every sample (after a reconnect).
    /// </summary>
    public void Reset()
    {
        this.samples.Clear();
        this.Offset = 0;
        this.RoundTrip = 0;
    }
    #endregion

    private readonly List<(double offset, double roundTrip)> samples = new List<(double offset, double roundTrip)>();
}
