using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Sets up the UI Toolkit main menu in Main.unity: removes the old uGUI menu canvas (keeping its
/// EventSystem), adds MenuController, builds the car turntable stage (the high-detail showcase
/// export of the car on a beveled podium) and frames the camera on it, and sets the player icon
/// and turns off the Unity splash, so the menu's own splash is the first thing shown.
/// Each step replaces what an earlier run made, so Apply can run again.
/// </summary>
/// <remarks>
/// Batch mode:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod MenuSetup.Apply</code>
/// </remarks>
public static class MenuSetup
{
    private const string mainScenePath = "Assets/Scenes/Main.unity";
    private const string layoutPath = "Assets/UI/Menu/MainMenu.uxml";
    private const string modelPath = "Assets/Models/RacecarNeoV2/RacecarNeoV2Showcase.fbx";
    private const string floorMaterialPath = "Assets/Materials/MenuFloor.mat";
    private const string podiumMeshPath = "Assets/Models/MenuPodium.asset";
    private const string appIconPath = "Assets/UI/Brand/app-icon-1024.png";
    private static readonly string[] brandTextures =
    {
        "Assets/UI/Brand/lockup-horizontal-reversed.png", "Assets/UI/Brand/lockup-horizontal-white.png",
        "Assets/UI/Brand/mark-white.png", MenuSetup.appIconPath,
    };

    /// <summary>
    /// Fraction of the screen width the car spans, and how far right of center it sits.
    /// </summary>
    private const float carScreenWidth = 0.38f;
    private const float carScreenOffset = 0.2f;

    /// <summary>
    /// Runs every step and saves Main.unity.
    /// </summary>
    public static void Apply()
    {
        MenuSetup.ImportBrandTextures();
        MenuSetup.SetPlayerBranding();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(MenuSetup.mainScenePath, OpenSceneMode.Single);
        GameObject oldRoot = GameObject.Find("MainMenu");
        if (oldRoot != null)
        {
            // The EventSystem routes input to the UI Toolkit panels too
            Transform events = oldRoot.transform.Find("EventSystem");
            events?.SetParent(null, true);
            Object.DestroyImmediate(oldRoot);
        }

        Bounds car = MenuSetup.BuildStage(out MenuShowcase showcase);
        MenuSetup.BuildMenu(showcase);
        MenuSetup.FrameCamera(car);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"MenuSetup: Main.unity updated; car bounds {car.center:F2} size {car.size:F2}");
    }

    private static void BuildMenu(MenuShowcase showcase)
    {
        MenuSetup.DestroyExisting("Menu");
        GameObject menu = new GameObject("Menu");
        UIDocument document = menu.AddComponent<UIDocument>();
        document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiThemeSetup.PanelSettingsPath);
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuSetup.layoutPath);
        MenuController controller = menu.AddComponent<MenuController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("document").objectReferenceValue = document;
        serialized.FindProperty("showcase").objectReferenceValue = showcase;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// The turntable stage at the origin: the car model centered on a turntable, on a slate podium.
    /// Returns the car's bounds.
    /// </summary>
    private static Bounds BuildStage(out MenuShowcase showcase)
    {
        MenuSetup.DestroyExisting("MenuStage");
        GameObject stage = new GameObject("MenuStage");
        GameObject turntable = new GameObject("Turntable");
        turntable.transform.SetParent(stage.transform, false);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MenuSetup.modelPath));
        model.transform.SetParent(turntable.transform, false);
        model.transform.localRotation = Quaternion.Euler(0, 35, 0);
        Bounds bounds = MenuSetup.RendererBounds(model);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = MenuSetup.RendererBounds(model);

        Renderer band = model.transform.Find("LedBar").GetComponent<Renderer>();
        band.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RacecarPrefabSetup.LedBandMaterialPath);

        GameObject podium = new GameObject("Podium");
        podium.transform.SetParent(stage.transform, false);
        float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.85f;
        float height = bounds.size.y * 0.06f;
        podium.transform.localPosition = new Vector3(0, -height, 0);
        podium.AddComponent<MeshFilter>().sharedMesh = MenuSetup.PodiumMesh(radius, height, height * 0.5f);
        podium.AddComponent<MeshRenderer>().sharedMaterial = MenuSetup.FloorMaterial();

        showcase = stage.AddComponent<MenuShowcase>();
        SerializedObject serialized = new SerializedObject(showcase);
        serialized.FindProperty("turntable").objectReferenceValue = turntable.transform;
        serialized.FindProperty("band").objectReferenceValue = band;
        serialized.FindProperty("shell").objectReferenceValue = model.transform.Find("Shell").GetComponent<Renderer>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return bounds;
    }

    /// <summary>
    /// Main Camera on a charcoal background, looking down at the car from the front-left so the
    /// car spans carScreenWidth of the screen, carScreenOffset right of center, clear of the menu
    /// column on the left.
    /// </summary>
    private static void FrameCamera(Bounds car)
    {
        Camera camera = GameObject.Find("Main Camera").GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        // A shade darker than the charcoal transition cover, so the cover reads against the menu
        ColorUtility.TryParseHtmlString(ThemeTokens.Roles["code-bg"], out Color stage);
        camera.backgroundColor = stage;
        camera.fieldOfView = 30;

        float length = Mathf.Max(car.size.x, car.size.z);
        float aspect = 16f / 9f;
        float halfWidth = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
        float distance = length / MenuSetup.carScreenWidth / 2 / halfWidth;
        Quaternion rotation = Quaternion.Euler(16, -20, 0);
        Vector3 right = rotation * Vector3.right;
        Vector3 target = car.center - right * (MenuSetup.carScreenOffset * 2 * halfWidth * distance);
        camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        camera.nearClipPlane = distance * 0.05f;
        camera.farClipPlane = distance * 10;
    }

    /// <summary>
    /// A disc of the given radius and height, top at y = height, with a 45 degree bevel of the
    /// given size around the top edge; 160 segments, saved as podiumMeshPath.
    /// </summary>
    private static Mesh PodiumMesh(float radius, float height, float bevel)
    {
        const int segments = 160;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Rings of (radius, height, normal) around the axis; a repeated radius with a new normal
        // makes a hard edge between the top, the bevel, and the side
        (float Radius, float Height, Vector2 Normal)[] profile =
        {
            (radius - bevel, height, new Vector2(0, 1)),
            (radius - bevel, height, new Vector2(0.7071f, 0.7071f)),
            (radius, height - bevel, new Vector2(0.7071f, 0.7071f)),
            (radius, height - bevel, new Vector2(1, 0)),
            (radius, 0, new Vector2(1, 0)),
        };
        vertices.Add(new Vector3(0, height, 0));
        normals.Add(Vector3.up);
        foreach ((float ringRadius, float ringHeight, Vector2 normal) in profile)
        {
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add(direction * ringRadius + Vector3.up * ringHeight);
                normals.Add((direction * normal.x + Vector3.up * normal.y).normalized);
            }
        }
        int Ring(int ring, int i) => 1 + ring * (segments + 1) + i;
        for (int i = 0; i < segments; i++)
        {
            triangles.AddRange(new[] { 0, Ring(0, i + 1), Ring(0, i) });
            foreach (int ring in new[] { 1, 3 })
            {
                triangles.AddRange(new[] { Ring(ring, i), Ring(ring, i + 1), Ring(ring + 1, i + 1) });
                triangles.AddRange(new[] { Ring(ring, i), Ring(ring + 1, i + 1), Ring(ring + 1, i) });
            }
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MenuSetup.podiumMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "MenuPodium" };
            AssetDatabase.CreateAsset(mesh, MenuSetup.podiumMeshPath);
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material FloorMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MenuSetup.floorMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, MenuSetup.floorMaterialPath);
        }
        material.color = ThemeTokens.Slate;
        material.SetFloat("_Glossiness", 0.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ImportBrandTextures()
    {
        foreach (string path in MenuSetup.brandTextures)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// The app icon, no Unity splash, and a charcoal startup background that matches the menu
    /// splash's first frame.
    /// </summary>
    private static void SetPlayerBranding()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuSetup.appIconPath);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.backgroundColor = ThemeTokens.Charcoal;
    }

    private static Bounds RendererBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static void DestroyExisting(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }
}
