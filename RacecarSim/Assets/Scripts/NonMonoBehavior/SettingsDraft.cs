using System.Linq;
using UnityEngine;

/// <summary>
/// Settings being edited on the Settings screen: a copy of Settings and the car customizations
/// that Apply saves and that the screen discards on Cancel. Defaults changes only the draft.
/// </summary>
public class SettingsDraft
{
    public bool IsRealism;
    public bool IsRealCarResponse;
    public bool IsAntennaColliderRemoved;
    public bool IsBatteryMode;
    public bool HideCarsInColorCamera;
    public bool ShowDotMatrix;
    public Settings.DepthResolution DepthRes;
    public Settings.CarDetailLevel CarDetail;
    public string Username;

    /// <summary>
    /// Shell color and finish per car, car 1 first.
    /// </summary>
    public Color[] ShellColors;
    public bool[] IsShellShiny;

    /// <summary>
    /// A draft of the current settings and saved customizations.
    /// </summary>
    public static SettingsDraft Current()
    {
        return SettingsDraft.From(SavedDataManager.Data.CarCustomizations);
    }

    /// <summary>
    /// A draft of the current settings with copies of the given car customizations.
    /// </summary>
    public static SettingsDraft From(CarCustomization[] customizations)
    {
        SettingsDraft draft = new SettingsDraft
        {
            IsRealism = Settings.IsRealism,
            IsRealCarResponse = Settings.IsRealCarResponse,
            IsAntennaColliderRemoved = Settings.IsAntennaColliderRemoved,
            IsBatteryMode = Settings.IsBatteryMode,
            HideCarsInColorCamera = Settings.HideCarsInColorCamera,
            ShowDotMatrix = Settings.ShowDotMatrix,
            DepthRes = Settings.DepthRes,
            CarDetail = Settings.CarDetail,
            Username = Settings.Username,
        };
        draft.LoadCustomizations(customizations);
        return draft;
    }

    /// <summary>
    /// Sets every field to its default (Settings.Default*, SavedData.Default customizations).
    /// </summary>
    public void RestoreDefaults()
    {
        this.IsRealism = Settings.DefaultIsRealism;
        this.IsRealCarResponse = Settings.DefaultIsRealCarResponse;
        this.IsAntennaColliderRemoved = Settings.DefaultIsAntennaColliderRemoved;
        this.IsBatteryMode = Settings.DefaultIsBatteryMode;
        this.HideCarsInColorCamera = Settings.DefaultHideCarsInColorCamera;
        this.ShowDotMatrix = Settings.DefaultShowDotMatrix;
        this.DepthRes = Settings.DefaultDepthRes;
        this.CarDetail = Settings.DefaultCarDetail;
        this.Username = Settings.DefaultUsername;
        this.LoadCustomizations(SavedData.Default.CarCustomizations);
    }

    /// <summary>
    /// Writes the draft to Settings and the saved car customizations, and saves both.
    /// </summary>
    public void Apply()
    {
        Settings.IsRealism = this.IsRealism;
        Settings.IsRealCarResponse = this.IsRealCarResponse;
        Settings.IsAntennaColliderRemoved = this.IsAntennaColliderRemoved;
        Settings.IsBatteryMode = this.IsBatteryMode;
        Settings.HideCarsInColorCamera = this.HideCarsInColorCamera;
        Settings.ShowDotMatrix = this.ShowDotMatrix;
        Settings.DepthRes = this.DepthRes;
        Settings.CarDetail = this.CarDetail;
        Settings.Username = SettingsDraft.CleanUsername(this.Username);
        Settings.SaveSettings();

        CarCustomization[] saved = SavedDataManager.Data.CarCustomizations;
        for (int i = 0; i < saved.Length && i < this.ShellColors.Length; i++)
        {
            Color color = this.ShellColors[i];
            saved[i].ShellColor = new SerializableColor(color.r, color.g, color.b);
            saved[i].IsShellShiny = this.IsShellShiny[i];
        }
        SavedDataManager.Save();
    }

    /// <summary>
    /// The username to save: trimmed, or the default when blank.
    /// </summary>
    public static string CleanUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? Settings.DefaultUsername : username.Trim();
    }

    private void LoadCustomizations(CarCustomization[] customizations)
    {
        this.ShellColors = customizations.Select(each => each.ShellColor.Color).ToArray();
        this.IsShellShiny = customizations.Select(each => each.IsShellShiny).ToArray();
    }
}
