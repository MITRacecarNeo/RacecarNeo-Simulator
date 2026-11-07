using UnityEngine;

/// <summary>
/// The car's 84-pixel WS2812B LED strip in the light band around the shell: the program's colors
/// when it sets them, otherwise the drive board firmware's battery display.
/// </summary>
/// <remarks>
/// LedBandRenderer draws the colors on the band; unlit segments show the band's diffuser color.
/// </remarks>
public class LedStrip : RacecarModule
{
    #region Set in Unity Editor
    /// <summary>
    /// The light band mesh (LedBar), with an emissive material.
    /// </summary>
    [SerializeField]
    private Renderer band;
    #endregion

    #region Constants
    /// <summary>
    /// The firmware's idle battery display: a red bar filling each end toward the middle.
    /// </summary>
    private static readonly Color32 batteryColor = new Color32(255, 0, 0, 255);
    #endregion

    #region Public Interface
    /// <summary>
    /// The firmware's idle display for a battery charge: SoC x 42 / 100 red LEDs lit from each end
    /// (racecar-pit-firmware helpers.cpp, ledStuffs).
    /// </summary>
    /// <param name="charge">Charge from 0 to 1, as the firmware reads it from the bus voltage.</param>
    public static Color32[] BatteryFrame(float charge)
    {
        Color32[] colors = new Color32[ActuatorCommands.LedCount];
        int half = (int)(Mathf.Clamp01(charge) * 100) * (ActuatorCommands.LedCount / 2) / 100;
        for (int i = 0; i < half; i++)
        {
            colors[i] = LedStrip.batteryColor;
            colors[ActuatorCommands.LedCount - 1 - i] = LedStrip.batteryColor;
        }
        return colors;
    }

    /// <summary>
    /// A rainbow along the index order, LED 0 at the given hue (0 to 1) and the hue rising toward
    /// LED 83, as in the rainbow reference renders of the car.
    /// </summary>
    public static Color32[] RainbowFrame(float startHue)
    {
        Color32[] colors = new Color32[ActuatorCommands.LedCount];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.HSVToRGB(Mathf.Repeat(startHue + (float)i / colors.Length, 1), 1, 1);
        }
        return colors;
    }

    /// <summary>
    /// The colors currently shown, LED 0 first.
    /// </summary>
    public Color32[] Shown => this.bandRenderer?.Shown;
    #endregion

    private LedBandRenderer bandRenderer;

    private void Start()
    {
        this.bandRenderer = new LedBandRenderer(this.band);
    }

    private void Update()
    {
        this.bandRenderer.Show(this.racecar.Actuators.GetLeds()
            ?? LedStrip.BatteryFrame(BatteryModel.ChargeFromVoltage(this.racecar.Readings.BatteryVoltage)));
    }

    private void OnDestroy()
    {
        this.bandRenderer?.Dispose();
    }
}
