using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Creates or updates the shared UI Toolkit assets: two panel settings on the RacecarSim theme,
/// scaled from a 1920 x 1080 reference so layouts keep their proportions at any resolution, and
/// the scene change overlay prefab that SceneFlow loads from Resources.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod UiThemeSetup.Apply</code>
/// </remarks>
public static class UiThemeSetup
{
    /// <summary>
    /// Panel settings for screens and the HUD.
    /// </summary>
    public const string PanelSettingsPath = "Assets/UI/RacecarSimPanel.asset";

    /// <summary>
    /// Panel settings for overlays drawn above every other panel and canvas (scene changes).
    /// </summary>
    public const string OverlayPanelSettingsPath = "Assets/UI/RacecarSimOverlayPanel.asset";

    /// <summary>
    /// Theme style sheet: the Unity default runtime theme, then Tokens.uss and Components.uss.
    /// </summary>
    public const string ThemePath = "Assets/UI/Theme/RacecarSim.tss";

    /// <summary>
    /// Scene change overlay prefab, loaded by SceneFlow with Resources.Load.
    /// </summary>
    public const string SceneFlowPrefabPath = "Assets/Resources/SceneFlowOverlay.prefab";

    /// <summary>
    /// Scene change overlay layout.
    /// </summary>
    public const string SceneFlowLayoutPath = "Assets/UI/SceneFlow/SceneFlowOverlay.uxml";

    /// <summary>
    /// Reference resolution for panel scaling.
    /// </summary>
    public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

    /// <summary>
    /// Sort order of the overlay panel; above uGUI canvases (all at sorting order 0 in this project).
    /// </summary>
    private const int overlaySortingOrder = 1000;

    /// <summary>
    /// Writes the panel settings and the overlay prefab, creating them on first run.
    /// </summary>
    public static void Apply()
    {
        ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiThemeSetup.ThemePath);
        if (theme == null)
        {
            throw new System.InvalidOperationException($"No theme style sheet at {UiThemeSetup.ThemePath}");
        }

        UiThemeSetup.WritePanelSettings(UiThemeSetup.PanelSettingsPath, theme, 0);
        PanelSettings overlay = UiThemeSetup.WritePanelSettings(UiThemeSetup.OverlayPanelSettingsPath, theme, UiThemeSetup.overlaySortingOrder);
        UiThemeSetup.WriteSceneFlowPrefab(overlay);
        AssetDatabase.SaveAssets();
    }

    private static PanelSettings WritePanelSettings(string path, ThemeStyleSheet theme, int sortingOrder)
    {
        PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        bool created = settings == null;
        if (created)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
        }

        settings.themeStyleSheet = theme;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = UiThemeSetup.ReferenceResolution;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        // Height match: wider screens (16:10, 21:9) gain room at the sides, where the HUD frames the view
        settings.match = 1;
        settings.clearColor = false;
        settings.sortingOrder = sortingOrder;

        if (created)
        {
            AssetDatabase.CreateAsset(settings, path);
        }
        else
        {
            EditorUtility.SetDirty(settings);
        }
        Debug.Log($"UiThemeSetup: panel settings {(created ? "created" : "updated")} at {path}");
        return settings;
    }

    private static void WriteSceneFlowPrefab(PanelSettings overlay)
    {
        VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiThemeSetup.SceneFlowLayoutPath);
        if (layout == null)
        {
            throw new System.InvalidOperationException($"No overlay layout at {UiThemeSetup.SceneFlowLayoutPath}");
        }

        GameObject root = new GameObject("SceneFlowOverlay");
        try
        {
            UIDocument document = root.AddComponent<UIDocument>();
            document.panelSettings = overlay;
            document.visualTreeAsset = layout;
            SceneFlow flow = root.AddComponent<SceneFlow>();
            SerializedObject serialized = new SerializedObject(flow);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(UiThemeSetup.SceneFlowPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, UiThemeSetup.SceneFlowPrefabPath);
            Debug.Log($"UiThemeSetup: overlay prefab written to {UiThemeSetup.SceneFlowPrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
