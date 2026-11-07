using UnityEngine;

/// <summary>
/// The HUD's overhead map camera: orthographic, north up (world +z), centered on the car and
/// following it, rendering the scene (walls and obstacles included) into a small texture every
/// few frames. Visual only; nothing here reaches the car's sensors or the Python API.
/// </summary>
public class MiniMap : MonoBehaviour
{
    /// <summary>
    /// Texture size in pixels (square).
    /// </summary>
    public const int TextureSize = 256;

    /// <summary>
    /// Smallest and largest half width of the map view in world units (1 unit = 1 dm).
    /// </summary>
    public const float MinHalfExtent = 25;
    public const float MaxHalfExtent = 90;

    /// <summary>
    /// Fraction of the level's top view width the map shows on each side of the car.
    /// </summary>
    private const float levelFraction = 0.18f;

    /// <summary>
    /// Height of the camera above the car, in world units.
    /// </summary>
    private const float height = 120;

    /// <summary>
    /// Frames between map renders; the overlay (route, car arrow) still moves every frame.
    /// </summary>
    private const int renderInterval = 3;

    private Camera mapCamera;
    private int frame;

    /// <summary>
    /// The followed car.
    /// </summary>
    public Transform Target { get; private set; }

    /// <summary>
    /// Half width of the visible square in world units.
    /// </summary>
    public float HalfExtent { get; private set; }

    /// <summary>
    /// The map image.
    /// </summary>
    public RenderTexture Texture { get; private set; }

    /// <summary>
    /// Half width of the map view for a level whose top view spans the given width.
    /// </summary>
    public static float HalfExtentFor(float levelWidth)
    {
        return Mathf.Clamp(levelWidth * MiniMap.levelFraction, MiniMap.MinHalfExtent, MiniMap.MaxHalfExtent);
    }

    /// <summary>
    /// Starts following the car; levelWidth sets the view size (HalfExtentFor).
    /// </summary>
    public void Follow(Transform target, float levelWidth)
    {
        this.Target = target;
        this.HalfExtent = MiniMap.HalfExtentFor(levelWidth);
        if (this.mapCamera == null)
        {
            this.Texture = new RenderTexture(MiniMap.TextureSize, MiniMap.TextureSize, 16) { name = "MiniMap", antiAliasing = 2 };
            GameObject cameraObject = new GameObject("MiniMapCamera");
            cameraObject.transform.SetParent(this.transform, false);
            this.mapCamera = cameraObject.AddComponent<Camera>();
            this.mapCamera.orthographic = true;
            this.mapCamera.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString(ThemeTokens.Roles["code-bg"], out Color background);
            this.mapCamera.backgroundColor = background;
            this.mapCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI") | Arena.LayerMaskBit);
            this.mapCamera.nearClipPlane = 1;
            this.mapCamera.farClipPlane = MiniMap.height * 2;
            this.mapCamera.allowHDR = false;
            this.mapCamera.allowMSAA = false;
            this.mapCamera.targetTexture = this.Texture;
            this.mapCamera.enabled = false;
        }
        this.mapCamera.orthographicSize = this.HalfExtent;
        this.frame = MiniMap.renderInterval;
    }

    private void LateUpdate()
    {
        if (this.Target == null || this.mapCamera == null)
        {
            return;
        }

        Vector3 position = this.Target.position;
        this.mapCamera.transform.SetPositionAndRotation(new Vector3(position.x, position.y + MiniMap.height, position.z), Quaternion.Euler(90, 0, 0));
        if (++this.frame >= MiniMap.renderInterval)
        {
            this.frame = 0;
            this.mapCamera.Render();
        }
    }

    private void OnDestroy()
    {
        if (this.Texture != null)
        {
            this.Texture.Release();
            Destroy(this.Texture);
        }
    }
}
