using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The summary shown after completing the autograder for a lab (UI Toolkit,
/// AutograderSummary.uxml): lab title, total score and time, a row per trial, the username, and
/// the score code with a copy button.
/// </summary>
public class AutograderSummary : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The document showing AutograderSummary.uxml.
    /// </summary>
    [SerializeField]
    private UIDocument document;
    #endregion

    #region Constants
    /// <summary>
    /// Shown in place of a score code when the build has no autograder key.
    /// </summary>
    private const string noKeyMessage = "Score codes unavailable in this build";

    /// <summary>
    /// The score code shown for the default username, which the online course rejects.
    /// </summary>
    private const string nullScoreCode = "0000000000000000";

    /// <summary>
    /// How long the copy button shows that the code was copied (in seconds).
    /// </summary>
    private const float copiedSeconds = 2;
    #endregion

    #region Public Interface
    /// <summary>
    /// True if autograding was cut short because the user did not pass a required level.
    /// </summary>
    public static bool WasRequiredLevelFailed = false;

    /// <summary>
    /// True if autograding was cut short because of an error.
    /// </summary>
    public static bool WasError = false;

    /// <summary>
    /// The score code on screen.
    /// </summary>
    public string ScoreCode { get; private set; }

    /// <summary>
    /// Return to the main menu.
    /// </summary>
    public void MainMenu()
    {
        AutograderManager.ResetAutograder();
        SceneFlow.Load(LevelCollection.MainMenuBuildIndex, SceneTransition.Wipe);
    }

    /// <summary>
    /// Copies the score code to the system clipboard.
    /// </summary>
    public void CopyScoreCode()
    {
        GUIUtility.systemCopyBuffer = this.ScoreCode;
        this.copyButton.text = "Copied";
        this.copiedUntil = Time.unscaledTime + AutograderSummary.copiedSeconds;
    }

    /// <summary>
    /// Formats the plaintext score-code payload. Numbers use the invariant culture so the
    /// decoder reads the same text on every OS locale.
    /// </summary>
    /// <param name="levelCode">The lab's autograder level code.</param>
    /// <param name="score">The user's total score on the lab autograder.</param>
    /// <param name="maxScore">The maximum score for the lab.</param>
    /// <param name="username">The course ID from the Edly autograder problem (Settings.Username).</param>
    /// <returns>The payload in the form code|score|max|username.</returns>
    public static string FormatScorePayload(string levelCode, float score, float maxScore, string username)
    {
        return FormattableString.Invariant($"{levelCode}|{score}|{maxScore}|{username}");
    }
    #endregion

    private VisualElement root;
    private Button copyButton;
    private float copiedUntil = -1;

    private void Start()
    {
        this.root = this.document.rootVisualElement;
        this.copyButton = this.root.Q<Button>("copy");
        this.copyButton.clicked += this.CopyScoreCode;
        this.root.Q<Button>("menu").clicked += this.MainMenu;

        LevelInfo lab = LevelManager.LevelInfo;
        AutograderLevelScore[] scores = AutograderManager.levelScores.ToArray();
        // Float accumulation in trial order, as before, so the score code payload is unchanged
        float totalScore = 0;
        float totalTime = 0;
        for (int i = 0; i < Mathf.Min(scores.Length, lab.AutograderLevels.Length); i++)
        {
            totalScore += scores[i].Score;
            totalTime += scores[i].Time;
        }

        this.root.Q<Label>("lab-title").text = lab.DisplayName;
        this.root.Q<Label>("total-score").text = $"{AutograderSummary.Points(totalScore)} / {AutograderSummary.Points(lab.AutograderMaxScore)}";
        this.root.Q<Label>("total-time").text = $"{totalTime.ToString("F2", CultureInfo.InvariantCulture)} s";
        this.root.Q<GradientBar>("total-progress").Fill = lab.AutograderMaxScore > 0 ? Mathf.Clamp01(totalScore / lab.AutograderMaxScore) : 0;

        ScrollView trials = this.root.Q<ScrollView>("trials");
        for (int i = 0; i < lab.AutograderLevels.Length; i++)
        {
            trials.Add(AutograderSummary.TrialRow(i, lab.AutograderLevels[i], i < scores.Length ? scores[i] : null));
        }
        this.root.Q("required-note").style.display = lab.AutograderLevels.Any(level => level.IsRequired) ? DisplayStyle.Flex : DisplayStyle.None;

        this.root.Q<Label>("username").text = Settings.Username;
        this.ScoreCode = AutograderSummary.GenerateScoreCode(lab, totalScore, Settings.Username);
        this.root.Q<TextField>("score-code").value = this.ScoreCode;
        this.copyButton.SetEnabled(this.ScoreCode != AutograderSummary.noKeyMessage);

        string trialLog = string.Join("; ", scores.Select((level, i) => $"{i + 1}: {level.Score} pts, {level.Time:F2} s"));
        Debug.Log($"Autograder summary: {lab.FullName}, score {totalScore}, error {AutograderSummary.WasError}, trials [{trialLog}], score code {this.ScoreCode}");

        if (AutograderSummary.WasError || AutograderSummary.WasRequiredLevelFailed)
        {
            this.root.Q("cut-short").style.display = DisplayStyle.Flex;
            this.root.Q<Label>("cut-short-text").text = AutograderSummary.WasError
                ? "The autograder was cut short because an error occurred. This may be because your Python program encountered an error."
                : AutograderSummary.RequiredFailedMessage(lab, scores.Length);
        }
        AutograderSummary.WasError = false;
        AutograderSummary.WasRequiredLevelFailed = false;
    }

    private void Update()
    {
        if (this.copiedUntil > 0 && Time.unscaledTime >= this.copiedUntil)
        {
            this.copiedUntil = -1;
            this.copyButton.text = "Copy";
        }
        if (Input.GetKeyDown(Hotkeys.Exit) && !SceneFlow.IsTransitioning)
        {
            this.MainMenu();
        }
    }

    private static string RequiredFailedMessage(LevelInfo lab, int finishedLevels)
    {
        int failedLevel = Mathf.Clamp(finishedLevels, 1, lab.AutograderLevels.Length);
        AutograderLevelInfo failed = lab.AutograderLevels[failedLevel - 1];
        return $"The autograder was cut short because you did not pass the required trial <b>{failedLevel}. {failed.Title}</b>. To complete the full autograder for this lab, you must pass that trial with full points.";
    }

    private static VisualElement TrialRow(int index, AutograderLevelInfo level, AutograderLevelScore score)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("ags-row");
        row.Add(AutograderSummary.RowLabel((index + 1).ToString(CultureInfo.InvariantCulture), "rcn-numeric", "rcn-muted", "ags-row-index"));
        row.Add(AutograderSummary.RowLabel(level.IsRequired ? $"*{level.Title}" : level.Title, "rcn-body", "ags-row-title"));

        GradientBar bar = new GradientBar { Fill = score != null && level.MaxPoints > 0 ? Mathf.Clamp01(score.Score / level.MaxPoints) : 0 };
        bar.AddToClassList("rcn-progress");
        bar.AddToClassList("ags-row-bar");
        row.Add(bar);

        string points = score != null ? AutograderSummary.Points(score.Score) : "--";
        Label scoreLabel = AutograderSummary.RowLabel($"{points} / {AutograderSummary.Points(level.MaxPoints)}", "rcn-numeric", "ags-row-score");
        string scoreClass = AutograderSummary.ScoreClass(level.MaxPoints, score?.Score);
        if (scoreClass != null)
        {
            scoreLabel.AddToClassList(scoreClass);
        }
        row.Add(scoreLabel);

        string time = score != null ? $"{score.Time.ToString("F2", CultureInfo.InvariantCulture)} s" : "--";
        row.Add(AutograderSummary.RowLabel(time, "rcn-numeric", "rcn-muted", "ags-row-time"));
        return row;
    }

    /// <summary>
    /// The style class for a trial score: none (no points or not run), partial, extra (over the
    /// maximum), or null for full credit.
    /// </summary>
    public static string ScoreClass(float maxPoints, float? score)
    {
        if (score == null || (maxPoints > 0 && score.Value <= 0))
        {
            return "ags-row-score--none";
        }
        if (score.Value > maxPoints)
        {
            return "ags-row-score--extra";
        }
        return score.Value < maxPoints ? "ags-row-score--partial" : null;
    }

    private static Label RowLabel(string text, params string[] classes)
    {
        Label label = new Label(text);
        foreach (string name in classes)
        {
            label.AddToClassList(name);
        }
        return label;
    }

    private static string Points(float points)
    {
        return points.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Generates an encrypted score code which encodes the user's score for the level.
    /// </summary>
    /// <param name="levelInfo">Information about the lab.</param>
    /// <param name="score">The user's total score on the lab autograder.</param>
    /// <param name="username">The course ID from the Edly autograder problem (Settings.Username).</param>
    /// <returns>An hex ciphertext which encodes the user's score for the level.</returns>
    private static string GenerateScoreCode(LevelInfo levelInfo, float score, string username)
    {
        if (username == "Default")
        {
            // Null code to prevent users from using default username in Edly autograder
            return AutograderSummary.nullScoreCode;
        }
        if (!Utilities.HasKey)
        {
            return AutograderSummary.noKeyMessage;
        }
        string scoreCode = AutograderSummary.FormatScorePayload(levelInfo.AutograderLevelCode, score, levelInfo.AutograderMaxScore, username);
        return Utilities.Encrypt(scoreCode);
    }
}
