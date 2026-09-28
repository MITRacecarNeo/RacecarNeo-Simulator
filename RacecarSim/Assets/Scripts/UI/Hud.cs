using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the heads-up display shown to the user during races with a single car.
/// </summary>
public class Hud : ScreenManager, IAutograderHud
{
    #region Set in Unity Editor
    /// <summary>
    /// The textbox shown when the user fails an objective.
    /// </summary>
    [SerializeField]
    private GameObject FailureMessage;

    /// <summary>
    /// The textbox shown when the user successfully completes a lab.
    /// </summary>
    [SerializeField]
    private GameObject SuccessMessage;

    /// <summary>
    /// The time spent on each checkpoint.
    /// </summary>
    [SerializeField]
    private Text checkpointTimesText;

    /// <summary>
    /// The slow-motion label.
    /// </summary>
    [SerializeField]
    private Text timeScaleText;

    /// <summary>
    /// The car's true speed.
    /// </summary>
    [SerializeField]
    private Text trueSpeedText;

    /// <summary>
    /// The car's linear acceleration.
    /// </summary>
    [SerializeField]
    private Text linearAccelerationText;

    /// <summary>
    /// The car's angular velocity.
    /// </summary>
    [SerializeField]
    private Text angularVelocityText;

    /// <summary>
    /// The simulation mode label.
    /// </summary>
    [SerializeField]
    private Text modeText;

    /// <summary>
    /// The reason shown in the failure message.
    /// </summary>
    [SerializeField]
    private Text failureText;

    /// <summary>
    /// The title of the success message.
    /// </summary>
    [SerializeField]
    private Text successMessageText;

    /// <summary>
    /// The time shown in the success message.
    /// </summary>
    [SerializeField]
    private Text successTimeText;

    /// <summary>
    /// The autograder trial title.
    /// </summary>
    [SerializeField]
    private Text autograderTitleText;

    /// <summary>
    /// The autograder trial description.
    /// </summary>
    [SerializeField]
    private Text autograderDescriptionText;

    /// <summary>
    /// The autograder trial score.
    /// </summary>
    [SerializeField]
    private Text autograderScoreText;

    /// <summary>
    /// The autograder time limit or time bonus.
    /// </summary>
    [SerializeField]
    private Text maxTimeText;

    /// <summary>
    /// The filter darkening the screen in slow motion.
    /// </summary>
    [SerializeField]
    private RawImage timeWarp;

    /// <summary>
    /// The depth camera visualization.
    /// </summary>
    [SerializeField]
    private RawImage depthFeed;

    /// <summary>
    /// The LIDAR visualization.
    /// </summary>
    [SerializeField]
    private RawImage lidarMap;

    /// <summary>
    /// The background of the simulation mode label.
    /// </summary>
    [SerializeField]
    private RawImage modeBackground;

    /// <summary>
    /// The Python icon, dimmed when no program is connected.
    /// </summary>
    [SerializeField]
    private RawImage connectedProgram;

    /// <summary>
    /// The controller input icons: each Controller.Button, then each Controller.Trigger, then each Controller.Joystick, in enum order.
    /// </summary>
    [SerializeField]
    private RawImage[] controllerInputs;
    #endregion

    #region Constants
    /// <summary>
    /// The color used for the background of sensor visualizations.
    /// </summary>
    public static readonly Color SensorBackgroundColor = new Color(0.2f, 0.2f, 0.2f);

    /// <summary>
    /// The number of times the LIDAR map is smaller than the color and depth visualizations.
    /// </summary>
    private const int lidarMapScale = 4;

    /// <summary>
    /// The alpha (transparency) of the Python icon when no script is connected.
    /// </summary>
    private const float unconnectedScriptAlpha = 0.25f;

    /// <summary>
    /// In the autograder, when the current time is this fraction of the time limit away from the time limit, the current time is shown as a warning color.
    /// </summary>
    private const float autograderWarningTimeRatio = 0.25f;

    /// <summary>
    /// The background color of the mode label when the simulation is in each SimulationMode.
    /// </summary>
    private static readonly Color[] modeColors =
    {
        new Color(0f, 0.75f, 0.25f), // default drive
        new Color(0.75f, 0f, 0.25f), // user program
        new Color(1f, 0.5f, 0f), // wait
        new Color(0f, 0f, 0f) // finished
    };

