using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The scene for a connected physical car: the sim car follows the car's pose (from its map, or
/// its odometry before a map exists), walls rise from the car's map as it grows, the car's lidar
/// returns are drawn and compared with the sim lidar, and a panel shows the link and the car's
/// state. Without a link and with a captured world chosen (WorldLevels.Current), the scene is
/// that world as a level instead: its walls built at once, the start at the recorded start pose,
/// and the scene's LevelManager turned on for keyboard and Python driving.
/// </summary>
/// <remarks>
/// The followed car is kinematic: its drive and IMU modules are off, and it is posed from
/// CarLinkSession.Tracker in FixedUpdate, on the ground under the pose. The Mapping section sends
/// start, save, and stop through CarLinkSession.MapRun. Esc or Disconnect closes the link and
/// returns to the main menu.
/// </remarks>
public class CarWorld : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Scene name, loaded by name (not a LevelInfo level).
    /// </summary>
    public const string SceneName = "CarWorld";

    /// <summary>
    /// Layers the ground probe ignores: the car (9), its sensors' Ignore Raycast (2), and the arena
    /// scenery (10).
    /// </summary>
    private const int groundMask = ~((1 << 9) | (1 << 2) | (1 << 10));

    /// <summary>
    /// Only the built walls (Default layer) take part in the scan match.
    /// </summary>
    private const int wallMask = 1 << 0;

    /// <summary>
    /// The trail gains a point after the car moves this far (in meters).
    /// </summary>
    private const float trailSpacing = 0.05f;

    private const int trailMaxPoints = 4000;

    /// <summary>
    /// Drift between game time and link time (in seconds) that resets the pairing, after a frame
    /// longer than Unity's maximum step.
    /// </summary>
    private const double timeDriftLimit = 0.1;
    #endregion

    #region Public Interface
    /// <summary>
    /// The followed car, once spawned.
    /// </summary>
    public Racecar Car { get; private set; }

    /// <summary>
    /// The car's map as sim walls and floor.
    /// </summary>
    public CarWorldMap Map { get; private set; }

    /// <summary>
    /// The car's real lidar returns.
    /// </summary>
    public ScanOverlay ScanPoints { get; private set; }

    /// <summary>
    /// Share of real lidar returns matched by the sim lidar for the newest scan (0 to 1), or NaN.
    /// </summary>
    public float ScanMatchScore { get; private set; } = float.NaN;

    /// <summary>
    /// The world Save world wrote last, or null.
    /// </summary>
    public CapturedWorld SavedWorld { get; private set; }

    public int TrailPointCount { get { return this.trailPoints.Count; } }

    /// <summary>
    /// The top-down view of the car's map, top right.
    /// </summary>
    public CarMinimap Minimap { get; private set; }

    /// <summary>
    /// Library Save world writes to; null for the data directory's (tests set it).
    /// </summary>
    public WorldLibrary Library { get; set; }
    #endregion

    [SerializeField]
    private GameObject playerPrefab;

    /// <summary>
    /// The scene's LevelManager, inactive until a captured world is driven.
    /// </summary>
    [SerializeField]
    private GameObject levelManager;

    [SerializeField]
    private UIDocument document;

    [SerializeField]
    private Material wallMaterial;

    [SerializeField]
    private Material floorMaterial;

    [SerializeField]
    private Material scanMaterial;

    [SerializeField]
    private Material trailMaterial;

    private CarLinkSession session;
    private Rigidbody body;
    private VisualElement dot;
    private Label status;
    private Label carName;
    private Label mode;
    private Label pose;
    private Label battery;
    private Label latency;
    private Label world;
    private Label scan;
    private Label run;
    private TextField mapName;
    private Button startButton;
    private Button saveButton;
    private Button stopButton;
    private Button saveWorldButton;
    private string shownRunMap;
    private Label worldSaved;
    private Task<CapturedWorld> worldSave;
    private LineRenderer trail;
    private readonly List<Vector3> trailPoints = new List<Vector3>();
    private int shownMapVersion;
    private int shownScanVersion;
    private float nextHudUpdate;
    private float groundHeight;
    private bool isLeaving;
    private double linkTimeOffset = double.NaN;
    private CarPose shownPose;
    private bool hasShownPose;

    private void Start()
    {
        this.session = CarLinkSession.Current;
        if (this.session == null)
        {
            if (WorldLevels.Current != null)
            {
                this.StartWorld(WorldLevels.Current);
                return;
            }
            SceneFlow.Load(LevelCollection.MainMenuBuildIndex, SceneTransition.Cut);
            return;
        }

        GameObject player = GameObject.Instantiate(this.playerPrefab, Vector3.zero, Quaternion.identity);
        this.Car = player.GetComponentInChildren<Racecar>();
        this.Car.SetIndex(0);
        this.body = this.Car.GetComponent<Rigidbody>();
        this.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        this.body.isKinematic = true;
        // Posed at the fixed rate; interpolation keeps a close camera from shaking between steps
        this.body.interpolation = RigidbodyInterpolation.Interpolate;
        this.Car.Drive.enabled = false;
        this.Car.Physics.enabled = false;
        this.Car.IsCameraOffsetSmoothed = true;

        float ground = this.GroundHeight(Vector3.zero);
        this.Map = new GameObject("CarMap").AddComponent<CarWorldMap>();
        this.Map.Initialize(this.wallMaterial, this.floorMaterial);
        this.Map.GroundHeight = ground;
        this.ScanPoints = new GameObject("CarScan").AddComponent<ScanOverlay>();
        this.ScanPoints.Initialize(this.scanMaterial);
        GameObject trailObject = new GameObject("CarTrail") { layer = 10 };
        this.trail = trailObject.AddComponent<LineRenderer>();
        this.trail.sharedMaterial = this.trailMaterial;
        this.trail.widthMultiplier = 0.25f;
        this.trail.positionCount = 0;
        this.trail.useWorldSpace = true;

        VisualElement root = this.document.rootVisualElement;
        this.dot = root.Q("cl-dot");
        this.status = root.Q<Label>("cl-status");
        this.carName = root.Q<Label>("cl-car");
        this.mode = root.Q<Label>("cl-mode");
        this.pose = root.Q<Label>("cl-pose");
        this.battery = root.Q<Label>("cl-battery");
        this.latency = root.Q<Label>("cl-latency");
        this.world = root.Q<Label>("cl-world");
        this.scan = root.Q<Label>("cl-scan");
        root.Q<Button>("cl-disconnect").clicked += this.Leave;
        this.run = root.Q<Label>("cl-run");
        this.mapName = root.Q<TextField>("cl-map-name");
        this.mapName.value = CarMapRun.DefaultName(System.DateTime.Now);
        this.startButton = root.Q<Button>("cl-start");
        this.saveButton = root.Q<Button>("cl-save");
        this.stopButton = root.Q<Button>("cl-stop");
        this.startButton.clicked += () => this.MapCommand(run =>
        {
            run.StartMapping(this.mapName.value);
            if (run.Step == CarMapRunStep.Starting)
            {
                this.ClearWorld();
            }
        });
        this.saveButton.clicked += () => this.MapCommand(run => run.Save());
        this.stopButton.clicked += () => this.MapCommand(run => run.Stop());
        this.saveWorldButton = root.Q<Button>("cl-save-world");
        this.worldSaved = root.Q<Label>("cl-world-saved");
        this.worldSaved.text = string.Empty;
        this.saveWorldButton.clicked += this.SaveWorld;
        root.Q<Button>("cl-clear").clicked += this.ClearWorld;
        this.Minimap = root.Q<CarMinimap>("cl-minimap");
        this.UpdateHud();
    }

    private void Update()
    {
        if (this.session == null)
        {
            return;
        }
        this.PairTime();
        if (Input.GetKeyDown(Hotkeys.Exit))
        {
            // Esc while typing a map name leaves the field, not the car
            if (this.IsTyping())
            {
                this.mapName.Blur();
                return;
            }
            this.Leave();
            return;
        }
        // A new run on the car (started here or on the car) starts the world over
        string runMap = this.session.Autonomy?.Mode == "mapping" ? this.session.Autonomy.Map : null;
        if (runMap != null && this.shownRunMap != null && runMap != this.shownRunMap)
        {
            // The new run's first map may already be waiting; keep it
            int waiting = this.shownMapVersion;
            this.ClearWorld();
            this.shownMapVersion = waiting;
        }
        if (runMap != null)
        {
            this.shownRunMap = runMap;
        }
        if (this.session.MapVersion != this.shownMapVersion && this.session.Map != null)
        {
            this.shownMapVersion = this.session.MapVersion;
            this.Map.Show(this.session.Map);
            this.Minimap.SetMap(this.Map.IsTooLarge ? null : this.session.Map);
        }
        if (this.session.ScanVersion != this.shownScanVersion && this.session.Scan != null)
        {
            this.shownScanVersion = this.session.ScanVersion;
            this.ShowScan(this.session.Scan, this.session.ScanTime);
        }
        if (this.hasShownPose)
        {
            this.Minimap.SetCar(this.shownPose);
        }
        // The scroll wheel zooms the map, not the camera, while the pointer is over the map
        if (this.Minimap.IsPointerOver)
        {
            PlayerCameraViews.ScrollCaptures.Add(this);
        }
        else
        {
            PlayerCameraViews.ScrollCaptures.Remove(this);
        }
        if (Time.unscaledTime >= this.nextHudUpdate)
        {
            this.nextHudUpdate = Time.unscaledTime + 0.25f;
            this.UpdateHud();
        }
    }

    private void FixedUpdate()
    {
        // Each fixed step samples its own place on the timeline: catch-up steps run back to back
        // in one frame, and sampling the wall clock there gave them the same pose, which the
        // interpolation showed as the car surging and stalling
        if (double.IsNaN(this.linkTimeOffset))
        {
            this.PairTime();
        }
        double now = Time.fixedTimeAsDouble + this.linkTimeOffset;
        if (this.session == null || this.Car == null || !this.session.Tracker.TrySample(now, out CarPose carPose))
        {
            return;
        }
        (Vector3 position, Quaternion rotation) = CarFrames.RootPose(carPose.X, carPose.Y, carPose.Yaw);
        position.y = this.GroundHeight(position);
        this.body.MovePosition(position);
        this.body.MoveRotation(rotation);
        this.shownPose = carPose;
        this.hasShownPose = true;
        this.ExtendTrail(position);
    }

    private void OnDisable()
    {
        PlayerCameraViews.ScrollCaptures.Remove(this);
    }

    private void OnDestroy()
    {
        this.Minimap?.Release();
    }

    /// <summary>
    /// Removes the walls, floor, scan points, and trail; walls return with the car's next map.
    /// </summary>
    public void ClearWorld()
    {
        this.Map.Clear();
        this.ScanPoints.Show(new List<Vector3>());
        this.ScanMatchScore = float.NaN;
        this.trailPoints.Clear();
        this.trail.positionCount = 0;
        this.Minimap.ClearMap();
        this.shownMapVersion = this.session?.MapVersion ?? 0;
        this.UpdateHud();
    }

    /// <summary>
    /// Renders a saved world's map select thumbnail from the built walls and floor, without the
    /// scan points and the trail.
    /// </summary>
    private void SaveThumbnail(CapturedWorld world)
    {
        this.Map.Flush();
        this.ScanPoints.gameObject.SetActive(false);
        this.trail.enabled = false;
        WorldThumbnail.Save(world, WorldThumbnail.CourseBounds(this.Map.transform));
        this.ScanPoints.gameObject.SetActive(true);
        this.trail.enabled = true;
    }

    /// <summary>
    /// Builds a captured world and hands the scene to its LevelManager: walls with the world's
    /// build settings, the start at the recorded start pose on the ground, the link panel hidden.
    /// </summary>
    private void StartWorld(CapturedWorld world)
    {
        this.document.rootVisualElement.style.display = DisplayStyle.None;
        OccupancyGridMsg grid;
        try
        {
            grid = WorldLibrary.LoadGrid(world);
            if (world.Unknown == CapturedWorld.UnknownClosed)
            {
                grid = WorldLevels.CloseUnknownEdges(grid);
            }
        }
        catch (System.Exception exception) when (exception is System.IO.IOException || exception is CarLinkFormatException)
        {
            Debug.LogError($"World {world.Name} does not load: {exception.Message}");
            SceneFlow.Load(LevelCollection.MainMenuBuildIndex, SceneTransition.Cut);
            return;
        }

        (Vector3 start, Quaternion rotation) = CarFrames.RootPose(world.StartX, world.StartY, world.StartYaw);
        start.y = this.GroundHeight(start);
        this.Map = new GameObject("WorldMap").AddComponent<CarWorldMap>();
        this.Map.Initialize(this.wallMaterial, this.floorMaterial);
        this.Map.GroundHeight = start.y;
        this.Map.WallHeight = (float)world.WallHeight;
        this.Map.MinComponent = world.SpeckleCells;
        this.Map.Show(grid);
        if (this.Map.IsTooLarge)
        {
            Debug.LogError($"World {world.Name} is too large to build ({grid.Width} x {grid.Height} cells)");
            SceneFlow.Load(LevelCollection.MainMenuBuildIndex, SceneTransition.Cut);
            return;
        }
        this.Map.Flush();
        // Worlds saved before thumbnails were rendered get one on their first drive
        if (!WorldThumbnail.Exists(world))
        {
            WorldThumbnail.Save(world, WorldThumbnail.CourseBounds(this.Map.transform));
        }

        KeyPoint keyPoint = new GameObject("WorldStart").AddComponent<KeyPoint>();
        keyPoint.transform.SetPositionAndRotation(start, rotation);
        this.levelManager.SetActive(true);
    }

    /// <summary>
    /// Pairs game time with link time (RosBridgeClient.Now) once, again only after they drift
    /// apart, so fixed steps map onto link time at even spacing.
    /// </summary>
    private void PairTime()
    {
        double offset = RosBridgeClient.Now - Time.timeAsDouble;
        if (double.IsNaN(this.linkTimeOffset) || System.Math.Abs(offset - this.linkTimeOffset) > CarWorld.timeDriftLimit)
        {
            this.linkTimeOffset = offset;
        }
    }

    private void ExtendTrail(Vector3 position)
    {
        Vector3 point = position + Vector3.up * 0.1f;
        if (this.trailPoints.Count > 0 && Vector3.Distance(this.trailPoints[this.trailPoints.Count - 1], point) < CarWorld.trailSpacing * CarFrames.UnitsPerMeter)
        {
            return;
        }
        this.trailPoints.Add(point);
        if (this.trailPoints.Count > CarWorld.trailMaxPoints)
        {
            this.trailPoints.RemoveAt(0);
        }
        this.trail.positionCount = this.trailPoints.Count;
        this.trail.SetPositions(this.trailPoints.ToArray());
    }

    /// <summary>
    /// Draws a scan where the car was when it was taken and scores it against the sim lidar.
    /// </summary>
    private void ShowScan(LaserScanMsg laserScan, double time)
    {
        if (!this.session.Tracker.TryPoseAt(time, out CarPose carPose))
        {
            return;
        }
        RacecarInfo info = this.session.Info;
        double offsetX = (info?.Frame("laser_x_mm", 238.3) ?? 238.3) / 1000;
        double offsetY = (info?.Frame("laser_y_mm", 0) ?? 0) / 1000;
        double offsetZ = (info?.Frame("laser_z_mm", 195.0) ?? 195.0) / 1000;
        double offsetYaw = (info?.Frame("laser_yaw_deg", 180) ?? 180) * System.Math.PI / 180;
        (double x, double y, double yaw) laser = ScanMatch.LaserPose(carPose, offsetX, offsetY, offsetYaw);
        float height = this.Map.GroundHeight + (float)offsetZ * CarFrames.UnitsPerMeter;

        List<Vector3> points = new List<Vector3>(laserScan.Ranges.Length);
        for (int i = 0; i < laserScan.Ranges.Length; i++)
        {
            if (!laserScan.IsValid(i))
            {
                continue;
            }
            double angle = laser.yaw + laserScan.AngleMin + i * laserScan.AngleIncrement;
            double range = laserScan.Ranges[i];
            Vector3 point = CarFrames.ToUnity(laser.x + range * System.Math.Cos(angle), laser.y + range * System.Math.Sin(angle), 0);
            point.y = height;
            points.Add(point);
        }
        this.ScanPoints.Show(points);

        float[] simRanges = ScanMatch.Cast(laserScan, laser, height, CarWorld.wallMask);
        this.ScanMatchScore = ScanMatch.Score(laserScan, simRanges, out int _);
    }

    /// <summary>
    /// Height of the ground under a point, from above; the last height when nothing is hit.
    /// </summary>
    private float GroundHeight(Vector3 position)
    {
        if (Physics.Raycast(new Vector3(position.x, 1000, position.z), Vector3.down, out RaycastHit hit, 2000, CarWorld.groundMask, QueryTriggerInteraction.Ignore))
        {
            this.groundHeight = hit.point.y;
        }
        return this.groundHeight;
    }

    private void UpdateHud()
    {
        if (this.status == null)
        {
            return;
        }
        CarLinkStatus link = this.session.Status;
        bool connected = link == CarLinkStatus.Connected || link == CarLinkStatus.Offline;
        this.status.text = CarWorld.StatusText(link);
        this.dot.EnableInClassList("cl-dot--on", connected && !this.session.Tracker.IsStale);
        this.dot.EnableInClassList("cl-dot--waiting", link == CarLinkStatus.Reconnecting || (connected && this.session.Tracker.IsStale));
        this.dot.EnableInClassList("cl-dot--off", link == CarLinkStatus.Failed);

        string host = this.session.Info?.Hostname;
        this.carName.text = string.IsNullOrEmpty(host) ? this.session.Target.Label : $"{host} ({this.session.Target.Label})";
        this.mode.text = CarWorld.ModeText(this.session.Autonomy);
        this.pose.text = CarWorld.PoseText(this.session.Tracker);
        this.battery.text = this.session.BatteryVoltage.HasValue
            ? this.session.BatteryVoltage.Value.ToString("0.0", CultureInfo.InvariantCulture) + " V"
            : "-";
        this.world.text = CarWorld.WorldText(this.Map);
        this.scan.text = float.IsNaN(this.ScanMatchScore) ? "-" : (this.ScanMatchScore * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        this.UpdateMapping();
        this.latency.text = this.session.Clock.IsSynchronized
            ? (this.session.Clock.RoundTrip * 1000).ToString("0", CultureInfo.InvariantCulture) + " ms"
            : "-";
    }

    private void MapCommand(System.Action<CarMapRun> command)
    {
        if (this.session?.MapRun == null)
        {
            return;
        }
        command(this.session.MapRun);
        this.UpdateMapping();
    }

    private void UpdateMapping()
    {
        CarMapRun mapRun = this.session.MapRun;
        this.run.text = CarWorld.RunText(mapRun);
        this.startButton.SetEnabled(mapRun != null && mapRun.CanStart);
        this.saveButton.SetEnabled(mapRun != null && mapRun.CanSave);
        this.stopButton.SetEnabled(mapRun != null && mapRun.CanStop);
        this.mapName.SetEnabled(mapRun != null && mapRun.CanStart);
        this.saveWorldButton.SetEnabled(this.worldSave == null && (mapRun?.DownloadedMap != null || this.session.Map != null));
        if (this.worldSave != null && this.worldSave.IsCompleted)
        {
            if (this.worldSave.Status == TaskStatus.RanToCompletion)
            {
                this.SavedWorld = this.worldSave.Result;
                this.SaveThumbnail(this.SavedWorld);
                this.worldSaved.text = $"Saved to Mapped Worlds as {this.SavedWorld.Name}";
            }
            else
            {
                Debug.LogWarning($"Car world: world not saved: {this.worldSave.Exception?.InnerException?.Message}");
                this.worldSaved.text = "Could not save the world.";
            }
            this.worldSave = null;
        }
    }

    /// <summary>
    /// Saves the car's map as a captured world: the downloaded files of the last saved map, else
    /// the live map. The files are written off the main thread.
    /// </summary>
    private void SaveWorld()
    {
        if (this.worldSave != null || this.session == null)
        {
            return;
        }
        CarMapRun mapRun = this.session.MapRun;
        string downloaded = mapRun?.DownloadedMap;
        string carMap = downloaded ?? this.session.Autonomy?.Map;
        string folder = downloaded != null ? mapRun.MapFolder(downloaded) : null;
        string name = carMap ?? CarMapRun.DefaultName(System.DateTime.Now);
        OccupancyGridMsg liveMap = this.session.Map;
        RacecarInfo info = this.session.Info;
        WorldLibrary library = this.Library ?? WorldLibrary.Default;
        this.worldSaved.text = "Saving world";
        this.worldSave = Task.Run(() => library.SaveFromCar(name, folder, carMap, liveMap, info));
        this.UpdateMapping();
    }

    private bool IsTyping()
    {
        VisualElement focused = this.document.rootVisualElement?.focusController?.focusedElement as VisualElement;
        return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
    }

    /// <summary>
    /// The panel's mapping line: the command in progress, its failure, or the car's run.
    /// </summary>
    public static string RunText(CarMapRun mapRun)
    {
        if (mapRun == null)
        {
            return "-";
        }
        if (mapRun.State == null)
        {
            return mapRun.IsReachable ? "-" : "Waiting for the car";
        }
        switch (mapRun.Step)
        {
            case CarMapRunStep.Starting:
                return "Starting";
            case CarMapRunStep.Saving:
                return "Saving";
            case CarMapRunStep.Stopping:
                return "Stopping";
            case CarMapRunStep.Downloading:
                return mapRun.IsRetrying ? "Download interrupted; retrying" : $"Downloading {(mapRun.DownloadProgress * 100).ToString("0", CultureInfo.InvariantCulture)}%";
        }
        if (mapRun.Message != null)
        {
            return mapRun.Message;
        }
        if (!mapRun.IsReachable)
        {
            return "No map service found on car link";
        }
        if (mapRun.IsOwnRun)
        {
            return mapRun.DownloadedMap == mapRun.OwnedMap ? $"Mapping {mapRun.OwnedMap}, saved" : $"Mapping {mapRun.OwnedMap}";
        }
        if (mapRun.IsMapping)
        {
            return $"Mapping {mapRun.State.Map}, started on the car";
        }
        if (mapRun.State.JobRunning)
        {
            return "The car is running a map command";
        }
        return mapRun.DownloadedMap != null ? $"Saved {mapRun.DownloadedMap}" : "Not mapping";
    }

    /// <summary>
    /// The panel's status line for a link state.
    /// </summary>
    public static string StatusText(CarLinkStatus status)
    {
        switch (status)
        {
            case CarLinkStatus.Connected:
            case CarLinkStatus.Offline:
                return "Connected";
            case CarLinkStatus.Reconnecting:
                return "Reconnecting";
            case CarLinkStatus.Failed:
                return "Disconnected";
            default:
                return "Connecting";
        }
    }

    /// <summary>
    /// The panel's mode line from the car's autonomy state.
    /// </summary>
    public static string ModeText(AutonomyStatus autonomy)
    {
        if (autonomy == null || autonomy.Mode == null || autonomy.Mode == "off")
        {
            return "No map";
        }
        string map = string.IsNullOrEmpty(autonomy.Map) ? "map" : autonomy.Map;
        if (autonomy.Mode == "mapping")
        {
            return "Mapping " + map;
        }
        return autonomy.Localized == false ? map + ", not localized" : map;
    }

    /// <summary>
    /// The panel's world line: the map's size, or why there is none.
    /// </summary>
    public static string WorldText(CarWorldMap map)
    {
        if (map == null || map.IsTooLarge)
        {
            return map != null ? "Map too large" : "-";
        }
        OccupancyGridMsg grid = map.Grid;
        if (grid == null)
        {
            return "-";
        }
        double width = grid.Width * (double)grid.Resolution;
        double height = grid.Height * (double)grid.Resolution;
        return $"{width.ToString("0.0", CultureInfo.InvariantCulture)} x {height.ToString("0.0", CultureInfo.InvariantCulture)} m";
    }

    /// <summary>
    /// The panel's pose line from the tracker.
    /// </summary>
    public static string PoseText(CarPoseTracker tracker)
    {
        if (tracker.IsStale)
        {
            return "No updates";
        }
        switch (tracker.Source)
        {
            case CarPoseSource.Map:
                return "Map";
            case CarPoseSource.Odometry:
                return "Odometry";
            default:
                return "Waiting";
        }
    }

    private void Leave()
    {
        if (this.isLeaving || SceneFlow.IsTransitioning)
        {
            return;
        }
        this.isLeaving = true;
        this.session.Close();
        this.session = null;
        SceneFlow.Load(LevelCollection.MainMenuBuildIndex, SceneTransition.Wipe);
    }
}
