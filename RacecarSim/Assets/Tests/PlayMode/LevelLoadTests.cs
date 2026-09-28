using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Every level scene loads in exploration mode and runs without logging an error or exception.
/// </summary>
public class LevelLoadTests
{
    /// <summary>
    /// Frames to run after loading, covering Start and the first updates.
    /// </summary>
    private const int framesToRun = 30;

    /// <summary>
    /// Separates a level's display name from a random-map build index in a case name.
    /// </summary>
    private const string randomMapSeparator = " #";

    /// <summary>
    /// Each level's display name, plus "name #index" for each of its random maps.
    /// </summary>
    public static IEnumerable<string> LevelScenes()
    {
        foreach (LevelInfo level in LevelCollection.LevelCollections.SelectMany(collection => collection.Levels))
        {
            yield return level.DisplayName;
            if (level.HasRandomMaps && level.RandomSceneBuildIndices != null)
            {
                foreach (int index in level.RandomSceneBuildIndices.Where(index => index != level.BuildIndex))
                {
                    yield return level.DisplayName + LevelLoadTests.randomMapSeparator + index;
                }
            }
        }
    }

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Level_RunsWithoutErrors([ValueSource(nameof(LevelScenes))] string levelScene)
    {
        string[] parts = levelScene.Split(new[] { LevelLoadTests.randomMapSeparator }, System.StringSplitOptions.None);
        LevelInfo level = PlayModeLevels.Find(parts[0]);
        int buildIndex = parts.Length > 1 ? int.Parse(parts[1]) : level.BuildIndex;

        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, buildIndex);
        for (int i = 0; i < LevelLoadTests.framesToRun; i++)
        {
            yield return null;
        }

        Assert.IsNotNull(LevelManager.GetCar(), $"{levelScene} spawned no car.");
    }
}
