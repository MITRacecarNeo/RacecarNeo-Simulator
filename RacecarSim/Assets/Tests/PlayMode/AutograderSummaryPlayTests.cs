using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// The autograder summary screen: totals, a row per trial with score colors, the required-trial
/// note and cut-short notice, and the score code. Writes Logs/ui-preview-autograder-summary.png.
/// </summary>
public class AutograderSummaryPlayTests
{
    private const string labName = "Lab 2a: Line Following";

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        AutograderSummary.WasRequiredLevelFailed = false;
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Summary_ShowsTrialsTotalsAndCode()
    {
        LevelInfo lab = PlayModeLevels.Find(AutograderSummaryPlayTests.labName);
        LevelManager.LevelInfo = lab;
        LevelManager.LevelManagerMode = LevelManagerMode.Autograder;
        AutograderManager.ResetAutograder();
        AutograderLevelInfo[] levels = lab.AutograderLevels;
        AutograderManager.levelScores.Add(new AutograderLevelScore { Score = levels[0].MaxPoints, Time = 8.5f });
        AutograderManager.levelScores.Add(new AutograderLevelScore { Score = levels[1].MaxPoints * 0.5f, Time = 12.25f });
        AutograderManager.levelScores.Add(new AutograderLevelScore { Score = 0, Time = 20 });
        AutograderSummary.WasRequiredLevelFailed = true;

        SceneManager.LoadScene(AutograderManager.AutograderSummaryScene);
        yield return null;
        yield return null;

        AutograderSummary summary = Object.FindAnyObjectByType<AutograderSummary>();
        VisualElement root = summary.GetComponent<UIDocument>().rootVisualElement;
        Assert.AreEqual(lab.DisplayName, root.Q<Label>("lab-title").text);
        Assert.AreEqual(levels.Length, root.Q<ScrollView>("trials").contentContainer.childCount, "a row per trial");
        Assert.AreEqual("40.75 s", root.Q<Label>("total-time").text);
        Assert.AreEqual(DisplayStyle.Flex, root.Q("cut-short").resolvedStyle.display, "cut-short notice");
        Assert.AreEqual(DisplayStyle.Flex, root.Q("required-note").resolvedStyle.display, "required note");
        Assert.AreEqual(summary.ScoreCode, root.Q<TextField>("score-code").value);
        Assert.IsFalse(AutograderSummary.WasRequiredLevelFailed, "flag cleared after showing");

        summary.CopyScoreCode();
        Assert.AreEqual("Copied", root.Q<Button>("copy").text);

        yield return MenuPlayTests.Capture(summary.GetComponent<UIDocument>(), "ui-preview-autograder-summary.png");
    }

    /// <summary>
    /// A full-length score code (130 characters for a short payload) stays inside its field: the
    /// field, Copy, and Main menu sit side by side without overlap. Writes
    /// Logs/ui-preview-autograder-summary-long-code.png.
    /// </summary>
    [UnityTest]
    public IEnumerator Summary_LongScoreCodeDoesNotOverlapButtons()
    {
        LevelInfo lab = PlayModeLevels.Find(AutograderSummaryPlayTests.labName);
        LevelManager.LevelInfo = lab;
        LevelManager.LevelManagerMode = LevelManagerMode.Autograder;
        AutograderManager.ResetAutograder();
        AutograderManager.levelScores.Add(new AutograderLevelScore { Score = 0, Time = 20 });

        SceneManager.LoadScene(AutograderManager.AutograderSummaryScene);
        yield return null;
        yield return null;

        AutograderSummary summary = Object.FindAnyObjectByType<AutograderSummary>();
        VisualElement root = summary.GetComponent<UIDocument>().rootVisualElement;
        root.Q<Label>("username").text = "U8XPVVPH";
        root.Q<TextField>("score-code").value = "R2" + string.Concat(System.Linq.Enumerable.Repeat("0123456789ABCDEF", 8));
        yield return null;
        yield return null;

        Rect code = root.Q<TextField>("score-code").worldBound;
        Rect copy = root.Q<Button>("copy").worldBound;
        Rect menu = root.Q<Button>("menu").worldBound;
        Rect panel = root.Q(className: "ags-panel").worldBound;
        Assert.LessOrEqual(code.xMax, copy.xMin + 0.5f, "score code field ends before Copy");
        Assert.LessOrEqual(copy.xMax, menu.xMin + 0.5f, "Copy ends before Main menu");
        Assert.LessOrEqual(menu.xMax, panel.xMax + 0.5f, "Main menu inside the card");
        Assert.GreaterOrEqual(copy.width, 129.5f, "Copy keeps its minimum width");

        yield return MenuPlayTests.Capture(summary.GetComponent<UIDocument>(), "ui-preview-autograder-summary-long-code.png");
    }

    [Test]
    public void ScoreClass_MatchesCredit()
    {
        Assert.AreEqual("ags-row-score--none", AutograderSummary.ScoreClass(1, null), "not run");
        Assert.AreEqual("ags-row-score--none", AutograderSummary.ScoreClass(1, 0), "no credit");
        Assert.AreEqual("ags-row-score--partial", AutograderSummary.ScoreClass(1, 0.5f));
        Assert.IsNull(AutograderSummary.ScoreClass(1, 1), "full credit");
        Assert.AreEqual("ags-row-score--extra", AutograderSummary.ScoreClass(1, 1.5f));
    }
}
