using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// JSON save format and best-time lookup by level Id.
/// </summary>
public class SavedDataTests
{
    private static LevelInfo Level(string id, int numCheckpoints)
    {
        return new LevelInfo() { DisplayName = id, Id = id, IsRaceable = true, NumCheckpoints = numCheckpoints };
    }

    [Test]
    public void GetBestTimes_CreatesEmptyEntryOnce()
    {
        SavedData data = SavedData.Default;
        LevelInfo level = SavedDataTests.Level("grand-prix-2026", 4);

        BestTimeInfo first = data.GetBestTimes(level);
        BestTimeInfo second = data.GetBestTimes(level);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, data.BestTimes.Count);
        Assert.AreEqual("grand-prix-2026", first.LevelId);
        Assert.AreEqual(float.MaxValue, first.OverallTime);
        Assert.AreEqual(5, first.CheckpointTimes.Length);
    }

    [Test]
    public void GetBestTimes_ResetsEntryWhenCheckpointCountChanges()
    {
        SavedData data = SavedData.Default;
        data.GetBestTimes(SavedDataTests.Level("greece-time-trial-2025", 0)).OverallTime = 42;

        BestTimeInfo updated = data.GetBestTimes(SavedDataTests.Level("greece-time-trial-2025", 3));

        Assert.AreEqual(4, updated.CheckpointTimes.Length);
        Assert.AreEqual(float.MaxValue, updated.OverallTime);
        Assert.AreEqual(1, data.BestTimes.Count);
    }

    [Test]
    public void GetBestTimes_KeysByIdNotOrder()
    {
        SavedData data = SavedData.Default;
        data.GetBestTimes(SavedDataTests.Level("lab-5-ar-tag-decisions", 2)).OverallTime = 30;
        data.GetBestTimes(SavedDataTests.Level("grand-prix-2026", 4)).OverallTime = 90;

        data.BestTimes.Reverse();

        Assert.AreEqual(30, data.GetBestTimes(SavedDataTests.Level("lab-5-ar-tag-decisions", 2)).OverallTime);
        Assert.AreEqual(90, data.GetBestTimes(SavedDataTests.Level("grand-prix-2026", 4)).OverallTime);
    }

    [Test]
    public void Json_RoundTripsAllFields()
    {
        SavedData data = SavedData.Default;
        data.WasUsernameDialogShown = true;
        BestTimeInfo times = data.GetBestTimes(SavedDataTests.Level("grand-prix-2026", 2));
        times.OverallTime = 95.25f;
        times.CheckpointTimes[0] = 12.5f;
        data.CarCustomizations[1].ShellColor = new SerializableColor(0.1f, 0.2f, 0.3f);
        data.CarCustomizations[1].IsShellShiny = true;

        SavedData loaded = SavedDataManager.FromJson(SavedDataManager.ToJson(data));

        Assert.AreEqual(SavedData.CurrentVersion, loaded.Version);
        Assert.IsTrue(loaded.WasUsernameDialogShown);
        BestTimeInfo loadedTimes = loaded.GetBestTimes(SavedDataTests.Level("grand-prix-2026", 2));
        Assert.AreEqual(95.25f, loadedTimes.OverallTime);
        Assert.AreEqual(12.5f, loadedTimes.CheckpointTimes[0]);
        Assert.AreEqual(float.MaxValue, loadedTimes.CheckpointTimes[2]);
        Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f), loaded.CarCustomizations[1].ShellColor.Color);
        Assert.IsTrue(loaded.CarCustomizations[1].IsShellShiny);
    }

    [Test]
    public void FromJson_Version2ColorsResetToShellDefaults()
    {
        SavedData loaded = SavedDataManager.FromJson(
            "{\"Version\": 2, \"WasUsernameDialogShown\": true, \"CarCustomizations\": [" +
            "{\"FrontColor\": {\"r\": 1, \"g\": 1, \"b\": 1}, \"IsFrontShiny\": true, \"BackColor\": {\"r\": 1, \"g\": 0, \"b\": 0}}," +
            "{\"FrontColor\": {\"r\": 1, \"g\": 0, \"b\": 0}}, {\"FrontColor\": {\"r\": 0, \"g\": 0, \"b\": 1}}, {\"FrontColor\": {\"r\": 1, \"g\": 1, \"b\": 0}}]}");

        Assert.AreEqual(SavedData.CurrentVersion, loaded.Version);
        Assert.IsTrue(loaded.WasUsernameDialogShown);
        for (int i = 0; i < loaded.CarCustomizations.Length; i++)
        {
            Assert.AreEqual(SavedData.Default.CarCustomizations[i].ShellColor.Color, loaded.CarCustomizations[i].ShellColor.Color, $"car {i}");
            Assert.IsFalse(loaded.CarCustomizations[i].IsShellShiny);
        }
        Assert.AreEqual(CarCustomization.DefaultShellColor, loaded.CarCustomizations[0].ShellColor.Color);
    }

    [Test]
    public void FromJson_FillsMissingFieldsWithDefaults()
    {
        SavedData loaded = SavedDataManager.FromJson("{\"WasUsernameDialogShown\": true}");

        Assert.IsTrue(loaded.WasUsernameDialogShown);
        Assert.IsNotNull(loaded.BestTimes);
        Assert.AreEqual(SavedData.Default.CarCustomizations.Length, loaded.CarCustomizations.Length);
    }

    [Test]
    public void FromJson_DropsEntriesWithoutLevelId()
    {
        SavedData loaded = SavedDataManager.FromJson(
            "{\"BestTimes\": [{\"LevelId\": \"\", \"OverallTime\": 1}, {\"LevelId\": \"grand-prix-2026\", \"OverallTime\": 2, \"CheckpointTimes\": [1, 2, 3, 4, 5]}]}");

        Assert.AreEqual(1, loaded.BestTimes.Count);
        Assert.AreEqual("grand-prix-2026", loaded.BestTimes[0].LevelId);
    }

    [TestCase("")]
    [TestCase("not json")]
    public void FromJson_RejectsInvalidText(string json)
    {
        Assert.Throws(Is.InstanceOf<Exception>(), () => SavedDataManager.FromJson(json));
    }
}