    /// <summary>
    /// The text displayed on the mode label when the simulation is in each SimulationMode.
    /// </summary>
    private static readonly string[] modeNames =
    {
        "Default Drive",
        "User Program",
        "Wait",
        "Finished"
    };
    #endregion

    #region Public Interface
    #region Overrides
    public override void HandleWin(float time, bool isNewBestTime = false)
    {
        this.SuccessMessage.SetActive(true);
        this.successMessageText.text = isNewBestTime ? "New Best Time!" : "Mission Accomplished!";
        this.successTimeText.text = $"Time: {time:F3} seconds";
    }

    public override void HandleFailure(int carIndex, string reason)
    {
        this.FailureMessage.SetActive(true);
        this.failureText.text = reason;
    }

    public override void UpdateConnectedPrograms(bool[] connectedPrograms)
    {
        this.connectedProgram.color = connectedPrograms.Length > 0 ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, Hud.unconnectedScriptAlpha);
    }

    public override void UpdateMode(SimulationMode mode)
    {
        this.modeText.text = Hud.modeNames[(int)mode];
        this.modeBackground.color = Hud.modeColors[(int)mode];
    }

    public override void UpdateTimeScale(float timeScale)
    {
        this.timeWarp.color = new Color(1, 1, 1, Mathf.Max(0, 1 - Mathf.Sqrt(timeScale)));
        this.timeScaleText.text = timeScale >= 1 ? string.Empty : $"{Mathf.Round(1 / timeScale)}x Slow Motion";
    }

    public override void UpdateTime(float mainTime, float[] keyPointDurations)
    {
        base.UpdateTime(mainTime, keyPointDurations);

        // If the level contains checkpoints, show the time spent on each checkpoint
        if (keyPointDurations.Length > 2)
        {
            string text = $"1) {keyPointDurations[1]:F3}";
            for (int i = 2; i < keyPointDurations.Length; i++)
            {
                if (keyPointDurations[i] == 0)
                {
                    text += $"\n{i}) --";
                }
                else
                {
                    text += $"\n{i}) {keyPointDurations[i]:F3}";
                }
            }

            this.checkpointTimesText.text = text;
        }
    }
    #endregion

    #region IAutograderHud
    void IAutograderHud.SetLevelInfo(int levelIndex, string title, string description)
    {
        // The trial title and description occupy the bottom strip, so messages move above them
        RectTransform messageBox = (RectTransform)this.messageText.transform.parent;
        messageBox.anchorMin = Hud.autograderMessageAnchorMin;
        messageBox.anchorMax = Hud.autograderMessageAnchorMax;

        this.autograderTitleText.text = $"<b>Trial {levelIndex + 1}</b> - {title}";
        this.autograderDescriptionText.text = description;
    }

    void IAutograderHud.UpdateScore(float score, float maxScore)
    {
        this.autograderScoreText.text = $"{score:F2}/{maxScore:F2}";

        if (score == maxScore)
        {
            this.autograderScoreText.color = Color.green;
        }
    }

    void IAutograderHud.UpdateTime(float time, float timeLimit)
    {
        base.UpdateTime(time, new float[0]);

        if (time >= timeLimit)
        {
            this.mainTimeText.color = Color.red;
        }
        else if (timeLimit - time < timeLimit * Hud.autograderWarningTimeRatio)
        {
            this.mainTimeText.color = Color.yellow;
        }
    }

    void IAutograderHud.SetMaxTime(float maxTime)
    {
        this.maxTimeText.text = $"Max: {maxTime:F1}";
    }

    void IAutograderHud.SetTimeBonus(float maxTime, float bonus, bool isLastBracket)
    {
        if (bonus >= 0)
        {
            this.maxTimeText.text = $"Bonus: +{bonus} (under {maxTime:F1} sec)";
            this.maxTimeText.color = bonus > 0 ? Color.green : Color.white;
        }
        else
        {
            this.maxTimeText.text = $"Penalty: {bonus} (under {maxTime:F1} sec)";
            this.maxTimeText.color = isLastBracket ? Color.red : Color.yellow;
        }
    }
    #endregion

    /// <summary>
    /// The texture containing the LIDAR visualization.
    /// </summary>
    public Texture2D LidarVisualization
    {
        get
        {
            return (Texture2D)this.lidarMap.texture;
        }
    }

    /// <summary>
    /// The texture containing the depth camera visualization.
    /// </summary>
    public Texture2D DepthVisualization
    {
        get
        {
            return (Texture2D)this.depthFeed.texture;
        }
    }

    /// <summary>
    /// Update the physics statistics shown on the HUD.
    /// </summary>
    /// <param name="speed">The magnitude of the car's linear velocity in m/s.</param>
    /// <param name="linearAcceleration">The car's linear acceleration in m/s^2.</param>
    /// <param name="angularVelocity">The car's angular velocity in rad/s.</param>
    public void UpdatePhysics(float speed, Vector3 linearAcceleration, Vector3 angularVelocity)
    {
        this.trueSpeedText.text = speed.ToString("F2");
        this.linearAccelerationText.text = FormatVector3(linearAcceleration);
        this.angularVelocityText.text = FormatVector3(angularVelocity);
    }
    #endregion

    /// <summary>
    /// Message box anchors during autograder runs: above the trial title (0.08-0.14) and
    /// description (0.02-0.07), below the car.
    /// </summary>
    private static readonly Vector2 autograderMessageAnchorMin = new Vector2(0.28f, 0.16f);
    private static readonly Vector2 autograderMessageAnchorMax = new Vector2(0.72f, 0.28f);

    /// <summary>
    /// Controller enum values, cached because Enum.GetValues allocates on every call.
    /// </summary>
    private static readonly Controller.Button[] buttons = (Controller.Button[])Enum.GetValues(typeof(Controller.Button));
    private static readonly Controller.Trigger[] triggers = (Controller.Trigger[])Enum.GetValues(typeof(Controller.Trigger));
    private static readonly Controller.Joystick[] joysticks = (Controller.Joystick[])Enum.GetValues(typeof(Controller.Joystick));

    protected override void Awake()
    {
        base.Awake();

        this.lidarMap.texture = new Texture2D(CameraModule.ColorWidth / Hud.lidarMapScale, CameraModule.ColorHeight / Hud.lidarMapScale, TextureFormat.RGBA32, false);
        this.depthFeed.texture = new Texture2D(CameraModule.DepthWidth, CameraModule.DepthHeight, TextureFormat.RGBA32, false);
    }

    private void OnDestroy()
    {
        Destroy(this.lidarMap.texture);
        Destroy(this.depthFeed.texture);
    }

    private void Start()
    {
        this.FailureMessage.SetActive(false);
        this.SuccessMessage.SetActive(false);

        this.checkpointTimesText.text = string.Empty;
    }

    protected override void Update()
    {
        this.UpdateController();
        base.Update();
    }

    /// <summary>
    /// Update the controller icon to show the current buttons, triggers, and joysticks being pressed.
    /// </summary>
    private void UpdateController()
    {
        int index = 0;

        foreach (Controller.Button button in Hud.buttons)
        {
            this.controllerInputs[index].enabled = Controller.IsDown(button);
            index++;
        }

        foreach (Controller.Trigger trigger in Hud.triggers)
        {
            this.controllerInputs[index].enabled = Controller.GetTrigger(trigger) > 0;
            index++;
        }

        foreach (Controller.Joystick joystick in Hud.joysticks)
        {
            Vector2 joystickAxes = Controller.GetJoystick(joystick);
            this.controllerInputs[index].enabled = joystickAxes.x != 0 || joystickAxes.y != 0;
            index++;
        }
    }

    /// <summary>
    /// Formats a vector with a constant as a string with a constant width.
    /// </summary>
    /// <param name="vector">The vector to format.</param>
    /// <returns>The vector formatted as a string with exactly 19 characters.</returns>
    private string FormatVector3(Vector3 vector)
    {
        return $"({FormatFloat(vector.x)},{FormatFloat(vector.y)},{FormatFloat(vector.z)})";       
    }

    /// <summary>
    /// Rounds and formats a float as a string with a constant width.
    /// </summary>
    /// <param name="value">A value less than 10.</param>
    /// <returns>The provided value formatted as a string with exactly five characters.</returns>
    private string FormatFloat(float value)
    {
        string str = value.ToString("F2");

        // Add a leading space if there is no negative sign
        if (str[0] != '-')
        {
            return $" {str}";
        }

        return str;
    }
}
