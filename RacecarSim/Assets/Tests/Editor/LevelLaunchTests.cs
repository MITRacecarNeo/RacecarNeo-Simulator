using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Level starts from the menu: the scene for each mode, random variants outside Exploration, the
/// car count, and the menu's time format.
/// </summary>
public class LevelLaunchTests
{
    private static LevelInfo Level(string displayName)
    {
        return LevelCollection.LevelCollections.SelectMany(collection => collection.Levels).First(level => level.DisplayName == displayName);
    }

    [Test]
    public void BuildIndex_FollowsMode()
    {
        LevelInfo lab = LevelLaunchTests.Level("Lab F: Line Follower");
        Assert.AreEqual(lab.BuildIndex, LevelLaunch.BuildIndex(lab, LevelManagerMode.Exploration));
        Assert.AreEqual(lab.AutograderBuildIndex, LevelLaunch.BuildIndex(lab, LevelManagerMode.Autograder));
    }

    [Test]
    public void BuildIndex_RandomVariantOutsideExploration()
    {
        LevelInfo random = LevelCollection.LevelCollections.SelectMany(collection => collection.Levels).FirstOrDefault(level => level.HasRandomMaps);
        Assume.That(random, Is.Not.Null, "no level with random maps");
        Assert.AreEqual(random.BuildIndex, LevelLaunch.BuildIndex(random, LevelManagerMode.Exploration));
        Random.InitState(7);
        for (int i = 0; i < 20; i++)
        {
            Assert.Contains(LevelLaunch.BuildIndex(random, LevelManagerMode.Race), random.RandomSceneBuildIndices);
        }
    }

    [Test]
    public void Cars_ClampedAndOneOutsideRace()
    {
        LevelInfo multi = LevelCollection.LevelCollections.SelectMany(collection => collection.Levels).First(level => level.MaxCars > 1);
        Assert.AreEqual(multi.MaxCars, LevelLaunch.Cars(multi, LevelManagerMode.Race, 99));
        Assert.AreEqual(1, LevelLaunch.Cars(multi, LevelManagerMode.Race, 0));
        Assert.AreEqual(1, LevelLaunch.Cars(multi, LevelManagerMode.Exploration, 3));
    }

    [TestCase(9.5f, "9.50")]
    [TestCase(59.999f, "60.00")]
    [TestCase(83.456f, "1:23.46")]
    [TestCase(605.1f, "10:05.10")]
    public void FormatTime_SecondsOrMinutes(float seconds, string expected)
    {
        Assert.AreEqual(expected, MenuController.FormatTime(seconds));
    }
}
