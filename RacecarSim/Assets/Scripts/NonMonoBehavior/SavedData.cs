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
    public const int CurrentVersion = 3;

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
    /// True once the username reminder was shown. Kept in the saved file; the menu now shows the
    /// reminder on every Autograder start while the username is the default (LevelLaunch).
    /// </summary>
    public bool WasUsernameDialogShown;

    /// <summary>
    /// Cars connected to with Connect car, newest first, at most MaxRecentCars.
    /// </summary>
    public List<CarLinkTarget> RecentCars = new List<CarLinkTarget>();

    /// <summary>
    /// The number of recent cars kept.
    /// </summary>
    public const int MaxRecentCars = 5;

    /// <summary>
    /// Moves a car to the front of the recent list, replacing an entry with the same address.
    /// </summary>
    public void RememberCar(CarLinkTarget target)
    {
        this.RecentCars.RemoveAll(entry => entry == null || entry.SameAddress(target));
        this.RecentCars.Insert(0, target);
        if (this.RecentCars.Count > SavedData.MaxRecentCars)
        {
            this.RecentCars.RemoveRange(SavedData.MaxRecentCars, this.RecentCars.Count - SavedData.MaxRecentCars);
        }
    }

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
        // Car 1 has the physical car's shell color; the others avoid red and yellow so the R logo
        // and the beaver stay visible
        this.CarCustomizations = new CarCustomization[]
        {
            new CarCustomization(CarCustomization.DefaultShellColor),
            new CarCustomization(new Color(0.12f, 0.35f, 0.85f)),
            new CarCustomization(new Color(0.1f, 0.6f, 0.25f)),
            new CarCustomization(new Color(0.45f, 0.2f, 0.75f))
        };
    }
}
