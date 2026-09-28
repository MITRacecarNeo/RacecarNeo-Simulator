using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Autograder runs through the real level scenes: task order, scoring, time bonuses, time limits,
/// and the hand-off to the summary.
/// </summary>
public class AutograderRunTests
{
    /// <summary>
    /// Every lab with an autograder, by display name.
    /// </summary>
    public static IEnumerable<string> AutograderLabs()
    {
        return LevelCollection.LevelCollections
            .SelectMany(collection => collection.Levels)
            .Where(level => level.AutograderLevels != null && level.AutograderLevels.Length > 0)
            .Select(level => level.DisplayName);
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

    /// <summary>
    /// Completes every task of every trial at once. Each trial must score its task points plus
    /// the first time bonus, and the tasks must be worth MaxPoints; a trial with MaxPoints 0 is
    /// extra credit and exempt from the second check. The run ends at the summary without error.
    /// </summary>
    [UnityTest]
    public IEnumerator CompletingEveryTask_ScoresTaskPointsAndFirstBonus([ValueSource(nameof(AutograderLabs))] string labName)
    {
        LevelInfo lab = PlayModeLevels.Find(labName);
        yield return PlayModeLevels.Load(lab, LevelManagerMode.Autograder, lab.AutograderBuildIndex);

        List<string> failures = new List<string>();
        float expectedTotal = 0;
        for (int i = 0; i < lab.AutograderLevels.Length; i++)
        {
            AutograderLevelInfo info = lab.AutograderLevels[i];
            string trial = $"{labName} / {info.Title}";
            AutograderManager manager = Object.FindAnyObjectByType<AutograderManager>();
            Assert.IsNotNull(manager, $"{trial} has no AutograderManager.");
            AutograderTask[] tasks = manager.GetComponentsInChildrenOrdered<AutograderTask>();

            manager.HandleStart(Object.FindAnyObjectByType<Hud>());
            foreach (AutograderTask task in tasks)
            {
                AutograderManager.CompleteTask(task);
            }

            // A trial without tasks ends at its time limit
            if (tasks.Length == 0)
            {
                Time.timeScale = 20;
            }
            yield return PlayModeLevels.WaitFor(() => AutograderManager.levelScores.Count > i, $"{trial} to finish");

            float taskPoints = tasks.Sum(task => task.Points);
            float bonus = tasks.Length > 0 && info.TimeBonuses != null ? info.TimeBonuses[0].y : 0;
            float score = AutograderManager.levelScores[i].Score;
            expectedTotal += taskPoints + bonus;
            if (Mathf.Abs(score - (taskPoints + bonus)) > 1e-4f)
            {
                failures.Add($"{trial}: score {score}, tasks worth {taskPoints}, first time bonus {bonus}");
            }

            if (info.MaxPoints != 0 && Mathf.Abs(taskPoints - info.MaxPoints) > 1e-4f)
            {
                failures.Add($"{trial}: {tasks.Length} tasks worth {taskPoints}, MaxPoints {info.MaxPoints}");
            }

            bool isLast = i + 1 == lab.AutograderLevels.Length;
            int nextIndex = lab.AutograderBuildIndex + i + 1;
            yield return PlayModeLevels.WaitFor(
                () => isLast ? SceneManager.GetActiveScene().name == AutograderManager.AutograderSummaryScene : SceneManager.GetActiveScene().buildIndex == nextIndex,
                $"the scene after {trial}");
            yield return null;
            yield return null;
        }

        Assert.IsEmpty(failures, string.Join("\n", failures));
        Assert.AreEqual(expectedTotal, AutograderManager.levelScores.Sum(level => level.Score), 1e-3f);
        Assert.IsFalse(AutograderSummary.WasError);
    }

    [UnityTest]
    public IEnumerator OutOfOrderTask_IsIgnored()
    {
        LevelInfo lab = PlayModeLevels.Find("Lab 2a: Line Following");
        yield return PlayModeLevels.Load(lab, LevelManagerMode.Autograder, lab.AutograderBuildIndex);
        AutograderManager manager = Object.FindAnyObjectByType<AutograderManager>();
        AutograderTask[] tasks = manager.GetComponentsInChildrenOrdered<AutograderTask>();
        Assert.GreaterOrEqual(tasks.Length, 2, "The first Lab 2a trial needs two tasks for this test.");
        manager.HandleStart(Object.FindAnyObjectByType<Hud>());

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not the active task"));
        AutograderManager.CompleteTask(tasks[1]);
        yield return null;

        Assert.IsEmpty(AutograderManager.levelScores);
        Assert.IsFalse(tasks[1].gameObject.activeSelf, "An out-of-order task was enabled.");
    }

    [UnityTest]
    public IEnumerator TimeLimit_EndsTrialWithPenaltyInTime()
    {
        LevelInfo lab = PlayModeLevels.Find("Lab F: Line Follower");
        AutograderLevelInfo info = lab.AutograderLevels[0];
        yield return PlayModeLevels.Load(lab, LevelManagerMode.Autograder, lab.AutograderBuildIndex);
        AutograderManager manager = Object.FindAnyObjectByType<AutograderManager>();

        manager.HandleStart(Object.FindAnyObjectByType<Hud>());
        float startTime = Time.time;
        LevelManager.AddTimePenalty(3);
        Time.timeScale = 20;
        float elapsedAtFinish = 0;
        yield return PlayModeLevels.WaitFor(() =>
        {
            elapsedAtFinish = Time.time - startTime;
            return AutograderManager.levelScores.Count > 0;
        }, "the time limit");

        AutograderLevelScore result = AutograderManager.levelScores[0];
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(3, result.Time - elapsedAtFinish, 1e-3f, "The penalty is not part of the recorded time.");
        Assert.GreaterOrEqual(result.Time, info.TimeLimit);
    }
}
