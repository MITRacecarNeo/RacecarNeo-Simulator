using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The single-car race HUD (UI Toolkit, Hud.uxml): the overhead map top left and the LIDAR,
/// camera, and depth feeds in an L-shaped window bottom left; mode chip, program connection, time
/// scale, and messages top center; lap timer badge, checkpoint pips, and the autograder trial panel
/// top right; dot matrix, IMU, battery, controller input, body frame, and speed gauge bottom right;
/// pause and result cards over the view. Hotkeys.ToggleMap shows and hides the map and gauge, Hotkeys.ToggleSensors
/// the sensor trays. The bottom center stays clear for the car.
/// </summary>
public class Hud : ScreenManager, IAutograderHud
{
    #region Set in Unity Editor
    /// <summary>
    /// The HUD document (Hud.uxml on the shared panel settings).
    /// </summary>
    [SerializeField]
    private UIDocument document;
    #endregion

    #region Constants
    /// <summary>
    /// The background of sensor visualizations (depth and LIDAR) where there is no reading.
    /// </summary>
    public static readonly Color SensorBackgroundColor = new Color(0.2f, 0.2f, 0.2f);

    /// <summary>
    /// The number of times the LIDAR map is smaller than the color and depth visualizations.
    /// </summary>
    private const int lidarMapScale = 4;

    /// <summary>
    /// In the autograder, when the current time is this fraction of the time limit away from the time limit, the timer turns to the warning color.
    /// </summary>
    private const float autograderWarningTimeRatio = 0.25f;

    /// <summary>
    /// Current at a full current bar (in amps).
    /// </summary>
    private const float maxBatteryCurrent = 10;

    /// <summary>
    /// Label and icon for each SimulationMode.
    /// </summary>
    internal static readonly (string Label, MenuIcon.Glyph Icon)[] Modes =
    {
        ("Drive", MenuIcon.Glyph.Controls),
        ("Program", MenuIcon.Glyph.Play),
        ("Waiting", MenuIcon.Glyph.BestTimes),
        ("Finished", MenuIcon.Glyph.Race),
    };

    /// <summary>
    /// Controller inputs shown in the tray, in order: label and whether it is pressed.
    /// </summary>
    private static readonly (string Label, Func<bool> IsDown)[] padInputs =
    {
        ("A", () => Controller.IsDown(Controller.Button.A)),
        ("B", () => Controller.IsDown(Controller.Button.B)),
        ("X", () => Controller.IsDown(Controller.Button.X)),
        ("Y", () => Controller.IsDown(Controller.Button.Y)),
        ("LB", () => Controller.IsDown(Controller.Button.LB)),
        ("RB", () => Controller.IsDown(Controller.Button.RB)),
        ("LT", () => Controller.GetTrigger(Controller.Trigger.LEFT) > 0),
        ("RT", () => Controller.GetTrigger(Controller.Trigger.RIGHT) > 0),
        ("LS", () => Controller.GetJoystick(Controller.Joystick.LEFT) != Vector2.zero || Controller.IsDown(Controller.Button.LJOY)),
        ("RS", () => Controller.GetJoystick(Controller.Joystick.RIGHT) != Vector2.zero || Controller.IsDown(Controller.Button.RJOY)),
        ("Back", () => Controller.IsDown(Controller.Button.BACK)),
        ("Start", () => Controller.IsDown(Controller.Button.START)),
    };
    #endregion

    private VisualElement root;
    private MiniMapView mapView;
    private SpeedGauge gauge;
    private MenuIcon modeIcon;
    private Label modeLabel;
    private VisualElement connection;
    private Label timeScaleLabel;
    private VisualElement toast;
    private Label toastText;
    private ParallelogramElement timer;
    private VisualElement pips;
    private Label accelText;
    private Label gyroText;
    private VisualElement battery;
    private VisualElement voltsFill;
    private VisualElement ampsFill;
    private Label voltsText;
    private Label ampsText;
    private VisualElement dotMatrix;
    private Label[] padKeys;
    private VisualElement[] trays;
    private VisualElement[] navigation;
    private BodyFrameView bodyFrame;

    private Texture2D lidarTexture;
    private Texture2D depthTexture;
    private DotMatrixPanel dotMatrixPanel;
    private MiniMap miniMap;

