using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Frame-time measurements for scripted performance runs, enabled with -racecarsim-frametime
/// (see LaunchOptions). Writes one CSV row per frame: wall frame time, Unity's CPU main-thread,
/// present-wait, render-thread, and GPU times (FrameTimingManager, a few frames behind), physics
/// steps in the frame, and the CPU time of each SimProfiler section. Runs at 60 frames per second
/// without vsync, so the main-thread and GPU times hold work, not waiting for the display.
/// </summary>
[DefaultExecutionOrder(10000)]
public class FrameTimeLog : MonoBehaviour
{
    /// <summary>
    /// Frame rate of a frame-time run, the display rate most players run at.
    /// </summary>
    public const int FramesPerSecond = 60;

    private StreamWriter writer;

    private readonly FrameTiming[] timings = new FrameTiming[1];

    private readonly StringBuilder row = new StringBuilder();

    private int physicsSteps;

    private void Start()
    {
        string path = LaunchOptions.Current.FrameTimePath;
        bool append = File.Exists(path);
        this.writer = new StreamWriter(path, append);
        if (!append)
        {
            StringBuilder header = new StringBuilder("level,frame,time,frame_ms,cpu_main_ms,cpu_present_wait_ms,cpu_render_ms,gpu_ms,physics_steps");
            for (int i = 0; i < SimProfiler.Count; i++)
            {
                header.Append(',').Append(((SimProfiler.Section)i).ToString()).Append("_ms");
            }
            this.writer.WriteLine(header);
        }
        SimProfiler.Enabled = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FrameTimeLog.FramesPerSecond;
    }

    private void FixedUpdate()
    {
        this.physicsSteps++;
    }

    private void LateUpdate()
    {
        FrameTimingManager.CaptureFrameTimings();
        bool hasTiming = FrameTimingManager.GetLatestTimings(1, this.timings) > 0;
        CultureInfo culture = CultureInfo.InvariantCulture;
        this.row.Clear();
        this.row.Append(LaunchOptions.Slug(LevelManager.LevelInfo.DisplayName)).Append(',')
            .Append(Time.frameCount).Append(',')
            .Append(Time.unscaledTime.ToString("F3", culture)).Append(',')
            .Append((Time.unscaledDeltaTime * 1000).ToString("F3", culture)).Append(',')
            .Append(hasTiming ? this.timings[0].cpuMainThreadFrameTime.ToString("F3", culture) : "nan").Append(',')
            .Append(hasTiming ? this.timings[0].cpuMainThreadPresentWaitTime.ToString("F3", culture) : "nan").Append(',')
            .Append(hasTiming ? this.timings[0].cpuRenderThreadFrameTime.ToString("F3", culture) : "nan").Append(',')
            .Append(hasTiming ? this.timings[0].gpuFrameTime.ToString("F3", culture) : "nan").Append(',')
            .Append(this.physicsSteps);
        for (int i = 0; i < SimProfiler.Count; i++)
        {
            this.row.Append(',').Append(SimProfiler.Take((SimProfiler.Section)i).ToString("F3", culture));
        }
        this.writer.WriteLine(this.row);
        this.physicsSteps = 0;
    }

    private void OnDestroy()
    {
        SimProfiler.Enabled = false;
        this.writer?.Dispose();
    }
}
