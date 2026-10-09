using UnityEngine;

/// <summary>
/// Encapsulates a RACECAR-MN.
/// </summary>
public class Racecar : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The cameras through which the user can observe the car; the first shows every view (PlayerCameraViews).
    /// </summary>
    [SerializeField]
    private Camera[] playerCameras;

    /// <summary>
    /// The model part recolored with the customization's shell color (the payload shell).
    /// </summary>
    [SerializeField]
    private GameObject shell;
    #endregion

    #region Constants
    /// <summary>
    /// The speed at which the camera follows the car.
    /// </summary>
    private const float cameraSpeed = 6;

    /// <summary>
    /// Height of Center above the car root (tire contact plane).
    /// </summary>
    private const float centerHeight = 1.22f;
    #endregion

    #region Public Interface
    /// <summary>
    /// The index of the racecar.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Exposes the RealSense D435i color and depth channels.
    /// </summary>
    public CameraModule Camera { get; private set; }

    /// <summary>
    /// Exposes the car motors.
    /// </summary>
    public Drive Drive { get; private set; }

    /// <summary>
    /// Exposes the YDLIDAR X4 sensor.
    /// </summary>
    public Lidar Lidar { get; private set; }

    /// <summary>
    /// Exposes the RealSense D435i IMU.
    /// </summary>
    public PhysicsModule Physics { get; private set; }

    /// <summary>
    /// The latest sensor values for Python, safe to read from the async thread.
    /// </summary>
    public SensorReadings Readings { get; } = new SensorReadings();

    /// <summary>
    /// The dot matrix and LED strip contents sent by Python.
    /// </summary>
    public ActuatorCommands Actuators { get; } = new ActuatorCommands();

    /// <summary>
    /// The heads-up display controlled by this car, if any.
    /// </summary>
    public Hud Hud { get; set; }

    /// <summary>
    /// The center point of the car, inside the payload shell collider (mid-height of the lower
    /// shell). Level objects aim raycasts at it to find the car's surface.
    /// </summary>
    public Vector3 Center
    {
        get
        {
            return this.transform.position + this.transform.up * Racecar.centerHeight;
        }
    }

    /// <summary>
    /// Called on the first frame when the car enters default drive mode.
    /// </summary>
    public void DefaultDriveStart()
    {
        this.Drive.MaxSpeed = Drive.DefaultMaxSpeed;
        this.Drive.Stop();
    }

    /// <summary>
    /// Called each frame that the car is in default drive mode.
    /// </summary>
    public void DefaultDriveUpdate()
    {
        this.Drive.Speed = Controller.GetTrigger(Controller.Trigger.RIGHT) - Controller.GetTrigger(Controller.Trigger.LEFT);
        this.Drive.Angle = Controller.GetJoystick(Controller.Joystick.LEFT).x;

        // Use the bumpers to adjust max speed
        if (Controller.WasPressed(Controller.Button.RB))
        {
            this.Drive.MaxSpeed = Mathf.Min(this.Drive.MaxSpeed + 0.1f, 1);
        }
        if (Controller.WasPressed(Controller.Button.LB))
        {
            this.Drive.MaxSpeed = Mathf.Max(this.Drive.MaxSpeed - 0.1f, 0);
        }
    }

    /// <summary>
    /// Sets the render texture and audio listener of the player perspective (3rd person) cameras.
    /// </summary>
    /// <param name="texture">The render texture to which to assign the cameras.</param>
    /// <param name="enableAudio">True if the audio listeners of the cameras should be enabled.</param>
    public void SetPlayerCameraFeatures(RenderTexture texture, bool enableAudio)
    {
        foreach (Camera camera in this.playerCameras)
        {
            camera.targetTexture = texture;
            camera.GetComponent<AudioListener>().enabled = enableAudio;
        }
    }

    /// <summary>
    /// Sets the index of the car.
    /// </summary>
    /// <param name="index">The index of the car in the race.</param>
    public void SetIndex(int index)
    {
        this.Index = index;

        // Set car color and customization based on saved data
        CarCustomization customization = SavedDataManager.Data.CarCustomizations[index];

        // Renderer.material creates a per-car copy, destroyed in OnDestroy
        Destroy(this.shellMaterial);
        this.shellMaterial = this.shell.GetComponent<Renderer>().material;
        this.shellMaterial.color = customization.ShellColor.Color;
        this.shellMaterial.SetFloat("_Metallic", customization.IsShellShiny ? 1 : 0);
    }

    /// <summary>
    /// The current view index in PlayerCameraViews.
    /// </summary>
    public int CameraView { get; private set; }

    /// <summary>
    /// The current zoom factor; 1 is the view's default distance.
    /// </summary>
    public float CameraZoom { get; private set; } = 1;

    /// <summary>
    /// Switches to a view at its default distance.
    /// </summary>
    /// <param name="cameraIndex">The view index in PlayerCameraViews.</param>
    public void SetCamera(int cameraIndex)
    {
        this.CameraView = cameraIndex;
        this.CameraZoom = 1;
        if (this.playerCameras.Length > 0)
        {
            this.playerCameras[0].transform.position = PlayerCameraViews.Position(this.transform, this.Center, this.CameraView, this.CameraZoom);
            this.playerCameras[0].transform.LookAt(this.Center);
        }
    }
    #endregion

    /// <summary>
    /// Per-car copy of the shell material created by SetIndex.
    /// </summary>
    private Material shellMaterial;

    private void OnDestroy()
    {
        Destroy(this.shellMaterial);
    }

    private void Awake()
    {
        // Find submodules
        this.Camera = this.GetComponent<CameraModule>();
        this.Drive = this.GetComponent<Drive>();
        this.Lidar = this.GetComponentInChildren<Lidar>();
        this.Physics = this.GetComponent<PhysicsModule>();

        // The first camera shows every view; the others stay off
        if (this.playerCameras.Length > 0)
        {
            this.playerCameras[0].enabled = true;
            for (int i = 1; i < this.playerCameras.Length; i++)
            {
                this.playerCameras[i].enabled = false;
            }
        }
    }

    private void Update()
    {
        // Space cycles the views and C returns to the first; each view starts at its default distance
        if (Input.GetKeyDown(KeyCode.Space))
        {
            this.SetCamera((this.CameraView + 1) % PlayerCameraViews.Count);
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            this.SetCamera(0);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0 && PlayerCameraViews.ScrollCaptures.Count == 0)
        {
            this.CameraZoom = PlayerCameraViews.Zoom(this.CameraZoom, scroll);
        }
    }

    private void LateUpdate()
    {
        if (this.playerCameras.Length == 0)
        {
            return;
        }

        Transform cameraTransform = this.playerCameras[0].transform;
        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            PlayerCameraViews.Position(this.transform, this.Center, this.CameraView, this.CameraZoom),
            Racecar.cameraSpeed * Time.deltaTime);
        cameraTransform.LookAt(this.Center);
    }
}
