using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Rebuilds Prefabs/UI/Hud.prefab as the UI Toolkit HUD: a UIDocument (Hud.uxml on the shared panel
/// settings) and the Hud component; and Prefabs/UI/RaceScreen.prefab the same way (RaceScreen.uxml,
/// RaceTile.uxml). Saving over a prefab keeps its asset id, so LevelManager's hudPrefab and
/// raceScreenPrefab references hold.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod HudSetup.Apply</code>
/// <code>Unity -batchmode -quit -projectPath . -executeMethod HudSetup.ApplyRaceScreen</code>
/// </remarks>
public static class HudSetup
{
    /// <summary>
    /// The HUD prefab LevelManager spawns for a single car.
    /// </summary>
    public const string PrefabPath = "Assets/Prefabs/UI/Hud.prefab";

    private const string layoutPath = "Assets/UI/Hud/Hud.uxml";

    /// <summary>
    /// The screen LevelManager spawns for a race with 2 or more cars.
    /// </summary>
    public const string RaceScreenPrefabPath = "Assets/Prefabs/UI/RaceScreen.prefab";

    private const string raceLayoutPath = "Assets/UI/Hud/RaceScreen.uxml";

    private const string raceTilePath = "Assets/UI/Hud/RaceTile.uxml";

    /// <summary>
    /// Writes the HUD prefab.
    /// </summary>
    public static void Apply()
    {
        GameObject root = new GameObject("Hud");
        try
        {
            UIDocument document = root.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiThemeSetup.PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudSetup.layoutPath);
            Hud hud = root.AddComponent<Hud>();
            SerializedObject serialized = new SerializedObject(hud);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudSetup.PrefabPath);
            Debug.Log($"HudSetup: {HudSetup.PrefabPath} written");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Writes the race screen prefab.
    /// </summary>
    public static void ApplyRaceScreen()
    {
        GameObject root = new GameObject("RaceScreen");
        try
        {
            UIDocument document = root.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiThemeSetup.PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudSetup.raceLayoutPath);
            RaceScreen screen = root.AddComponent<RaceScreen>();
            SerializedObject serialized = new SerializedObject(screen);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("tileTemplate").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudSetup.raceTilePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudSetup.RaceScreenPrefabPath);
            Debug.Log($"HudSetup: {HudSetup.RaceScreenPrefabPath} written");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
