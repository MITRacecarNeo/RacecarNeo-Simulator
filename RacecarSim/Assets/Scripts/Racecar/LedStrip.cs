using UnityEngine;

/// <summary>
/// The car's 84-pixel WS2812B LED strip in the light band around the shell: the program's colors
/// when it sets them, otherwise the drive board firmware's battery display.
/// </summary>
/// <remarks>
/// The band mesh's UV x runs from LED 0 at the front-left end, around the rear, to LED 83 at the
/// front-right end. Each LED lights its own segment of an emission texture; unlit segments show
/// the band's diffuser color.
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
    /// Texture pixels per LED along the band; the outer pixel of each LED is dimmed so neighboring
    /// LEDs read as separate dots.
    /// </summary>
    private const int pixelsPerLed = 4;

    /// <summary>
    /// The firmware's idle battery display: a red bar filling each end toward the middle.
    /// </summary>
    private static readonly Color32 batteryColor = new Color32(255, 0, 0, 255);

    /// <summary>
    /// Emission strength of a fully lit LED, so lit segments stand out in daylight scenes.
    /// </summary>
    private static readonly Color emissionColor = new Color(1.6f, 1.6f, 1.6f);

    private static readonly int emissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");
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
    /// The colors currently shown, LED 0 first.
    /// </summary>
    public Color32[] Shown { get; private set; }
    #endregion

    private Texture2D texture;

    private Color32[] texturePixels;

    private Material material;

    private void Start()
    {
        this.texture = new Texture2D(ActuatorCommands.LedCount * LedStrip.pixelsPerLed, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        this.texturePixels = new Color32[this.texture.width];

        // Renderer.material creates a per-car copy, destroyed in OnDestroy
        this.material = this.band.material;
        this.material.SetTexture(LedStrip.emissionMapId, this.texture);
        this.material.SetColor(LedStrip.emissionColorId, LedStrip.emissionColor);
    }

    private void Update()
    {
        Color32[] colors = this.racecar.Actuators.GetLeds()
            ?? LedStrip.BatteryFrame(BatteryModel.ChargeFromVoltage(this.racecar.Readings.BatteryVoltage));
        if (this.Shown != null && LedStrip.Same(colors, this.Shown))
        {
            return;
        }

        this.Shown = colors;
        for (int i = 0; i < colors.Length; i++)
        {
            Color32 dim = Color32.Lerp(new Color32(0, 0, 0, 255), colors[i], 0.35f);
            int x = i * LedStrip.pixelsPerLed;
            this.texturePixels[x] = dim;
            this.texturePixels[x + 1] = colors[i];
            this.texturePixels[x + 2] = colors[i];
            this.texturePixels[x + 3] = dim;
        }
        this.texture.SetPixels32(this.texturePixels);
        this.texture.Apply(false);
    }

    private void OnDestroy()
    {
        Destroy(this.texture);
        Destroy(this.material);
    }

    private static bool Same(Color32[] a, Color32[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }
}