    private SimulationMode simulationMode = SimulationMode.DefaultDrive;
    private bool isProgramConnected;
    private float toastHideAt = -1;

    #region Public Interface
    /// <summary>
    /// The texture containing the LIDAR visualization.
    /// </summary>
    public Texture2D LidarVisualization => this.lidarTexture;

    /// <summary>
    /// The texture containing the depth camera visualization.
    /// </summary>
    public Texture2D DepthVisualization => this.depthTexture;

    /// <summary>
    /// The dot matrix drawn on the HUD (shown when Settings.ShowDotMatrix is on).
    /// </summary>
    public DotMatrixPanel DotMatrix => this.dotMatrixPanel;

    /// <summary>
    /// True while the sensor tray is shown.
    /// </summary>
    public bool AreSensorsShown { get; private set; } = true;

    /// <summary>
    /// True while the map and speed gauge are shown.
    /// </summary>
    public bool IsMapShown { get; private set; } = true;

    /// <summary>
    /// Fill of the voltage bar: the charge the LED strip shows for the voltage (7.0 V empty,
    /// 8.4 V full), in the strip's steps of one LED per side.
    /// </summary>
    public static float BatteryBarFraction(float volts)
    {
        int halfStrip = ActuatorCommands.LedCount / 2;
        int lit = (int)(Mathf.Clamp01(BatteryModel.ChargeFromVoltage(volts)) * 100) * halfStrip / 100;
        return (float)lit / halfStrip;
    }

