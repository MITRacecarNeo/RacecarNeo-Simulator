using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Guards the serialized references of the UI scripts (for example Hud.modeText,
/// SettingsUI.colorSliders). The test records the object each reference points to and fails when
/// a reference is missing or moves, so a hierarchy edit cannot silently rewire the UI.
/// </summary>
public class UiLayoutTests
{
    /// <summary>
    /// Snapshot of UI references. Regenerate with WriteUiLayout only after checking that each
    /// changed reference points at the intended object.
    /// </summary>
    public const string SnapshotPath = "Assets/Tests/Editor/UiLayout.txt";

    private static readonly string[] prefabs =
    {
        "Assets/Prefabs/UI/Hud.prefab", "Assets/Prefabs/UI/RaceScreen.prefab",
        "Assets/Prefabs/UI/AutograderLevelEntry.prefab", "Assets/Prefabs/UI/BestTimeEntry.prefab"
    };

    private static readonly string[] scenes = { "Assets/Scenes/Main.unity", "Assets/Scenes/AutograderSummary.unity" };

    private static readonly Type[] scriptTypes =
    {
        typeof(ScreenManager), typeof(SettingsUI), typeof(BestTimesUI), typeof(MainMenu), typeof(AutograderSummary),
        typeof(NoUsernameUI), typeof(ControlsUI), typeof(AutograderUIEntry), typeof(BestTimeUIEntry)
    };

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
    public void UiReferences_MatchSnapshot()
    {
        Assume.That(!UiLayoutTests.HasDirtyScene(), "Open scenes have unsaved changes.");
        Assert.IsTrue(File.Exists(UiLayoutTests.SnapshotPath), $"{UiLayoutTests.SnapshotPath} missing; run UiLayoutTests.WriteUiLayout.");

        string[] expected = File.ReadAllLines(UiLayoutTests.SnapshotPath);
        string[] actual = UiLayoutTests.BuildSnapshot();
        IEnumerable<string> missing = expected.Except(actual);
        IEnumerable<string> added = actual.Except(expected);

        Assert.IsTrue(expected.SequenceEqual(actual),
            "UI references changed.\nExpected, not found:\n" + string.Join("\n", missing) + "\nFound, not expected:\n" + string.Join("\n", added));
    }

    [Test]
    public void UiReferences_AreAssigned()
    {
        Assume.That(!UiLayoutTests.HasDirtyScene(), "Open scenes have unsaved changes.");

        IEnumerable<string> unassigned = UiLayoutTests.BuildSnapshot().Where(line => line.EndsWith("|null", StringComparison.Ordinal));
        Assert.IsEmpty(unassigned, "Unassigned UI references.");
    }

    /// <summary>
    /// Writes the current UI references to SnapshotPath. Batch mode:
    /// -executeMethod UiLayoutTests.WriteUiLayout
    /// </summary>
    public static void WriteUiLayout()
    {
        File.WriteAllText(UiLayoutTests.SnapshotPath, string.Join("\n", UiLayoutTests.BuildSnapshot()) + "\n");
        AssetDatabase.ImportAsset(UiLayoutTests.SnapshotPath);
    }

    private static string[] BuildSnapshot()
    {
        List<string> lines = new List<string>();

        foreach (string path in UiLayoutTests.prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                lines.AddRange(UiLayoutTests.Describe(path, new[] { root }));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (string path in UiLayoutTests.scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            lines.AddRange(UiLayoutTests.Describe(path, scene.GetRootGameObjects()));
        }
        return lines.ToArray();
    }

    /// <summary>
    /// Lists every object reference serialized on each UI script under the roots: the target's
    /// path from the script, "asset:" and its asset path, or "null".
    /// </summary>
    private static IEnumerable<string> Describe(string source, GameObject[] roots)
    {
        foreach (Type scriptType in UiLayoutTests.scriptTypes)
        {
            foreach (Component script in roots.SelectMany(root => root.GetComponentsInChildren(scriptType, true)).Where(script => script.GetType() == scriptType || scriptType == typeof(ScreenManager)).Distinct())
            {
                SerializedProperty property = new SerializedObject(script).GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = property.propertyType != SerializedPropertyType.String;
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script")
                    {
                        continue;
                    }
                    yield return $"{source}|{script.GetType().Name}:{script.name}|{property.propertyPath}|{UiLayoutTests.Target(script.transform, property.objectReferenceValue)}";
                }
            }
        }
    }

    private static string Target(Transform root, UnityEngine.Object value)
    {
        if (value == null)
        {
            return "null";
        }
        if (EditorUtility.IsPersistent(value))
        {
            return "asset:" + AssetDatabase.GetAssetPath(value);
        }
        Transform target = value is Component component ? component.transform : ((GameObject)value).transform;
        return UiLayoutTests.PathFrom(root, target);
    }

    private static string PathFrom(Transform root, Transform target)
    {
        List<string> names = new List<string>();
        for (Transform t = target; t != null && t != root; t = t.parent)
        {
            names.Add(t.name);
        }
        names.Reverse();
        return names.Count == 0 ? "." : string.Join("/", names);
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
