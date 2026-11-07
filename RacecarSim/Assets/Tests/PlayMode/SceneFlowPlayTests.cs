using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Scene changes through SceneFlow in an interactive run: a level load from the menu shows the
/// loading card under the cover for at least CardMinimumSeconds and lands on the level; a restart
/// slides the cover in over the frozen level and keeps it up through the ReloadBuffer scene; an
/// autograder trial change slides the cover in over the frozen trial; every change ends uncovered.
/// </summary>
public class SceneFlowPlayTests
{
    [SetUp]
    public void Interactive()
    {
        PlayModeLevels.RequireDataDirectory();
        Presentation.Override = true;
    }

    [UnityTearDown]
    public IEnumerator Release()
    {
        Presentation.Override = null;
        GameObject overlay = GameObject.Find("SceneFlowOverlay");
        if (overlay != null)
        {
            Object.Destroy(overlay);
        }
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator LoadLevel_ShowsCardThenLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        LevelManager.LevelInfo = level;
        LevelManager.LevelManagerMode = LevelManagerMode.Exploration;
        LevelManager.NumPlayers = 1;

        SceneFlow.LoadLevel(level.BuildIndex);
        Assert.IsTrue(SceneFlow.IsTransitioning, "transition starts on the request");

        VisualElement root = GameObject.Find("SceneFlowOverlay").GetComponent<UIDocument>().rootVisualElement;
        yield return PlayModeLevels.WaitFor(() => root.Q("card").ClassListContains("sf-card--visible"), "the loading card");
        Assert.AreEqual(level.DisplayName, root.Q<Label>("card-title").text);
        Assert.AreEqual(LevelPresentationCatalog.Get(level).Thumbnail, root.Q("card-thumbnail").resolvedStyle.backgroundImage.texture, "the level's thumbnail on the card");
        Assert.AreEqual(1, root.Q<TransitionWipe>("wipe").Progress, 1e-4, "covered while the card shows");
        Assert.AreEqual((Color)ThemeTokens.Charcoal, root.Q<TransitionWipe>("wipe").CoverColor, "exploration cover is charcoal");

        float cardShown = Time.realtimeSinceStartup;
        TransitionWipe wipe = root.Q<TransitionWipe>("wipe");

        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == level.BuildIndex, "the level");
        yield return PlayModeLevels.WaitFor(() => wipe.Progress > 1, "the reveal to start");
        Assert.GreaterOrEqual(Time.realtimeSinceStartup - cardShown, SceneFlow.CardMinimumSeconds - 0.1f, "card shown for the minimum time");
        yield return PlayModeLevels.WaitFor(() => !SceneFlow.IsTransitioning, "the reveal");
        Assert.AreEqual(DisplayStyle.None, root.Q("scene-flow").resolvedStyle.display, "overlay hidden after the reveal");
    }

    [UnityTest]
    public IEnumerator Restart_HoldsCoverThroughReloadBuffer()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);

        LevelManager manager = Object.FindAnyObjectByType<LevelManager>();
        LevelManager.RestartLevel();
        VisualElement root = GameObject.Find("SceneFlowOverlay").GetComponent<UIDocument>().rootVisualElement;
        TransitionWipe wipe = root.Q<TransitionWipe>("wipe");

        // The cover slides in over the frozen level
        yield return null;
        Assert.AreEqual(level.BuildIndex, SceneManager.GetActiveScene().buildIndex, "level still shown while the cover slides in");
        Assert.IsFalse(manager.enabled, "level manager stopped");
        Assert.AreEqual(0, Time.timeScale, "time stopped");
        Assert.Less(wipe.Progress, 1, "cover still sliding in");

        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == ReloadBuffer.BuildIndex, "the reload buffer");
        Assert.AreEqual(1, Time.timeScale, "time resumes for the next scene");
        while (SceneManager.GetActiveScene().buildIndex == ReloadBuffer.BuildIndex)
        {
            Assert.IsTrue(SceneFlow.IsTransitioning, "still transitioning in the buffer");
            Assert.AreEqual(1, wipe.Progress, 1e-4, "covered in the buffer");
            yield return null;
        }

        Assert.AreEqual(level.BuildIndex, SceneManager.GetActiveScene().buildIndex, "the level reloads");
        yield return PlayModeLevels.WaitFor(() => !SceneFlow.IsTransitioning, "the reveal");
    }

    [UnityTest]
    public IEnumerator AutograderTrial_WipesOverFrozenTrial()
    {
        LevelInfo lab = PlayModeLevels.Find("Lab 2a: Line Following");
        yield return PlayModeLevels.Load(lab, LevelManagerMode.Autograder, lab.AutograderBuildIndex);
        AutograderManager manager = Object.FindAnyObjectByType<AutograderManager>();
        LevelManager level = Object.FindAnyObjectByType<LevelManager>();
        manager.HandleStart(Object.FindAnyObjectByType<Hud>());
        foreach (AutograderTask task in manager.GetComponentsInChildrenOrdered<AutograderTask>())
        {
            AutograderManager.CompleteTask(task);
        }

        // The first trial freezes while the cover slides in; the Python interface is handed on
        yield return PlayModeLevels.WaitFor(() => !level.enabled, "the trial to freeze");
        VisualElement root = GameObject.Find("SceneFlowOverlay").GetComponent<UIDocument>().rootVisualElement;
        TransitionWipe wipe = root.Q<TransitionWipe>("wipe");
        Assert.AreEqual(lab.AutograderBuildIndex, SceneManager.GetActiveScene().buildIndex, "first trial shown while the cover slides in");
        Assert.AreEqual(0, Time.timeScale, "time stopped");
        Assert.Less(wipe.Progress, 1, "cover still sliding in");
        Assert.IsTrue(root.Q("scene-flow").ClassListContains("sf--autograder"), "autograder tint");
        VisualElement hud = Object.FindAnyObjectByType<Hud>().GetComponent<UIDocument>().rootVisualElement;
        StringAssert.StartsWith("TRIAL 1", hud.Q<Label>("toast-text").text, "trial result shown during the hold");
        Assert.AreNotEqual((Color)ThemeTokens.Charcoal, wipe.CoverColor, "charcoal cover tinted toward crimson");
        Assert.Greater(wipe.CoverColor.r - wipe.CoverColor.b, ((Color)ThemeTokens.Charcoal).r - ((Color)ThemeTokens.Charcoal).b, "tint leans red");

        int next = lab.AutograderBuildIndex + 1;
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == next, "the second trial");
        yield return null;
        yield return null;
        Assert.AreEqual(LaunchOptions.Current.TimeScale, Time.timeScale, "time runs in the second trial");
        yield return PlayModeLevels.WaitFor(() => !SceneFlow.IsTransitioning, "the reveal");
    }
}