    /// <summary>
    /// Lap time as the timer badge shows it: MM:SS.cc.
    /// </summary>
    public static string FormatLapTime(float seconds)
    {
        int minutes = Mathf.Min(99, (int)(seconds / 60));
        float rest = seconds - minutes * 60;
        return $"{minutes:00}:{rest.ToString("00.00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Shows or hides the map and speed gauge; the map camera stops rendering while hidden.
    /// </summary>
    public void SetMapShown(bool isShown)
    {
        this.IsMapShown = isShown;
        foreach (VisualElement element in this.navigation)
        {
            element.EnableInClassList("hud-nav--hidden", !isShown);
        }
        this.miniMap.enabled = isShown;
    }

    /// <summary>
    /// Shows or hides the sensor trays in the bottom corners.
    /// </summary>
    public void SetSensorsShown(bool isShown)
    {
        this.AreSensorsShown = isShown;
        foreach (VisualElement tray in this.trays)
        {
            tray.EnableInClassList("hud-tray--hidden", !isShown);
        }
    }

    public override void ShowMessage(string message, Color color, float persistTime = -1, float fadeTime = 1.0f)
    {
        bool show = !string.IsNullOrEmpty(message);
        this.toast.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        this.toastText.text = message;
        this.toast.EnableInClassList("hud-toast--error", ScreenManager.IsError(color));
        this.toast.EnableInClassList("hud-toast--warning", ScreenManager.IsWarning(color));
        this.toast.RemoveFromClassList("hud-toast--success");
        this.toastHideAt = show && persistTime > 0 ? Time.unscaledTime + persistTime + fadeTime : -1;
    }

    public override void UpdateTime(float mainTime, float[] keyPointDurations)
    {
        this.timer.Text = Hud.FormatLapTime(mainTime);

        // One pip per checkpoint; durations[i] is nonzero once key point i is passed
        int checkpoints = Mathf.Max(0, keyPointDurations.Length - 2);
        if (this.pips.childCount != checkpoints)
        {
            this.pips.Clear();
            for (int i = 0; i < checkpoints; i++)
            {
                VisualElement pip = new VisualElement();
                pip.AddToClassList("hud-pip");
                this.pips.Add(pip);
            }
        }
        for (int i = 0; i < checkpoints; i++)
        {
            this.pips[i].EnableInClassList("hud-pip--done", i + 2 < keyPointDurations.Length && keyPointDurations[i + 2] > 0);
        }
    }

    public override void SetPause(bool isPaused)
    {
        this.root.Q("pause").style.display = isPaused ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public override void HandleWin(float time, bool isNewBestTime = false)
    {
        Hud.ShowResult(this.root, MenuIcon.Glyph.Race, "Finished", Hud.FormatLapTime(time), isNewBestTime, false);
    }

    public override void HandleFailure(int carIndex, string reason)
    {
        Hud.ShowResult(this.root, MenuIcon.Glyph.Exit, "Objective failed", reason, false, true);
    }

    public override void UpdateConnectedPrograms(bool[] connectedPrograms)
    {
        this.isProgramConnected = connectedPrograms.Length > 0 && connectedPrograms[0];
        this.connection.EnableInClassList("hud-connection--on", this.isProgramConnected);
        this.dotMatrixPanel.SetDriveMode(this.simulationMode, this.isProgramConnected);
    }

    public override void UpdateMode(SimulationMode mode)
    {
        this.simulationMode = mode;
        (string label, MenuIcon.Glyph icon) = Hud.Modes[(int)mode];
        this.modeLabel.text = label;
        this.modeIcon.Icon = icon;
        this.dotMatrixPanel.SetDriveMode(this.simulationMode, this.isProgramConnected);
    }

    public override void UpdateTimeScale(float timeScale)
    {
        this.timeScaleLabel.text = timeScale >= 1 ? string.Empty : $"{timeScale.ToString("0.###", CultureInfo.InvariantCulture)}x";
    }

    /// <summary>
    /// Update the physics readouts: speed (gauge) and IMU values.
    /// </summary>
    /// <param name="speed">The magnitude of the car's linear velocity in m/s.</param>
    /// <param name="linearAcceleration">The car's linear acceleration in m/s^2.</param>
    /// <param name="angularVelocity">The car's angular velocity in rad/s.</param>
    public void UpdatePhysics(float speed, Vector3 linearAcceleration, Vector3 angularVelocity)
    {
        this.gauge.Speed = speed;
        this.accelText.text = Hud.FormatVector3(linearAcceleration);
        this.gyroText.text = Hud.FormatVector3(angularVelocity);
    }

    /// <summary>
    /// Update the battery meters.
    /// </summary>
    /// <param name="volts">Bus voltage in V.</param>
    /// <param name="amps">Current draw in A.</param>
    /// <param name="isEmpty">True to show the reading in the warning color.</param>
    public void UpdateBattery(float volts, float amps, bool isEmpty)
    {
        this.battery.EnableInClassList("hud-battery--empty", isEmpty);
        this.voltsFill.style.width = Length.Percent(Hud.BatteryBarFraction(volts) * 100);
        this.ampsFill.style.width = Length.Percent(Mathf.Clamp01(amps / Hud.maxBatteryCurrent) * 100);
        this.voltsText.text = $"{volts.ToString("F2", CultureInfo.InvariantCulture)} V";
        this.ampsText.text = $"{amps.ToString("F2", CultureInfo.InvariantCulture)} A";
    }
    #endregion

    #region IAutograderHud
    void IAutograderHud.SetLevelInfo(int levelIndex, string title, string description)
    {
        this.root.Q("autograder").style.display = DisplayStyle.Flex;
        this.root.Q<Label>("ag-trial").text = $"TRIAL {levelIndex + 1}";
        this.root.Q<Label>("ag-title").text = title;
        this.root.Q<Label>("ag-description").text = description;
    }

    void IAutograderHud.UpdateScore(float score, float maxScore)
    {
        this.root.Q<Label>("ag-score").text = $"{score.ToString("0.##", CultureInfo.InvariantCulture)} / {maxScore.ToString("0.##", CultureInfo.InvariantCulture)}";
        this.root.Q<GradientBar>("ag-progress").Fill = maxScore > 0 ? score / maxScore : 0;
    }

    void IAutograderHud.UpdateTime(float time, float timeLimit)
    {
        bool isOver = time >= timeLimit;
        bool isWarning = !isOver && timeLimit - time < timeLimit * Hud.autograderWarningTimeRatio;
        this.timer.Text = Hud.FormatLapTime(time);
        this.timer.EnableInClassList("hud-timer--over", isOver);
        this.timer.EnableInClassList("hud-timer--warning", isWarning);
        TimeRing ring = this.root.Q<TimeRing>("ag-ring");
        ring.Elapsed = timeLimit > 0 ? time / timeLimit : 0;
        ring.EnableInClassList("hud-ag-ring--over", isOver);
        ring.EnableInClassList("hud-ag-ring--warning", isWarning);
    }

    void IAutograderHud.SetTimeBrackets(float timeLimit, float[] bracketTimes)
    {
        this.root.Q<TimeRing>("ag-ring").Ticks = bracketTimes.Select(time => timeLimit > 0 ? time / timeLimit : 0).ToArray();
        this.root.Q<Label>("ag-ring-text").text = $"{timeLimit.ToString("0.#", CultureInfo.InvariantCulture)} s";
    }

    void IAutograderHud.ShowTrialResult(int levelIndex, float score, float maxScore)
    {
        this.ShowMessage($"TRIAL {levelIndex + 1}   {score.ToString("0.##", CultureInfo.InvariantCulture)} / {maxScore.ToString("0.##", CultureInfo.InvariantCulture)}", Color.white);
        this.toast.EnableInClassList("hud-toast--success", score >= maxScore);
        this.toast.EnableInClassList("hud-toast--warning", score > 0 && score < maxScore);
        this.toast.EnableInClassList("hud-toast--error", score <= 0 && maxScore > 0);
    }

    void IAutograderHud.SetMaxTime(float maxTime)
    {
        // The ring shows the limit; the label carries only time bonuses
        this.root.Q<Label>("ag-limit").text = string.Empty;
    }

    void IAutograderHud.SetTimeBonus(float maxTime, float bonus, bool isLastBracket)
    {
        string sign = bonus >= 0 ? "+" : string.Empty;
        this.root.Q<Label>("ag-limit").text = $"{sign}{bonus.ToString("0.##", CultureInfo.InvariantCulture)} under {maxTime.ToString("0.#", CultureInfo.InvariantCulture)} s";
    }
    #endregion

    private void Awake()
    {
        this.root = this.document.rootVisualElement;
        this.mapView = this.root.Q<MiniMapView>("map-overlay");
        this.gauge = this.root.Q<SpeedGauge>("gauge");
        this.modeIcon = this.root.Q<MenuIcon>("mode-icon");
        this.modeLabel = this.root.Q<Label>("mode-label");
        this.connection = this.root.Q("connection");
        this.timeScaleLabel = this.root.Q<Label>("time-scale");
        this.toast = this.root.Q("toast");
        this.toastText = this.root.Q<Label>("toast-text");
        this.timer = this.root.Q<ParallelogramElement>("timer");
        this.pips = this.root.Q("pips");
        this.accelText = this.root.Q<Label>("accel");
        this.gyroText = this.root.Q<Label>("gyro");
        this.battery = this.root.Q("battery");
        this.voltsFill = this.root.Q("volts-fill");
        this.ampsFill = this.root.Q("amps-fill");
        this.voltsText = this.root.Q<Label>("volts-text");
        this.ampsText = this.root.Q<Label>("amps-text");
        this.dotMatrix = this.root.Q("dot-matrix");
        this.trays = new[] { this.root.Q("tray-left"), this.root.Q("tray-right") };
        this.navigation = new[] { this.root.Q("map"), this.root.Q("gauge-box") };
        this.bodyFrame = this.root.Q<BodyFrameView>("body-frame");

        this.lidarTexture = new Texture2D(CameraModule.ColorWidth / Hud.lidarMapScale, CameraModule.ColorHeight / Hud.lidarMapScale, TextureFormat.RGBA32, false);
        this.depthTexture = new Texture2D(CameraModule.DepthWidth, CameraModule.DepthHeight, TextureFormat.RGBA32, false);
        this.root.Q("lidar-feed").style.backgroundImage = this.lidarTexture;
        this.root.Q("depth-feed").style.backgroundImage = this.depthTexture;
        this.dotMatrixPanel = new DotMatrixPanel(LevelManager.LevelManagerMode != LevelManagerMode.Autograder || AutograderManager.IsFirstLevel);
        this.dotMatrix.style.backgroundImage = this.dotMatrixPanel.Texture;

        VisualElement pad = this.root.Q("pad");
        this.padKeys = Hud.padInputs.Select(input =>
        {
            Label key = new Label(input.Label);
            key.AddToClassList("hud-pad-key");
            pad.Add(key);
            return key;
        }).ToArray();

        this.miniMap = this.gameObject.AddComponent<MiniMap>();
        this.ShowMessage(string.Empty, Color.white);
        this.UpdateMode(SimulationMode.DefaultDrive);
        this.UpdateTimeScale(1);
        this.UpdateTime(0, new float[0]);
        this.UpdatePhysics(0, Vector3.zero, Vector3.zero);
        this.SetPause(false);
    }

    private void Start()
    {
        Racecar car = LevelManager.GetCar(0);
        if (car == null)
        {
            return;
        }
        this.root.Q("color-feed").style.backgroundImage = Background.FromRenderTexture(car.GetComponent<CameraModule>().ColorImage);
        LevelPresentation presentation = LevelPresentationCatalog.Get(LevelManager.LevelInfo);
        this.miniMap.Follow(car.transform, presentation.TopViewArea.width);
        this.root.Q("map-image").style.backgroundImage = Background.FromRenderTexture(this.miniMap.Texture);
        this.mapView.SetRoute(LevelManager.KeyPointPositions, presentation.IsLoop);
    }

    private void Update()
    {
        if (this.toastHideAt > 0 && Time.unscaledTime >= this.toastHideAt)
        {
            this.ShowMessage(string.Empty, Color.white);
        }

        if (Input.GetKeyDown(Hotkeys.ToggleSensors))
        {
            this.SetSensorsShown(!this.AreSensorsShown);
        }
        if (Input.GetKeyDown(Hotkeys.ToggleMap))
        {
            this.SetMapShown(!this.IsMapShown);
        }

        for (int i = 0; i < this.padKeys.Length; i++)
        {
            this.padKeys[i].EnableInClassList("hud-pad-key--down", Hud.padInputs[i].IsDown());
        }

        Racecar car = LevelManager.GetCar(0);
        if (car != null)
        {
            this.bodyFrame.SetOrientation(car.transform.rotation);
        }
        bool showMatrix = Settings.ShowDotMatrix && car != null;
        this.dotMatrix.style.display = showMatrix ? DisplayStyle.Flex : DisplayStyle.None;
        if (showMatrix)
        {
            this.dotMatrixPanel.Refresh(car);
        }

        if (this.miniMap.Target != null)
        {
            Transform target = this.miniMap.Target;
            this.mapView.SetView(target.position, target.eulerAngles.y, this.miniMap.HalfExtent, LevelManager.NextKeyPoint(0));
        }
    }

    private void OnDestroy()
    {
        Destroy(this.lidarTexture);
        Destroy(this.depthTexture);
        this.dotMatrixPanel?.Dispose();
    }

    /// <summary>
    /// Shows the result card (the "result" element of Hud.uxml and RaceScreen.uxml) with restart
    /// and menu hints.
    /// </summary>
    internal static void ShowResult(VisualElement root, MenuIcon.Glyph icon, string title, string detail, bool isNewBestTime, bool isFailure)
    {
        VisualElement result = root.Q("result");
        result.style.display = DisplayStyle.Flex;
        result.Q("result-card").EnableInClassList("hud-result--failed", isFailure);
        result.Q<MenuIcon>("result-icon").Icon = icon;
        result.Q<Label>("result-title").text = title;
        result.Q<Label>("result-detail").text = detail;
        result.Q("result-best").style.display = isNewBestTime ? DisplayStyle.Flex : DisplayStyle.None;

        VisualElement hints = result.Q("result-hints");
        hints.Clear();
        hints.Add(Hud.Hint(new[] { Controller.KeyFor(Controller.Button.START), Controller.KeyFor(Controller.Button.BACK) }, "Restart"));
        hints.Add(Hud.Hint(new[] { Hotkeys.Exit }, "Menu"));
    }

    private static VisualElement Hint(KeyCode[] keys, string text)
    {
        VisualElement hint = new VisualElement();
        hint.AddToClassList("hud-hint");
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0)
            {
                Label join = new Label("+");
                join.AddToClassList("rcn-key-join");
                hint.Add(join);
            }
            Label cap = new Label(InputBindings.KeyLabel(keys[i]));
            cap.AddToClassList("rcn-key");
            hint.Add(cap);
        }
        Label label = new Label(text);
        label.AddToClassList("rcn-label");
        label.AddToClassList("hud-hint-label");
        hint.Add(label);
        return hint;
    }

    /// <summary>
    /// A vector as three fixed-width values: " 0.12  -9.81   0.00".
    /// </summary>
    private static string FormatVector3(Vector3 vector)
    {
        return $"{Hud.FormatFloat(vector.x)} {Hud.FormatFloat(vector.y)} {Hud.FormatFloat(vector.z)}";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6);
    }
}
