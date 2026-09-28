using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Checkpoint and finish handling in a loaded race level.
/// </summary>
public class RaceTests
{
    private const string levelName = "Lab F: Line Follower";

    [UnitySetUp]
    public IEnumerator LoadRace()
    {
        PlayModeLevels.RequireDataDirectory();
        LevelInfo level = PlayModeLevels.Find(RaceTests.levelName);
        yield return PlayModeLevels.Load(level, LevelManagerMode.Race, level.BuildIndex);
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Checkpoints_CountOnlyInOrder()
    {
        int numCheckpoints = LevelManager.LevelInfo.NumCheckpoints;
        Assert.Greater(numCheckpoints, 1);
        Assert.AreEqual(0, LevelManager.GetKeyPointIndex(0));

        LevelManager.HandleCheckpoint(0, 1);
        Assert.AreEqual(0, LevelManager.GetKeyPointIndex(0), "Checkpoint 1 counted before checkpoint 0.");

        LevelManager.HandleFinish(0);
        Assert.AreEqual(0, LevelManager.GetKeyPointIndex(0), "Finish counted before any checkpoint.");

        for (int i = 0; i < numCheckpoints; i++)
        {
            LevelManager.HandleCheckpoint(0, i);
            LevelManager.HandleCheckpoint(0, i);
            Assert.AreEqual(i + 1, LevelManager.GetKeyPointIndex(0), $"After checkpoint {i}.");
        }

        LevelManager.HandleFinish(0);
        Assert.AreEqual(numCheckpoints + 1, LevelManager.GetKeyPointIndex(0));
        Assert.AreEqual(SimulationMode.Finished, LevelManager.Mode);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Failure_StopsCheckpointProgress()
    {
        LevelManager.HandleCheckpoint(0, 0);
        LevelManager.HandleFailure(0, "Test failure.");
        LevelManager.HandleCheckpoint(0, 1);

        Assert.AreEqual(1, LevelManager.GetKeyPointIndex(0));
        Assert.AreNotEqual(SimulationMode.Finished, LevelManager.Mode);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TimePenalty_AccumulatesInRace()
    {
        LevelManager.AddTimePenalty(2.5f);
        LevelManager.AddTimePenalty(1);

        Assert.AreEqual(3.5f, LevelManager.TimePenalty, 1e-4f);
        yield return null;
    }
}
