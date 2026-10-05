using System;
using System.Threading;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Simulates the color and depth channels of the RealSense camera.
/// </summary>
public class CameraModule : RacecarModule
{
    #region Constants
    /// <summary>
    /// The width (in pixels) of the color images captured by the camera.
    /// </summary>
    public const int ColorWidth = 640;

    /// <summary>
    /// The height (in pixels) of the color images captured by the camera.
    /// </summary>
    public const int ColorHeight = 480;

    /// <summary>
    /// The field of view (in degrees) of the camera.
    /// Based on the Intel RealSense D435i datasheet.
    /// </summary>
    private static readonly Vector2 fieldOfView = new Vector2(69.4f, 42.5f);

    /// <summary>
    /// The minimum distance (in dm) that can be detected by the depth channel.
    /// Based on the Intel RealSense D435i datasheet.
    /// </summary>
    private static float minRange = 1.05f;

    /// <summary>
    /// The value recorded for a depth sample less than minRange.
    /// </summary>
    private static float minCode = 0.0f;

    /// <summary>
    /// The maximum distance (in dm) that can be detected by the depth channel.
    /// Based on the Intel RealSense D435i datasheet.
    /// </summary>
    private static float maxRange = 100f;

    /// <summary>
    /// The value recorded for a depth sample greater than maxRange.
    /// </summary>
    private static float maxCode = 0.0f;

    /// <summary>
    /// The average relative error of distance measurements.
    /// Based on the Intel RealSense D435i datasheet.
    /// </summary>
    private const float averageErrorFactor = 0.02f;

    /// <summary>
    /// Maximum time (in ms) an async call waits for the main thread to capture an image. A call
    /// normally returns after one frame; the limit applies while no frames run (scene loading).
    /// </summary>
    private const int asyncWaitTime = 1000;

    /// <summary>
    /// Depth ray casts per job in the batched depth image.
    /// </summary>
    private const int raycastsPerJob = 64;

    /// <summary>
    /// Seconds between refreshes of the HUD depth view.
    /// </summary>
    private const float depthVisualizationInterval = 0.1f;
    #endregion

    #region Public Interface
    /// <summary>
    /// The width (in pixels) of the depth images captured by the camera.
    /// </summary>
    public static int DepthWidth { get { return CameraModule.ColorWidth / Settings.DepthDivideFactor; } }

    /// <summary>
    /// The height (in pixels) of the depth images captured by the camera.
    /// </summary>
    public static int DepthHeight { get { return CameraModule.ColorHeight / Settings.DepthDivideFactor; } }

    /// <summary>
    /// The GPU-side texture to which the color camera renders.
    /// </summary>
    public RenderTexture ColorImage
    {
        get
        {
            return this.colorCamera.targetTexture;
        }
    }

    /// <summary>
    /// The raw bytes of the color image captured by the color camera this frame.
    /// Each pixel is stored in the ARGB 32-bit format, from top left to bottom right.
    /// </summary>
    public byte[] ColorImageRaw
    {
        get
        {
            if (!isColorImageRawValid)
            {
                this.UpdateColorImageRaw();
            }
            return this.colorImageRaw;
        }
    }

    /// <summary>
    /// The depth values (in cm) captured by the depth camera this frame, from top left to bottom right.
    /// </summary>
    public float[][] DepthImage
    {
        get
        {
            if (!isDepthImageValid)
            {
                this.UpdateDepthImage();
            }

            return this.depthImage;
        }
    }

    /// <summary>
    /// The raw bytes of the depth values (in cm) captured by the depth camera this frame.
    /// Each value is a 32-bit IEEE float, indexed from top left to bottom right.
    /// </summary>
    public byte[] DepthImageRaw
    {
        get
        {
            if (!this.isDepthImageRawValid)
            {
                this.UpdateDepthImageRaw();
            }

            return this.depthImageRaw;
        }
    }

