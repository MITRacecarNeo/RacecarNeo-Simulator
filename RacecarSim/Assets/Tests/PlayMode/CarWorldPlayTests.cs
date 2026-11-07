using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// The car world follows the link's pose (fed through an offline session), its panel fills, and
/// Disconnect returns to the main menu. The menu's Connect tile is grayed out by default; turned on,
/// the Connect car screen reports a car that does not answer. Writes Logs/ui-preview-car-world.png and Logs/ui-preview-connect.png.
/// </summary>
public class CarWorldPlayTests
{
    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        CarLinkSession.Current?.Close();
        WorldLevels.Current = null;
        yield return PlayModeLevels.Unload();
    }

    /// <summary>
    /// Feeds one map pose each frame for a time, as the bridge would.
    /// </summary>
    private static IEnumerator Feed(CarLinkSession session, CarPose pose, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            session.Tracker.AddMapPose(RosBridgeClient.Now, pose.X, pose.Y, pose.Yaw);
            yield return null;
        }
    }

    private static void AssertFollows(Racecar car, CarPose pose)
    {
        (Vector3 expected, Quaternion rotation) = CarFrames.RootPose(pose.X, pose.Y, pose.Yaw);
        Vector3 actual = car.transform.position;
        Assert.AreEqual(expected.x, actual.x, 0.1f, "x within 1 cm");
        Assert.AreEqual(expected.z, actual.z, 0.1f, "z within 1 cm");
        Assert.Less(Quaternion.Angle(rotation, Quaternion.Euler(0, car.transform.eulerAngles.y, 0)), 0.5f, "heading within 0.5 deg");
    }

    private static void Submit(Button button)
    {
        using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
        {
            submit.target = button;
            button.SendEvent(submit);
        }
    }

    [UnityTest]
    public IEnumerator CarWorld_FollowsThePoseAndDisconnects()
    {
        CarLinkSession session = CarLinkSession.BeginOffline(new CarLinkTarget { Host = "127.0.0.1" });
        session.Tracker.IsMapAvailable = true;
        SceneManager.LoadScene(CarWorld.SceneName, LoadSceneMode.Single);
        CarWorld world = null;
        yield return PlayModeLevels.WaitFor(() => (world = Object.FindAnyObjectByType<CarWorld>()) != null && world.Car != null, "the car world");

        Rigidbody body = world.Car.GetComponent<Rigidbody>();
        Assert.IsTrue(body.isKinematic, "followed car is kinematic");
        Assert.IsFalse(world.Car.Drive.enabled, "drive off");

        CarPose first = new CarPose(1.5, -0.75, 0.6);
        yield return CarWorldPlayTests.Feed(session, first, 0.5f);
        CarWorldPlayTests.AssertFollows(world.Car, first);
        Assert.IsTrue(Physics.Raycast(world.Car.transform.position + Vector3.up * 50, Vector3.down, out RaycastHit ground, 100, ~((1 << 9) | (1 << 2) | (1 << 10))));
        Assert.AreEqual(ground.point.y, world.Car.transform.position.y, 0.05f, "on the ground");

        CarPose second = new CarPose(-2.0, 3.25, -2.8);
        yield return CarWorldPlayTests.Feed(session, second, 0.5f);
        CarWorldPlayTests.AssertFollows(world.Car, second);

        UIDocument document = world.GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual("Connected", root.Q<Label>("cl-status").text);
        Assert.AreEqual("Map", root.Q<Label>("cl-pose").text);
        // No API in an offline session: the mapping controls stay off
        Assert.AreEqual("-", root.Q<Label>("cl-run").text);
        Assert.IsFalse(root.Q<Button>("cl-start").enabledSelf);
        Assert.IsFalse(root.Q<Button>("cl-save").enabledSelf);
        Assert.IsTrue(CarApiClient.IsValidName(root.Q<TextField>("cl-map-name").value));
        yield return MenuPlayTests.Capture(document, "ui-preview-car-world.png");

        CarWorldPlayTests.Submit(root.Q<Button>("cl-disconnect"));
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == LevelCollection.MainMenuBuildIndex, "the main menu");
        Assert.IsNull(CarLinkSession.Current, "session closed");
    }

    /// <summary>
    /// The followed car driving straight at 1 m/s from poses at 20 Hz moves evenly from frame to
    /// frame (no frame stalls or surges) and the camera keeps its distance, with frame times
    /// alternating between about 7 and 33 ms and an 80 ms frame every 30 frames (as when a map
    /// update rebuilds walls).
    /// </summary>
    [UnityTest]
    public IEnumerator CarWorld_FollowedCarMovesEvenly()
    {
        CarLinkSession session = CarLinkSession.BeginOffline(new CarLinkTarget { Host = "127.0.0.1" });
        session.Tracker.IsMapAvailable = true;
        SceneManager.LoadScene(CarWorld.SceneName, LoadSceneMode.Single);
        CarWorld world = null;
        yield return PlayModeLevels.WaitFor(() => (world = Object.FindAnyObjectByType<CarWorld>()) != null && world.Car != null, "the car world");
        Application.targetFrameRate = 60;
        try
        {
            double start = RosBridgeClient.Now;
            double nextPose = start;
            List<float> speeds = new List<float>();
            List<float> cameraDistances = new List<float>();
            float lastZ = float.NaN;
            Vector3 lastCenter = Vector3.zero;
            float lastTime = 0;
            while (RosBridgeClient.Now - start < 4)
            {
                // Uneven frames, as on a busy computer. The pause comes before the poses are added:
                // the link receives on its own thread, so poses keep arriving through a long frame
                Application.targetFrameRate = Time.frameCount % 2 == 0 ? 30 : 144;
                if (Time.frameCount % 30 == 0)
                {
                    System.Threading.Thread.Sleep(80);
                }
                while (RosBridgeClient.Now >= nextPose)
                {
                    session.Tracker.AddMapPose(nextPose, nextPose - start, 0, 0);
                    nextPose += 0.05;
                }
                yield return null;
                float z = world.Car.transform.position.z;
                if (RosBridgeClient.Now - start > 1 && !float.IsNaN(lastZ) && Time.time > lastTime)
                {
                    speeds.Add((z - lastZ) / CarFrames.UnitsPerMeter / (Time.time - lastTime));
                    // The camera moved in the last frame's LateUpdate, after this test last ran
                    cameraDistances.Add(Vector3.Distance(world.Car.ViewCamera.transform.position, lastCenter));
                }
                lastCenter = world.Car.Center;
                lastZ = z;
                lastTime = Time.time;
            }
            speeds.Sort();
            float low = speeds[speeds.Count / 20];
            float high = speeds[speeds.Count - 1 - speeds.Count / 20];
            Debug.Log($"Followed car speed over {speeds.Count} frames: 5th percentile {low:F3}, median {speeds[speeds.Count / 2]:F3}, 95th percentile {high:F3} m/s");
            Assert.AreEqual(1, speeds[speeds.Count / 2], 0.05, "median speed");
            Assert.Greater(low, 0.8f, "no stalls");
            Assert.Less(high, 1.2f, "no surges");

            // The camera keeps its distance from a car moving straight, whatever the frame times
            float near = cameraDistances.Min();
            float far = cameraDistances.Max();
            Debug.Log($"Camera distance {near:F3} to {far:F3} units");
            Assert.Less(far - near, 0.005f * far, "camera distance steady within 0.5%");
        }
        finally
        {
            Application.targetFrameRate = -1;
        }
    }

    /// <summary>
    /// A 5 x 4 m room (origin -2.5, -2.0; walls one cell thick on the border, free inside).
    /// </summary>
    private static OccupancyGridMsg Room()
    {
        int width = 100;
        int height = 80;
        sbyte[] data = new sbyte[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                bool border = row == 0 || column == 0 || row == height - 1 || column == width - 1;
                data[row * width + column] = border ? (sbyte)100 : (sbyte)0;
            }
        }
        return new OccupancyGridMsg { Width = width, Height = height, Resolution = 0.05f, OriginX = -2.5, OriginY = -2.0, Data = data, FrameId = "map" };
    }

    /// <summary>
    /// A scan of the room's inner faces (x -2.45 to 2.45, y -1.95 to 1.95) from a lidar at
    /// (laserX, 0) facing aft, plus an offset on every range.
    /// </summary>
    private static LaserScanMsg RoomScan(double laserX, float offset)
    {
        int samples = 1080;
        LaserScanMsg scan = new LaserScanMsg
        {
            AngleMin = -Mathf.PI,
            AngleMax = Mathf.PI,
            AngleIncrement = 2 * Mathf.PI / samples,
            RangeMin = 0.15f,
            RangeMax = 12,
            Ranges = new float[samples],
            FrameId = "laser",
        };
        for (int i = 0; i < samples; i++)
        {
            double angle = System.Math.PI + scan.AngleMin + i * scan.AngleIncrement;
            double dx = System.Math.Cos(angle);
            double dy = System.Math.Sin(angle);
            double t = double.PositiveInfinity;
            if (dx > 1e-9) t = System.Math.Min(t, (2.45 - laserX) / dx);
            if (dx < -1e-9) t = System.Math.Min(t, (-2.45 - laserX) / dx);
            if (dy > 1e-9) t = System.Math.Min(t, 1.95 / dy);
            if (dy < -1e-9) t = System.Math.Min(t, -1.95 / dy);
            scan.Ranges[i] = (float)t + offset;
        }
        return scan;
    }

    [UnityTest]
    public IEnumerator CarWorld_BuildsTheMapAndMatchesTheScan()
    {
        CarLinkSession session = CarLinkSession.BeginOffline(new CarLinkTarget { Host = "127.0.0.1" });
        session.Tracker.IsMapAvailable = true;
        SceneManager.LoadScene(CarWorld.SceneName, LoadSceneMode.Single);
        CarWorld world = null;
        yield return PlayModeLevels.WaitFor(() => (world = Object.FindAnyObjectByType<CarWorld>()) != null && world.Car != null, "the car world");

        session.SetMap(CarWorldPlayTests.Room());
        yield return PlayModeLevels.WaitFor(() => world.Map.Grid != null && world.Map.PendingCount == 0, "the walls");
        Assert.Greater(world.Map.TileCount, 0);

        // The inner face of the +x wall is at x 2.45 m: sim Z 24.5
        float height = world.Map.GroundHeight + 1.95f;
        Assert.IsTrue(Physics.Raycast(new Vector3(0, height, 0), Vector3.forward, out RaycastHit hit, 100, 1));
        Assert.AreEqual(24.5f, hit.point.z, 0.1f, "+x wall within 1 cm");
        Assert.IsTrue(Physics.Raycast(new Vector3(0, height, 0), Vector3.right, out hit, 100, 1));
        Assert.AreEqual(19.5f, hit.point.x, 0.1f, "-y wall (sim +X) within 1 cm");

        CarPose center = new CarPose(0, 0, 0);
        yield return CarWorldPlayTests.Feed(session, center, 0.3f);
        session.SetScan(CarWorldPlayTests.RoomScan(0.2383, 0), RosBridgeClient.Now);
        yield return PlayModeLevels.WaitFor(() => !float.IsNaN(world.ScanMatchScore), "the scan match");
        Assert.GreaterOrEqual(world.ScanMatchScore, 0.95f, "matching scan");
        Assert.Greater(world.ScanPoints.PointCount, 1000);

        UIDocument document = world.GetComponent<UIDocument>();
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual("5.0 x 4.0 m", document.rootVisualElement.Q<Label>("cl-world").text);

        // The top-down map shows the room with the car; the wheel zooms between 1 and 16
        Assert.IsTrue(world.Minimap.HasMap);
        Assert.AreEqual(DisplayStyle.Flex, world.Minimap.resolvedStyle.display);
        Assert.Greater(world.Minimap.PathCount, 0, "car on the map");
        world.Minimap.ZoomBy(3);
        Assert.AreEqual(Mathf.Pow(CarMinimap.ZoomStep, 3), world.Minimap.Zoom, 1e-4);
        world.Minimap.ZoomBy(-100);
        Assert.AreEqual(CarMinimap.MinZoom, world.Minimap.Zoom);
        yield return MenuPlayTests.Capture(document, "ui-preview-car-world-map.png");

        session.SetScan(CarWorldPlayTests.RoomScan(0.2383, 0.3f), RosBridgeClient.Now);
        yield return null;
        yield return null;
        Assert.Less(world.ScanMatchScore, 0.2f, "scan 0.3 m off");

        // Save world writes the live map; the saved world reloads with the same grid
        string worlds = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "racecarsim-playworlds-" + System.Guid.NewGuid().ToString("N"));
        world.Library = new WorldLibrary(worlds);
        try
        {
            Button saveWorld = document.rootVisualElement.Q<Button>("cl-save-world");
            Assert.IsTrue(saveWorld.enabledSelf, "Save world on with a map");
            CarWorldPlayTests.Submit(saveWorld);
            yield return PlayModeLevels.WaitFor(() => world.SavedWorld != null, "the saved world");
            CapturedWorld saved = world.Library.List().Single();
            Assert.AreEqual(MapServerFile.GridHash(CarWorldPlayTests.Room()), saved.GridHash);
            Assert.AreEqual(saved.GridHash, MapServerFile.GridHash(WorldLibrary.LoadGrid(saved)));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual($"Saved to Mapped Worlds as {saved.Name}", document.rootVisualElement.Q<Label>("cl-world-saved").text);
            Assert.IsTrue(WorldThumbnail.Exists(saved), "thumbnail rendered on save");
            Texture2D thumbnail = WorldLevels.Thumbnail(saved);
            Assert.AreEqual(WorldThumbnail.Width, thumbnail.width);
            Assert.AreEqual(WorldThumbnail.Height, thumbnail.height);
            System.IO.File.Copy(System.IO.Path.Combine(saved.Folder, WorldThumbnail.FileName), System.IO.Path.Combine(Application.dataPath, "..", "Logs", "ui-preview-world-thumbnail.jpg"), true);
        }
        finally
        {
            System.IO.Directory.Delete(worlds, true);
        }

        // Clear removes walls, scan points, and trail; the car's next map builds the walls again
        Assert.Greater(world.TrailPointCount, 0);
        CarWorldPlayTests.Submit(document.rootVisualElement.Q<Button>("cl-clear"));
        yield return null;
        Assert.AreEqual(0, world.Map.TileCount, "walls cleared");
        Assert.AreEqual(0, world.ScanPoints.PointCount, "scan cleared");
        Assert.IsFalse(world.Minimap.HasMap, "top-down map cleared");
        Assert.IsNull(world.Map.Grid);
        Assert.IsFalse(Physics.Raycast(new Vector3(0, height, 0), Vector3.forward, 100, 1), "no wall left");
        session.SetMap(CarWorldPlayTests.Room());
        yield return PlayModeLevels.WaitFor(() => world.Map.Grid != null && world.Map.PendingCount == 0, "the walls again");
        Assert.IsTrue(Physics.Raycast(new Vector3(0, height, 0), Vector3.forward, out hit, 100, 1));
        Assert.AreEqual(24.5f, hit.point.z, 0.1f, "+x wall rebuilt");
    }

    /// <summary>
    /// A captured world (the 5 x 4 m room, start 1 m along x facing +y) loads as a level: a
    /// drivable car at the start pose on the ground, the walls built, the link panel hidden.
    /// </summary>
    [UnityTest]
    public IEnumerator CapturedWorld_DrivesAsALevel()
    {
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "racecarsim-playdrive-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            WorldLibrary library = new WorldLibrary(folder);
            string download = System.IO.Path.Combine(folder, "download");
            System.IO.Directory.CreateDirectory(download);
            MapServerFile.Save(CarWorldPlayTests.Room(), System.IO.Path.Combine(download, "room.yaml"), "room.pgm");
            System.IO.File.WriteAllText(System.IO.Path.Combine(download, "meta.yaml"), "start_pose: [1.0, 0.0, 1.5707963]\n");
            CapturedWorld world = library.SaveFromCar("room", download, "room", null, null);
            Assert.IsFalse(WorldThumbnail.Exists(world));

            SceneManager.LoadScene(WorldLevels.Prepare(world), LoadSceneMode.Single);
            Racecar car = null;
            yield return PlayModeLevels.WaitFor(() => (car = Object.FindAnyObjectByType<Racecar>()) != null && LevelManager.Mode == SimulationMode.DefaultDrive, "the world's car");
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(SceneManager.GetActiveScene().buildIndex, WorldLevels.BuildIndex);
            Assert.AreEqual(LevelManagerMode.Exploration, LevelManager.LevelManagerMode);
            Assert.IsTrue(car.Drive.enabled, "drivable");
            Assert.IsFalse(car.GetComponent<Rigidbody>().isKinematic, "physics car");
            (Vector3 expected, Quaternion rotation) = CarFrames.RootPose(1.0, 0.0, 1.5707963);
            Assert.AreEqual(expected.x, car.transform.position.x, 0.2f, "start x within 2 cm");
            Assert.AreEqual(expected.z, car.transform.position.z, 0.2f, "start z within 2 cm");
            Assert.Less(Quaternion.Angle(rotation, Quaternion.Euler(0, car.transform.eulerAngles.y, 0)), 1f, "start heading");

            CarWorld scene = Object.FindAnyObjectByType<CarWorld>();
            Assert.AreEqual(DisplayStyle.None, scene.GetComponent<UIDocument>().rootVisualElement.resolvedStyle.display, "link panel hidden");
            Assert.Greater(scene.Map.TileCount, 0, "walls built");
            float height = scene.Map.GroundHeight + 1.95f;
            Assert.IsTrue(Physics.Raycast(new Vector3(0, height, 0), Vector3.forward, out RaycastHit hit, 100, 1));
            Assert.AreEqual(24.5f, hit.point.z, 0.1f, "+x wall within 1 cm");
            Assert.IsTrue(WorldThumbnail.Exists(world), "thumbnail rendered on the first drive");
            yield return MenuPlayTests.Capture(Object.FindAnyObjectByType<Hud>().GetComponent<UIDocument>(), "ui-preview-world.png");
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    private static IEnumerator LoadMenu()
    {
        SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
        yield return PlayModeLevels.WaitFor(() => Object.FindAnyObjectByType<MenuController>() != null, "the main menu");
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator Connect_IsGrayedOutByDefault()
    {
        Assert.IsFalse(MenuController.IsConnectCarEnabled, "Connect car off by default");
        yield return CarWorldPlayTests.LoadMenu();
        VisualElement root = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>().rootVisualElement;

        Button tile = root.Q<Button>("connect");
        Assert.IsFalse(tile.enabledInHierarchy, "Connect tile grayed out");
        CarWorldPlayTests.Submit(tile);
        yield return null;
        Assert.IsFalse(root.Q("connect-screen").ClassListContains("mn-screen--open"), "Connect car stays closed");
    }

    [UnityTest]
    public IEnumerator Connect_ReportsACarThatDoesNotAnswer()
    {
        MenuController.IsConnectCarEnabled = true;
        try
        {
            yield return CarWorldPlayTests.LoadMenu();
            yield return CarWorldPlayTests.ReportsACarThatDoesNotAnswer(Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>());
        }
        finally
        {
            MenuController.IsConnectCarEnabled = false;
        }
    }

    private static IEnumerator ReportsACarThatDoesNotAnswer(UIDocument document)
    {
        VisualElement root = document.rootVisualElement;

        CarWorldPlayTests.Submit(root.Q<Button>("connect"));
        yield return null;
        Assert.IsTrue(root.Q("connect-screen").ClassListContains("mn-screen--open"), "Connect car open");

        root.Q<TextField>("connect-host").value = "not a host";
        CarWorldPlayTests.Submit(root.Q<Button>("connect-go"));
        yield return null;
        Assert.AreEqual("Enter the car's address.", root.Q<Label>("connect-status").text);

        // A port nothing listens on: the API refuses the connection
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        root.Q<TextField>("connect-host").value = "127.0.0.1";
        root.Q<TextField>("connect-api-port").value = port.ToString();
        CarWorldPlayTests.Submit(root.Q<Button>("connect-go"));
        yield return PlayModeLevels.WaitFor(() => root.Q<Label>("connect-status").text.Contains("car link is off"), "the refused message");
        Assert.IsTrue(root.Q<Button>("connect-go").enabledSelf, "Connect enabled again");
        Assert.IsNull(CarLinkSession.Current, "failed session closed");
        yield return MenuPlayTests.Capture(document, "ui-preview-connect.png");
    }
}
