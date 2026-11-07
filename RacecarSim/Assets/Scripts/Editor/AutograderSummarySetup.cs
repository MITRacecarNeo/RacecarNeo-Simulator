using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Rebuilds AutograderSummary.unity on UI Toolkit: removes every root object except the camera,
/// adds a UIDocument (AutograderSummary.uxml on the shared panel settings) with the
/// AutograderSummary component, and sets the camera to the menu's stage color.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod AutograderSummarySetup.Apply</code>
/// </remarks>
public static class AutograderSummarySetup
{
    private const string scenePath = "Assets/Scenes/AutograderSummary.unity";
    private const string layoutPath = "Assets/UI/Autograder/AutograderSummary.uxml";

    /// <summary>
    /// Rebuilds and saves the scene.
    /// </summary>
    public static void Apply()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(AutograderSummarySetup.scenePath, OpenSceneMode.Single);
        Camera camera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out Camera found))
            {
                camera = found;
                continue;
            }
            Debug.Log($"AutograderSummarySetup: removed {root.name}");
            Object.DestroyImmediate(root);
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        ColorUtility.TryParseHtmlString(ThemeTokens.Roles["code-bg"], out Color stage);
        camera.backgroundColor = stage;

        GameObject summary = new GameObject("AutograderSummary");
        UIDocument document = summary.AddComponent<UIDocument>();
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiThemeSetup.PanelSettingsPath);
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AutograderSummarySetup.layoutPath);
        AutograderSummary component = summary.AddComponent<AutograderSummary>();
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("document").objectReferenceValue = document;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"AutograderSummarySetup: {AutograderSummarySetup.scenePath} written");
    }
}
