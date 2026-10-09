using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the settings pane of the main menu.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    #region Public Interface
    /// <summary>
    /// Restore the default settings.
    /// </summary>
    public void RestoreDefaultSetting()
    {
        // Only the inputs change; Save applies them and Cancel discards them
        this.realismToggle.isOn = Settings.DefaultIsRealism;
        this.batteryModeToggle.isOn = Settings.DefaultIsBatteryMode;
        this.hideCarsToggle.isOn = Settings.DefaultHideCarsInColorCamera;
        this.showDotMatrixToggle.isOn = Settings.DefaultShowDotMatrix;
        this.depthResDropdown.value = (int)Settings.DefaultDepthRes;
        this.username.text = Settings.DefaultUsername;
        this.LoadColors(SavedData.Default.CarCustomizations);
    }

    /// <summary>
    /// Save current settings and close the settings screen.
    /// </summary>
    public void SaveSettings()
    {
        Settings.IsRealism = this.realismToggle.isOn;
        Settings.IsBatteryMode = this.batteryModeToggle.isOn;
        Settings.HideCarsInColorCamera = this.hideCarsToggle.isOn;
        Settings.ShowDotMatrix = this.showDotMatrixToggle.isOn;
        Settings.DepthRes = (Settings.DepthResolution)this.depthResDropdown.value;
        Settings.Username = this.username.text;
        
        this.ApplyColorInputs();

        Settings.SaveSettings();
        this.gameObject.SetActive(false);
    }

    /// <summary>
    /// Close the settings screen without saving any changes.
    /// </summary>
    public void Cancel()
    {
        this.UpdateInputs();
        this.gameObject.SetActive(false);
    }

    public void ColorChanged()
    {
        // Slider change hook referenced by the scene; colors apply on Save
    }
    #endregion

    #region Set in Unity Editor
    /// <summary>
    /// Realism mode (sensor noise).
    /// </summary>
    [SerializeField]
    private Toggle realismToggle;

    /// <summary>
    /// Battery mode (an empty battery stops the car).
    /// </summary>
    [SerializeField]
    private Toggle batteryModeToggle;

    /// <summary>
    /// Hide other cars in the color camera.
    /// </summary>
    [SerializeField]
    private Toggle hideCarsToggle;

    /// <summary>
    /// Show the car's dot matrix on the HUD.
    /// </summary>
    [SerializeField]
    private Toggle showDotMatrixToggle;

    /// <summary>
    /// The depth camera resolution.
    /// </summary>
    [SerializeField]
    private Dropdown depthResDropdown;

    /// <summary>
    /// The username.
    /// </summary>
    [SerializeField]
    private InputField username;

    /// <summary>
    /// Shell color sliders, three per car: red, green, blue.
    /// </summary>
    [SerializeField]
    private Slider[] colorSliders;

    /// <summary>
    /// Shiny shell toggles, one per car.
    /// </summary>
    [SerializeField]
    private Toggle[] shinyToggles;
    #endregion

    private void Start()
    {
        this.UpdateInputs();
    }

    /// <summary>
    /// Update all input values on the settings pane with the current settings.
    /// </summary>
    private void UpdateInputs()
    {
        this.realismToggle.isOn = Settings.IsRealism;
        this.batteryModeToggle.isOn = Settings.IsBatteryMode;
        this.hideCarsToggle.isOn = Settings.HideCarsInColorCamera;
        this.showDotMatrixToggle.isOn = Settings.ShowDotMatrix;
        this.depthResDropdown.value = (int)Settings.DepthRes;
        this.username.text = Settings.Username;

        this.LoadColors(SavedDataManager.Data.CarCustomizations);
    }

    /// <summary>
    /// The number of cars with color inputs: three sliders (shell RGB) per car, limited by the
    /// number of saved customizations.
    /// </summary>
    private int NumColorInputCars
    {
        get
        {
            return Mathf.Min(this.colorSliders.Length / 3, SavedDataManager.Data.CarCustomizations.Length);
        }
    }

    /// <summary>
    /// Applies the current customization options to the saved data.
    /// </summary>
    private void ApplyColorInputs()
    {
        for (int i = 0; i < this.NumColorInputCars; i++)
        {
            SavedDataManager.Data.CarCustomizations[i].ShellColor = new SerializableColor(
                this.colorSliders[3 * i].value,
                this.colorSliders[3 * i + 1].value,
                this.colorSliders[3 * i + 2].value);
            SavedDataManager.Data.CarCustomizations[i].IsShellShiny = this.shinyToggles[i].isOn;
        }

        SavedDataManager.Save();
    }

    /// <summary>
    /// Update the customization interface with the current saved data.
    /// </summary>
    private void LoadColors(CarCustomization[] customizations)
    {
        for (int i = 0; i < this.NumColorInputCars; i++)
        {
            CarCustomization customization = customizations[i];
            this.colorSliders[3 * i].value = customization.ShellColor.r;
            this.colorSliders[3 * i + 1].value = customization.ShellColor.g;
            this.colorSliders[3 * i + 2].value = customization.ShellColor.b;
            this.shinyToggles[i].isOn = customization.IsShellShiny;
        }
    }
}
