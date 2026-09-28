using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages loading, saving, and accessing saved game data.
/// </summary>
public static class SavedDataManager
{
    #region Constants
    /// <summary>
    /// The path of the file containing the saved game data.
    /// </summary>
    private static readonly string saveFilePath = Path.Combine(SavedDataManager.DataDirectory, "GameData.json");

    /// <summary>
    /// The save file written by versions before the JSON format. It is never read; its presence
    /// without a JSON save means the player's earlier best times were reset.
    /// </summary>
    private static readonly string legacySaveFilePath = Path.Combine(SavedDataManager.DataDirectory, "GameData.dat");
    #endregion

    /// <summary>
    /// The folder holding saved data: Application.persistentDataPath, or -racecarsim-data-dir.
    /// </summary>
    private static string DataDirectory
    {
        get
        {
            string directory = LaunchOptions.Current.DataDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                return Application.persistentDataPath;
            }
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    /// <summary>
    /// The current game data loaded in memory.
    /// </summary>
    public static SavedData Data { get; private set; } = null;

    /// <summary>
    /// True when this session replaced a save from an earlier format, resetting best times.
    /// The main menu shows a notice once when this is set.
    /// </summary>
    public static bool WasLegacyDataReset { get; set; } = false;

    /// <summary>
    /// Saves the current game data to disk.
    /// </summary>
    public static void Save()
    {
        if (SavedDataManager.Data == null)
        {
            Debug.LogError("Attempted to save data before any data was loaded.");
            return;
        }

        string tempPath = SavedDataManager.saveFilePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, SavedDataManager.ToJson(SavedDataManager.Data));
            if (File.Exists(SavedDataManager.saveFilePath))
            {
                File.Replace(tempPath, SavedDataManager.saveFilePath, null);
            }
            else
            {
                File.Move(tempPath, SavedDataManager.saveFilePath);
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogError($"Unable to save data to [{SavedDataManager.saveFilePath}]. Exception: [{e}]");
        }
    }

    /// <summary>
    /// Serializes saved data to JSON.
    /// </summary>
    /// <param name="data">The data to serialize.</param>
    /// <returns>The JSON text.</returns>
    public static string ToJson(SavedData data)
    {
        return JsonUtility.ToJson(data, true);
    }

    /// <summary>
    /// Parses saved data from JSON, filling fields missing from older or damaged files with defaults.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The parsed data.</returns>
    /// <exception cref="ArgumentException">The text is not valid JSON.</exception>
    public static SavedData FromJson(string json)
    {
        SavedData data = JsonUtility.FromJson<SavedData>(json);
        if (data == null)
        {
            throw new ArgumentException("Saved data is empty.");
        }

        if (data.BestTimes == null)
        {
            data.ClearBestTimes();
        }
        data.BestTimes.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.LevelId));

        if (data.CarCustomizations == null || data.CarCustomizations.Length < SavedData.Default.CarCustomizations.Length)
        {
            data.ClearCustomization();
        }
        return data;
    }

    /// <summary>
    /// Loads the saved game data on disk into memory, or creates default data if no saved data is found.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by Unity")]
    private static void Load()
    {
        if (!File.Exists(SavedDataManager.saveFilePath))
        {
            SavedDataManager.Data = SavedData.Default;
            if (File.Exists(SavedDataManager.legacySaveFilePath))
            {
                SavedDataManager.WasLegacyDataReset = true;
                SavedDataManager.Save();
            }
            return;
        }

        try
        {
            SavedDataManager.Data = SavedDataManager.FromJson(File.ReadAllText(SavedDataManager.saveFilePath));
        }
        catch (Exception e)
        {
            Debug.LogError($"Unable to load saved data, so using default data instead. Exception: [{e}]");
            SavedDataManager.Data = SavedData.Default;
            try
            {
                File.Copy(SavedDataManager.saveFilePath, SavedDataManager.saveFilePath + ".corrupt", true);
            }
            catch (Exception copyException) when (copyException is IOException || copyException is UnauthorizedAccessException)
            {
                Debug.LogError($"Unable to back up unreadable saved data. Exception: [{copyException}]");
            }
        }
    }
}