    /// <summary>
    /// Creates a visualization of the current depth image.
    /// </summary>
    /// <param name="texture">The texture to which the visualization is rendered (must be DepthWidth by DepthHeight).</param>
    public void VisualizeDepth(Texture2D texture)
    {
        if (texture.width != CameraModule.DepthWidth || texture.height != CameraModule.DepthHeight)
        {
            throw new Exception("Texture dimensions must match depth image dimensions.");
        }

        Unity.Collections.NativeArray<Color32> rawData = texture.GetRawTextureData<Color32>();

        for (int i = 0; i < rawData.Length; i++)
        {
            rawData[i] = Hud.SensorBackgroundColor;
        }

        for (int r = 0; r < CameraModule.DepthHeight; r++)
        {
            for (int c = 0; c < CameraModule.DepthWidth; c++)
            {
                if (this.DepthImage[r][c] != CameraModule.minCode && this.DepthImage[r][c] != CameraModule.maxCode)
                {
                    rawData[(CameraModule.DepthHeight - 1 - r) * texture.width + c] = CameraModule.InterpolateDepthColor(DepthImage[r][c]);
                }
            }
        }

        texture.Apply();
    }

    /// <summary>
    /// Requests a color image from a background thread and waits for the main thread to capture it.
    /// </summary>
    /// <returns>A copy of the color image captured on the next frame, or the last copy if no frame
    /// ran within asyncWaitTime ms (for example while a scene loads).</returns>
    public byte[] GetColorImageRawAsync()
    {
        return this.colorRequest.Wait(CameraModule.asyncWaitTime);
    }

    /// <summary>
    /// Requests a depth image from a background thread and waits for the main thread to capture it.
    /// </summary>
    /// <returns>A copy of the depth image captured on the next frame, or the last copy if no frame
    /// ran within asyncWaitTime ms.</returns>
    public byte[] GetDepthImageRawAsync()
    {
        return this.depthRequest.Wait(CameraModule.asyncWaitTime);
    }
    #endregion

    /// <summary>
    /// Private member for the ColorImageRaw accessor.
    /// </summary>
    private byte[] colorImageRaw;

    /// <summary>
    /// True if colorImageRaw is up to date with the color image rendered for the current frame.
    /// </summary>
    private bool isColorImageRawValid = false;

    /// <summary>
    /// Private member for the DepthImage accessor.
    /// </summary>
    private float[][] depthImage;

    /// <summary>
    /// True if depthImage is up to date with the depth image captured for the current frame.
    /// </summary>
    private bool isDepthImageValid = false;

    /// <summary>
    /// Private member for the DepthImageRaw accessor.
    /// </summary>
    private byte[] depthImageRaw;

    /// <summary>
    /// True if depthImageRaw is up to date with the depth image captured for the current frame.
    /// </summary>
    private bool isDepthImageRawValid = false;

    /// <summary>
    /// The color camera on the car.
    /// </summary>
    private Camera colorCamera;

    /// <summary>
    /// The depth camera on the car. Its viewport defines the raycast directions in UpdateDepthImage.
    /// </summary>
    private Camera depthCamera;

    /// <summary>
    /// A frame capture requested by the async (Jupyter) thread and fulfilled on the main thread.
    /// Each fulfilled request stores a new array, so the requesting thread never reads a buffer
    /// that the main thread is writing.
    /// </summary>
    private class AsyncCaptureRequest
    {
        private readonly ManualResetEventSlim fulfilled = new ManualResetEventSlim(false);

        private volatile bool isRequested;

        private volatile byte[] snapshot;

        public AsyncCaptureRequest(int length)
        {
            this.snapshot = new byte[length];
        }

        /// <summary>
        /// True while a background thread waits for a capture.
        /// </summary>
        public bool IsRequested { get { return this.isRequested; } }

        /// <summary>
        /// Called on the background thread: requests a capture and blocks until it arrives or the timeout passes.
        /// </summary>
        public byte[] Wait(int timeoutMs)
        {
            this.fulfilled.Reset();
            this.isRequested = true;
            this.fulfilled.Wait(timeoutMs);
            return this.snapshot;
        }

        /// <summary>
        /// Called on the main thread: stores a copy of the current data and releases the waiting thread.
        /// </summary>
        public void Fulfill(byte[] current)
        {
            this.snapshot = (byte[])current.Clone();
            this.isRequested = false;
            this.fulfilled.Set();
        }
    }

