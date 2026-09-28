using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level loading and cleanup shared by the PlayMode tests.
/// </summary>
public static class PlayModeLevels
{
    /// <summary>
    /// The longest a test waits for a scene load or level result, in real seconds.
    /// </summary>
    public const float TimeoutSeconds = 30;

    /// <summary>
    /// Returns the level with the given display name.
    /// </summary>
    public static LevelInfo Find(string displayName)
    {
        return LevelCollection.LevelCollections.SelectMany(collection => collection.Levels).First(level => level.DisplayName == displayName);
    }

    /// <summary>
    /// Skips the test unless saved data is redirected, since levels read and write best times.
    /// </summary>
    public static void RequireDataDirectory()
    {
        if (string.IsNullOrEmpty(LaunchOptions.Current.DataDirectory))
        {
            Assert.Ignore("Run with -racecarsim-data-dir DIR so saved data is not modified.");
        }
    }

    /// <summary>
    /// Loads a level scene as the main menu would, then waits for its LevelManager to start.
    /// </summary>
    public static IEnumerator Load(LevelInfo level, LevelManagerMode mode, int buildIndex)
    {
        LevelManager.LevelInfo = level;
        LevelManager.LevelManagerMode = mode;
        LevelManager.NumPlayers = 1;
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == buildIndex, $"scene {buildIndex} to load");

        // Awake runs on load, Start before the next frame's Update
        yield return null;
        yield return null;
    }

    /// <summary>
    /// Waits until a condition holds, failing the test after TimeoutSeconds.
    /// </summary>
    public static IEnumerator WaitFor(Func<bool> condition, string description)
    {
        float deadline = Time.realtimeSinceStartup + PlayModeLevels.TimeoutSeconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Assert.Fail($"Timed out waiting for {description}.");
            }
            yield return null;
        }
    }

    /// <summary>
    /// Replaces every loaded scene with an empty one, destroying the level and closing its UDP
    /// ports, and restores global state the tests change.
    /// </summary>
    public static IEnumerator Unload()
    {
        Time.timeScale = 1;
        Time.fixedDeltaTime = Settings.DefaultFixedDeltaTime;

        Scene empty = SceneManager.CreateScene($"Empty{Time.frameCount}");
        SceneManager.SetActiveScene(empty);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != empty)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        AutograderManager.ResetAutograder();
        LevelManager.LevelInfo = LevelInfo.DefaultLevel;
        LevelManager.LevelManagerMode = LevelManagerMode.Exploration;
    }
}
