using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the summary shown after completing the autograder for a lab.
/// </summary>
public class AutograderSummary : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The UI template which displays the information about a single autograder level.
    /// </summary>
    [SerializeField]
    private GameObject levelEntry;

    /// <summary>
    /// The UI object which contains all of the level entries.
    /// </summary>
    [SerializeField]
    private GameObject levelEntryContainer;

    /// <summary>
    /// The UI object which is displayed when the autograder was cut short.
    /// </summary>
    [SerializeField]
    private GameObject cutShortMessage;

    /// <summary>
    /// The text inside cutShortMessage.
    /// </summary>
    [SerializeField]
    private Text cutShortText;

    /// <summary>
    /// The lab title.
    /// </summary>
    [SerializeField]
    private Text titleText;

    /// <summary>
    /// The total score and time.
    /// </summary>
    [SerializeField]
    private Text totalText;

    /// <summary>
    /// The note explaining required trials, shown when the lab has one.
    /// </summary>
    [SerializeField]
    private Text requiredTrialText;

    /// <summary>
    /// The username.
    /// </summary>
    [SerializeField]
    private InputField usernameInput;

    /// <summary>
    /// The score code.
    /// </summary>
    [SerializeField]
    private InputField scoreCodeInput;
    #endregion

    #region Constants
    /// <summary>
    /// The width of a level entry divided by the space between two level entries.
    /// </summary>
    private const int entryWidthToBufferRatio = 3;

    /// <summary>
    /// The fraction of the container that a single level entry should take up.
    /// </summary>
    private const float entryHeight = 0.2f;

    /// <summary>
    /// The fraction of the container that a level entry should leave unoccupied on the left and right.
    /// </summary>
    private const float entryXBuffer = 0.02f;

    /// <summary>
    /// Shown in place of a score code when the build has no autograder key.
    /// </summary>
    private const string noKeyMessage = "Score codes unavailable in this build";
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
    /// Return to the main menu.
    /// </summary>
    public void MainMenu()
    {
        AutograderManager.ResetAutograder();
        SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
    }
    #endregion

    private void Start()
    {
        this.PopulateLevelEntries(LevelManager.LevelInfo.AutograderLevels, AutograderManager.levelScores.ToArray(), out float totalScore, out float totalTime, out bool requiredTrial);
        this.titleText.text = $"{LevelManager.LevelInfo.FullName} Autograder";
        this.totalText.text = $"{totalScore:F2}/{LevelManager.LevelInfo.AutograderMaxScore:F2}; {totalTime} seconds";
        this.requiredTrialText.gameObject.SetActive(requiredTrial);

        this.usernameInput.text = Settings.Username;
        this.scoreCodeInput.text = this.GenerateScoreCode(LevelManager.LevelInfo, totalScore, Settings.Username);
        string trials = string.Join("; ", AutograderManager.levelScores.Select((level, i) => $"{i + 1}: {level.Score} pts, {level.Time:F2} s"));
        Debug.Log($"Autograder summary: {LevelManager.LevelInfo.FullName}, score {totalScore}, error {AutograderSummary.WasError}, trials [{trials}], score code {this.scoreCodeInput.text}");

        if (AutograderSummary.WasError || AutograderSummary.WasRequiredLevelFailed)
        { 
            this.cutShortMessage.SetActive(true);
            Text message = this.cutShortText;
            if (AutograderSummary.WasError)
            {
                message.text = "The autograder was cut short because an error occurred. This may be because your Python program encountered an error.";
            }
            else // wasRequiredLevelFailed
            {
                int failedLevel = Mathf.Clamp(AutograderManager.levelScores.Count, 1, LevelManager.LevelInfo.AutograderLevels.Length);
                AutograderLevelInfo lastLevelInfo = LevelManager.LevelInfo.AutograderLevels[failedLevel - 1];
                message.text = $"The autograder was cut short because you did not pass the required trial <b>{failedLevel}. {lastLevelInfo.Title}</b>. To complete the full autograder for this lab, you must pass that trial with full points.";
            }
        }
        AutograderSummary.WasError = false;
        AutograderSummary.WasRequiredLevelFailed = false;
    }

    private void Update()
    {
        if (Input.anyKeyDown)
        {
            this.cutShortMessage.SetActive(false);
        }
    }

    /// <summary>
    /// Populates the level entry container with information about every level of the autograder.
    /// </summary>
    /// <param name="levelInfos">Information about each autograder level.</param>
    /// <param name="levelScores">Information about the user's performance on each autograder level.</param>
    /// <param name="totalMaxScore">The sum of the max score of each autograder level.</param>
    /// <param name="totalScore">The sum of the user's score on each level.</param>
    /// <param name="totalTime">The sum of the user's time spent on each level.</param>
    /// <param name="requiredTrial">True if this lab contained one or more required trials.</param>
    private void PopulateLevelEntries(AutograderLevelInfo[] levelInfos, AutograderLevelScore[] levelScores, out float totalScore, out float totalTime, out bool requiredTrial)
    {
        totalScore = 0;
        totalTime = 0;
        requiredTrial = false;

        // Set anchor points of container
        RectTransform container = (RectTransform)this.levelEntryContainer.transform;
        container.anchorMax = new Vector2(1, 1);
        container.anchorMin = new Vector2(0, 1 - AutograderSummary.entryHeight * levelInfos.Length);
        container.anchoredPosition = new Vector2(0, 0);
        container.sizeDelta = new Vector2(0, 0);

        float entryYBuffer = 1.0f / (levelInfos.Length * (AutograderSummary.entryWidthToBufferRatio + 1) + 2);
        float entryHeight = entryYBuffer * AutograderSummary.entryWidthToBufferRatio;
        float anchorY = 1 - entryYBuffer;

        for (int i = 0; i < levelInfos.Length; i++)
        {
            GameObject entry = GameObject.Instantiate(this.levelEntry, Vector3.zero, Quaternion.identity);

            // Set uiEntry's anchor points inside of the container
            entry.transform.SetParent(this.levelEntryContainer.transform);
            RectTransform rect = entry.GetComponent<RectTransform>();
            rect.anchorMax = new Vector2(1 - AutograderSummary.entryXBuffer, anchorY);
            anchorY -= entryHeight;
            rect.anchorMin = new Vector2(AutograderSummary.entryXBuffer, anchorY);
            anchorY -= entryYBuffer;

            // Size exactly to the anchor points
            rect.anchoredPosition = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(0, 0);

            // Set entry
            if (i < levelScores.Length)
            {
                totalScore += levelScores[i].Score;
                totalTime += levelScores[i].Time;
                entry.GetComponent<AutograderUIEntry>().SetInfo(levelInfos[i], levelScores[i]);
            }
            else
            {
                entry.GetComponent<AutograderUIEntry>().SetInfo(levelInfos[i], null);
            }

            requiredTrial |= levelInfos[i].IsRequired;
        }
    }

    /// <summary>
    /// Generates an encrypted score code which encodes the user's score for the level.
    /// </summary>
    /// <param name="levelInfo">Information about the lab.</param>
    /// <param name="score">The user's total score on the lab autograder.</param>
    /// <param name="username">The user's OpenEdx username.</param>
    /// <returns>An hex ciphertext which encodes the user's score for the level.</returns>
    private string GenerateScoreCode(LevelInfo levelInfo, float score, string username)
    {
        if (username == "Default"){
            return "0000000000000000"; // Null code to prevent users from using default username in Edly autograder
        } else if (!Utilities.HasKey) {
            return AutograderSummary.noKeyMessage;
        } else {
            string scoreCode = AutograderSummary.FormatScorePayload(levelInfo.AutograderLevelCode, score, levelInfo.AutograderMaxScore, username);
            return Utilities.Encrypt(scoreCode);
        }

    }

    /// <summary>
    /// Formats the plaintext score-code payload. Numbers use the invariant culture so the
    /// decoder reads the same text on every OS locale.
    /// </summary>
    /// <param name="levelCode">The lab's autograder level code.</param>
    /// <param name="score">The user's total score on the lab autograder.</param>
    /// <param name="maxScore">The maximum score for the lab.</param>
    /// <param name="username">The user's OpenEdx username.</param>
    /// <returns>The payload in the form code|score|max|username.</returns>
    public static string FormatScorePayload(string levelCode, float score, float maxScore, string username)
    {
        return FormattableString.Invariant($"{levelCode}|{score}|{maxScore}|{username}");
    }
}
