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
        this.hideCarsToggle.isOn = Settings.DefaultHideCarsInColorCamera;
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
        Settings.HideCarsInColorCamera = this.hideCarsToggle.isOn;
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
    /// Hide other cars in the color camera.
    /// </summary>
    [SerializeField]
    private Toggle hideCarsToggle;

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
    /// Car color sliders, six per car: front red, green, blue, then back red, green, blue.
    /// </summary>
    [SerializeField]
    private Slider[] colorSliders;

    /// <summary>
    /// Shiny paint toggles, two per car: front, then back.
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
        this.hideCarsToggle.isOn = Settings.HideCarsInColorCamera;
        this.depthResDropdown.value = (int)Settings.DepthRes;
        this.username.text = Settings.Username;

        this.LoadColors(SavedDataManager.Data.CarCustomizations);
    }

    /// <summary>
    /// The number of cars with color inputs: six sliders (front and back RGB) per car, limited by
    /// the number of saved customizations.
    /// </summary>
    private int NumColorInputCars
    {
        get
        {
            return Mathf.Min(this.colorSliders.Length / 6, SavedDataManager.Data.CarCustomizations.Length);
        }
    }

    /// <summary>
    /// Applies the current customization options to the saved data.
    /// </summary>
    private void ApplyColorInputs()
    {
        for (int i = 0; i < this.NumColorInputCars; i++)
        {
            SavedDataManager.Data.CarCustomizations[i].FrontColor = new SerializableColor(
                this.colorSliders[6 * i].value,
                this.colorSliders[6 * i + 1].value,
                this.colorSliders[6 * i + 2].value);

            SavedDataManager.Data.CarCustomizations[i].BackColor = new SerializableColor(
                this.colorSliders[6 * i + 3].value,
                this.colorSliders[6 * i + 4].value,
                this.colorSliders[6 * i + 5].value);

            SavedDataManager.Data.CarCustomizations[i].IsFrontShiny = this.shinyToggles[2 * i].isOn;
            SavedDataManager.Data.CarCustomizations[i].IsBackShiny = this.shinyToggles[2 * i + 1].isOn;
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
            this.colorSliders[6 * i].value = customization.FrontColor.r;
            this.colorSliders[6 * i + 1].value = customization.FrontColor.g;
            this.colorSliders[6 * i + 2].value = customization.FrontColor.b;

            this.colorSliders[6 * i + 3].value = customization.BackColor.r;
            this.colorSliders[6 * i + 4].value = customization.BackColor.g;
            this.colorSliders[6 * i + 5].value = customization.BackColor.b;

            this.shinyToggles[2 * i].isOn = customization.IsFrontShiny;
            this.shinyToggles[2 * i + 1].isOn = customization.IsBackShiny;
        }
    }
}
