using System.IO;
using UnityEngine;

/// <summary>
/// A captured world's map select thumbnail, rendered like the built-in levels' (three-quarter view
/// from the south-west, 960 x 540) from the world's built walls and floor, and stored as
/// thumbnail.jpg in the world's folder.
/// </summary>
public static class WorldThumbnail
{
    #region Constants
    public const string FileName = "thumbnail.jpg";
    public const int Width = 960;
    public const int Height = 540;

    // The framing of the built-in thumbnails (LevelCaptureTool)
    private const float fieldOfView = 35;
    private const float pitch = 38;
    private const float yaw = 40;

    /// <summary>
    /// Smallest framed radius (in sim units), so a tiny or empty map still shows its surroundings.
    /// </summary>
    private const float minRadius = 20;

    /// <summary>
    /// Layers left out: UI (5) and the car (9).
    /// </summary>
    private const int cullingMask = ~((1 << 5) | (1 << 9));
    #endregion

    #region Public Interface
    /// <summary>
    /// The bounds of the enabled renderers under a root (walls and floor), or an empty bounds at
    /// the root when it has none.
    /// </summary>
    public static Bounds CourseBounds(Transform root)
    {
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (any)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                any = true;
            }
        }
        return bounds;
    }

    /// <summary>
    /// Renders the thumbnail framing a course; the caller destroys the texture.
    /// </summary>
    public static Texture2D Render(Bounds course)
    {
        GameObject holder = new GameObject("WorldThumbnailCamera");
        RenderTexture target = new RenderTexture(WorldThumbnail.Width, WorldThumbnail.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        RenderTexture previous = RenderTexture.active;
        try
        {
            Camera camera = holder.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = WorldThumbnail.cullingMask;
            camera.fieldOfView = WorldThumbnail.fieldOfView;
            float radius = Mathf.Max(WorldThumbnail.minRadius, new Vector2(course.extents.x, course.extents.z).magnitude);
            float distance = radius / Mathf.Sin(WorldThumbnail.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.8f;
            Quaternion rotation = Quaternion.Euler(WorldThumbnail.pitch, WorldThumbnail.yaw, 0);
            camera.transform.SetPositionAndRotation(course.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = distance + radius * 4 + 500;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            Texture2D pixels = new Texture2D(WorldThumbnail.Width, WorldThumbnail.Height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, WorldThumbnail.Width, WorldThumbnail.Height), 0, 0);
            pixels.Apply();
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(holder);
        }
    }

    /// <summary>
    /// Renders a course and writes the thumbnail into a world's folder; false (with a warning) when
    /// the file cannot be written.
    /// </summary>
    public static bool Save(CapturedWorld world, Bounds course)
    {
        Texture2D thumbnail = WorldThumbnail.Render(course);
        try
        {
            File.WriteAllBytes(Path.Combine(world.Folder, WorldThumbnail.FileName), thumbnail.EncodeToJPG(90));
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"Worlds: thumbnail for {world.Name} not saved: {exception.Message}");
            return false;
        }
        finally
        {
            Object.Destroy(thumbnail);
        }
    }

    /// <summary>
    /// True when a world's folder holds a rendered thumbnail.
    /// </summary>
    public static bool Exists(CapturedWorld world)
    {
        return world.Folder != null && File.Exists(Path.Combine(world.Folder, WorldThumbnail.FileName));
    }
    #endregion
}
