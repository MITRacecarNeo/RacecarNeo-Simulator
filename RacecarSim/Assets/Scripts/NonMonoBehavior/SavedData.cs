using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The data which is serialized to disk to persist after the program is closed.
/// </summary>
[Serializable]
public class SavedData
{
    /// <summary>
    /// The save format version written by this build.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// The save format version of this data.
    /// </summary>
    public int Version = SavedData.CurrentVersion;

    /// <summary>
    /// The best time information for levels that have been played, keyed by BestTimeInfo.LevelId.
    /// </summary>
    public List<BestTimeInfo> BestTimes = new List<BestTimeInfo>();

    /// <summary>
    /// The customization for each car, indexed by car.
    /// </summary>
    public CarCustomization[] CarCustomizations;

    /// <summary>
    /// True if the dialog was shown prompting the user to select a username.
    /// </summary>
    public bool WasUsernameDialogShown;

    /// <summary>
    /// Returns the default data, which should be used when no saved data can be found.
    /// </summary>
    public static SavedData Default
    {
        get
        {
            SavedData data = new SavedData()
            {
                WasUsernameDialogShown = false
            };

            data.ClearBestTimes();
            data.ClearCustomization();
            return data;
        }
    }

    /// <summary>
    /// Returns the best times for a level, creating an empty entry when the level has none or
    /// when its checkpoint count changed.
    /// </summary>
    /// <param name="level">A raceable level with a non-empty Id.</param>
    /// <returns>The level's best time information, stored in BestTimes.</returns>
    public BestTimeInfo GetBestTimes(LevelInfo level)
    {
        int index = this.BestTimes.FindIndex(entry => entry.LevelId == level.Id);
        BestTimeInfo fresh = new BestTimeInfo(level.Id, level.NumCheckpoints);

        if (index < 0)
        {
            this.BestTimes.Add(fresh);
            return fresh;
        }

        BestTimeInfo existing = this.BestTimes[index];
        if (existing.CheckpointTimes == null || existing.CheckpointTimes.Length != fresh.CheckpointTimes.Length)
        {
            this.BestTimes[index] = fresh;
            return fresh;
        }
        return existing;
    }

    /// <summary>
    /// Reset all best times to store no progress toward any levels.
    /// </summary>
    public void ClearBestTimes()
    {
        this.BestTimes = new List<BestTimeInfo>();
    }

    /// <summary>
    /// Resets the car customizations to the default colors.
    /// </summary>
    public void ClearCustomization()
    {
        this.CarCustomizations = new CarCustomization[]
        {
            new CarCustomization(Color.white, Color.red),
            new CarCustomization(Color.red),
            new CarCustomization(Color.blue),
            new CarCustomization(Color.yellow)
        };
    }
}
