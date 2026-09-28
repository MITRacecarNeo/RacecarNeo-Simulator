using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages an entry displaying the performance on a single autograder level.
/// </summary>
public class AutograderUIEntry : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The trial title.
    /// </summary>
    [SerializeField]
    private Text nameText;

    /// <summary>
    /// The score and max score.
    /// </summary>
    [SerializeField]
    private Text scoreText;

    /// <summary>
    /// The trial time.
    /// </summary>
    [SerializeField]
    private Text timeText;
    #endregion

    #region Constants
    /// <summary>
    /// The color shown for a score which is between 0 and full credit, not inclusive.
    /// </summary>
    private static readonly Color partialCreditColor = new Color(1, 0.5f, 0);

    /// <summary>
    /// The color shown for a score which is greater than full credit.
    /// </summary>
    private static readonly Color extraCreditColor = new Color(0.75f, 0, 1);
    #endregion

    #region Public Interface
    /// <summary>
    /// Initializes the entry with score and time information.
    /// </summary>
    /// <param name="levelInfo">Information about the level.</param>
    /// <param name="bestTimeInfo">Information about the user's performance in the level.</param>
    public void SetInfo(AutograderLevelInfo levelInfo, AutograderLevelScore levelScore)
    {
        this.nameText.text = levelInfo.IsRequired ? $"*{levelInfo.Title}" : levelInfo.Title;

        if (levelScore != null)
        {
            this.scoreText.text = $"{levelScore.Score:F2}/{levelInfo.MaxPoints:F2}";
            this.timeText.text = levelScore.Time.ToString("F2");

            if (levelInfo.MaxPoints > 0 && levelScore.Score == 0)
            {
                // No credit
                this.scoreText.color = Color.red;
            }
            else if (levelScore.Score > levelInfo.MaxPoints)
            {
                // Extra credit
                this.scoreText.color = AutograderUIEntry.extraCreditColor;
            }
            else if (levelScore.Score != levelInfo.MaxPoints)
            {
                // Partial credit
                this.scoreText.color = AutograderUIEntry.partialCreditColor;
            }
        }
        else
        {
            this.scoreText.text = $"--/{levelInfo.MaxPoints:F2}";
            this.scoreText.color = Color.red;
            this.timeText.text = "--";
        }
    }
    #endregion
}
