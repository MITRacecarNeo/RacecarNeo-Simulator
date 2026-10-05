using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders a model or prefab from fixed viewpoints to PNG files, for checking geometry and
/// materials without opening the editor UI. Batch use:
/// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod ModelPreview.RenderBatch
/// [-previewAsset Assets/...] [-previewOut Logs/model-preview]</c>
/// </summary>
public static class ModelPreview
{
    private const int Width = 1200;
    private const int Height = 800;

    private static readonly (string Name, Vector3 Direction)[] Views =
    {
        ("front-left", new Vector3(-1, 0.6f, 1.2f)),
        ("rear-right", new Vector3(1, 0.6f, -1.2f)),
        ("left", new Vector3(-1, 0.05f, 0)),
        ("front", new Vector3(0, 0.1f, 1)),
        ("top", new Vector3(0, 1, 0.001f)),
        ("under", new Vector3(0.3f, -1, 0.2f)),
        ("rear-low", new Vector3(0.25f, 0.3f, -1)),
    };

    /// <summary>
    /// Entry point for -executeMethod; reads -previewAsset and -previewOut from the command line.
    /// </summary>
    public static void RenderBatch()
    {
        string asset = ModelPreview.Argument("-previewAsset") ?? RacecarModelImport.ModelPath;
        string output = ModelPreview.Argument("-previewOut") ?? "Logs/model-preview";
        ModelPreview.Render(asset, output);
    }

    /// <summary>
    /// Renders the asset at assetPath in an empty scene on a ground plane and writes one PNG per
    /// view to outputFolder.
    /// </summary>
    public static void Render(string assetPath, string outputFolder)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            throw new ArgumentException($"No model or prefab at {assetPath}");
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);
        RenderSettings.skybox = null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        Bounds bounds = ModelPreview.RendererBounds(instance);
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 5;
        Material groundMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.55f, 0.55f) };
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        Camera camera = Camera.main;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
        camera.fieldOfView = 30;
        camera.nearClipPlane = 0.05f;
        float distance = bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;

        RenderTexture target = new RenderTexture(ModelPreview.Width, ModelPreview.Height, 24);
        Texture2D image = new Texture2D(ModelPreview.Width, ModelPreview.Height, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        Directory.CreateDirectory(outputFolder);
        foreach ((string name, Vector3 direction) in ModelPreview.Views)
        {
            ground.SetActive(direction.y >= 0);
            camera.transform.position = bounds.center + direction.normalized * distance;
            camera.transform.LookAt(bounds.center);
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, ModelPreview.Width, ModelPreview.Height), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outputFolder, $"{name}.png"), image.EncodeToPNG());
        }
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(groundMaterial);
        Debug.Log($"ModelPreview: {ModelPreview.Views.Length} views of {assetPath} in {outputFolder}");
    }

    private static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
