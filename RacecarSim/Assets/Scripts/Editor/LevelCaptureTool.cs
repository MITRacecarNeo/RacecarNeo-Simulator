using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders each level's thumbnail (three-quarter view, 960 x 540) and top view (orthographic,
/// 512 x 512) from its scene, records the key point route, and writes the entries to
/// Resources/LevelPresentation.asset. The course area comes from CourseBounds; terrain and large
/// floor planes are scenery around it.
/// </summary>
/// <remarks>
/// Batch mode (editor closed), all levels or the given keys:
/// <code>Unity -batchmode -quit -projectPath . -executeMethod LevelCaptureTool.CaptureAll</code>
/// <code>Unity -batchmode -quit -projectPath . -executeMethod LevelCaptureTool.CaptureAll -levels demo-world,grand-prix-2026</code>
/// </remarks>
public static class LevelCaptureTool
{
    /// <summary>
    /// The catalog asset LevelPresentationCatalog loads from Resources.
    /// </summary>
    public const string CatalogPath = "Assets/Resources/LevelPresentation.asset";

    private const string thumbnailFolder = "Assets/UI/Levels/Thumbnails";
    private const string topViewFolder = "Assets/UI/Levels/TopViews";
    private const int thumbnailWidth = 960;
    private const int thumbnailHeight = 540;
    private const int topViewSize = 512;
    private const int jpegQuality = 88;

    /// <summary>
    /// Renderers larger than this along any axis (m) are terrain-like scenery, not course.
    /// </summary>
    private const float maxPartSize = 150;

    /// <summary>
    /// Smallest course side (m), so a level of a few parts still frames sensibly.
    /// </summary>
    private const float minCourseSize = 12;

    /// <summary>
    /// Course side (m) for a level with no parts or key points in its scene (terrain only).
    /// </summary>
    private const float emptyCourseSize = 60;

    /// <summary>
    /// Smallest margin (m) around the key points within which parts count as course.
    /// </summary>
    private const float keyPointMargin = 40;

    /// <summary>
    /// Parts taller than this (m) are cliffs and peaks: drawn, but not framed as course.
    /// </summary>
    private const float maxPartHeight = 30;

    /// <summary>
    /// A finish closer than this to the start (m) makes the route a loop.
    /// </summary>
    private const float loopDistance = 4;

    private const float thumbnailFieldOfView = 35;
    private const float thumbnailPitch = 38;

    /// <summary>
    /// Thumbnail pitch when terrain rises more than tallTerrain above the course, to look past it.
    /// </summary>
    private const float steepThumbnailPitch = 68;

    /// <summary>
    /// Terrain height above the course top (m) that switches the thumbnail to steepThumbnailPitch.
    /// </summary>
    private const float tallTerrain = 10;
    private const float thumbnailYaw = 40;

    /// <summary>
    /// Captures every level, or only the keys after -levels (comma-separated).
    /// </summary>
    [MenuItem("RacecarSim/Capture Level Presentation")]
    public static void CaptureAll()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-levels");
        HashSet<string> only = flag >= 0 && flag + 1 < args.Length ? new HashSet<string>(args[flag + 1].Split(',')) : null;

        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        LevelInfo[] levels = LevelCollection.LevelCollections.SelectMany(collection => collection.Levels).ToArray();
        List<LevelPresentation> captured = new List<LevelPresentation>();
        Directory.CreateDirectory(LevelCaptureTool.thumbnailFolder);
        Directory.CreateDirectory(LevelCaptureTool.topViewFolder);

