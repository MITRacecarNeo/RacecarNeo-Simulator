using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds CarWorld.unity, the scene for a connected physical car and for captured worlds: a copy
/// of Demo.unity reduced to its terrain and sun, plus the CarWorld component with its status
/// panel and an inactive LevelManager. Bakes the scene's own
/// environment lighting (no lightmaps, no bake on load) and appends the scene to the build list,
/// after every level, so no build index moves.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod CarWorldSetup.Apply</code>
/// </remarks>
public static class CarWorldSetup
{
    private const string sourceScenePath = "Assets/Scenes/Demo.unity";
    private const string sourceLightingPath = "Assets/Scenes/DemoSettings.lighting";
    private const string scenePath = "Assets/Scenes/CarWorld.unity";
    private const string lightingPath = "Assets/Scenes/CarWorldSettings.lighting";
    private const string layoutPath = "Assets/UI/CarLink/CarLinkHud.uxml";
    private const string playerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string levelManagerPrefabPath = "Assets/Prefabs/LevelManager.prefab";
    private const string materialFolder = "Assets/Materials/CarLink";
    private static readonly string[] keptPrefabs = { "Assets/Prefabs/Terrain.prefab", "Assets/Prefabs/Sun.prefab" };

    /// <summary>
    /// Builds, bakes, saves, and lists the scene.
    /// </summary>
    public static void Apply()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CarWorldSetup.scenePath) == null)
        {
            AssetDatabase.CopyAsset(CarWorldSetup.sourceScenePath, CarWorldSetup.scenePath);
        }
        if (AssetDatabase.LoadAssetAtPath<LightingSettings>(CarWorldSetup.lightingPath) == null)
        {
            AssetDatabase.CopyAsset(CarWorldSetup.sourceLightingPath, CarWorldSetup.lightingPath);
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(CarWorldSetup.scenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (CarWorldSetup.keptPrefabs.Contains(prefab))
            {
                continue;
            }
            Debug.Log($"CarWorldSetup: removed {root.name}");
            Object.DestroyImmediate(root);
        }

        GameObject world = new GameObject("CarWorld");
        UIDocument document = world.AddComponent<UIDocument>();
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiThemeSetup.PanelSettingsPath);
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CarWorldSetup.layoutPath);
        CarWorld component = world.AddComponent<CarWorld>();
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("document").objectReferenceValue = document;
        serialized.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CarWorldSetup.playerPrefabPath);
        // Inactive until a captured world is driven; then it spawns the car and runs the level
        GameObject levelManager = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CarWorldSetup.levelManagerPrefabPath), scene);
        levelManager.SetActive(false);
        serialized.FindProperty("levelManager").objectReferenceValue = levelManager;
        serialized.FindProperty("wallMaterial").objectReferenceValue = CarWorldSetup.WriteMaterial("CarWorldWall", "Standard", new Color32(120, 125, 140, 255));
        serialized.FindProperty("floorMaterial").objectReferenceValue = CarWorldSetup.WriteMaterial("CarWorldFloor", "Unlit/Transparent", Color.white);
        serialized.FindProperty("scanMaterial").objectReferenceValue = CarWorldSetup.WriteMaterial("CarWorldScan", "Unlit/Color", new Color32(244, 154, 75, 255));
        serialized.FindProperty("trailMaterial").objectReferenceValue = CarWorldSetup.WriteMaterial("CarWorldTrail", "Unlit/Color", new Color32(25, 104, 108, 255));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(CarWorldSetup.lightingPath);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Bake once; the lighting does not depend on the CarWorld object
        if (Lightmapping.lightingDataAsset == null || AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset).StartsWith("Assets/Scenes/Demo/"))
        {
            if (!Lightmapping.Bake())
            {
                Debug.LogError("CarWorldSetup: lighting bake failed");
            }
            EditorSceneManager.SaveScene(scene);
        }

        if (!EditorBuildSettings.scenes.Any(entry => entry.path == CarWorldSetup.scenePath))
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Append(new EditorBuildSettingsScene(CarWorldSetup.scenePath, true))
                .ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"CarWorldSetup: {CarWorldSetup.scenePath} written and listed");
    }

    /// <summary>
    /// Creates or updates a material in the car link material folder.
    /// </summary>
    private static Material WriteMaterial(string name, string shader, Color color)
    {
        if (!AssetDatabase.IsValidFolder(CarWorldSetup.materialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "CarLink");
        }
        string path = $"{CarWorldSetup.materialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find(shader);
        material.color = color;
        if (shader == "Standard")
        {
            material.SetFloat("_Glossiness", 0.1f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}