    /// <summary>
    /// CPU-side copy target for color captures, reused across frames.
    /// </summary>
    private Texture2D captureTexture;

    /// <summary>
    /// The Time.unscaledTime at which the HUD depth view next refreshes.
    /// </summary>
    private float nextDepthVisualizationTime;

    /// <summary>
    /// Pending async color image request.
    /// </summary>
    private AsyncCaptureRequest colorRequest;

    /// <summary>
    /// Pending async depth image request.
    /// </summary>
    private AsyncCaptureRequest depthRequest;

    protected override void Awake()
    {
        Camera[] cameras = this.GetComponentsInChildren<Camera>();
        this.colorCamera = cameras[0];
        this.depthCamera = cameras[1];

        this.depthImage = new float[CameraModule.DepthHeight][];
        for (int r = 0; r < CameraModule.DepthHeight; r++)
        {
            this.depthImage[r] = new float[CameraModule.DepthWidth];
        }

        this.depthImageRaw = new byte[sizeof(float) * CameraModule.DepthHeight * CameraModule.DepthWidth];
        this.colorImageRaw = new byte[sizeof(float) * CameraModule.ColorWidth * CameraModule.ColorHeight];
        this.colorRequest = new AsyncCaptureRequest(this.colorImageRaw.Length);
        this.depthRequest = new AsyncCaptureRequest(this.depthImageRaw.Length);

        if (Settings.HideCarsInColorCamera)
        {
            this.colorCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));
        }

        base.Awake();
    }

    private void Start()
    {
        this.colorCamera.fieldOfView = CameraModule.fieldOfView.y;
        this.depthCamera.fieldOfView = CameraModule.fieldOfView.y;

        // The depth camera only defines the depth ray directions; rendering it would draw the
        // scene a second time into the color camera's target every frame
        this.depthCamera.enabled = false;
    }

    private void Update()
    {
        if (this.colorRequest.IsRequested)
        {
            this.colorRequest.Fulfill(this.ColorImageRaw);
        }

        if (this.depthRequest.IsRequested)
        {
            this.depthRequest.Fulfill(this.DepthImageRaw);
        }

        // The HUD depth view raycasts the full depth image, so refresh it at a fixed rate rather than every frame
        if (this.racecar.Hud != null && Time.unscaledTime >= this.nextDepthVisualizationTime)
        {
            // Includes the depth raycasts when no program read the depth image this frame
            using SimProfiler.Scope profile = SimProfiler.Measure(SimProfiler.Section.DepthHud);
            this.VisualizeDepth(this.racecar.Hud.DepthVisualization);
            this.nextDepthVisualizationTime = Time.unscaledTime + CameraModule.depthVisualizationInterval;
        }
    }

    private void OnDestroy()
    {
        Destroy(this.captureTexture);
    }

    private void LateUpdate()
    {
        this.isColorImageRawValid = false;
        this.isDepthImageValid = false;
        this.isDepthImageRawValid = false;
    }

    /// <summary>
    /// Interpolate a depth sample to a white-yellow-red-blue-gray range.
    /// </summary>
    /// <param name="depth">The depth of a particular sample (in cm).</param>
    /// <returns>The color representing the supplied depth.</returns>
    private static Color InterpolateDepthColor(float depth)
    {
        // Convert depth to a [0, 1] range
        depth /= 10 * CameraModule.maxRange;

        // Select correct color range and interpolate
        if (depth < 0.05f)
        {
            return Color.Lerp(Color.white, Color.yellow, depth / 0.05f);
        }
        else if (depth < 0.2f)
        {
            return Color.Lerp(Color.yellow, Color.red, (depth - 0.05f) / 0.15f);
        }
        else if (depth < 0.6f)
        {
            return Color.Lerp(Color.red, Color.blue, (depth - 0.2f) / 0.4f);
        }
        else
        {
            return Color.Lerp(Color.blue, Hud.SensorBackgroundColor, (depth - 0.6f) / 0.4f);
        }
    }

    /// <summary>
    /// Update colorImageRaw by rendering the color camera on the GPU and copying to the CPU.
    /// Warning: this operation is very expensive.
    /// </summary>
    private void UpdateColorImageRaw()
    {
        using SimProfiler.Scope profile = SimProfiler.Measure(SimProfiler.Section.ColorReadback);
        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture.active = this.ColorImage;

        // An enabled color camera already renders to ColorImage every frame; reading that image
        // costs one frame of latency instead of a second render. Render explicitly only when the
        // camera is disabled or has not rendered yet.
        if (!this.colorCamera.enabled || Time.frameCount <= 1)
        {
            this.colorCamera.Render();
        }

        // Copy this image from the GPU to a reused Texture2D on the CPU
        if (this.captureTexture == null || this.captureTexture.width != this.ColorImage.width || this.captureTexture.height != this.ColorImage.height)
        {
            Destroy(this.captureTexture);
            this.captureTexture = new Texture2D(this.ColorImage.width, this.ColorImage.height, TextureFormat.RGBA32, false);
        }
        this.captureTexture.ReadPixels(new Rect(0, 0, this.ColorImage.width, this.ColorImage.height), 0, 0);

        // Restore the previous GPU render target
        RenderTexture.active = activeRenderTexture;

        // Copy the bytes from the Texture2D to this.colorImageRaw, reversing row order
        // (Unity orders bottom-to-top, we want top-to-bottom)
        Unity.Collections.NativeArray<byte> bytes = this.captureTexture.GetRawTextureData<byte>();
        int bytesPerRow = CameraModule.ColorWidth * 4;
        for (int r = 0; r < CameraModule.ColorHeight; r++)
        {
            Unity.Collections.NativeArray<byte>.Copy(bytes, (CameraModule.ColorHeight - r - 1) * bytesPerRow, this.colorImageRaw, r * bytesPerRow, bytesPerRow);
        }

        this.isColorImageRawValid = true;
    }

    /// <summary>
    /// Update depthImage with one ray cast per depth pixel, run as a batch on the job threads.
    /// </summary>
    private void UpdateDepthImage()
    {
        using SimProfiler.Scope profile = SimProfiler.Measure(SimProfiler.Section.DepthImage);
        int width = CameraModule.DepthWidth;
        int height = CameraModule.DepthHeight;
        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(width * height, Allocator.TempJob);
        NativeArray<RaycastHit> hits = new NativeArray<RaycastHit>(width * height, Allocator.TempJob);
        try
        {
            QueryParameters query = new QueryParameters(Constants.IgnoreUIMask);
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    Ray ray = this.depthCamera.ViewportPointToRay(new Vector3(
                        (float)c / (width - 1),
                        (height - r - 1.0f) / (height - 1),
                        0));
                    commands[r * width + c] = new RaycastCommand(ray.origin, ray.direction, query, CameraModule.maxRange);
                }
            }
            RaycastCommand.ScheduleBatch(commands, hits, CameraModule.raycastsPerJob).Complete();

            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    RaycastHit raycastHit = hits[r * width + c];
                    if (raycastHit.collider != null)
                    {
                        float distance = Settings.IsRealism
                            ? raycastHit.distance * NormalDist.Random(1, CameraModule.averageErrorFactor)
                            : raycastHit.distance;
                        this.depthImage[r][c] = distance > CameraModule.minRange ? distance * 10 : CameraModule.minCode;
                    }
                    else
                    {
                        this.depthImage[r][c] = CameraModule.maxCode;
                    }
                }
            }
        }
        finally
        {
            commands.Dispose();
            hits.Dispose();
        }

        this.isDepthImageValid = true;
    }

    /// <summary>
    /// Update depthImageRaw from DepthImage
    /// </summary>
    private void UpdateDepthImageRaw()
    {
        for (int r = 0; r < CameraModule.DepthHeight; r++)
        {
            Buffer.BlockCopy(this.DepthImage[r], 0,
                            this.depthImageRaw, r * CameraModule.DepthWidth * sizeof(float),
                            CameraModule.DepthWidth * sizeof(float));
        }

        this.isDepthImageRawValid = true;
    }
}