        SceneSetup[] setup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (LevelInfo level in levels.Where(level => only == null || only.Contains(level.Key)))
            {
                EditorSceneManager.OpenScene(scenes[level.BuildIndex], OpenSceneMode.Single);
                LevelPresentation entry = LevelCaptureTool.Capture(level);
                captured.Add(entry);
                Debug.Log($"LevelCaptureTool: {level.Key} from {scenes[level.BuildIndex]}, area {entry.TopViewArea}, route {entry.Route.Length} points{(entry.IsLoop ? ", loop" : "")}");
            }
        }
        finally
        {
            if (setup != null)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        // Opening scenes unloads unreferenced assets, so the catalog loads after the captures
        LevelPresentationCatalog catalog = LevelCaptureTool.LoadOrCreateCatalog();
        foreach (LevelPresentation entry in captured)
        {
            entry.Thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(LevelCaptureTool.ThumbnailPath(entry.Key));
            entry.TopView = AssetDatabase.LoadAssetAtPath<Texture2D>(LevelCaptureTool.TopViewPath(entry.Key));
            // Hand-set presentation choices survive a recapture
            if (catalog.Find(entry.Key) is LevelPresentation previous)
            {
                entry.ArenaPalette = previous.ArenaPalette;
            }
            catalog.Entries.RemoveAll(existing => existing.Key == entry.Key);
            catalog.Entries.Add(entry);
        }
        List<string> order = levels.Select(level => level.Key).ToList();
        catalog.Entries.RemoveAll(entry => !order.Contains(entry.Key));
        catalog.Entries.Sort((a, b) => order.IndexOf(a.Key).CompareTo(order.IndexOf(b.Key)));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// The course area in world x and z: key points plus renderers up to maxPartSize, trimmed to
    /// the middle 90 percent of part centers when there are 20 or more; parts taller than
    /// maxPartHeight are scenery. With three or more key points, only parts within a margin of them
    /// count (the larger of keyPointMargin and half their span), so geometry parked far from the
    /// course does not widen the frame. Two key points are often a shared start and finish, too
    /// close together to anchor the course.
    /// </summary>
    public static Bounds CourseBounds(IList<Bounds> parts, IList<Vector3> keyPoints)
    {
        if (parts.Count == 0 && keyPoints.Count == 0)
        {
            return new Bounds(Vector3.zero, new Vector3(LevelCaptureTool.emptyCourseSize, 0, LevelCaptureTool.emptyCourseSize));
        }

        List<Bounds> course = parts
            .Where(part => Mathf.Max(part.size.x, part.size.z) <= LevelCaptureTool.maxPartSize && part.size.y <= LevelCaptureTool.maxPartHeight)
            .ToList();
        if (keyPoints.Count >= 3)
        {
            Bounds anchor = new Bounds(keyPoints[0], Vector3.zero);
            foreach (Vector3 point in keyPoints)
            {
                anchor.Encapsulate(point);
            }
            float margin = Mathf.Max(LevelCaptureTool.keyPointMargin, Mathf.Max(anchor.size.x, anchor.size.z) / 2);
            course = course.Where(part =>
                part.center.x >= anchor.min.x - margin && part.center.x <= anchor.max.x + margin &&
                part.center.z >= anchor.min.z - margin && part.center.z <= anchor.max.z + margin).ToList();
        }
        if (course.Count >= 20)
        {
            (float xMin, float xMax) = LevelCaptureTool.Middle(course.Select(part => part.center.x));
            (float zMin, float zMax) = LevelCaptureTool.Middle(course.Select(part => part.center.z));
            course = course.Where(part => part.center.x >= xMin && part.center.x <= xMax && part.center.z >= zMin && part.center.z <= zMax).ToList();
        }

        Bounds bounds = course.Count > 0 ? course[0] : new Bounds(keyPoints.Count > 0 ? keyPoints[0] : Vector3.zero, Vector3.zero);
        foreach (Bounds part in course)
        {
            bounds.Encapsulate(part);
        }
        foreach (Vector3 point in keyPoints)
        {
            bounds.Encapsulate(point);
        }
        Vector3 size = bounds.size;
        bounds.size = new Vector3(Mathf.Max(size.x, LevelCaptureTool.minCourseSize), size.y, Mathf.Max(size.z, LevelCaptureTool.minCourseSize));
        return bounds;
    }

    private static (float Min, float Max) Middle(IEnumerable<float> values)
    {
        List<float> sorted = values.OrderBy(value => value).ToList();
        return (sorted[(int)(sorted.Count * 0.05f)], sorted[(int)(sorted.Count * 0.95f) - 1]);
    }

    private static LevelPresentation Capture(LevelInfo level)
    {
        KeyPoint[] keyPoints = UnityEngine.Object.FindObjectsByType<KeyPoint>();
        Array.Sort(keyPoints);
        Vector3[] route = keyPoints.Select(point => point.transform.position).ToArray();
        Bounds[] parts = UnityEngine.Object.FindObjectsByType<Renderer>()
            .Where(renderer => renderer.enabled && !(renderer is ParticleSystemRenderer))
            .Select(renderer => renderer.bounds)
            .ToArray();
        Bounds course = LevelCaptureTool.CourseBounds(parts, route);
        Debug.Log($"LevelCaptureTool: {level.Key} key points [{string.Join("; ", route.Select(point => $"{point.x:F0},{point.z:F0}"))}], {parts.Length} renderers, course center {course.center:F0} size {course.size:F0}");

        GameObject cameraObject = new GameObject("LevelCaptureCamera");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;

            // Top view: square, north up, 10 percent margin
            float side = Mathf.Max(course.size.x, course.size.z) * 1.1f;
            camera.orthographic = true;
            camera.orthographicSize = side / 2;
            camera.transform.SetPositionAndRotation(new Vector3(course.center.x, course.max.y + 200, course.center.z), Quaternion.Euler(90, 0, 0));
            camera.nearClipPlane = 1;
            camera.farClipPlane = course.size.y + 400;
            Texture2D topView = LevelCaptureTool.Render(camera, LevelCaptureTool.topViewSize, LevelCaptureTool.topViewSize);

            // Thumbnail: three-quarter view from the south-west, the course filling the frame
            camera.orthographic = false;
            camera.fieldOfView = LevelCaptureTool.thumbnailFieldOfView;
            float radius = new Vector2(course.extents.x, course.extents.z).magnitude;
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.8f;
            float pitch = LevelCaptureTool.TerrainTop(course) - course.max.y > LevelCaptureTool.tallTerrain
                ? LevelCaptureTool.steepThumbnailPitch
                : LevelCaptureTool.thumbnailPitch;
            Quaternion rotation = Quaternion.Euler(pitch, LevelCaptureTool.thumbnailYaw, 0);
            camera.transform.SetPositionAndRotation(course.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = distance + radius * 4 + 500;
            Texture2D thumbnail = LevelCaptureTool.Render(camera, LevelCaptureTool.thumbnailWidth, LevelCaptureTool.thumbnailHeight);

            LevelCaptureTool.Save(thumbnail, LevelCaptureTool.ThumbnailPath(level.Key));
            LevelCaptureTool.Save(topView, LevelCaptureTool.TopViewPath(level.Key));
            Rect area = new Rect(course.center.x - side / 2, course.center.z - side / 2, side, side);
            LevelPresentation entry = new LevelPresentation
            {
                Key = level.Key,

                TopViewArea = area,
                IsLoop = route.Length >= 3 && Vector3.Distance(route[0], route[route.Length - 1]) < LevelCaptureTool.loopDistance,
            };
            entry.Route = route.Select(entry.ToTopView).ToArray();
            return entry;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    /// <summary>
    /// Highest terrain point within the course area and a margin of half its size around it.
    /// </summary>
    private static float TerrainTop(Bounds course)
    {
        float top = float.MinValue;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            for (int i = 0; i <= 20; i++)
            {
                for (int j = 0; j <= 20; j++)
                {
                    Vector3 point = new Vector3(
                        course.min.x - course.extents.x + i / 20f * course.size.x * 2,
                        0,
                        course.min.z - course.extents.z + j / 20f * course.size.z * 2);
                    top = Mathf.Max(top, terrain.SampleHeight(point) + terrain.transform.position.y);
                }
            }
        }
        return top;
    }

    private static Texture2D Render(Camera camera, int width, int height)
    {
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            return pixels;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static string ThumbnailPath(string key) => $"{LevelCaptureTool.thumbnailFolder}/{key}.jpg";

    private static string TopViewPath(string key) => $"{LevelCaptureTool.topViewFolder}/{key}.jpg";

    private static void Save(Texture2D pixels, string path)
    {
        File.WriteAllBytes(path, pixels.EncodeToJPG(LevelCaptureTool.jpegQuality));
        UnityEngine.Object.DestroyImmediate(pixels);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    private static LevelPresentationCatalog LoadOrCreateCatalog()
    {
        LevelPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelPresentationCatalog>(LevelCaptureTool.CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LevelPresentationCatalog>();
            AssetDatabase.CreateAsset(catalog, LevelCaptureTool.CatalogPath);
        }
        return catalog;
    }
}
