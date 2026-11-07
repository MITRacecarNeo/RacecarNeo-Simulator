using UnityEngine;

public static class Settings
{
    #region Constants
    /// <summary>
    /// The default value of DepthRes.
    /// </summary>
    public const DepthResolution DefaultDepthRes = DepthResolution.High;

    /// <summary>
    /// The default value of CarDetail.
    /// </summary>
    public const CarDetailLevel DefaultCarDetail = CarDetailLevel.Medium;

    /// <summary>
    /// The default value of IsRealism. 
    /// </summary>
    public const bool DefaultIsRealism = true;

    /// <summary>
    /// The default value of IsRealCarResponse.
    /// </summary>
    public const bool DefaultIsRealCarResponse = false;

    /// <summary>
    /// The default value of IsBatteryMode.
    /// </summary>
    public const bool DefaultIsBatteryMode = false;

    /// <summary>
    /// The default value of ShowDotMatrix.
    /// </summary>
    public const bool DefaultShowDotMatrix = true;

    /// <summary>
    /// The default value of HideCarsInColorCamera.
    /// </summary>
    public const bool DefaultHideCarsInColorCamera = false;

    /// <summary>
    /// The default value of Username.
    /// </summary>
    public const string DefaultUsername = "Default";

    /// <summary>
    /// The default fixed delta time (in seconds) chosen by Unity.
    /// </summary>
    public static readonly float DefaultFixedDeltaTime = Time.fixedDeltaTime;

    /// <summary>
    /// The divide factor corresponding to each DepthResolution.
    /// </summary>
    private static readonly int[] depthDivideFactors =
    {
        32,
        16,
        8
    };
    #endregion

    #region Public Interface
    /// <summary>
    /// Quality levels supported by the RacecarSim (higher quality is more computationally intensive).
    /// </summary>
    public enum DepthResolution
    {
        Low,
        Moderate,
        High,
    }

    /// <summary>
    /// Triangle counts of the car models in levels (CarModelDetail); visual only.
    /// </summary>
    public enum CarDetailLevel
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// If true, Gaussian error is added to all sensor readings.
    /// </summary>
    public static bool IsRealism;

    /// <summary>
    /// If true, the car's speed follows the physical car's measured response (CarResponse):
    /// forward overshoot, reverse climb. If false, the car holds the commanded speed.
    /// </summary>
    public static bool IsRealCarResponse;

    /// <summary>
    /// If true, a car with an empty battery cannot drive (outside autograder levels).
    /// </summary>
    public static bool IsBatteryMode;

    /// <summary>
    /// If true, other cars do not show up in the color camera.
    /// </summary>
    public static bool HideCarsInColorCamera;

    /// <summary>
    /// If true, the HUD shows the car's 8x24 dot matrix.
    /// </summary>
    public static bool ShowDotMatrix;

    /// <summary>
    /// If true, controller input is still sent in evaluation mode but best times are not recorded.
    /// </summary>
    public static bool CheatMode = false;

    /// <summary>
    /// The resolution to use for the depth camera.
    /// </summary>
    public static DepthResolution DepthRes;

    /// <summary>
    /// How detailed the car models are in levels. Scripted runs and tests use DefaultCarDetail.
    /// </summary>
    public static CarDetailLevel CarDetail;

    /// <summary>
    /// The course ID from the Edly autograder problem, written into score codes. Shown as "Course ID";
    /// the field keeps its name and its PlayerPrefs key so saved values carry over.
    /// </summary>
    public static string Username;

    /// <summary>
    /// The factor that the depth image is smaller than the color image along each axis.
    /// </summary>
    public static int DepthDivideFactor
    {
        get
        {
            return Settings.depthDivideFactors[(int)Settings.DepthRes];
        }
    }

    /// <summary>
    /// Restore all settings to the default values.
    /// </summary>
    public static void RestoreDefaults()
    {
        Settings.IsRealism = Settings.DefaultIsRealism;
        Settings.IsRealCarResponse = Settings.DefaultIsRealCarResponse;
        Settings.IsBatteryMode = Settings.DefaultIsBatteryMode;
        Settings.HideCarsInColorCamera = Settings.DefaultHideCarsInColorCamera;
        Settings.ShowDotMatrix = Settings.DefaultShowDotMatrix;
        Settings.DepthRes = Settings.DefaultDepthRes;
        Settings.CarDetail = Settings.DefaultCarDetail;
        Settings.Username = Settings.DefaultUsername;
    }

    /// <summary>
    /// Save all settings to PlayerPrefs.
    /// </summary>
    public static void SaveSettings()
    {
        PlayerPrefs.SetInt("IsRealism", System.Convert.ToInt32(Settings.IsRealism));
        PlayerPrefs.SetInt("IsRealCarResponse", System.Convert.ToInt32(Settings.IsRealCarResponse));
        PlayerPrefs.SetInt("IsBatteryMode", System.Convert.ToInt32(Settings.IsBatteryMode));
        PlayerPrefs.SetInt("HideCarsInColorCamera", System.Convert.ToInt32(Settings.HideCarsInColorCamera));
        PlayerPrefs.SetInt("ShowDotMatrix", System.Convert.ToInt32(Settings.ShowDotMatrix));
        PlayerPrefs.SetInt("DepthRes", (int)Settings.DepthRes);
        PlayerPrefs.SetInt("CarDetail", (int)Settings.CarDetail);
        PlayerPrefs.SetString("Username", Settings.Username);
        PlayerPrefs.Save();
    }
    #endregion

    static Settings()
    {
        Settings.LoadSettings();
    }

    /// <summary>
    /// Load all settings from PlayerPrefs.
    /// </summary>
    private static void LoadSettings()
    {
        Settings.IsRealism = System.Convert.ToBoolean(PlayerPrefs.GetInt("IsRealism", System.Convert.ToInt32(Settings.DefaultIsRealism)));
        Settings.IsRealCarResponse = System.Convert.ToBoolean(PlayerPrefs.GetInt("IsRealCarResponse", System.Convert.ToInt32(Settings.DefaultIsRealCarResponse)));
        Settings.IsBatteryMode = System.Convert.ToBoolean(PlayerPrefs.GetInt("IsBatteryMode", System.Convert.ToInt32(Settings.DefaultIsBatteryMode)));
        Settings.HideCarsInColorCamera = System.Convert.ToBoolean(PlayerPrefs.GetInt("HideCarsInColorCamera", System.Convert.ToInt32(Settings.DefaultHideCarsInColorCamera)));
        Settings.ShowDotMatrix = System.Convert.ToBoolean(PlayerPrefs.GetInt("ShowDotMatrix", System.Convert.ToInt32(Settings.DefaultShowDotMatrix)));

        // An out-of-range stored value (edited preferences, older build) falls back to the default
        int depthRes = PlayerPrefs.GetInt("DepthRes", (int)Settings.DefaultDepthRes);
        Settings.DepthRes = System.Enum.IsDefined(typeof(DepthResolution), depthRes) ? (DepthResolution)depthRes : Settings.DefaultDepthRes;
        int carDetail = PlayerPrefs.GetInt("CarDetail", (int)Settings.DefaultCarDetail);
        Settings.CarDetail = System.Enum.IsDefined(typeof(CarDetailLevel), carDetail) ? (CarDetailLevel)carDetail : Settings.DefaultCarDetail;
        Settings.Username = PlayerPrefs.GetString("Username", Settings.DefaultUsername);
    }
}
