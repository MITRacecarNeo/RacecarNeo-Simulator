using UnityEngine;

/// <summary>
/// The car on the main menu: turns slowly on its turntable, wears player 1's shell customization
/// (updated as soon as Settings saves a change, or the shell being edited while Settings is open),
/// and runs a rainbow along its LED band in index order (LED 0 front-left, around the rear, 83
/// front-right).
/// </summary>
public class MenuShowcase : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The transform the car turns on.
    /// </summary>
    [SerializeField]
    private Transform turntable;

    /// <summary>
    /// The car model's light band (LedBar).
    /// </summary>
    [SerializeField]
    private Renderer band;

    /// <summary>
    /// The car model's shell.
    /// </summary>
    [SerializeField]
    private Renderer shell;
    #endregion

    #region Constants
    /// <summary>
    /// Turntable speed in degrees per second.
    /// </summary>
    private const float turnSpeed = 12;

    /// <summary>
    /// Rainbow travel in hue turns per second; the colors move from LED 83 toward LED 0.
    /// </summary>
    private const float rainbowSpeed = 0.15f;
    #endregion

    private LedBandRenderer bandRenderer;
    private Material shellMaterial;
    private Color shownColor;
    private bool shownShiny;
    private bool isPreviewing;
    private float spinStart = -1;
    private float spinSeconds;
    private float spunDegrees;

    /// <summary>
    /// Extra half turn the turntable makes when the car spins in (SpinIn).
    /// </summary>
    private const float spinInDegrees = 180;

    /// <summary>
    /// Spins the car in: a half turn on top of the slow turn, fast at first and easing out over
    /// the given seconds.
    /// </summary>
    public void SpinIn(float seconds)
    {
        this.spinStart = Time.unscaledTime;
        this.spinSeconds = seconds;
        this.spunDegrees = 0;
    }

    /// <summary>
    /// Shows a shell color and finish until ClearPreview, instead of player 1's saved ones.
    /// </summary>
    public void Preview(Color color, bool shiny)
    {
        this.isPreviewing = true;
        this.Show(color, shiny);
    }

    /// <summary>
    /// Returns to player 1's saved shell.
    /// </summary>
    public void ClearPreview()
    {
        this.isPreviewing = false;
        this.ApplyCustomization(true);
    }

    private void Start()
    {
        this.bandRenderer = new LedBandRenderer(this.band);
        this.shellMaterial = this.shell.material;
        this.ApplyCustomization(true);
    }

    private void Update()
    {
        this.turntable.Rotate(0, MenuShowcase.turnSpeed * Time.deltaTime, 0, Space.World);
        if (this.spinStart >= 0)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - this.spinStart) / this.spinSeconds);
            float target = MenuShowcase.spinInDegrees * (1 - Mathf.Pow(1 - t, 3));
            this.turntable.Rotate(0, target - this.spunDegrees, 0, Space.World);
            this.spunDegrees = target;
            if (t >= 1)
            {
                this.spinStart = -1;
            }
        }
        this.bandRenderer.Show(LedStrip.RainbowFrame(Time.time * MenuShowcase.rainbowSpeed));
        this.ApplyCustomization(false);
    }

    /// <summary>
    /// Shows player 1's saved shell color and finish when they differ from what is shown.
    /// </summary>
    private void ApplyCustomization(bool force)
    {
        CarCustomization customization = SavedDataManager.Data.CarCustomizations[0];
        Color color = customization.ShellColor.Color;
        if (this.isPreviewing || (!force && color == this.shownColor && customization.IsShellShiny == this.shownShiny))
        {
            return;
        }
        this.Show(color, customization.IsShellShiny);
    }

    private void Show(Color color, bool shiny)
    {
        this.shownColor = color;
        this.shownShiny = shiny;
        this.shellMaterial.color = color;
        this.shellMaterial.SetFloat("_Metallic", shiny ? 1 : 0);
    }

    private void OnDestroy()
    {
        this.bandRenderer?.Dispose();
        Destroy(this.shellMaterial);
    }
}
