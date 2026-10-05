using System;
using System.Diagnostics;

/// <summary>
/// CPU time of the simulator's own per-frame work, summed per frame for frame-time runs
/// (-racecarsim-frametime, see FrameTimeLog). A disabled measurement costs one branch.
/// </summary>
public static class SimProfiler
{
    /// <summary>
    /// The measured sections of work.
    /// </summary>
    public enum Section
    {
        LidarScan,
        LidarHud,
        ColorReadback,
        DepthImage,
        DepthHud,
        Drive,
        PythonSync,
    }

    /// <summary>
    /// Number of sections.
    /// </summary>
    public static readonly int Count = Enum.GetValues(typeof(Section)).Length;

    /// <summary>
    /// True while a frame-time run records sections.
    /// </summary>
    public static bool Enabled { get; set; }

    private static readonly long[] ticks = new long[SimProfiler.Count];

    /// <summary>
    /// Starts timing a section; dispose the result to add the elapsed time to the section.
    /// </summary>
    public static Scope Measure(Section section)
    {
        return new Scope(section);
    }

    /// <summary>
    /// Returns the milliseconds recorded for a section since the last call, and clears them.
    /// </summary>
    public static double Take(Section section)
    {
        double milliseconds = SimProfiler.ticks[(int)section] * 1000.0 / Stopwatch.Frequency;
        SimProfiler.ticks[(int)section] = 0;
        return milliseconds;
    }

    /// <summary>
    /// One timed run of a section.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly int section;

        private readonly long start;

        public Scope(Section section)
        {
            this.section = (int)section;
            this.start = SimProfiler.Enabled ? Stopwatch.GetTimestamp() : 0;
        }

        public void Dispose()
        {
            if (SimProfiler.Enabled)
            {
                SimProfiler.ticks[this.section] += Stopwatch.GetTimestamp() - this.start;
            }
        }
    }
}
