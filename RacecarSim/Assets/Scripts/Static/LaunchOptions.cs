using System;
using System.Linq;

/// <summary>
/// Command-line options for starting a level without the main menu, for scripted runs:
/// <code>RacecarSim -racecarsim-level grand-prix-2026 -racecarsim-mode race -racecarsim-cars 2 -racecarsim-autostart</code>
/// -racecarsim-level takes a LevelInfo.Id; -racecarsim-mode is exploration, race, or autograder
/// (default race); -racecarsim-cars defaults to 1; -racecarsim-autostart enters User Program mode
/// once every car has a connected Python program; -racecarsim-data-dir stores saved data (best
/// times, car colors) in the given folder instead of Application.persistentDataPath.
/// -racecarsim-level also accepts a level's display name in lowercase with hyphens (for example
/// demo-world), for levels without an Id. -racecarsim-timescale sets the starting time scale;
/// -racecarsim-diagnostics CSV enables SimDiagnostics, with -racecarsim-screenshot-interval
/// and -racecarsim-restart-interval in seconds. -racecarsim-username sets the username for this
/// session only (not saved), so an unattended autograder run produces a score code.
/// -racecarsim-frametime CSV enables FrameTimeLog.
/// </summary>
public static class LaunchOptions
{
    /// <summary>
    /// The options parsed from a command line.
    /// </summary>
    public class Options
    {
        public string LevelId;
        public LevelManagerMode Mode = LevelManagerMode.Race;
        public int NumCars = 1;
        public bool AutoStart;
        public string DataDirectory;
        public float TimeScale = 1;
        public string DiagnosticsPath;
        public float ScreenshotInterval;
        public float RestartInterval;
        public string Username;
        public string FrameTimePath;
    }

    /// <summary>
    /// The options passed to this process.
    /// </summary>
    public static readonly Options Current = LaunchOptions.Parse(Environment.GetCommandLineArgs());

    /// <summary>
    /// True once the requested level was loaded, so returning to the main menu shows the menu.
    /// </summary>
    private static bool wasLevelRequestTaken;

    /// <summary>
    /// Parses launch options; unknown arguments are ignored.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed options.</returns>
    public static Options Parse(string[] args)
    {
        Options options = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i].ToLowerInvariant())
            {
                case "-racecarsim-level":
                    options.LevelId = next;
                    break;
                case "-racecarsim-mode":
                    if (Enum.TryParse(next, true, out LevelManagerMode mode))
                    {
                        options.Mode = mode;
                    }
                    break;
                case "-racecarsim-cars":
                    if (int.TryParse(next, out int cars) && cars > 0)
                    {
                        options.NumCars = cars;
                    }
                    break;
                case "-racecarsim-autostart":
                    options.AutoStart = true;
                    break;
                case "-racecarsim-data-dir":
                    options.DataDirectory = next;
                    break;
                case "-racecarsim-timescale":
                    options.TimeScale = LaunchOptions.ParsePositive(next, options.TimeScale);
                    break;
                case "-racecarsim-diagnostics":
                    options.DiagnosticsPath = next;
                    break;
                case "-racecarsim-screenshot-interval":
                    options.ScreenshotInterval = LaunchOptions.ParsePositive(next, 0);
                    break;
                case "-racecarsim-username":
                    options.Username = next;
                    break;
                case "-racecarsim-frametime":
                    options.FrameTimePath = next;
                    break;
                case "-racecarsim-restart-interval":
                    options.RestartInterval = LaunchOptions.ParsePositive(next, 0);
                    break;
            }
        }
        return options;
    }

    /// <summary>
    /// Lowercase letters and digits of a name joined by single hyphens ("Lab 5: AR Tags" -> "lab-5-ar-tags").
    /// </summary>
    public static string Slug(string name)
    {
        string lower = new string((name ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        return string.Join("-", lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static float ParsePositive(string text, float fallback)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0 ? value : fallback;
    }

    /// <summary>
    /// Returns the level requested on the command line the first time it is called, then false.
    /// </summary>
    /// <param name="level">The requested level, if found.</param>
    /// <returns>True if a known level was requested and not yet taken.</returns>
    public static bool TryTakeLevelRequest(out LevelInfo level)
    {
        level = null;
        if (LaunchOptions.wasLevelRequestTaken || string.IsNullOrEmpty(LaunchOptions.Current.LevelId))
        {
            return false;
        }
        LaunchOptions.wasLevelRequestTaken = true;

        string requested = LaunchOptions.Current.LevelId;
        level = LevelCollection.LevelCollections
            .SelectMany(collection => collection.Levels)
            .FirstOrDefault(info => info.Id == requested || LaunchOptions.Slug(info.DisplayName) == requested);
        if (level == null)
        {
            UnityEngine.Debug.LogError($"Unknown level id [{LaunchOptions.Current.LevelId}] passed to -racecarsim-level.");
        }
        return level != null;
    }
}
