using UnityEngine;

/// <summary>
/// Whether presentation-only features (transitions, splash, flyover, countdown, music) run.
/// Scripted runs and tests skip them so their timing and output match earlier versions.
/// </summary>
public static class Presentation
{
    /// <summary>
    /// Forces IsInteractive on or off; null uses the launch options. For tests.
    /// </summary>
    public static bool? Override;

    /// <summary>
    /// True for a person at the keyboard: not batch mode, no level requested on the command line,
    /// and no autostart.
    /// </summary>
    public static bool IsInteractive => Presentation.Override ?? Presentation.IsInteractiveRun(LaunchOptions.Current, Application.isBatchMode);

    /// <summary>
    /// True when the given launch options and batch mode describe an interactive run.
    /// </summary>
    public static bool IsInteractiveRun(LaunchOptions.Options options, bool isBatchMode)
    {
        return !isBatchMode && string.IsNullOrEmpty(options.LevelId) && !options.AutoStart;
    }
}
