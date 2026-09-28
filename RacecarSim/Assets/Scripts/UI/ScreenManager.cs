using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The base class from which all in-simulation HUDs and screen managers inherit.
/// </summary>
public abstract class ScreenManager : MonoBehaviour
{
    #region Constant
    /// <summary>
    /// The number of seconds we expect a user to read each character in a message.
    /// </summary>
    private const float secondsPerChar = 0.08f;

    /// <summary>
    /// The minimum time (in seconds) that an error message can be shown.
    /// </summary>
    private const float minErrorTime = 5;

    /// <summary>
    /// The color of text used in an error message.
    /// </summary>
    private static readonly Color errorColor = new Color(1, 0.25f, 0.25f);

    /// <summary>
    /// The minimum time (in seconds) that a warning message can be shown.
    /// </summary>
    private const float minWarningTime = 3;

    /// <summary>
    /// The color of text used in a warning message.
    /// </summary>
    private static readonly Color warningColor = Color.yellow;
    #endregion

    #region Public Interface
    /// <summary>
    /// Show a text message to the user.
    /// </summary>
    /// <param name="message">The text to show.</param>
    /// <param name="color">The color of the text.</param>
    /// <param name="persistTime">The time in seconds the text is shown.</param>
    /// <param name="fadeTime">In the time in seconds it takes for the text to fade out after persistTime has passed.</param>
    public void ShowMessage(string message, Color color, float persistTime = -1, float fadeTime = 1.0f)
    {
        this.messageText.text = message;
        this.messageText.color = color;

        this.messageColor = color;
        this.messageCounter = 0;
        this.messagePersistTime = persistTime;
        this.messageFadeTime = fadeTime;
    }

    public void ShowError(string errorText)
    {
        float persistTime = Mathf.Max(ScreenManager.minErrorTime, errorText.Length * ScreenManager.secondsPerChar);
        this.ShowMessage(errorText, ScreenManager.errorColor, persistTime);
    }

    public void ShowWarning(string warningText)
    {
        float persistTime = Mathf.Max(ScreenManager.minWarningTime, warningText.Length * ScreenManager.secondsPerChar);
        this.ShowMessage(warningText, ScreenManager.warningColor, persistTime);
    }

    /// <summary>
    /// Updates the element showing the current time elapsed in the race.
    /// </summary>
    /// <param name="mainTime">The total time in seconds that the current level has been running.</param>
    /// <param name="keyPointDurations">The time which the 0th car spent on each key point, indexed by key point.</param>
    public virtual void UpdateTime(float mainTime, float[] keyPointDurations)
    {
        this.mainTimeText.text = mainTime.ToString("F3");

        // The children of this class can override this method if they wish to display key point times
    }

    /// <summary>
    /// Updates the elements showing when the simulation is paused.
    /// </summary>
    /// <param name="isPaused">True if the simulation is currently paused.</param>
    public void SetPause(bool isPaused)
    {
        this.pauseScreen.gameObject.SetActive(isPaused);
    }

    #region Abstract
    /// <summary>
    /// Update the element(s) indicating that the race is won.
    /// </summary>
    /// <param name="time">The total time in seconds elapsed during the race.</param>
    /// <param name="isNewBestTime">True if this completion was a new overall best time.</param>
    public abstract void HandleWin(float time, bool isNewBestTime);

    /// <summary>
    /// Update the element(s) indicating that a car failed a critical objective.
    /// </summary>
    /// <param name="carIndex">The index of the car which failed the objective.</param>
    /// <param name="reason">A description of the failure.</param>
    public abstract void HandleFailure(int carIndex, string reason);

    /// <summary>
    /// Update the element(s) indicating the Python script(s) connected to RacecarSim.
    /// </summary>
    /// <param name="connectedPrograms">An array in which each element indicates whether the racecar of the same index is connected to a Python script.</param>
    public abstract void UpdateConnectedPrograms(bool[] connectedPrograms);

    /// <summary>
    /// Updates the element(s) showing the current simulation mode.
    /// </summary>
    /// <param name="mode">The current mode of the simulation.</param>
    public abstract void UpdateMode(SimulationMode mode);

    /// <summary>
    /// Updates the element(s) showing the current rate at which time progresses.
    /// </summary>
    /// <param name="timeScale">The current rate at which time progresses. 1 is full speed, 0.5 is half speed, and 0 is paused.</param>
    public abstract void UpdateTimeScale(float timeScale);
    #endregion
    #endregion

    #region Set in Unity Editor
    /// <summary>
    /// The text showing messages, warnings, and errors.
    /// </summary>
    [SerializeField]
    protected Text messageText;

    /// <summary>
    /// The text showing the main timer.
    /// </summary>
    [SerializeField]
    protected Text mainTimeText;

    /// <summary>
    /// The overlay shown while the simulation is paused.
    /// </summary>
    [SerializeField]
    protected RawImage pauseScreen;
    #endregion

    protected virtual void Awake()
    {
        this.messagePersistTime = -1;
        this.messageText.text = string.Empty;
        this.mainTimeText.text = string.Empty;

        this.SetPause(false);
    }

    protected virtual void Update()
    {
        // Handle message persistence and fadeout
        if (this.messagePersistTime > 0)
        {
            this.messageCounter += Time.deltaTime;
            if (this.messageCounter > this.messagePersistTime)
            {
                this.messagePersistTime = 0;
                this.messageCounter = 0;
            }
        }
        else if (this.messagePersistTime == 0 && this.messageCounter < this.messageFadeTime)
        {
            this.messageCounter += Time.deltaTime;
            this.messageText.color = Color.Lerp(this.messageColor, Color.clear, this.messageCounter / this.messageFadeTime);
        }
    }

    /// <summary>
    /// A counter used to track message persistence and fade out.
    /// </summary>
    private float messageCounter;

    /// <summary>
    /// The time is seconds that the current message will persist.  If -1, the current message will persist indefinitely.
    /// </summary>
    private float messagePersistTime;

    /// <summary>
    /// The time in seconds that the current message will take to fade out.
    /// </summary>
    private float messageFadeTime;

    /// <summary>
    /// The color of the current message.
    /// </summary>
    private Color messageColor;
}
