using UnityEngine;

/// <summary>
/// Starting a level from a menu or the command line: the scene for a mode, the level settings
/// LevelManager reads on load, and the first-autograder username reminder.
/// </summary>
public static class LevelLaunch
{
    /// <summary>
    /// The scene to load: the first autograder scene in Autograder mode; in Race and Autograder a
    /// random variant when the level has them (HasRandomMaps); otherwise BuildIndex.
    /// </summary>
    public static int BuildIndex(LevelInfo level, LevelManagerMode mode)
    {
        int[] variants = level.RandomSceneBuildIndices;
        if (level.HasRandomMaps && mode != LevelManagerMode.Exploration && variants != null && variants.Length > 0)
        {
            return variants[Random.Range(0, variants.Length)];
        }
        return mode == LevelManagerMode.Autograder ? level.AutograderBuildIndex : level.BuildIndex;
    }

    /// <summary>
    /// The number of cars a level starts with: cars clamped to 1 to MaxCars, and 1 outside Race.
    /// </summary>
    public static int Cars(LevelInfo level, LevelManagerMode mode, int cars)
    {
        return mode == LevelManagerMode.Race ? Mathf.Clamp(cars, 1, level.MaxCars) : 1;
    }

    /// <summary>
    /// True when the course ID reminder should show before this start: every Autograder start while
    /// the course ID (Settings.Username) is still the default, since the default gives no usable score code.
    /// </summary>
    public static bool NeedsUsernameReminder(LevelManagerMode mode)
    {
        return mode == LevelManagerMode.Autograder && Settings.Username == Settings.DefaultUsername;
    }

    /// <summary>
    /// Sets the level, mode, and car count LevelManager reads on load and returns the scene to
    /// load (BuildIndex).
    /// </summary>
    public static int Prepare(LevelInfo level, LevelManagerMode mode, int cars)
    {
        LevelManager.LevelInfo = level;
        LevelManager.LevelManagerMode = mode;
        LevelManager.NumPlayers = LevelLaunch.Cars(level, mode, cars);
        return LevelLaunch.BuildIndex(level, mode);
    }
}
