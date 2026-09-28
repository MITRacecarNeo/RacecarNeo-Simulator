using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Data checks for LevelCollection against ProjectSettings/EditorBuildSettings.asset and the
/// level scenes.
/// </summary>
public class LevelCollectionTests
{
    /// <summary>
    /// Snapshot of every level-to-scene mapping. Regenerate with WriteSceneMap only after an
    /// intended build-list change.
    /// </summary>
    public const string SceneMapPath = "Assets/Tests/Editor/LevelSceneMap.txt";

    private SceneSetup[] savedSetup;

    [OneTimeSetUp]
    public void SaveSceneSetup()
    {
        this.savedSetup = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void RestoreSceneSetup()
    {
        if (this.savedSetup != null && this.savedSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(this.savedSetup);
        }
    }

    [Test]
    public void BuildIndices_AreWithinEnabledScenes()
    {
        int count = LevelCollectionTests.EnabledScenes().Length;
        List<string> failures = new List<string>();

        foreach ((string label, int index) in LevelCollectionTests.AllBuildIndices())
        {
            if (index < 0 || index >= count)
            {
                failures.Add($"{label}: {index}");
            }
        }

        Assert.IsEmpty(failures, $"Build indices outside 0..{count - 1}:\n" + string.Join("\n", failures));
    }

    [Test]
    public void SystemScenes_AreAtTheirFixedPlaces()
    {
        string[] scenes = LevelCollectionTests.EnabledScenes();

        Assert.AreEqual("Assets/Scenes/Main.unity", scenes[LevelCollection.MainMenuBuildIndex]);
        Assert.AreEqual("Assets/Scenes/ReloadBuffer.unity", scenes[ReloadBuffer.BuildIndex]);
        Assert.IsTrue(scenes.Any(scene => Path.GetFileNameWithoutExtension(scene) == AutograderManager.AutograderSummaryScene),
            $"{AutograderManager.AutograderSummaryScene} is not an enabled build scene.");
    }

    [Test]
    public void BuildIndices_MatchSceneMap()
    {
        Assert.IsTrue(File.Exists(LevelCollectionTests.SceneMapPath), $"{LevelCollectionTests.SceneMapPath} missing; run LevelCollectionTests.WriteSceneMap.");

        string[] expected = File.ReadAllLines(LevelCollectionTests.SceneMapPath);
        string[] actual = LevelCollectionTests.BuildSceneMap();
        IEnumerable<string> missing = expected.Except(actual);
        IEnumerable<string> added = actual.Except(expected);

        Assert.IsTrue(!missing.Any() && !added.Any(),
            "Level-to-scene mapping changed.\nExpected, not found:\n" + string.Join("\n", missing) +
            "\nFound, not expected:\n" + string.Join("\n", added));
    }

    [Test]
    public void AutograderLevelCodes_AreUnique()
    {
        IEnumerable<string> duplicates = LevelCollectionTests.AllLevels()
            .Where(level => level.AutograderLevels != null && level.AutograderLevels.Length > 0)
            .GroupBy(level => level.AutograderLevelCode)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: " + string.Join(", ", group.Select(level => level.DisplayName)));

        Assert.IsEmpty(duplicates, "Duplicate autograder level codes.");
    }

    [Test]
    public void AutograderMode_OnlyForLevelsWithAnAutograderScene()
    {
        IEnumerable<string> invalid = LevelCollectionTests.AllLevels()
            .Where(level => level.SupportedModes.Contains(LevelManagerMode.Autograder) && level.AutograderBuildIndex <= ReloadBuffer.BuildIndex)
            .Select(level => $"{level.DisplayName}: AutograderBuildIndex {level.AutograderBuildIndex}");
        Assert.IsEmpty(invalid, "Autograder mode would load a system scene.");

        LevelInfo raceOnly = LevelCollectionTests.AllLevels().First(level => level.Id == "grand-prix-2026");
        CollectionAssert.AreEqual(new[] { LevelManagerMode.Exploration, LevelManagerMode.Race }, raceOnly.SupportedModes);
    }

    [Test]
    public void RaceableLevels_HaveUniqueSlugIds()
    {
        List<LevelInfo> raceable = LevelCollectionTests.AllLevels().Where(level => level.IsRaceable).ToList();

        IEnumerable<string> invalid = raceable
            .Where(level => string.IsNullOrEmpty(level.Id) || !Regex.IsMatch(level.Id, "^[a-z0-9]+(-[a-z0-9]+)*$"))
            .Select(level => $"{level.DisplayName}: '{level.Id}'");
        Assert.IsEmpty(invalid, "Raceable levels need an Id of lowercase letters, digits, and hyphens.");

        IEnumerable<string> duplicates = LevelCollectionTests.AllLevels()
            .Where(level => !string.IsNullOrEmpty(level.Id))
            .GroupBy(level => level.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        Assert.IsEmpty(duplicates, "Duplicate level Ids.");
    }

    [Test]
    public void AutograderTimeBonuses_AreSortedAndEndAtInfinity()
    {
        List<string> failures = new List<string>();
        foreach (LevelInfo level in LevelCollectionTests.AllLevels().Where(level => level.AutograderLevels != null))
        {
            foreach (AutograderLevelInfo autograderLevel in level.AutograderLevels.Where(info => info.TimeBonuses != null))
            {
                Vector2[] bonuses = autograderLevel.TimeBonuses;
                bool sorted = bonuses.Zip(bonuses.Skip(1), (a, b) => a.x < b.x).All(x => x);
                if (bonuses.Length == 0 || !sorted || !float.IsPositiveInfinity(bonuses[bonuses.Length - 1].x))
                {
                    failures.Add($"{level.DisplayName} / {autograderLevel.Title}");
                }
            }
        }

        Assert.IsEmpty(failures, "Time bonuses must be sorted by time and end with +Infinity:\n" + string.Join("\n", failures));
    }

    [Test]
    public void NumCheckpoints_MatchesSceneKeyPoints()
    {
        Assume.That(!LevelCollectionTests.HasDirtyScene(), "Open scenes have unsaved changes.");

        string[] scenes = LevelCollectionTests.EnabledScenes();
        List<string> failures = new List<string>();

        foreach (LevelInfo level in LevelCollectionTests.AllLevels().Where(level => level.IsRaceable))
        {
            Scene scene = EditorSceneManager.OpenScene(scenes[level.BuildIndex], OpenSceneMode.Single);
            int found = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<KeyPoint>(false))
                .Count(keyPoint => keyPoint.Type == KeyPoint.KeyPointType.Checkpoint);

            if (found != level.NumCheckpoints)
            {
                failures.Add($"{level.DisplayName} ({scenes[level.BuildIndex]}): NumCheckpoints {level.NumCheckpoints}, scene {found}");
            }
        }

        Assert.IsEmpty(failures, "NumCheckpoints mismatches:\n" + string.Join("\n", failures));
    }

    [Test]
    public void Checkpoints_HaveContiguousUniqueIndices()
    {
        Assume.That(!LevelCollectionTests.HasDirtyScene(), "Open scenes have unsaved changes.");

        string[] scenes = LevelCollectionTests.EnabledScenes();
        List<string> failures = new List<string>();

        foreach (LevelInfo level in LevelCollectionTests.AllLevels().Where(level => level.IsRaceable))
        {
            Scene scene = EditorSceneManager.OpenScene(scenes[level.BuildIndex], OpenSceneMode.Single);
            int[] indices = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<KeyPoint>(false))
                .Where(keyPoint => keyPoint.Type == KeyPoint.KeyPointType.Checkpoint)
                .Select(keyPoint => new SerializedObject(keyPoint).FindProperty("checkpointIndex").intValue)
                .OrderBy(index => index)
                .ToArray();

            if (!indices.SequenceEqual(Enumerable.Range(0, indices.Length)))
            {
                failures.Add($"{level.DisplayName}: [{string.Join(", ", indices)}]");
            }
        }

        Assert.IsEmpty(failures, "Checkpoint indices must be 0..n-1 with no gaps or duplicates:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Writes the current level-to-scene mapping to SceneMapPath. Batch mode:
    /// -executeMethod LevelCollectionTests.WriteSceneMap
    /// </summary>
    public static void WriteSceneMap()
    {
        File.WriteAllText(LevelCollectionTests.SceneMapPath, string.Join("\n", LevelCollectionTests.BuildSceneMap()) + "\n");
        AssetDatabase.ImportAsset(LevelCollectionTests.SceneMapPath);
    }

    private static string[] BuildSceneMap()
    {
        string[] scenes = LevelCollectionTests.EnabledScenes();
        return LevelCollectionTests.AllBuildIndices()
            .Select(entry => $"{entry.label}\t{entry.index}\t{(entry.index >= 0 && entry.index < scenes.Length ? scenes[entry.index] : "<out of range>")}")
            .ToArray();
    }

    private static IEnumerable<(string label, int index)> AllBuildIndices()
    {
        foreach (LevelCollection collection in LevelCollection.LevelCollections)
        {
            foreach (LevelInfo level in collection.Levels)
            {
                string label = $"{collection.ShortName}/{level.DisplayName}";
                yield return (label, level.BuildIndex);

                if (level.AutograderLevels != null)
                {
                    for (int i = 0; i < level.AutograderLevels.Length; i++)
                    {
                        yield return ($"{label} [autograder {i + 1}]", level.AutograderBuildIndex + i);
                    }
                }

                if (level.HasRandomMaps && level.RandomSceneBuildIndices != null)
                {
                    foreach (int index in level.RandomSceneBuildIndices)
                    {
                        yield return ($"{label} [random]", index);
                    }
                }
            }
        }
    }

    private static IEnumerable<LevelInfo> AllLevels()
    {
        return LevelCollection.LevelCollections.SelectMany(collection => collection.Levels);
    }

    private static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
    }

    private static bool HasDirtyScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                return true;
            }
        }
        return false;
    }
}
